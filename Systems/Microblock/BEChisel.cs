using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using VSSurvivalMod.Systems.ChiselModes;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityChisel : BlockEntityMicroBlock
    {
        public static bool ForceDetailingMode = false;
        public static ChiselMode defaultMode = new OneByChiselMode();
        public ushort[] AvailMaterialQuantities;
        protected byte nowmaterialIndex;

        public override void WasPlaced(Block block, string blockName)
        {
            base.WasPlaced(block, blockName);

            AvailMaterialQuantities = new ushort[1];

            CuboidWithMaterial cwm = new CuboidWithMaterial();
            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], cwm);
                AvailMaterialQuantities[0] = (ushort)(AvailMaterialQuantities[0] + cwm.SizeXYZ);
            }
        }

        public bool DetailingMode
        {
            get {
                IPlayer player = (Api.World as IClientWorldAccessor).Player;
                ItemSlot slot = player?.InventoryManager?.ActiveHotbarSlot;
                ItemStack stack = slot?.Itemstack;

                return Api.Side == EnumAppSide.Client && (stack?.Collectible?.GetTool(slot) == EnumTool.Chisel || ForceDetailingMode); 
            }
        }

        public SkillItem GetChiselMode(IPlayer player)
        {
            if (Api.Side != EnumAppSide.Client) return null;

            var clientApi = (ICoreClientAPI)Api;
            ItemSlot slot = player?.InventoryManager?.ActiveHotbarSlot;
            var chisel = (ItemChisel)slot?.Itemstack.Collectible;
            int? mode = chisel.GetToolMode(slot, player, new BlockSelection() { Position = Pos });

            if (!mode.HasValue) return null;

            var modes = chisel.GetToolModes(slot, clientApi.World.Player, new BlockSelection() { Position = Pos });

            return modes[mode.Value];
        }

        public ChiselMode GetChiselModeData(IPlayer player)
        {
            var slot = player?.InventoryManager?.ActiveHotbarSlot;
            var itemChisel = slot?.Itemstack?.Collectible as ItemChisel;
            if (itemChisel == null) return defaultMode;

            int? mode = itemChisel.GetToolMode(slot, player, new BlockSelection() { Position = Pos });

            if (!mode.HasValue) return null;

            return (ChiselMode)itemChisel.ToolModes[mode.Value].Data;
        }

        public int GetChiselSize(IPlayer player)
        {
            var mode = GetChiselModeData(player);
            return mode == null ? 0 : mode.ChiselSize;
        }

        public Vec3i GetVoxelPos(BlockSelection blockSel, int chiselSize)
        {
            RegenSelectionVoxelBoxes(true, chiselSize);
            var boxes = selectionBoxesVoxels;
            if (blockSel.SelectionBoxIndex >= boxes.Length) return null;

            Cuboidf box = boxes[blockSel.SelectionBoxIndex];
            return new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));
        }


        internal void OnBlockInteract(IPlayer byPlayer, BlockSelection blockSel, bool isBreak)
        {
            if (Api.World.Side == EnumAppSide.Client && DetailingMode)
            {
                var boxes = GetOrCreateVoxelSelectionBoxes(byPlayer);
                if (blockSel.SelectionBoxIndex >= boxes.Length) return;

                Cuboidf box = boxes[blockSel.SelectionBoxIndex];
                Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));

                UpdateVoxel(byPlayer, byPlayer.InventoryManager.ActiveHotbarSlot, voxelPos, blockSel.Face, isBreak);
            }
        }
        public bool Interact(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer != null && byPlayer.InventoryManager.ActiveTool == EnumTool.Knife)
            {
                if (!Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    return false;
                }

                var face = blockSel.Face;
                int rotfaceindex = face.IsVertical ? face.Index : BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(face.HorizontalAngleIndex + rotationY / 90, 4)].Index;

                if (DecorIds != null && DecorIds[rotfaceindex] != 0)
                {
                    var block = Api.World.Blocks[DecorIds[rotfaceindex]];
                    Api.World.SpawnItemEntity(block.OnPickBlock(Api.World, Pos), Pos);
                    DecorIds[rotfaceindex] = 0;
                    MarkDirty(true, byPlayer);
                }
                return true;
            }

            return false;
        }

        public void SetNowMaterialId(int materialId)
        {
            nowmaterialIndex = (byte)Math.Max(0, BlockIds.IndexOf(materialId));
        }

        public void PickBlockMaterial(IPlayer byPlayer)
        {
            if (byPlayer != null && byPlayer.InventoryManager?.ActiveHotbarSlot is ItemSlot slot && slot.Itemstack?.Collectible is ItemChisel chisel)
            {
                var hitPos = byPlayer.CurrentBlockSelection.HitPosition;
                Vec3i voxPos = new Vec3i(Math.Min(15, (int)(hitPos.X * 16)), Math.Min(15, (int)(hitPos.Y * 16)), Math.Min(15, (int)(hitPos.Z * 16)));
                int matNum = GetVoxelMaterialAt(voxPos);
                int toolMode = (byte)Math.Max(0, BlockIds.IndexOf(matNum)) + chisel.ToolModes.Length;
                chisel.SetToolMode(slot, byPlayer, byPlayer.CurrentBlockSelection, toolMode);
            }
        }


        internal void UpdateVoxel(IPlayer byPlayer, ItemSlot itemslot, Vec3i voxelPos, BlockFacing facing, bool isBreak)
        {
            if (!Api.World.Claims.TryAccess(byPlayer, Pos, EnumBlockAccessFlags.Use))
            {
                MarkDirty(true, byPlayer);
                return;
            }

            var modeData = GetChiselModeData(byPlayer);

            var wasChanged = modeData.Apply(this, byPlayer, voxelPos, facing, isBreak, nowmaterialIndex);

            if (!wasChanged) return;

            if (Api.Side == EnumAppSide.Client)
            {
                MarkMeshDirty();
                UpdateNeighbors(this);
            }

            RegenSelectionBoxes(Api.World, byPlayer);
            MarkDirty(true, byPlayer);

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (Api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(voxelPos, facing, isBreak);
            }

            double posx = Pos.X + voxelPos.X / 16f;
            double posy = Pos.InternalY + voxelPos.Y / 16f;
            double posz = Pos.Z + voxelPos.Z / 16f;
            Api.World.PlaySoundAt(new AssetLocation("sounds/player/knap" + (Api.World.Rand.Next(2) > 0 ? 1 : 2)), posx, posy, posz, byPlayer, true, 12, 1);

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative && Api.World.Rand.Next(3) == 0)
            {
                itemslot.Itemstack?.Collectible.DamageItem(Api.World, byPlayer.Entity, itemslot);
            }


            if (VoxelCuboids.Count == 0)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                Api.World.BlockAccessor.RemoveBlockLight(GetLightHsv(Api.World.BlockAccessor), Pos);
                return;
            }
        }

        public void SendUseOverPacket(Vec3i voxelPos, BlockFacing facing, bool isBreak)
        {
            byte[] data;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                writer.Write(voxelPos.X);
                writer.Write(voxelPos.Y);
                writer.Write(voxelPos.Z);
                writer.Write(isBreak);
                writer.Write((ushort)facing.Index);
                writer.Write(nowmaterialIndex);
                data = ms.ToArray();
            }

            ((ICoreClientAPI)Api).Network.SendBlockEntityPacket(
                Pos,
                (int)1010,
                data
            );
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (!Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (packetid == (int)EnumSignPacketId.SaveText)
            {
                var packet = SerializerUtil.Deserialize<EditSignPacket>(data);
                BlockName = packet.Text;
                MarkDirty(true, player);
                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos).MarkModified();
            }


            if (packetid == 1010)
            {
                Vec3i voxelPos;
                bool isBreak;
                BlockFacing facing;
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    voxelPos = new Vec3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                    isBreak = reader.ReadBoolean();
                    facing = BlockFacing.ALLFACES[reader.ReadInt16()];
                    nowmaterialIndex = (byte)Math.Clamp(reader.ReadByte(), 0, BlockIds.Length - 1);
                }

                UpdateVoxel(player, player.InventoryManager.ActiveHotbarSlot, voxelPos, facing, isBreak);
            }

            if (packetid == 1011) PickBlockMaterial(player);
        }



        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos, IPlayer forPlayer = null)
        {
            if (Api?.Side == EnumAppSide.Client && DetailingMode)
            {
                if (forPlayer == null) forPlayer = (Api.World as IClientWorldAccessor).Player;

                int nowSize = GetChiselSize(forPlayer);
                
                if (prevSize > 0 && prevSize != nowSize)
                {
                    selectionBoxesVoxels = null;
                }

                prevSize = nowSize;

                return GetOrCreateVoxelSelectionBoxes(forPlayer);
            }

            return base.GetSelectionBoxes(world, pos, forPlayer);
        }


        private Cuboidf[] GetOrCreateVoxelSelectionBoxes(IPlayer byPlayer)
        {
            if (selectionBoxesVoxels == null)
            {
                GenerateSelectionVoxelBoxes(byPlayer);
            }
            return selectionBoxesVoxels;
        }


        #region Voxel math

        public static bool ConstrainToAvailableMaterialQuantity = true;

        public bool SetVoxel(Vec3i voxelPos, bool add, IPlayer byPlayer, byte materialId)
        {
            int size = GetChiselSize(byPlayer);

            if (add && ConstrainToAvailableMaterialQuantity && AvailMaterialQuantities != null)
            {
                int availableSumMaterial = AvailMaterialQuantities[materialId];
                CuboidWithMaterial cwm = new CuboidWithMaterial();
                int usedSumMaterial = 0;
                foreach (var cubint in VoxelCuboids)
                {
                    FromUint(cubint, cwm);
                    if (cwm.Material == materialId)
                    {
                        usedSumMaterial += cwm.SizeXYZ;
                    }
                }
                usedSumMaterial += size * size * size;

                if (usedSumMaterial > availableSumMaterial)
                {
                    (Api as ICoreClientAPI)?.TriggerIngameError(this, "outofmaterial", Lang.Get("Out of material, add more material to continue adding voxels"));
                    return false;
                }
            }

            bool wasChanged = SetVoxel(voxelPos, add, materialId, size);

            if (!wasChanged) return false;

            if (Api.Side == EnumAppSide.Client && !add)
            {
                Vec3d basepos = Pos
                    .ToVec3d()
                    .Add(voxelPos.X / 16.0, voxelPos.Y / 16.0, voxelPos.Z / 16.0)
                    .Add(size / 4f / 16.0, size / 4f / 16.0, size / 4f / 16.0)
                ;

                int q = size * 5 - 2 + Api.World.Rand.Next(5);
                Block block = Api.World.GetBlock(BlockIds[materialId]);

                while (q-- > 0)
                {
                    Api.World.SpawnParticles(
                        1,
                        block.GetRandomColor(Api as ICoreClientAPI, Pos, BlockFacing.UP) | (0xff << 24),
                        basepos,
                        basepos.Clone().Add(size / 4f / 16.0, size / 4f / 16.0, size / 4f / 16.0),
                        new Vec3f(-1, -0.5f, -1),
                        new Vec3f(1, 1 + size/3f, 1),
                        1, 1, size/30f + 0.1f + (float)Api.World.Rand.NextDouble() * 0.25f, EnumParticleModel.Cube
                    );
                }
            }

            return true;
        }




        public override void RegenSelectionBoxes(IWorldAccessor worldForResolve, IPlayer byPlayer)
        {
            base.RegenSelectionBoxes(worldForResolve, byPlayer);

            if (byPlayer != null)
            {
                int size = GetChiselSize(byPlayer);
                RegenSelectionVoxelBoxes(false, size);
            } else
            {
                selectionBoxesVoxels = null;
            }
        }


        public void GenerateSelectionVoxelBoxes(IPlayer byPlayer)
        {
            int size = GetChiselSize(byPlayer);
            RegenSelectionVoxelBoxes(true, size);
        }


        public void RegenSelectionVoxelBoxes(bool mustLoad, int chiselSize)
        {
            if (selectionBoxesVoxels == null && !mustLoad) return;

            HashSet<Cuboidf> boxes = new HashSet<Cuboidf>();

            if (chiselSize <= 0) chiselSize = 16;

            float sx = chiselSize / 16f;
            float sy = chiselSize / 16f;
            float sz = chiselSize / 16f;

            CuboidWithMaterial cwm = tmpCuboids[0];

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], cwm);
                
                for (int x1 = cwm.X1; x1 < cwm.X2; x1 += chiselSize)
                {
                    for (int y1 = cwm.Y1; y1 < cwm.Y2; y1 += chiselSize)
                    {
                        for (int z1 = cwm.Z1; z1 < cwm.Z2; z1 += chiselSize)
                        {
                            float px = (float)Math.Floor((float)x1 / chiselSize) * sx;
                            float py = (float)Math.Floor((float)y1 / chiselSize) * sy;
                            float pz = (float)Math.Floor((float)z1 / chiselSize) * sz;

                            if (px + sx > 1 || py + sy > 1 || pz + sz > 1) continue;

                            boxes.Add(new Cuboidf(px, py, pz, px + sx, py + sy, pz + sz));
                        }
                    }
                }
            }

            selectionBoxesVoxels = boxes.ToArray();
        }


        public int AddMaterial(Block addblock, out bool isFull, bool compareToPickBlock = true)
        {
            Cuboidf[] collboxes = addblock.GetCollisionBoxes(Api.World.BlockAccessor, Pos);
            int sum = 0;
            if (collboxes == null) collboxes = new Cuboidf[] { Cuboidf.Default() };

            for (int i = 0; i < collboxes.Length; i++)
            {
                Cuboidf box = collboxes[i];
                sum += new Cuboidi((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1), (int)(16 * box.X2), (int)(16 * box.Y2), (int)(16 * box.Z2)).SizeXYZ;
            }

            if (compareToPickBlock && !BlockIds.Contains(addblock.Id))
            {
                foreach (int blockid in BlockIds)
                {
                    var matblock = Api.World.Blocks[blockid];
                    var stack = matblock.OnPickBlock(Api.World, Pos);
                    if (stack.Block?.Id == addblock.Id)
                    {
                        addblock = matblock;
                    }
                }
            }

            if (!BlockIds.Contains(addblock.Id))
            {
                isFull = false;
                BlockIds = BlockIds.Append(addblock.Id);
                if (AvailMaterialQuantities != null) AvailMaterialQuantities = AvailMaterialQuantities.Append((ushort)sum);
                return BlockIds.Length - 1;
            }
            else
            {
                int index = BlockIds.IndexOf(addblock.Id);
                isFull = AvailMaterialQuantities[index] >= 16 * 16 * 16;
                if (AvailMaterialQuantities != null) AvailMaterialQuantities[index] = (ushort)Math.Min(ushort.MaxValue, AvailMaterialQuantities[index] + sum);
                return index;
            }
        }

        public int AddMaterial(Block block)
        {
            return AddMaterial(block, out _);
        }

        public override bool RemoveMaterial(Block block)
        {
            int index = BlockIds.IndexOf(block.Id);
            if (AvailMaterialQuantities != null && index >= 0)
            {   
                AvailMaterialQuantities = AvailMaterialQuantities.RemoveAt(index);
            }

            return base.RemoveMaterial(block);
        }

        #endregion



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            var intarrattr = tree["availMaterialQuantities"] as IntArrayAttribute;
            if (intarrattr != null)
            {
                AvailMaterialQuantities = new ushort[intarrattr.value.Length];
                for (int i = 0; i < intarrattr.value.Length; i++) AvailMaterialQuantities[i] = (ushort)intarrattr.value[i];
                while (BlockIds.Length > AvailMaterialQuantities.Length) AvailMaterialQuantities = AvailMaterialQuantities.Append((ushort)(16 * 16 * 16));
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            if (AvailMaterialQuantities != null)
            {
                IntArrayAttribute attr = new IntArrayAttribute();
                attr.value = new int[AvailMaterialQuantities.Length];
                for (int i = 0; i < AvailMaterialQuantities.Length; i++) attr.value[i] = AvailMaterialQuantities[i];

                tree["availMaterialQuantities"] = attr;
            }
        }


    }
}
