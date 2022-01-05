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
        int selectedRecipeId = -1;
        public bool[,] Voxels = new bool[16, 16];
        public ItemStack BaseMaterial;

        // Temporary data, generated on be creation

        
        Cuboidf[] selectionBoxes = new Cuboidf[0];
        KnappingRenderer workitemRenderer;
        

        public KnappingRecipe SelectedRecipe
        {
            get { return Api.GetKnappingRecipes().FirstOrDefault(r => r.RecipeId == selectedRecipeId); }
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
                workitemRenderer = new KnappingRenderer(Pos, capi);

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
                if (Api.Side == EnumAppSide.Client)
                {
                    OpenDialog(Api.World as IClientWorldAccessor, Pos, byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack);
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
            if (Api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos, facing, mouseMode);
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Itemstack == null)
            {
          //      DidBeginUse = Math.Max(0, DidBeginUse - 1);
                return;
            }

            int toolMode = slot.Itemstack.Collectible.GetToolMode(slot, byPlayer, new BlockSelection() { Position = Pos });

            //float yaw = GameMath.Mod(byPlayer.Entity.Pos.Yaw, 2 * GameMath.PI);
            //BlockFacing towardsFace = BlockFacing.HorizontalFromAngle(yaw);
            

            //if (DidBeginUse > 0)
            {
                bool didRemove = mouseMode && OnRemove(voxelPos, toolMode);
                
                if (didRemove)
                {
                    for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                    {
                        BlockFacing face = BlockFacing.HORIZONTALS[i];
                        Vec3i nnode = voxelPos.AddCopy(face);

                        if (!Voxels[nnode.X, nnode.Z]) continue;
                        if (SelectedRecipe.Voxels[nnode.X, 0, nnode.Z]) continue;

                        tryBfsRemove(nnode.X, nnode.Z);
                    }
                }

                if (mouseMode && (didRemove || Voxels[voxelPos.X, voxelPos.Z]))
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/player/knap" + (Api.World.Rand.Next(2) > 0 ? 1 : 2)), lastRemovedLocalPos.X, lastRemovedLocalPos.Y, lastRemovedLocalPos.Z, byPlayer, true, 12, 1);
                }

                if (didRemove && Api.Side == EnumAppSide.Client)
                {
                    spawnParticles(lastRemovedLocalPos);
                }

                RegenMeshAndSelectionBoxes();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
                Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);

                if (!HasAnyVoxel())
                {
                    Api.World.BlockAccessor.SetBlock(0, Pos);
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
                Voxels = new bool[16, 16];
                ItemStack outstack = SelectedRecipe.Output.ResolvedItemstack.Clone();
                selectedRecipeId = -1;

                if (outstack.StackSize == 1 && outstack.Class == EnumItemClass.Block)
                {
                    Api.World.BlockAccessor.SetBlock(outstack.Block.BlockId, Pos);
                    return;
                }

                int tries = 0;
                while (outstack.StackSize > 0)
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

                    if (tries++ > 1000)
                    {
                        throw new Exception("Endless loop prevention triggered. Something seems broken with a matching knapping recipe with number " + selectedRecipeId + ". Tried 1000 times to drop the resulting stack " + outstack.ToString());
                    }
                }

                Api.World.BlockAccessor.SetBlock(0, Pos);
            }
        }


        void spawnParticles(Vec3d pos)
        {
            Random rnd = Api.World.Rand;
            for (int i = 0; i < 3; i++)
            {
                Api.World.SpawnParticles(new SimpleParticleProperties()
                {
                    MinQuantity = 1,
                    AddQuantity = 2,
                    Color = BaseMaterial.Collectible.GetRandomColor(Api as ICoreClientAPI, BaseMaterial),
                    MinPos = new Vec3d(pos.X, pos.Y + 1 / 16f + 0.01f, pos.Z),
                    AddPos = new Vec3d(1 / 16f, 0.01f, 1 / 16f),
                    MinVelocity = new Vec3f(0, 1, 0),
                    AddVelocity = new Vec3f(
                        4 * ((float)rnd.NextDouble() - 0.5f),
                        1 * ((float)rnd.NextDouble() - 0.5f),
                        4 * ((float)rnd.NextDouble() - 0.5f)
                    ),
                    LifeLength = 0.2f,
                    GravityEffect = 1f,
                    MinSize = 0.1f,
                    MaxSize = 0.4f,
                    ParticleModel = EnumParticleModel.Cube,
                    SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.15f)
                });
            }
        }

        private bool MatchesRecipe()
        {
            if (SelectedRecipe == null) return false;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    if (Voxels[x, z] != SelectedRecipe.Voxels[x, 0, z])
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
                    if (SelectedRecipe.Voxels[x, 0, z])
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
        private bool OnRemove(Vec3i voxelPos, int radius)
        {
            // Required voxel, don't let the player break it
            if (SelectedRecipe == null || SelectedRecipe.Voxels[voxelPos.X, 0, voxelPos.Z]) return false;

            for (int dx = -(int)Math.Ceiling(radius/2f); dx <= radius /2; dx++)
            {
                for (int dz = -(int)Math.Ceiling(radius / 2f); dz <= radius / 2; dz++)
                {
                    Vec3i offPos = voxelPos.AddCopy(dx, 0, dz);
                    
                    if (voxelPos.X >= 0 && voxelPos.X < 16 && voxelPos.Z >= 0 && voxelPos.Z < 16 && Voxels[offPos.X, offPos.Z])
                    {
                        Voxels[offPos.X, offPos.Z] = false;

                        lastRemovedLocalPos.Set(Pos.X + voxelPos.X / 16f, Pos.Y + voxelPos.Y / 16f, Pos.Z + voxelPos.Z / 16f);
                        return true;
                    }
                }
            }



            return false;
        }

        private void tryBfsRemove(int x, int z)
        {
            Queue<Vec2i> nodesToVisit = new Queue<Vec2i>();
            HashSet<Vec2i> nodesVisited = new HashSet<Vec2i>();

            nodesToVisit.Enqueue(new Vec2i(x, z));

            List<Vec2i> foundPieces = new List<Vec2i>();

            while (nodesToVisit.Count > 0)
            {
                Vec2i node = nodesToVisit.Dequeue();

                for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing face = BlockFacing.HORIZONTALS[i];
                    Vec2i nnode = node.Copy().Add(face.Normali.X, face.Normali.Z);

                    if (nnode.X < 0 || nnode.X >= 16 || nnode.Y < 0 || nnode.Y >= 16) continue;
                    if (!Voxels[nnode.X, nnode.Y]) continue;

                    if (nodesVisited.Contains(nnode)) continue;
                    nodesVisited.Add(nnode);

                    foundPieces.Add(nnode);

                    if (SelectedRecipe.Voxels[nnode.X, 0, nnode.Y])
                    {
                        return;
                    }

                    nodesToVisit.Enqueue(nnode);
                }
            }

            // Single voxel with no neighbours
            if (nodesVisited.Count == 0 && foundPieces.Count == 0)
            {
                foundPieces.Add(new Vec2i(x, z));
            }


            Vec3d tmp = new Vec3d();
            foreach (var val in foundPieces)
            {
                Voxels[val.X, val.Y] = false;

                if (Api.Side == EnumAppSide.Client)
                {
                    tmp.Set(Pos.X + val.X / 16f, Pos.Y, Pos.Z + val.Y / 16f);
                    spawnParticles(tmp);
                }
            }
        }


        public void RegenMeshAndSelectionBoxes()
        {
            if (workitemRenderer != null && BaseMaterial != null)
            {
                BaseMaterial.ResolveBlockOrItem(Api.World);
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
            workitemRenderer?.Dispose();
            workitemRenderer = null;

            dlg?.TryClose();
            dlg?.Dispose();
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            deserializeVoxels(tree.GetBytes("voxels"));
            selectedRecipeId = tree.GetInt("selectedRecipeId", -1);
            BaseMaterial = tree.GetItemstack("baseMaterial");

            if (Api?.World != null)
            {
                BaseMaterial?.ResolveBlockOrItem(Api.World);
            }

            RegenMeshAndSelectionBoxes();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("voxels", serializeVoxels());
            tree.SetInt("selectedRecipeId", selectedRecipeId);
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
                if (BaseMaterial != null)
                {
                    Api.World.SpawnItemEntity(BaseMaterial, Pos.ToVec3d().Add(0.5));
                }
                Api.World.BlockAccessor.SetBlock(0, Pos);
                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
            }

            if (packetid == (int)EnumClayFormingPacket.SelectRecipe)
            {
                int recipeid = SerializerUtil.Deserialize<int>(data);
                KnappingRecipe recipe = Api.GetKnappingRecipes().FirstOrDefault(r => r.RecipeId == recipeid);

                if (recipe == null)
                {
                    Api.World.Logger.Error("Client tried to selected knapping recipe with id {0}, but no such recipe exists!");
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

        GuiDialog dlg;
        public void OpenDialog(IClientWorldAccessor world, BlockPos pos, ItemStack baseMaterial)
        {
            List<KnappingRecipe> recipes = Api.GetKnappingRecipes()
               .Where(r => r.Ingredient.SatisfiesAsIngredient(baseMaterial))
               .OrderBy(r => r.Output.ResolvedItemstack.Collectible.Code) // Cannot sort by name, thats language dependent!
               .ToList()
            ;

            List<ItemStack> stacks = recipes
               .Select(r => r.Output.ResolvedItemstack)
               .ToList()
            ;

            ICoreClientAPI capi = Api as ICoreClientAPI;

            dlg?.Dispose();
            dlg = new GuiDialogBlockEntityRecipeSelector(
                Lang.Get("Select recipe"),
                stacks.ToArray(),
                (selectedIndex) => {
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
            if (BaseMaterial == null || SelectedRecipe == null)
            {
                return;
            }

            dsc.AppendLine(Lang.Get("Output: {0}", SelectedRecipe.Output?.ResolvedItemstack?.GetName()));
        }


        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            workitemRenderer?.Dispose();
        }


    }

}
