using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{

    public class BlockEntityClayForm : BlockEntity
    {
        static BlockEntityClayForm()
        {

        }

        // Permanent data
        ItemStack workItemStack;
        int selectedRecipeNumber = -1;
        public int AvailableVoxels;
        public bool[,,] Voxels = new bool[16, 16, 16];

        // Temporary data, generated on be creation

        /// <summary>
        /// The base material used for the work item, used to check melting point
        /// </summary>
        ItemStack baseMaterial;

        Cuboidf[] selectionBoxes = new Cuboidf[0];
        public int didBeginUse;
        ClayFormRenderer workitemRenderer;


        public ClayFormingRecipe SelectedRecipe
        {
            get { return selectedRecipeNumber >= 0 && api != null ? api.World.ClayFormingRecipes[selectedRecipeNumber] : null; }
        }

        public bool CanWorkCurrent
        {
            get { return workItemStack != null && CanWork(workItemStack); }
        }


        public BlockEntityClayForm() : base() { }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(api.World);
                baseMaterial = new ItemStack(api.World.GetItem(new AssetLocation("clay-" + workItemStack.Collectible.LastCodePart())));
            }

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(workitemRenderer = new ClayFormRenderer(pos, capi), EnumRenderStage.Opaque);
                capi.Event.RegisterRenderer(workitemRenderer, EnumRenderStage.AfterFinalComposition);

                RegenMeshAndSelectionBoxes();
            }
        }


        public bool CanWork(ItemStack stack)
        {
            return true;
        }


        internal Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            return selectionBoxes;
        }
        


        public void PutClay(IItemSlot slot)
        {
            if (workItemStack == null)
            {
                if (api.World is IClientWorldAccessor)
                {
                    OpenDialog(api.World as IClientWorldAccessor, pos, slot.Itemstack);
                }

                CreateInitialWorkItem();
                workItemStack = new ItemStack(api.World.GetItem(new AssetLocation("clayworkitem-" + slot.Itemstack.Collectible.LastCodePart())));
                baseMaterial = new ItemStack(api.World.GetItem(new AssetLocation("clay-" + slot.Itemstack.Collectible.LastCodePart())));
            }

            AvailableVoxels += 25;

            slot.TakeOut(1);
            slot.MarkDirty();

            RegenMeshAndSelectionBoxes();
            MarkDirty();
        }

        

        internal void OnBeginUse(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (SelectedRecipe == null)
            {
                if (api.Side == EnumAppSide.Client)
                {
                    OpenDialog(api.World as IClientWorldAccessor, pos, byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack);
                }
                
                return;
            }

            didBeginUse++;
        }




        internal void OnUseOver(IPlayer byPlayer, int selectionBoxIndex, BlockFacing facing, bool mouseBreakMode)
        {
            if (selectionBoxIndex < 0 || selectionBoxIndex >= selectionBoxes.Length) return;

            Cuboidf box = selectionBoxes[selectionBoxIndex];

            Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));

            OnUseOver(byPlayer, voxelPos, facing, mouseBreakMode);
        }


        internal void OnUseOver(IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool mouseBreakMode)
        {
            if (voxelPos == null)
            {
                didBeginUse = Math.Max(didBeginUse, didBeginUse - 1);
                return;
            }

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos, facing, mouseBreakMode);
                
            }


            IItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null || !CanWorkCurrent)
            {
                didBeginUse = Math.Max(didBeginUse, didBeginUse - 1);
                return;
            }
            int toolMode = slot.Itemstack.Collectible.GetToolMode(slot, byPlayer, new BlockSelection() { Position = pos });

            float yaw = GameMath.Mod(byPlayer.Entity.Pos.Yaw, 2 * GameMath.PI);
            BlockFacing towardsFace = BlockFacing.HorizontalFromAngle(yaw);

            


            if (didBeginUse > 0)
            {
                bool didmodify = false;

                if (toolMode == 3)
                {
                    if (!mouseBreakMode) didmodify = OnCopyLayer();
                    else toolMode = 1;
                }

                if (toolMode != 3)
                {
                    didmodify = mouseBreakMode ? OnRemove(voxelPos, facing, toolMode) : OnAdd(voxelPos, facing, toolMode);
                }                


                if (didmodify && api.Side == EnumAppSide.Client)
                {
                    api.World.PlaySoundAt(new AssetLocation("sounds/player/clayform.ogg"), byPlayer, null, true, 8);
                }
                
                RegenMeshAndSelectionBoxes();
                api.World.BlockAccessor.MarkBlockDirty(pos);
                api.World.BlockAccessor.MarkBlockEntityDirty(pos);

                if (!HasAnyVoxel())
                {
                    AvailableVoxels = 0;
                    workItemStack = null;
                    didBeginUse = 0;
                    return;
                }
            }

            didBeginUse = Math.Max(didBeginUse, didBeginUse - 1);
            CheckIfFinished(byPlayer);
            MarkDirty();
        }


        public void CheckIfFinished(IPlayer byPlayer)
        {
            if (MatchesRecipe() && api.World is IServerWorldAccessor)
            {
                workItemStack = null;
                Voxels = new bool[16, 16, 16];
                AvailableVoxels = 0;
                ItemStack outstack = SelectedRecipe.Output.ResolvedItemstack.Clone();
                selectedRecipeNumber = -1;

                if (outstack.StackSize == 1 && outstack.Class == EnumItemClass.Block)
                {
                    api.World.BlockAccessor.SetBlock(outstack.Block.BlockId, pos);
                    return;
                }

                while (outstack.StackSize > 0)
                {
                    ItemStack dropStack = outstack.Clone();
                    dropStack.StackSize = Math.Min(outstack.StackSize, outstack.Collectible.MaxStackSize);
                    outstack.StackSize -= dropStack.StackSize;

                    if(byPlayer.InventoryManager.TryGiveItemstack(dropStack))
                    {
                        api.World.PlaySoundAt(new AssetLocation("sounds/player/collect"), byPlayer);
                    }
                    else
                    {
                        api.World.SpawnItemEntity(dropStack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }

                api.World.BlockAccessor.SetBlock(0, pos);
            }
        }

        private bool MatchesRecipe()
        {
            if (SelectedRecipe == null) return false;
            return NextNotMatchingRecipeLayer() >= SelectedRecipe.Pattern.Length;
        }


        private int NextNotMatchingRecipeLayer()
        {
            if (SelectedRecipe == null) return 0;

            for (int layer = 0; layer < 16; layer++)
            {
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (Voxels[x, layer, z] != SelectedRecipe.Voxels[x, layer, z])
                        {
                            
                            return layer;
                        }
                    }
                }
            }

            return 16;
        }

        Cuboidi LayerBounds(int layer)
        {
            Cuboidi bounds = new Cuboidi(8, 8, 8, 8, 8, 8);

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (SelectedRecipe.Voxels[x, layer, z])
                    {
                        bounds.X1 = Math.Min(bounds.X1, x);
                        bounds.X2 = Math.Max(bounds.X2, x);
                        bounds.Z1 = Math.Min(bounds.Z1, z);
                        bounds.Z2 = Math.Max(bounds.Z2, z);
                    }
                }
            }

            return bounds;
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

        public bool InBounds(Vec3i voxelPos, int layer)
        {
            if (layer < 0 || layer >= 16) return false;

            Cuboidi bounds = LayerBounds(layer);

            return voxelPos.X >= bounds.X1 && voxelPos.X <= bounds.X2 && voxelPos.Y >= 0 && voxelPos.Y < 16 && voxelPos.Z >= bounds.Z1 && voxelPos.Z <= bounds.Z2;
        }

        private bool OnRemove(Vec3i voxelPos, BlockFacing facing, int radius)
        {
            bool didremove = false;
            int layer = NextNotMatchingRecipeLayer();
            if (voxelPos.Y != layer) return didremove;

            for (int dx = -(int)Math.Ceiling(radius/2f); dx <= radius /2; dx++)
            {
                for (int dz = -(int)Math.Ceiling(radius / 2f); dz <= radius / 2; dz++)
                {
                    Vec3i offPos = voxelPos.AddCopy(dx, 0, dz);
                    
                    if (offPos.X >= 0 && offPos.X < 16 && offPos.Y >= 0 && offPos.Y <= 16 && offPos.Z >= 0 && offPos.Z < 16)
                    {
                        bool hadVoxel = Voxels[offPos.X, offPos.Y, offPos.Z];
                        didremove |= hadVoxel;

                        Voxels[offPos.X, offPos.Y, offPos.Z] = false;
                        if(hadVoxel) AvailableVoxels++;
                    }
                }
            }

            return didremove;
        }

        private bool OnCopyLayer()
        {
            int layer = NextNotMatchingRecipeLayer();
            if (layer == 0) return false;

            bool didplace = false;
            Vec3i voxelPos = new Vec3i();

            int quantity = 4;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (Voxels[x, layer - 1, z] && !Voxels[x, layer, z])
                    {
                        quantity--;
                        Voxels[x, layer, z] = true;
                        AvailableVoxels--;
                        didplace = true;
                    }

                    if (quantity == 0) return didplace;
                }
            }

            return didplace;
        }


        private bool OnAdd(Vec3i voxelPos, BlockFacing facing, int radius)
        {
            int layer = NextNotMatchingRecipeLayer();

            if (voxelPos.Y == layer && facing.IsVertical)
            {
                return OnAdd(voxelPos, radius, layer);
            }

            if (Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z])
            {
                Vec3i offPoss = voxelPos.AddCopy(facing);
                if (InBounds(offPoss, layer))
                {
                    return OnAdd(offPoss, radius, layer);
                }
            }
            else
            {
                return OnAdd(voxelPos, radius, layer);
            }

            return false;
        }
        
        bool OnAdd(Vec3i voxelPos, int radius, int layer)
        {
            bool didadd = false;

            for (int dx = -(int)Math.Ceiling(radius / 2f); dx <= radius / 2; dx++)
            {
                for (int dz = -(int)Math.Ceiling(radius / 2f); dz <= radius / 2; dz++)
                {
                    Vec3i offPos = voxelPos.AddCopy(dx, 0, dz);
                    if (InBounds(offPos, layer) && offPos.Y == layer)
                    {
                        if (!Voxels[offPos.X, offPos.Y, offPos.Z])
                        {
                            AvailableVoxels--;
                            didadd = true;
                        }
                        Voxels[offPos.X, offPos.Y, offPos.Z] = true;
                    }
                }
            }

            return didadd;
        }


        void RegenMeshAndSelectionBoxes()
        {
            int layer = NextNotMatchingRecipeLayer();

            if (workitemRenderer != null)
            {    
                if (layer != 16) workitemRenderer.RegenMesh(workItemStack, Voxels, SelectedRecipe, layer);
            }

            List<Cuboidf> boxes = new List<Cuboidf>();

            bool[,,] recipeVoxels = SelectedRecipe?.Voxels;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        if (y == 0 || Voxels[x, y, z] || (recipeVoxels!=null && y == layer && recipeVoxels[x, y, z]))
                        {
                            // Console.WriteLine("box {0} is voxel at {1},{2}", boxes.Count, x, z);
                            boxes.Add(new Cuboidf(x / 16f, y / 16f, z / 16f, x / 16f + 1 / 16f, y / 16f + 1 / 16f, z / 16f + 1 / 16f));
                        }
                    }
                }
            }

            selectionBoxes = boxes.ToArray();
        }


        public void CreateInitialWorkItem()
        {
            Voxels = new bool[16, 16, 16];

            for (int x = 4; x < 12; x++)
            {
                for (int z = 4; z < 12; z++)
                {
                    Voxels[x, 0, z] = true;
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


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            deserializeVoxels(tree.GetBytes("voxels"));
            workItemStack = tree.GetItemstack("workItemStack");
            AvailableVoxels = tree.GetInt("availableVoxels");
            selectedRecipeNumber = tree.GetInt("selectedRecipeNumber", -1);

            if (api != null && workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(api.World);
                baseMaterial = new ItemStack(api.World.GetItem(new AssetLocation("clay-" + workItemStack.Collectible.LastCodePart())));
            }

            RegenMeshAndSelectionBoxes();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("voxels", serializeVoxels());
            tree.SetItemstack("workItemStack", workItemStack);
            tree.SetInt("availableVoxels", AvailableVoxels);
            tree.SetInt("selectedRecipeNumber", selectedRecipeNumber);
        }


        byte[] serializeVoxels()
        {
            byte[] data = new byte[16 * 16 * 16 / 8];
            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
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

            if (data == null || data.Length < 16 * 16 * 16 / 8) return;

            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
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



        public void SendUseOverPacket(IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool mouseMode)
        {
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                writer.Write(voxelPos.X);
                writer.Write(voxelPos.Y);
                writer.Write(voxelPos.Z);
                writer.Write(mouseMode);
                writer.Write((ushort)facing.Index);
                data = ms.ToArray();
            }

            ((ICoreClientAPI)api).Network.SendBlockEntityPacket(
                pos.X, pos.Y, pos.Z,
                (int)EnumClayFormingPacket.OnUserOver,
                data
            );
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumClayFormingPacket.CancelSelect)
            {
                if (baseMaterial != null)
                {
                    api.World.SpawnItemEntity(baseMaterial, pos.ToVec3d().Add(0.5));
                }
                api.World.BlockAccessor.SetBlock(0, pos);
            }

            if (packetid == (int)EnumClayFormingPacket.SelectRecipe)
            {
                int num;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    num = reader.ReadInt32();
                }
                if(!TrySetSelectedRecipe(num))
                {
                    return;
                }

                // Tell server to save this chunk to disk again
                MarkDirty();
                api.World.BlockAccessor.GetChunkAtBlockPos(pos.X, pos.Y, pos.Z).MarkModified();
            }

            if (packetid == (int)EnumClayFormingPacket.OnUserOver)
            {
                Vec3i voxelPos;
                bool mouseMode;
                BlockFacing facing;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    voxelPos = new Vec3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    mouseMode = reader.ReadBoolean();
                    facing = BlockFacing.ALLFACES[reader.ReadInt16()];

                }

             //   api.World.Logger.Notification("ok got use over packet from {0} at pos {1}", player.PlayerName, voxelPos);

                OnUseOver(player, voxelPos, facing, mouseMode);
            }
        }



        private bool TrySetSelectedRecipe(int num)
        {
            baseMaterial = new ItemStack(api.World.GetItem(new AssetLocation("clay-" + workItemStack.Collectible.LastCodePart())));

            OrderedDictionary<int, ItemStack> stacks = new OrderedDictionary<int, ItemStack>();

            int i = 0;
            foreach (ClayFormingRecipe recipe in api.World.ClayFormingRecipes)
            {
                if (recipe.Ingredient.SatisfiesAsIngredient(baseMaterial))
                {
                    stacks[i] = recipe.Output.ResolvedItemstack;
                }
                i++;
            }

            if(num >= stacks.Count || num < 0)
            {
                return false;
            }
            else
            {
                selectedRecipeNumber = stacks.GetKeyAtIndex(num);
                return true;
            }
        }



        public static void OpenDialog(IClientWorldAccessor world, BlockPos pos, ItemStack ingredient)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            if (ingredient.Collectible is ItemWorkItem)
            {
                ingredient = new ItemStack(world.GetItem(new AssetLocation("clay-" + ingredient.Collectible.LastCodePart())));
            }

            foreach (ClayFormingRecipe recipe in world.ClayFormingRecipes)
            {
                if (recipe.Ingredient.SatisfiesAsIngredient(ingredient))
                {
                    stacks.Add(recipe.Output.ResolvedItemstack);
                }
            }

            world.OpenDialog("BlockEntityRecipeSelector", "Select recipe", stacks.ToArray(), pos);
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            if (workItemStack == null)
            {
                return "";
            }

            float temperature = workItemStack.Collectible.GetTemperature(api.World, workItemStack);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang.Get("Available Voxels: {0}", AvailableVoxels));

            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi != null)
            {
                HotKey k = null;
                capi.HotKeys.TryGetValue("toolmodeselect", out k);
                if (k != null)
                {
                    sb.AppendLine(Lang.Get("Hit '{0}' to select tool mode for quicker crafting", k.CurrentMapping.ToString()));
                }

            }

            return sb.ToString();
        }


  
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            workitemRenderer?.Unregister();
        }

    }

    public enum EnumClayFormingPacket
    {
        OpenDialog = 1000,
        SelectRecipe = 1001,
        OnUserOver = 1002,
        CancelSelect = 1003
    }
}
