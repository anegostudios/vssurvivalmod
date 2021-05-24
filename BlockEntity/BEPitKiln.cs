using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{

    public class BlockEntityPitKiln : BlockEntityGroundStorage, IHeatSource
    {
        protected ILoadedSound ambientSound;
        protected BuildStage[] buildStages;
        protected Shape shape;
        protected MeshData mesh;
        protected string[] selectiveElements;
        protected int currentBuildStage;


        public bool Lit;
        public bool IsComplete => currentBuildStage >= buildStages.Length;
        public BuildStage NextBuildStage => buildStages[currentBuildStage];

        public double BurningUntilTotalHours;

        protected override int invSlotCount => 10;

        public override void Initialize(ICoreAPI api)
        {
            var bh = GetBehavior<BEBehaviorBurning>();

            bh.OnFireTick = (dt) => {
                if (api.World.Calendar.TotalHours >= BurningUntilTotalHours)
                {
                    OnFired();
                }
            };

            bh.ShouldBurn = () => Lit;
            bh.OnCanBurn = (dt) => true;

            base.Initialize(api);

            DetermineBuildStages();
        }


        public override bool OnPlayerInteract(IPlayer player, BlockSelection bs)
        {
            ItemSlot hotbarSlot = player.InventoryManager.ActiveHotbarSlot;

            if (hotbarSlot.Empty) return false;

            if (currentBuildStage < buildStages.Length)
            {
                BuildStage stage = buildStages[currentBuildStage];

                if (stage.Material.Equals(Api.World, hotbarSlot.Itemstack, GlobalConstants.IgnoredStackAttributes) && stage.Material.StackSize <= hotbarSlot.StackSize)
                {
                    int toMove = stage.Material.StackSize;
                    for (int i = 4; i < invSlotCount && toMove > 0; i++)
                    {
                        toMove -= hotbarSlot.TryPutInto(Api.World, inventory[i], toMove);
                    }

                    hotbarSlot.MarkDirty();

                    currentBuildStage++;
                    mesh = null;
                    MarkDirty(true);
                    updateSelectiveElements();
                }
            }


            DetermineStorageProperties(null);

            return true;
        }

        public override void DetermineStorageProperties(ItemSlot sourceSlot)
        {
            base.DetermineStorageProperties(sourceSlot);

            if (buildStages != null)
            {
                colSelBoxes[0].X1 = 0;
                colSelBoxes[0].X2 = 1;
                colSelBoxes[0].Z1 = 0;
                colSelBoxes[0].Z2 = 1;
                colSelBoxes[0].Y2 = Math.Max(colSelBoxes[0].Y2, buildStages[Math.Min(buildStages.Length - 1, currentBuildStage)].MinHitboxY2 / 16f);
            }
        }


        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return Lit ? 3 : 0;
        }


        public void OnFired()
        {
            if (IsValidPitKiln())
            {
                foreach (var slot in inventory)
                {
                    if (slot.Empty) continue;
                    ItemStack rawStack = slot.Itemstack;
                    ItemStack firedStack = rawStack.Collectible.CombustibleProps?.SmeltedStack?.ResolvedItemstack;

                    if (firedStack != null)
                    {
                        slot.Itemstack = firedStack.Clone();
                        slot.Itemstack.StackSize = rawStack.StackSize / rawStack.Collectible.CombustibleProps.SmeltedRatio;
                    }
                }
            }

            Block blockgs = Api.World.GetBlock(new AssetLocation("groundstorage"));
            Api.World.BlockAccessor.SetBlock(blockgs.Id, Pos);

            var begs = Api.World.BlockAccessor.GetBlockEntity(Pos) as BlockEntityGroundStorage;
            begs.ForceStorageProps(StorageProps);

            for (int i = 0; i < begs.Capacity; i++)
            {
                begs.Inventory[i] = inventory[i];
            }

            MarkDirty(true);
        }


        protected bool IsValidPitKiln()
        {
            var world = Api.World;
            
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

            inventory[4].Itemstack = byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(buildStages[0].Material.StackSize);
            currentBuildStage++;

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
            selectiveElements = new string[currentBuildStage];
            for (int i = 0; i < currentBuildStage; i++)
            {
                selectiveElements[i] = buildStages[i].ElementName;
            }

            colSelBoxes[0].X1 = 0;
            colSelBoxes[0].X2 = 1;
            colSelBoxes[0].Z1 = 0;
            colSelBoxes[0].Z2 = 1;
            colSelBoxes[0].Y2 = Math.Max(colSelBoxes[0].Y2, buildStages[Math.Min(buildStages.Length - 1, currentBuildStage)].MinHitboxY2 / 16f);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            currentBuildStage = tree.GetInt("currentBuildStage");
            Lit = tree.GetBool("lit");

            if (Api != null)
            {
                DetermineBuildStages();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("currentBuildStage", currentBuildStage);
            tree.SetBool("lit", Lit);
        }


        public override bool OnTesselation(ITerrainMeshPool meshdata, ITesselatorAPI tesselator)
        {
            DetermineBuildStages();
            if (mesh == null)
            {
                tesselator.TesselateShape(Block, shape, out mesh, null, null, selectiveElements);
                mesh.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 1.005f, 1.005f, 1.005f);
                mesh.Translate(0, GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 10)/500f, 0);
            }

            meshdata.AddMeshData(mesh);

            base.OnTesselation(meshdata, tesselator);

            return true;
        }

        public bool CanIgnite()
        {
            return IsComplete && IsValidPitKiln() && !GetBehavior<BEBehaviorBurning>().IsBurning;
        }

        public void TryIgnite(IPlayer byPlayer)
        {
            BurningUntilTotalHours = Api.World.Calendar.TotalHours + 24f;

            var bh = GetBehavior<BEBehaviorBurning>();
            bh.EffectOffset = new Vec3d(0, 1, 0);
            Lit = true;
            bh.OnFirePlaced(BlockFacing.UP, byPlayer.PlayerUID);
        }
    }

}
