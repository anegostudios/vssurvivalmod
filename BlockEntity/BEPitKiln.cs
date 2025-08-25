using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    // The first 4 slots are the ground storage contents
    // Slots 5 to 10 is the fuel
    public class BlockEntityPitKiln : BlockEntityGroundStorage, IHeatSource
    {
        protected ILoadedSound ambientSound;
        protected BuildStage[] buildStages;
        protected Shape shape;
        protected MeshData mesh;

        protected string[] selectiveElements;
        protected Dictionary<string, string> textureCodeReplace = new Dictionary<string, string>();
        protected int currentBuildStage;


        public bool Lit;
        public bool IsComplete => currentBuildStage >= buildStages.Length;
        public BuildStage NextBuildStage => buildStages[currentBuildStage];

        public double BurningUntilTotalHours;

        protected override int invSlotCount => 10;

        public float BurnTimeHours = 20;

        bool nowTesselatingKiln;
        ITexPositionSource blockTexPos;

        public override TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (nowTesselatingKiln)
                {
                    if (textureCodeReplace.TryGetValue(textureCode, out string replaceCode))
                    {
                        textureCode = replaceCode;
                    }

                    return blockTexPos[textureCode];
                }

                return base[textureCode];
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            var bh = GetBehavior<BEBehaviorBurning>();

            // Make sure the kiln doesn't burn longer than intended (e.g. when exported from an old world and imported into a new world)
            if (Lit)
            {
                BurningUntilTotalHours = Math.Min(api.World.Calendar.TotalHours + BurnTimeHours, BurningUntilTotalHours);
            }

            base.Initialize(api);

            bh.OnFireTick = (dt) => {
                if (api.World.Calendar.TotalHours >= BurningUntilTotalHours)
                {
                    if (IsAreaLoaded()) // Wait until nearby chunks area loaded before firing fully
                    {
                        OnFired();
                    }
                }
            };
            bh.OnFireDeath = KillFire;

            bh.ShouldBurn = () => Lit;
            bh.OnCanBurn = (pos) =>
            {
                if (pos == Pos && !Lit && IsComplete) return true;

                Block block = Api.World.BlockAccessor.GetBlock(pos);
                Block upblock = Api.World.BlockAccessor.GetBlock(Pos.UpCopy());

                return block?.CombustibleProps != null && block.CombustibleProps.BurnDuration > 0 && (!IsAreaLoaded() || upblock.Replaceable >= 6000);
            };

            DetermineBuildStages();

            bh.FuelPos = Pos.Copy();
            bh.FirePos = Pos.UpCopy();
        }

        public bool IsAreaLoaded()
        {
            if (Api == null || Api.Side == EnumAppSide.Client) return true;

            ICoreServerAPI sapi = Api as ICoreServerAPI;

            const int chunksize = GlobalConstants.ChunkSize;
            int sizeX = sapi.WorldManager.MapSizeX / chunksize;
            int sizeY = sapi.WorldManager.MapSizeY / chunksize;
            int sizeZ = sapi.WorldManager.MapSizeZ / chunksize;

            int mincx = GameMath.Clamp((Pos.X - 1) / chunksize, 0, sizeX - 1);
            int maxcx = GameMath.Clamp((Pos.X + 1) / chunksize, 0, sizeX - 1);
            int mincy = GameMath.Clamp((Pos.Y - 1) / chunksize, 0, sizeY - 1);
            int maxcy = GameMath.Clamp((Pos.Y + 1) / chunksize, 0, sizeY - 1);
            int mincz = GameMath.Clamp((Pos.Z - 1) / chunksize, 0, sizeZ - 1);
            int maxcz = GameMath.Clamp((Pos.Z + 1) / chunksize, 0, sizeZ - 1);

            for (int cx = mincx; cx <= maxcx; cx++)
            {
                for (int cy = mincy; cy <= maxcy; cy++)
                {
                    for (int cz = mincz; cz <= maxcz; cz++)
                    {
                        if (sapi.WorldManager.GetChunk(cx, cy, cz) == null) return false;
                    }
                }
            }

            return true;
        }




        public override bool OnPlayerInteractStart(IPlayer player, BlockSelection bs)
        {
            ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

            if (hotbarSlot.Empty) return false;

            if (currentBuildStage < buildStages.Length)
            {
                BuildStage stage = buildStages[currentBuildStage];
                BuildStageMaterial[] mats = stage.Materials;

                for (int i = 0; i < mats.Length; i++)
                {
                    var stack = mats[i].ItemStack;

                    if (stack.Equals(Api.World, hotbarSlot.Itemstack, GlobalConstants.IgnoredStackAttributes) && stack.StackSize <= hotbarSlot.StackSize)
                    {
                        if (!isSameMatAsPreviouslyAdded(stack)) continue;

                        int toMove = stack.StackSize;
                        for (int j = 4; j < invSlotCount && toMove > 0; j++)
                        {
                            toMove -= hotbarSlot.TryPutInto(Api.World, inventory[j], toMove);
                        }

                        hotbarSlot.MarkDirty();

                        currentBuildStage++;
                        mesh = null;
                        MarkDirty(true);
                        updateSelectiveElements();
                        (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                        if (stack.Collectible.Attributes?["placeSound"].Exists == true)
                        {
                            AssetLocation sound = AssetLocation.Create(stack.Collectible.Attributes["placeSound"].AsString(), stack.Collectible.Code.Domain);
                            if (sound != null)
                            {
                                Api.World.PlaySoundAt(sound.WithPathPrefixOnce("sounds/"), Pos, -0.4, player, true, 12);
                            }
                        }
                    }
                }
            }


            DetermineStorageProperties(null);

            return true;
        }


        protected bool isSameMatAsPreviouslyAdded(ItemStack newStack)
        {
            BuildStage bstage = buildStages[currentBuildStage];

            for (int i = 0; i < inventory.Count; i++)
            {
                ItemSlot slot = inventory[i];
                if (slot.Empty) continue;

                if (bstage.Materials.FirstOrDefault(bsm => bsm.ItemStack.Equals(Api.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes)) != null)
                {
                    if (!newStack.Equals(Api.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes)) return false;
                }
            }

            return true;
        }

        public override void DetermineStorageProperties(ItemSlot sourceSlot)
        {
            base.DetermineStorageProperties(sourceSlot);

            if (buildStages != null)
            {
                colBoxes[0].X1 = 0;
                colBoxes[0].X2 = 1;
                colBoxes[0].Z1 = 0;
                colBoxes[0].Z2 = 1;
                colBoxes[0].Y2 = Math.Max(colBoxes[0].Y2, buildStages[Math.Min(buildStages.Length - 1, currentBuildStage)].MinHitboxY2 / 16f);

                selBoxes[0] = colBoxes[0];
            }
        }

        protected override void FixBrokenStorageLayout()
        {
            // Skip this since we haven't changed any storage layouts for clay items in vanilla and the
            // fuel will physically hide most items in the kiln anyway even if there were a display issue
        }


        public override float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return Lit ? 10 : 0;
        }


        public void OnFired()
        {
            if (IsValidPitKiln())
            {
                for (int i = 0; i < 4; i++)
                {
                    ItemSlot slot = inventory[i];
                    if (slot.Empty) continue;
                    ItemStack rawStack = slot.Itemstack;
                    ItemStack firedStack = rawStack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

                    if (firedStack != null)
                    {
                        slot.Itemstack = firedStack.Clone();
                        slot.Itemstack.StackSize = rawStack.StackSize / rawStack.Collectible.CombustibleProps.SmeltedRatio;
                    }
                }

                MarkDirty(true);
            }

            KillFire(true);
        }


        protected bool IsValidPitKiln()
        {
            var world = Api.World;

            Block liquidblock = world.BlockAccessor.GetBlock(Pos, BlockLayersAccess.Fluid);
            if (liquidblock.BlockId != 0)
            {
                return false;
            }

            foreach (var face in BlockFacing.HORIZONTALS.Append(BlockFacing.DOWN))
            {
                BlockPos npos = Pos.AddCopy(face);
                Block block = world.BlockAccessor.GetBlock(npos);
                if (!block.CanAttachBlockAt(world.BlockAccessor, Block, npos, face.Opposite))
                {
                    return false;
                }
                if (block.CombustibleProps != null)
                {
                    return false;
                }
            }

            Block upblock = world.BlockAccessor.GetBlock(Pos.UpCopy());
            if (upblock.Replaceable < 6000)
            {
                return false;
            }
            

            return true;
        }

        public void OnCreated(IPlayer byPlayer)
        {
            StorageProps = null;
            mesh = null;
            DetermineBuildStages();
            DetermineStorageProperties(null);

            var stack = inventory[4].Itemstack = byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(buildStages[0].Materials[0].ItemStack.StackSize);
            currentBuildStage++;

            if (stack.Collectible.Attributes?["placeSound"].Exists == true)
            {
                AssetLocation sound = AssetLocation.Create(stack.Collectible.Attributes["placeSound"].AsString(), stack.Collectible.Code.Domain);
                if (sound != null)
                {
                    Api.World.PlaySoundAt(sound.WithPathPrefixOnce("sounds/"), Pos, -0.4, byPlayer, true, 12);
                }
            }

            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

            updateSelectiveElements();
        }

        public void DetermineBuildStages()
        {
            BlockPitkiln blockpk = this.Block as BlockPitkiln;
            bool found = false;
            foreach (var val in blockpk.BuildStagesByBlock)
            {
                if (!inventory[0].Empty && WildcardUtil.Match(new AssetLocation(val.Key), inventory[0].Itemstack.Collectible.Code))
                {
                    buildStages = val.Value;
                    shape = blockpk.ShapesByBlock[val.Key];
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                if (blockpk.BuildStagesByBlock.TryGetValue("*", out buildStages))
                {
                    shape = blockpk.ShapesByBlock["*"];
                }
            }

            updateSelectiveElements();
        }

        private void updateSelectiveElements()
        {
            if (Api.Side == EnumAppSide.Client)
            {
                textureCodeReplace.Clear();

                Dictionary<string, string> matCodeToEleCode = new Dictionary<string, string>();
                for (int i = 0; i < currentBuildStage; i++)
                {
                    var bStage = buildStages[i];
                    if (!matCodeToEleCode.ContainsKey(bStage.MatCode))
                    {
                        var bsm = currentlyUsedMaterialOfStage(bStage);
                        matCodeToEleCode[bStage.MatCode] = bsm?.EleCode;

                        if (bsm.TextureCodeReplace != null)
                        {
                            textureCodeReplace[bsm.TextureCodeReplace.From] = bsm.TextureCodeReplace.To;
                        }
                    }
                }

                selectiveElements = new string[currentBuildStage];
                for (int i = 0; i < currentBuildStage; i++)
                {
                    string eleName = buildStages[i].ElementName;
                    if (matCodeToEleCode.TryGetValue(buildStages[i].MatCode, out string replace))
                    {
                        eleName = eleName.Replace("{eleCode}", replace);
                    }

                    selectiveElements[i] = eleName;
                }
            } else
            {
                for (int i = 0; i < currentBuildStage; i++)
                {
                    var bStage = buildStages[i];
                    var bsm = currentlyUsedMaterialOfStage(bStage);
                    if (bsm?.BurnTimeHours != null)
                    {
                        BurnTimeHours = (float)bsm.BurnTimeHours;
                    }
                }
            }

            

            colBoxes[0].X1 = 0;
            colBoxes[0].X2 = 1;
            colBoxes[0].Z1 = 0;
            colBoxes[0].Z2 = 1;
            colBoxes[0].Y2 = Math.Max(colBoxes[0].Y2, buildStages[Math.Min(buildStages.Length - 1, currentBuildStage)].MinHitboxY2 / 16f);

            selBoxes[0] = colBoxes[0];
        }



        BuildStageMaterial currentlyUsedMaterialOfStage(BuildStage buildStage)
        {
            BuildStageMaterial[] bsms = buildStage.Materials;

            for (int i = 0; i < bsms.Length; i++)
            {
                var bsm = bsms[i];
                foreach (var slot in inventory)
                {
                    if (slot.Empty) continue;
                    if (slot.Itemstack.Equals(Api.World, bsm.ItemStack, GlobalConstants.IgnoredStackAttributes)) return bsm;
                }
            }

            return null;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            BurningUntilTotalHours = tree.GetDouble("burnUntil");

            int prevStage = currentBuildStage;
            bool prevLit = Lit;

            currentBuildStage = tree.GetInt("currentBuildStage");
            Lit = tree.GetBool("lit");

            if (Api != null)
            {
                DetermineBuildStages();

                if (Api.Side == EnumAppSide.Client)
                {
                    if (prevStage != currentBuildStage) mesh = null;
                    if (!prevLit && Lit)
                    {
                        TryIgnite(null);
                    }
                    if (prevLit && !Lit)
                    {
                        var bh = GetBehavior<BEBehaviorBurning>();
                        bh.KillFire(false);
                    }
                }
            }

            // Do this last!!!
            RedrawAfterReceivingTreeAttributes(worldForResolving);     // Redraw on client after we have completed receiving the update from server
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("currentBuildStage", currentBuildStage);
            tree.SetBool("lit", Lit);
            tree.SetDouble("burnUntil", BurningUntilTotalHours);
        }


        

        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            DetermineBuildStages();
            if (mesh == null)
            {
                nowTesselatingKiln = true;
                blockTexPos = tesselator.GetTextureSource(Block);
                tesselator.TesselateShape("pitkiln", shape, out mesh, this, null, 0, 0, 0, null, selectiveElements);
                nowTesselatingKiln = false;
                mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 1.005f, 1.005f, 1.005f);
                mesh.Translate(0, GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 10)/500f, 0);
            }

            meshdata.AddMeshData(mesh);

            base.OnTesselation(meshdata, tesselator);

            return true;
        }

        public override bool CanIgnite => IsComplete && IsValidPitKiln() && !GetBehavior<BEBehaviorBurning>().IsBurning;
        

        public void TryIgnite(IPlayer byPlayer)
        {
            BurningUntilTotalHours = Api.World.Calendar.TotalHours + BurnTimeHours;

            var bh = GetBehavior<BEBehaviorBurning>();
            Lit = true;
            bh.OnFirePlaced(Pos.UpCopy(), Pos.Copy(), byPlayer?.PlayerUID);

            Api.World.BlockAccessor.ExchangeBlock(Block.Id, Pos); // Forces a relight of this block

            MarkDirty(true);
        }

        public override string GetBlockName()
        {
            return Lang.Get("Pit kiln");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (inventory.Empty) return;

            string[] contentSummary = getContentSummary();

            foreach (var line in contentSummary) dsc.AppendLine(line);

            if (Lit) dsc.AppendLine(Lang.Get("Lit"));
            else dsc.AppendLine(Lang.Get("Unlit"));
        }


        public override string[] getContentSummary()
        {
            API.Datastructures.OrderedDictionary<string, int> dict = new ();

            for (int i = 0; i < 4; i++)
            {
                ItemSlot slot = inventory[i];
                if (slot.Empty) continue;

                string stackName = slot.Itemstack.GetName();
                if (!dict.TryGetValue(stackName, out int cnt)) cnt = 0;

                dict[stackName] = cnt + slot.StackSize;
            }

            return dict.Select(elem => Lang.Get("{0}x {1}", elem.Value, elem.Key)).ToArray();
        }


        public void KillFire(bool consumefuel)
        {
            if (!consumefuel)
            {
                Lit = false;
                Api.World.BlockAccessor.RemoveBlockLight((Block as BlockPitkiln).litKilnLightHsv, Pos);
                MarkDirty(true);
                return; // Probably extinguished by rain
            }

            if (Api.Side == EnumAppSide.Client) return;

            Block blockgs = Api.World.GetBlock(new AssetLocation("groundstorage"));
            Api.World.BlockAccessor.SetBlock(blockgs.Id, Pos);
            Api.World.BlockAccessor.RemoveBlockLight((Block as BlockPitkiln).litKilnLightHsv, Pos);

            var begs = Api.World.BlockAccessor.GetBlockEntity(Pos) as BlockEntityGroundStorage;

            ItemStack sourceStack = inventory.FirstNonEmptySlot?.Itemstack;
            var storeprops = sourceStack?.Collectible.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;

            begs.ForceStorageProps(storeprops ?? StorageProps);

            for (int i = 0; i < 4; i++)
            {
                begs.Inventory[i] = inventory[i];
            }

            MarkDirty(true);
            Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
        }
    }

}
