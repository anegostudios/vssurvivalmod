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

    public class BlockEntityClayForm : BlockEntity
    {
        static BlockEntityClayForm()
        {

        }

        // Permanent data
        ItemStack workItemStack;
        int selectedRecipeId = -1;
        public int AvailableVoxels;
        public bool[,,] Voxels = new bool[16, 16, 16];

        // Temporary data, generated on be creation

        /// <summary>
        /// The base material used for the work item, used to check melting point
        /// </summary>
        ItemStack baseMaterial;

        Cuboidf[] selectionBoxes = new Cuboidf[0];

        ClayFormRenderer workitemRenderer;


        public ClayFormingRecipe SelectedRecipe
        {
            get { return Api != null ? Api.World.ClayFormingRecipes.FirstOrDefault(r => r.RecipeId == selectedRecipeId) : null; }
        }

        public bool CanWorkCurrent
        {
            get { return workItemStack != null && CanWork(workItemStack); }
        }

        public ItemStack BaseMaterial
        {
            get { return baseMaterial; }
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
                capi.Event.RegisterRenderer(workitemRenderer = new ClayFormRenderer(Pos, capi), EnumRenderStage.Opaque);
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
        


        public void PutClay(ItemSlot slot)
        {
            if (workItemStack == null)
            {
                if (Api.World is IClientWorldAccessor)
                {
                    OpenDialog(Api.World as IClientWorldAccessor, Pos, slot.Itemstack);
                }

                CreateInitialWorkItem();
                workItemStack = new ItemStack(Api.World.GetItem(new AssetLocation("clayworkitem-" + slot.Itemstack.Collectible.LastCodePart())));
                baseMaterial = new ItemStack(Api.World.GetItem(new AssetLocation("clay-" + slot.Itemstack.Collectible.LastCodePart())));
            }

            AvailableVoxels += 25;

            slot.TakeOut(1);
            slot.MarkDirty();

            RegenMeshAndSelectionBoxes();
            MarkDirty();
        }

        

        public void OnBeginUse(IPlayer byPlayer, BlockSelection blockSel)
        {
            //api.World.Logger.VerboseDebug("clay form on begin use");

            if (SelectedRecipe == null)
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    OpenDialog(Api.World as IClientWorldAccessor, Pos, byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack);
                }
                
                return;
            }
            
        }




        public void OnUseOver(IPlayer byPlayer, int selectionBoxIndex, BlockFacing facing, bool mouseBreakMode)
        {
            if (selectionBoxIndex < 0 || selectionBoxIndex >= selectionBoxes.Length) return;

            Cuboidf box = selectionBoxes[selectionBoxIndex];

            Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));

            OnUseOver(byPlayer, voxelPos, facing, mouseBreakMode);
        }


        public void OnUseOver(IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool mouseBreakMode)
        {
            if (SelectedRecipe == null) return;
            if (voxelPos == null)
            {
                return;
            }

            //api.World.Logger.VerboseDebug("clay form on user over start");

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (Api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos, facing, mouseBreakMode);
                
            }


            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null || !CanWorkCurrent)
            {
                
                return;
            }
            int toolMode = slot.Itemstack.Collectible.GetToolMode(slot, byPlayer, new BlockSelection() { Position = Pos });

            float yaw = GameMath.Mod(byPlayer.Entity.Pos.Yaw, 2 * GameMath.PI);
            BlockFacing towardsFace = BlockFacing.HorizontalFromAngle(yaw);


            //if (didBeginUse > 0)
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

                if (didmodify)
                {
                    //byPlayer.Entity.GetBehavior<EntityBehaviorHunger>()?.ConsumeSaturation(1f);
                }

                if (didmodify && Api.Side == EnumAppSide.Client)
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/player/clayform.ogg"), byPlayer, null, true, 8);
                }
                
                RegenMeshAndSelectionBoxes();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);

                if (!HasAnyVoxel())
                {
                    AvailableVoxels = 0;
                    workItemStack = null;
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                    //didBeginUse = 0;
                    return;
                }
            }


            //didBeginUse = Math.Max(0, didBeginUse - 1);
            CheckIfFinished(byPlayer);
            MarkDirty();

            //api.World.Logger.VerboseDebug("clay form on user over end");
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

                if (outstack.StackSize == 1 && outstack.Class == EnumItemClass.Block)
                {
                    Api.World.BlockAccessor.SetBlock(outstack.Block.BlockId, Pos);
                    return;
                }

                int tries = 500;
                while (outstack.StackSize > 0 && tries-- > 0)
                {
                    ItemStack dropStack = outstack.Clone();
                    dropStack.StackSize = Math.Min(outstack.StackSize, outstack.Collectible.MaxStackSize);
                    outstack.StackSize -= dropStack.StackSize;

                    if (byPlayer.InventoryManager.TryGiveItemstack(dropStack))
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/player/collect"), byPlayer);
                    }
                    else
                    {
                        Api.World.SpawnItemEntity(dropStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                    }
                }

                if (tries <= 1)
                {
                    Api.World.Logger.Error("Tried to drop finished clay forming item but failed after 500 times?! Gave up doing so. Out stack was " + outstack);
                }

                Api.World.BlockAccessor.SetBlock(0, Pos);
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
                        if (Voxels[x, y, z])
                        {
                            return true;
                        }
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
            selectedRecipeId = tree.GetInt("selectedRecipeId", -1);

            if (Api != null && workItemStack != null)
            {
                workItemStack.ResolveBlockOrItem(Api.World);
                baseMaterial = new ItemStack(Api.World.GetItem(new AssetLocation("clay-" + workItemStack.Collectible.LastCodePart())));
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

            ((ICoreClientAPI)Api).Network.SendBlockEntityPacket(
                Pos.X, Pos.Y, Pos.Z,
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
                    Api.World.SpawnItemEntity(baseMaterial, Pos.ToVec3d().Add(0.5));
                }
                Api.World.BlockAccessor.SetBlock(0, Pos);
            }

            if (packetid == (int)EnumClayFormingPacket.SelectRecipe)
            {
                int recipeid = SerializerUtil.Deserialize<int>(data);
                ClayFormingRecipe recipe = Api.World.ClayFormingRecipes.FirstOrDefault(r => r.RecipeId == recipeid);

                if (recipe == null)
                {
                    Api.World.Logger.Error("Client tried to selected clayforming recipe with id {0}, but no such recipe exists!");
                    return;
                }

                selectedRecipeId = recipe.RecipeId;
                // Tell server to save this chunk to disk again
                MarkDirty();
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos.X, Pos.Y, Pos.Z).MarkModified();
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



        public void OpenDialog(IClientWorldAccessor world, BlockPos pos, ItemStack ingredient)
        {
            if (ingredient.Collectible is ItemWorkItem)
            {
                ingredient = new ItemStack(world.GetItem(new AssetLocation("clay-" + ingredient.Collectible.LastCodePart())));
            }

            List<ClayFormingRecipe> recipes = world.ClayFormingRecipes
                .Where(r => r.Ingredient.SatisfiesAsIngredient(ingredient))
                .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code) // Cannot sort by name, thats language dependent!
                .ToList();
            ;
             

            List<ItemStack> stacks = recipes
                .Select(r => r.Output.ResolvedItemstack)
                .ToList()
            ;

            ICoreClientAPI capi = Api as ICoreClientAPI;
            
            GuiDialog dlg = new GuiDialogBlockEntityRecipeSelector(
                Lang.Get("Select recipe"), 
                stacks.ToArray(), 
                (selectedIndex) => {
                    capi.Logger.VerboseDebug("Select clay from recipe {0}, have {1} recipes.", selectedIndex, recipes.Count);

                    selectedRecipeId = recipes[selectedIndex].RecipeId;
                    capi.Network.SendBlockEntityPacket(pos.X, pos.Y, pos.Z, (int)EnumClayFormingPacket.SelectRecipe, SerializerUtil.Serialize(recipes[selectedIndex].RecipeId));
                },
                () => {
                    capi.Network.SendBlockEntityPacket(pos.X, pos.Y, pos.Z, (int)EnumClayFormingPacket.CancelSelect);
                },
                pos, 
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
            
            dsc.AppendLine(Lang.Get("Output: {0}", SelectedRecipe?.Output?.ResolvedItemstack?.GetName()));
            dsc.AppendLine(Lang.Get("Available Voxels: {0}", AvailableVoxels));
        }


  
        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            workitemRenderer?.Unregister();
        }



        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            workItemStack?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(workItemStack), blockIdMapping, itemIdMapping);
            baseMaterial?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(baseMaterial), blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping)
        {
            if (workItemStack?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                workItemStack = null;
            }

            if (baseMaterial?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                baseMaterial = null;
            }
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
