using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockEntityAnvil : BlockEntity
    {
        public static SimpleParticleProperties bigMetalSparks;
        public static SimpleParticleProperties smallMetalSparks;
        
        static BlockEntityAnvil()
        {
            smallMetalSparks = new SimpleParticleProperties(
                2, 5,
                ColorUtil.ToRgba(255, 255, 233, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-3f, 8f, -3f),
                new Vec3f(3f, 12f, 3f),
                0.1f,
                1f,
                0.25f, 0.25f,
                EnumParticleModel.Quad
            );
            smallMetalSparks.glowLevel = 128;
            smallMetalSparks.addPos.Set(1 / 16f, 0, 1 / 16f);
            smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.05f);


            bigMetalSparks = new SimpleParticleProperties(
                2, 8,
                ColorUtil.ToRgba(255, 255, 233, 83),
                new Vec3d(), new Vec3d(),
                new Vec3f(-1f, 0.5f, -1f),
                new Vec3f(2f, 1.5f, 2f),
                0.5f,
                1f,
                0.25f, 0.25f
            );
            bigMetalSparks.glowLevel = 128;
            bigMetalSparks.addPos.Set(1 / 16f, 0, 1 / 16f);
            bigMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);
        }

        // Permanent data
        ItemStack workItemStack;
        int selectedRecipeId = -1;
        public int AvailableVoxels;
        public bool[,,] Voxels = new bool[16, 16, 16];

        // Temporary data, generated on be creation

        Dictionary<string, MetalPropertyVariant> metalsByCode;
        /// <summary>
        /// The base material used for the work item, used to check melting point
        /// </summary>
        ItemStack baseMaterial;
        
        Cuboidf[] selectionBoxes = new Cuboidf[1];
        public int OwnMetalTier;
        AnvilWorkItemRenderer workitemRenderer;


        public SmithingRecipe SelectedRecipe
        {
            get { return Api.World.SmithingRecipes.FirstOrDefault(r => r.RecipeId == selectedRecipeId); }
        }

        public bool CanWorkCurrent
        {
            get { return workItemStack != null && CanWork(workItemStack); }
        }

        public ItemStack WorkItem
        {
            get { return workItemStack; }
        }

        public ItemStack BaseMaterial
        {
            get { return baseMaterial; }
        }

        public BlockEntityAnvil() : base() { }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            metalsByCode = new Dictionary<string, MetalPropertyVariant>();

            MetalProperty metals = api.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();
            for (int i = 0; i < metals.Variants.Length; i++)
            {
                // Metals currently don't have a domain
                metalsByCode[metals.Variants[i].Code.Path] = metals.Variants[i]; 
            }

            if (workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(api.World);
                baseMaterial = new ItemStack(api.World.GetItem(new AssetLocation("ingot-" + workItemStack.Collectible.LastCodePart())));
            }

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(workitemRenderer = new AnvilWorkItemRenderer(Pos, capi), EnumRenderStage.Opaque);
                capi.Event.RegisterRenderer(workitemRenderer, EnumRenderStage.AfterFinalComposition);

                RegenMeshAndSelectionBoxes();
            }

            string metalType = Block.LastCodePart();
            if (metalsByCode.ContainsKey(metalType)) OwnMetalTier = metalsByCode[metalType].Tier;
        }


        public bool CanWork(ItemStack stack)
        {
            float temperature = stack.Collectible.GetTemperature(Api.World, stack);
            float meltingpoint = stack.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(baseMaterial));

            if (stack.Collectible.Attributes?["workableTemperature"].Exists == true)
            {
                return stack.Collectible.Attributes["workableTemperature"].AsFloat(meltingpoint / 2) <= temperature;
            }

            return temperature >= meltingpoint / 2;
        }


        internal Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            return selectionBoxes;
        }

        internal bool OnPlayerInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer.Entity.Controls.Sneak)
            {
                return TryPut(world, byPlayer, blockSel);
            } else
            {
                return TryTake(world, byPlayer, blockSel);
            }
        }

        private bool TryTake(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (workItemStack == null) return false;

            workItemStack.Attributes.SetBytes("voxels", serializeVoxels());
            workItemStack.Attributes.SetInt("availableVoxels", AvailableVoxels);
            workItemStack.Attributes.SetInt("selectedRecipeId", selectedRecipeId);

            if (!byPlayer.InventoryManager.TryGiveItemstack(workItemStack))
            {
                Api.World.SpawnItemEntity(workItemStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            workItemStack = null;
            Voxels = new bool[16, 16, 16];
            AvailableVoxels = 0;

            RegenMeshAndSelectionBoxes();
            MarkDirty();

            return true;
        }


        private bool TryPut(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null) return false;

            ItemStack stack = slot.Itemstack;

            string metalType = stack.Collectible.LastCodePart();
            bool viableTier = metalsByCode.ContainsKey(metalType) && metalsByCode[metalType].Tier <= OwnMetalTier + 1;
            bool viableBaseMaterial = stack.Collectible is ItemIngot && CanWork(stack) && viableTier;

            // Place ingot
            if (viableBaseMaterial && (workItemStack == null || workItemStack.Collectible.LastCodePart().Equals(stack.Collectible.LastCodePart())))
            {
                if (workItemStack == null)
                {
                    if (world is IClientWorldAccessor)
                    {
                        OpenDialog(stack);
                    }

                    CreateInitialWorkItem();

                    workItemStack = new ItemStack(Api.World.GetItem(new AssetLocation("workitem-" + stack.Collectible.LastCodePart())));
                    workItemStack.Collectible.SetTemperature(Api.World, workItemStack, stack.Collectible.GetTemperature(Api.World, stack));

                    baseMaterial = new ItemStack(Api.World.GetItem(new AssetLocation("ingot-" + stack.Collectible.LastCodePart())));

                    List<SmithingRecipe> recipes = Api.World.SmithingRecipes
                        .Where(r => r.Ingredient.SatisfiesAsIngredient(baseMaterial))
                        .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code)
                        .ToList()
                    ;

                    selectedRecipeId = recipes[0].RecipeId;
                }

                AvailableVoxels += 32;

                stack.StackSize--;
                if (stack.StackSize <= 0)
                {
                    slot.Itemstack = null;
                }
                slot.MarkDirty();

                RegenMeshAndSelectionBoxes();
                MarkDirty();
                return true;
            }

            // Place workitem
            bool viableWorkItem = stack.Collectible.FirstCodePart().Equals("workitem") && viableTier;
            if (viableWorkItem)
            {
                try
                {
                    deserializeVoxels(slot.Itemstack.Attributes.GetBytes("voxels"));
                    AvailableVoxels = slot.Itemstack.Attributes.GetInt("availableVoxels");
                    selectedRecipeId = slot.Itemstack.Attributes.GetInt("selectedRecipeId");

                    workItemStack = stack.Clone();
                    slot.Itemstack = null;
                    slot.MarkDirty();
                }
                catch (Exception)
                {

                }

                if (selectedRecipeId < 0 && world is IClientWorldAccessor)
                {
                    OpenDialog(stack);
                }


                RegenMeshAndSelectionBoxes();
                CheckIfFinished(byPlayer);
                MarkDirty();
                return true;
            }

            return false;
        }

        internal void OnBeginUse(IPlayer byPlayer, BlockSelection blockSel)
        {
        }




        internal void OnUseOver(IPlayer byPlayer, int selectionBoxIndex)
        {
            // box index 0 is the anvil itself
            if (selectionBoxIndex <= 0 || selectionBoxIndex >= selectionBoxes.Length) return;

            Cuboidf box = selectionBoxes[selectionBoxIndex];

            Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));

            OnUseOver(byPlayer, voxelPos, new BlockSelection() { Position = Pos, SelectionBoxIndex = selectionBoxIndex });
        }


        internal void OnUseOver(IPlayer byPlayer, Vec3i voxelPos, BlockSelection blockSel)
        {
            if (voxelPos == null)
            {
                return;
            }

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (Api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos);
            }


            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null || !CanWorkCurrent)
            {
                return;
            }
            int toolMode = slot.Itemstack.Collectible.GetToolMode(slot, byPlayer, blockSel);

            float yaw = GameMath.Mod(byPlayer.Entity.Pos.Yaw, 2 * GameMath.PI);
            BlockFacing towardsFace = BlockFacing.HorizontalFromAngle(yaw);


            float temp = workItemStack.Collectible.GetTemperature(Api.World, workItemStack);
            
            if (Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z])
            {
                if (temp > 800)
                {
                    bigMetalSparks.minPos = Pos.ToVec3d().AddCopy(voxelPos.X / 16f, voxelPos.Y / 16f + 0.0625f, voxelPos.Z / 16f);
                    bigMetalSparks.glowLevel = (byte)GameMath.Clamp((int)(temp - 770) / 3, 32, 128);
                    byPlayer.Entity.World.SpawnParticles(bigMetalSparks, byPlayer);

                    smallMetalSparks.minPos = Pos.ToVec3d().AddCopy(voxelPos.X / 16f, voxelPos.Y / 16f + 0.0625f, voxelPos.Z / 16f);
                    smallMetalSparks.glowLevel = (byte)GameMath.Clamp((int)(temp - 770) / 3, 32, 128);
                    smallMetalSparks.model = EnumParticleModel.Quad;
                    smallMetalSparks.lifeLength = 0.03f;
                    smallMetalSparks.minVelocity = new Vec3f(-1f, 1f, -1f);
                    smallMetalSparks.addVelocity = new Vec3f(2f, 2f, 2f);
                    smallMetalSparks.minQuantity = 4;
                    smallMetalSparks.addQuantity = 6;
                    smallMetalSparks.minSize = 0.1f;
                    smallMetalSparks.maxSize = 0.1f;
                    smallMetalSparks.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.1f);
                    byPlayer.Entity.World.SpawnParticles(smallMetalSparks, byPlayer);
                }


                switch (toolMode)
                {
                    case 0: OnHit(voxelPos); break;
                    case 1: OnUpset(voxelPos, BlockFacing.NORTH.FaceWhenRotatedBy(0, yaw - GameMath.PIHALF, 0)); break;
                    case 2: OnUpset(voxelPos, BlockFacing.EAST.FaceWhenRotatedBy(0, yaw - GameMath.PIHALF, 0)); break;
                    case 3: OnUpset(voxelPos, BlockFacing.SOUTH.FaceWhenRotatedBy(0, yaw - GameMath.PIHALF, 0)); break;
                    case 4: OnUpset(voxelPos, BlockFacing.WEST.FaceWhenRotatedBy(0, yaw - GameMath.PIHALF, 0)); break;
                    case 5: OnSplit(voxelPos); break;
                }

                RegenMeshAndSelectionBoxes();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
                slot.Itemstack.Collectible.DamageItem(Api.World, byPlayer.Entity, slot);

                if (!HasAnyVoxel())
                {
                    AvailableVoxels = 0;
                    workItemStack = null;
                    return;
                }
            }

            CheckIfFinished(byPlayer);
            MarkDirty();
        }


        public void CheckIfFinished(IPlayer byPlayer)
        {
            if (MatchesRecipe() && Api.World is IServerWorldAccessor)
            {
                workItemStack = null;
                Voxels = new bool[16, 16, 16];
                AvailableVoxels = 0;
                ItemStack outstack = SelectedRecipe.Output.ResolvedItemstack.Clone();
                selectedRecipeId = -1;

                if (!byPlayer.InventoryManager.TryGiveItemstack(outstack))
                {
                    Api.World.SpawnItemEntity(outstack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                RegenMeshAndSelectionBoxes();
                MarkDirty();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }

        private bool MatchesRecipe()
        {
            if (SelectedRecipe == null) return false;
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (Voxels[x, 10, z] != SelectedRecipe.Voxels[x, z])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

 

        bool HasAnyVoxel()
        {
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (Voxels[x, y, z]) return true;
                    }
                }
            }

            return false;
        }



        private void OnSplit(Vec3i voxelPos)
        {
            Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z] = false;
        }

        private void OnUpset(Vec3i voxelPos, BlockFacing towardsFace)
        {
            if (AvailableVoxels <= 0) return;
            Vec3i npos = voxelPos.Add(towardsFace);

            if (npos.X >= 0 && npos.X < 16 && npos.Y >= 0 && npos.Y < 16 && npos.Z >= 0 && npos.Z < 16 && !Voxels[npos.X, npos.Y, npos.Z])
            {
                Voxels[npos.X, npos.Y, npos.Z] = true;
                AvailableVoxels--;
            }
        }

        private void OnHit(Vec3i voxelPos)
        {
            if (AvailableVoxels <= 0) return;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vec3i npos = voxelPos.AddCopy(dx, 0, dz);

                    if (npos.X >= 0 && npos.X < 16 && npos.Y >= 0 && npos.Y < 16 && npos.Z >= 0 && npos.Z < 16 && !Voxels[npos.X, npos.Y, npos.Z])
                    {
                        Voxels[npos.X, npos.Y, npos.Z] = true;
                        AvailableVoxels--;
                        if (AvailableVoxels <= 0)
                        {
                            return;
                        }
                    }
                }
            }
        }


        void RegenMeshAndSelectionBoxes()
        {
            if (workitemRenderer != null)
            {
                workitemRenderer.RegenMesh(workItemStack, Voxels, SelectedRecipe);
            }

            List<Cuboidf> boxes = new List<Cuboidf>();
            boxes.Add(null);

            for (int x = 0; x < 16; x++)
            {
                int y = 10;
                //for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (Voxels[x, y, z])
                        {
                           
                            boxes.Add(new Cuboidf(x / 16f, y / 16f, z / 16f, x / 16f + 1 / 16f, y / 16f + 1 / 16f, z / 16f + 1 / 16f));
                        }
                    }
                }
            }

            selectionBoxes = boxes.ToArray();
        }


        private void CreateInitialWorkItem()
        {
            Voxels = new bool[16, 16, 16];

            for (int x = 0; x < 7; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    Voxels[4 + x, 10, 6 + z] = true;
                }
            }
        }


        public override void OnBlockRemoved()
        {
            if (workitemRenderer != null)
            {
                workitemRenderer.Unregister();
                workitemRenderer = null;
            }
        }

        public override void OnBlockBroken()
        {
            if (workItemStack != null)
            {
                workItemStack.Attributes.SetBytes("voxels", serializeVoxels());
                workItemStack.Attributes.SetInt("availableVoxels", AvailableVoxels);
                workItemStack.Attributes.SetInt("selectedRecipeId", selectedRecipeId);

                Api.World.SpawnItemEntity(workItemStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            deserializeVoxels(tree.GetBytes("voxels"));
            workItemStack = tree.GetItemstack("workItemStack");
            AvailableVoxels = tree.GetInt("availableVoxels");
            selectedRecipeId = tree.GetInt("selectedRecipeId", -1);

            if (Api != null && workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(Api.World);
                baseMaterial = new ItemStack(Api.World.GetItem(new AssetLocation("ingot-" + workItemStack.Collectible.LastCodePart())));
            }

            RegenMeshAndSelectionBoxes();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("voxels", serializeVoxels());
            tree.SetItemstack("workItemStack", workItemStack);
            tree.SetInt("availableVoxels", AvailableVoxels);
            tree.SetInt("selectedRecipeId", selectedRecipeId);
        }


        byte[] serializeVoxels()
        {
            byte[] data = new byte[16 * 6 * 16 / 8];
            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 10; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        int bitpos = pos % 8;
                        data[pos / 8] |= (byte)((Voxels[x, y, z] ? 1 : 0) << bitpos);
                        pos++;
                    }
                }
            }

            return data;
        }

        void deserializeVoxels(byte[] data)
        {
            Voxels = new bool[16, 16, 16];

            if (data == null || data.Length < 16 * 6 * 16 / 8) return;

            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 10; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        int bitpos = pos % 8;
                        Voxels[x, y, z] = (data[pos / 8] & (1 << bitpos)) > 0;
                        pos++;
                    }
                }
            }
        }



        public void SendUseOverPacket(IPlayer byPlayer, Vec3i voxelPos)
        {
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                writer.Write(voxelPos.X);
                writer.Write(voxelPos.Y);
                writer.Write(voxelPos.Z);
                data = ms.ToArray();
            }

            ((ICoreClientAPI)Api).Network.SendBlockEntityPacket(
                Pos.X, Pos.Y, Pos.Z,
                (int)EnumAnvilPacket.OnUserOver,
                data
            );
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumAnvilPacket.SelectRecipe)
            {
                int recipeid = SerializerUtil.Deserialize<int>(data);
                SmithingRecipe recipe = Api.World.SmithingRecipes.FirstOrDefault(r => r.RecipeId == recipeid);

                if (recipe == null)
                {
                    Api.World.Logger.Error("Client tried to selected smithing recipe with id {0}, but no such recipe exists!");
                    return;
                }

                selectedRecipeId = recipe.RecipeId;

                // Tell server to save this chunk to disk again
                MarkDirty();
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos.X, Pos.Y, Pos.Z).MarkModified();
            }

            if (packetid == (int)EnumAnvilPacket.OnUserOver)
            {
                Vec3i voxelPos;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    voxelPos = new Vec3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                }

                OnUseOver(player, voxelPos, new BlockSelection() { Position = Pos });
            }
        }


        
        internal void OpenDialog(ItemStack ingredient)
        {
            if (ingredient.Collectible is ItemWorkItem)
            {
                ingredient = new ItemStack(Api.World.GetItem(new AssetLocation("ingot-" + ingredient.Collectible.LastCodePart())));
            }

            List<SmithingRecipe> recipes = Api.World.SmithingRecipes
                .Where(r => r.Ingredient.SatisfiesAsIngredient(ingredient))
                .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code) // Cannot sort by name, thats language dependent!
                .ToList()
            ;

            List<ItemStack> stacks = recipes
                .Select(r => r.Output.ResolvedItemstack)
                .ToList()
            ;

            IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;
            ICoreClientAPI capi = Api as ICoreClientAPI;
            
            GuiDialog dlg = new GuiDialogBlockEntityRecipeSelector(
                Lang.Get("Select smithing recipe"),
                stacks.ToArray(),
                (selectedIndex) => {
                    selectedRecipeId = recipes[selectedIndex].RecipeId;
                    capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumClayFormingPacket.SelectRecipe, SerializerUtil.Serialize(recipes[selectedIndex].RecipeId));
                },
                () => {
                    capi.Network.SendBlockEntityPacket(Pos.X, Pos.Y, Pos.Z, (int)EnumClayFormingPacket.CancelSelect);
                },
                Pos,
                Api as ICoreClientAPI
            );

            dlg.TryOpen();
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (workItemStack == null || SelectedRecipe == null)
            {
                return;
            }

            float temperature = workItemStack.Collectible.GetTemperature(Api.World, workItemStack);


            dsc.AppendLine(Lang.Get("Output: {0}", SelectedRecipe.Output?.ResolvedItemstack?.GetName()));
            dsc.AppendLine(Lang.Get("Available Voxels: {0}", AvailableVoxels));
            dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temperature));
            if (!CanWorkCurrent)
            {
                dsc.AppendLine(Lang.Get("Too cold to work"));
            }
            if (AvailableVoxels <= 0)
            {
                dsc.AppendLine(Lang.Get("Add another hot ingot to continue smithing"));
            }
        }



        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            if (workItemStack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                workItemStack = null;
            } 
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            if (workItemStack != null)
            {
                if (workItemStack.Class == EnumItemClass.Item)
                {
                    blockIdMapping[workItemStack.Id] = workItemStack.Item.Code;
                }
                else
                {
                    itemIdMapping[workItemStack.Id] = workItemStack.Block.Code;
                }
            }
        }

        public override void OnBlockUnloaded()
        {
            workitemRenderer?.Unregister();
        }

    }

    public enum EnumAnvilPacket
    {
        OpenDialog = 1000,
        SelectRecipe = 1001,
        OnUserOver = 1002
    }
}
