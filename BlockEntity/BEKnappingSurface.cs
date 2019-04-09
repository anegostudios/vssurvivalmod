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
    public class BlockEntityKnappingSurface : BlockEntity
    {
        // Permanent data
        int selectedRecipeNumber = -1;
        public bool[,] Voxels = new bool[16, 16];
        public ItemStack BaseMaterial;

        // Temporary data, generated on be creation

        
        Cuboidf[] selectionBoxes = new Cuboidf[0];
        KnappingRenderer workitemRenderer;
        

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

            if (BaseMaterial != null)
            {
                BaseMaterial.ResolveBlockOrItem(api.World);
            }

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                workitemRenderer = new KnappingRenderer(pos, capi);

                RegenMeshAndSelectionBoxes();
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

           // DidBeginUse++;
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
               // DidBeginUse = Math.Max(0, DidBeginUse - 1);
                return;
            }

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos, facing, mouseMode);
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null)
            {
          //      DidBeginUse = Math.Max(0, DidBeginUse - 1);
                return;
            }

            int toolMode = slot.Itemstack.Collectible.GetToolMode(slot, byPlayer, new BlockSelection() { Position = pos });

            float yaw = GameMath.Mod(byPlayer.Entity.Pos.Yaw, 2 * GameMath.PI);
            BlockFacing towardsFace = BlockFacing.HorizontalFromAngle(yaw);
            

            //if (DidBeginUse > 0)
            {
                bool didRemove = mouseMode && OnRemove(voxelPos, facing, toolMode, byPlayer);
                
                if (mouseMode)
                {
                    api.World.PlaySoundAt(new AssetLocation("sounds/player/knap" + (api.World.Rand.Next(2) > 0 ? 1 : 2)), lastRemovedLocalPos.X, lastRemovedLocalPos.Y, lastRemovedLocalPos.Z, byPlayer, true, 12, 1);
                }

                if (didRemove && api.Side == EnumAppSide.Client)
                {
                    Random rnd = api.World.Rand;
                    for (int i = 0; i < 3; i++)
                    {
                        api.World.SpawnParticles(new SimpleParticleProperties()
                        {
                            minQuantity = 1,
                            addQuantity = 2,
                            color = BaseMaterial.Collectible.GetRandomColor(api as ICoreClientAPI, BaseMaterial),
                            minPos = new Vec3d(lastRemovedLocalPos.X, lastRemovedLocalPos.Y + 1 / 16f + 0.01f, lastRemovedLocalPos.Z),
                            addPos = new Vec3d(1 / 16f, 0.01f, 1 / 16f),
                            minVelocity = new Vec3f(0, 1, 0),
                            addVelocity = new Vec3f(
                                4 * ((float)rnd.NextDouble() - 0.5f),
                                1 * ((float)rnd.NextDouble() - 0.5f),
                                4 * ((float)rnd.NextDouble() - 0.5f)
                            ),
                            lifeLength = 0.2f,
                            gravityEffect = 1f,
                            minSize = 0.1f,
                            maxSize = 0.4f,
                            model = EnumParticleModel.Cube,
                            SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.15f)
                        });
                    }

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

           // DidBeginUse = Math.Max(0, DidBeginUse - 1);
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

        Vec3d lastRemovedLocalPos = new Vec3d();
        private bool OnRemove(Vec3i voxelPos, BlockFacing facing, int radius, IPlayer byPlayer)
        {
            // Required voxel, don't let the player break it
            if (SelectedRecipe.Voxels[voxelPos.X, voxelPos.Z]) return false;

            for (int dx = -(int)Math.Ceiling(radius/2f); dx <= radius /2; dx++)
            {
                for (int dz = -(int)Math.Ceiling(radius / 2f); dz <= radius / 2; dz++)
                {
                    Vec3i offPos = voxelPos.AddCopy(dx, 0, dz);
                    
                    if (voxelPos.X >= 0 && voxelPos.X < 16 && voxelPos.Z >= 0 && voxelPos.Z < 16 && Voxels[offPos.X, offPos.Z])
                    {
                        Voxels[offPos.X, offPos.Z] = false;

                        lastRemovedLocalPos.Set(pos.X + voxelPos.X / 16f, pos.Y + voxelPos.Y / 16f, pos.Z + voxelPos.Z / 16f);
                        return true;
                    }
                }
            }

            return false;
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

            RegenMeshAndSelectionBoxes();
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
                int num = SerializerUtil.Deserialize<int>(data);
                if (!TrySetSelectedRecipe(num))
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

            ICoreClientAPI capi = api as ICoreClientAPI;

            GuiDialog dlg = new GuiDialogBlockEntityRecipeSelector(
                Lang.Get("Select recipe"),
                stacks.ToArray(),
                (recipeNum) => {
                    TrySetSelectedRecipe(recipeNum);
                    capi.Network.SendBlockEntityPacket(pos.X, pos.Y, pos.Z, (int)EnumClayFormingPacket.SelectRecipe, SerializerUtil.Serialize(recipeNum));
                },
                () => {
                    capi.Network.SendBlockEntityPacket(pos.X, pos.Y, pos.Z, (int)EnumClayFormingPacket.CancelSelect);
                },
                pos,
                api as ICoreClientAPI
            );

            dlg.TryOpen();
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            if (BaseMaterial == null || SelectedRecipe == null)
            {
                return "";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang.Get("Output: {0}", SelectedRecipe.Output?.ResolvedItemstack?.GetName()));

            return sb.ToString();
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            workitemRenderer?.Unregister();
        }


    }

}
