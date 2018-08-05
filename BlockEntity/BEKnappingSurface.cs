using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityKnappingSurface : BlockEntity
    {
        // Permanent data
        int selectedRecipeNumber = -1;
        public bool[,] Voxels = new bool[16, 16];
        public ItemStack BaseMaterial;

        // Temporary data, generated on be creation


        Cuboidf[] selectionBoxes = new Cuboidf[0];
        KnappingRenderer workitemRenderer;
        public int DidBeginUse;

        public KnappingRecipe SelectedRecipe
        {
            get { return selectedRecipeNumber >= 0 ? api.World.KnappingRecipes[selectedRecipeNumber] : null; }
        }
        


        public BlockEntityKnappingSurface() : base() { }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            CreateInitialWorkItem();
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                workitemRenderer = new KnappingRenderer(pos, capi);

                RegenMeshAndSelectionBoxes();
            }

            if (BaseMaterial != null)
            {
                BaseMaterial.ResolveBlockOrItem(api.World);
            }
        }



        internal Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos)
        {
            return selectionBoxes;
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

            DidBeginUse++;
        }




        internal void OnUseOver(IPlayer byPlayer, int selectionBoxIndex, BlockFacing facing, bool mouseMode)
        {
            if (selectionBoxIndex < 0 || selectionBoxIndex >= selectionBoxes.Length) return;

            Cuboidf box = selectionBoxes[selectionBoxIndex];

            Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));

            OnUseOver(byPlayer, voxelPos, facing, mouseMode);
        }


        internal void OnUseOver(IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool mouseMode)
        {
            if (voxelPos == null)
            {
                DidBeginUse = Math.Max(DidBeginUse, DidBeginUse - 1);
                return;
            }

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos, facing, mouseMode);
            }

            IItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null)
            {
                DidBeginUse = Math.Max(DidBeginUse, DidBeginUse - 1);
                return;
            }

            int toolMode = slot.Itemstack.Collectible.GetToolMode(slot, byPlayer, new BlockSelection() { Position = pos });

            float yaw = GameMath.Mod(byPlayer.Entity.Pos.Yaw, 2 * GameMath.PI);
            BlockFacing towardsFace = BlockFacing.HorizontalFromAngle(yaw);

            


            if (DidBeginUse > 0)
            {
                if (mouseMode)
                {
                    OnRemove(voxelPos, facing, toolMode, byPlayer);
                }
                
                RegenMeshAndSelectionBoxes();
                api.World.BlockAccessor.MarkBlockDirty(pos);
                api.World.BlockAccessor.MarkBlockEntityDirty(pos);

                if (!HasAnyVoxel())
                {
                    api.World.BlockAccessor.SetBlock(0, pos);
                    return;
                }
            }

            DidBeginUse = Math.Max(DidBeginUse, DidBeginUse - 1);
            CheckIfFinished(byPlayer);
            MarkDirty();
        }


        public void CheckIfFinished(IPlayer byPlayer)
        {
            if (MatchesRecipe() && api.World is IServerWorldAccessor)
            {
                Voxels = new bool[16, 16];
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

                    if (byPlayer.InventoryManager.TryGiveItemstack(dropStack))
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

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (Voxels[x, z] != SelectedRecipe.Voxels[x, z])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        Cuboidi LayerBounds()
        {
            Cuboidi bounds = new Cuboidi(8, 8, 8, 8, 8, 8);

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (SelectedRecipe.Voxels[x, z])
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
                for (int z = 0; z < 16; z++)
                {
                    if (Voxels[x, z]) return true;
                }
            }

            return false;
        }

        public bool InBounds(Vec3i voxelPos)
        {
            Cuboidi bounds = LayerBounds();
            return voxelPos.X >= bounds.X1 && voxelPos.X <= bounds.X2 && voxelPos.Y >= 0 && voxelPos.Y < 16 && voxelPos.Z >= bounds.Z1 && voxelPos.Z <= bounds.Z2;
        }

        private void OnRemove(Vec3i voxelPos, BlockFacing facing, int radius, IPlayer byPlayer)
        {
            if (SelectedRecipe.Voxels[voxelPos.X, voxelPos.Z]) return;

            for (int dx = -(int)Math.Ceiling(radius/2f); dx <= radius /2; dx++)
            {
                for (int dz = -(int)Math.Ceiling(radius / 2f); dz <= radius / 2; dz++)
                {
                    Vec3i offPos = voxelPos.AddCopy(dx, 0, dz);
                    
                    if (voxelPos.X >= 0 && voxelPos.X < 16 && voxelPos.Z >= 0 && voxelPos.Z < 16 && Voxels[offPos.X, offPos.Z])
                    {
                        Voxels[offPos.X, offPos.Z] = false;

                        double posx = pos.X + voxelPos.X / 16f;
                        double posy = pos.Y + voxelPos.Y / 16f;
                        double posz = pos.Z + voxelPos.Z / 16f;

                        api.World.PlaySoundAt(new AssetLocation("sounds/player/knap" + (api.World.Rand.Next(2) > 0 ? 1 : 2)), posx, posy, posz, byPlayer, true, 12, 1);
                    }
                }
            }
        }



        void RegenMeshAndSelectionBoxes()
        {
            if (workitemRenderer != null && BaseMaterial != null)
            {
                BaseMaterial.ResolveBlockOrItem(api.World);
                workitemRenderer.Material = BaseMaterial.Collectible.FirstCodePart(1);
                if (workitemRenderer.Material == null)
                {
                    workitemRenderer.Material = BaseMaterial.Collectible.FirstCodePart(0);
                }
                workitemRenderer.RegenMesh(Voxels, SelectedRecipe);
            }

            List<Cuboidf> boxes = new List<Cuboidf>();
            

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    boxes.Add(new Cuboidf(x / 16f, 0 / 16f, z / 16f, x / 16f + 1 / 16f, 0 / 16f + 1 / 16f, z / 16f + 1 / 16f));
                }
            }

            selectionBoxes = boxes.ToArray();
        }


        public void CreateInitialWorkItem()
        {
            Voxels = new bool[16, 16];

            for (int x = 3; x <= 12; x++)
            {
                for (int z = 3; z <= 12; z++)
                {
                    Voxels[x, z] = true;
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
            selectedRecipeNumber = tree.GetInt("selectedRecipeNumber", -1);
            BaseMaterial = tree.GetItemstack("baseMaterial");
            RegenMeshAndSelectionBoxes();

            if (api?.World != null)
            {
                BaseMaterial?.ResolveBlockOrItem(api.World);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("voxels", serializeVoxels());
            tree.SetInt("selectedRecipeNumber", selectedRecipeNumber);
            tree.SetItemstack("baseMaterial", BaseMaterial);
        }


        byte[] serializeVoxels()
        {
            byte[] data = new byte[16 * 16 / 8];
            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int bitpos = pos % 8;
                    data[pos / 8] |= (byte)((Voxels[x, z] ? 1 : 0) << bitpos);
                    pos++;
                }
            }

            return data;
        }


        void deserializeVoxels(byte[] data)
        {
            Voxels = new bool[16, 16];

            if (data == null || data.Length < 16 * 16 / 8) return;

            int pos = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int bitpos = pos % 8;
                    Voxels[x, z] = (data[pos / 8] & (1 << bitpos)) > 0;
                    pos++;
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
                if (BaseMaterial != null)
                {
                    api.World.SpawnItemEntity(BaseMaterial, pos.ToVec3d().Add(0.5));
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
            KnappingRecipe recipe = api.World.KnappingRecipes
                .Where(r => r.Ingredient.SatisfiesAsIngredient(BaseMaterial))
                .OrderBy(r => r.Output.ResolvedItemstack.GetName())
                .ElementAtOrDefault(num)
            ;

            selectedRecipeNumber = new List<KnappingRecipe>(api.World.KnappingRecipes).IndexOf(recipe);
            return selectedRecipeNumber >= 0;
        }



        public void OpenDialog(IClientWorldAccessor world, BlockPos pos, ItemStack baseMaterial)
        {
            List<ItemStack> stacks = world.KnappingRecipes
               .Where(r => r.Ingredient.SatisfiesAsIngredient(baseMaterial))
               .OrderBy(r => r.Output.ResolvedItemstack.GetName())
               .Select(r => r.Output.ResolvedItemstack)
               .ToList()
           ;
            
            GuiDialog dlg = new GuiDialogBlockEntityRecipeSelector("Select recipe", stacks.ToArray(), pos, api as ICoreClientAPI);
            dlg.TryOpen();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            workitemRenderer?.Unregister();
        }


    }

}
