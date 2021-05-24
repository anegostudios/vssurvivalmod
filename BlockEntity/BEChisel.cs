using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityChisel : BlockEntityMicroBlock, IBlockEntityRotatable
    {
        public bool DetailingMode
        {
            get {
                IPlayer player = (Api.World as IClientWorldAccessor).Player;
                ItemSlot slot = player?.InventoryManager?.ActiveHotbarSlot;
                ItemStack stack = slot?.Itemstack;

                return Api.Side == EnumAppSide.Client && stack?.Collectible?.Tool == EnumTool.Chisel; 
            }
        }



        public EnumChiselMode ChiselMode(IPlayer player)
        {
            ItemSlot slot = player?.InventoryManager?.ActiveHotbarSlot;
            int? mode = slot?.Itemstack?.Collectible.GetToolMode(slot, player, new BlockSelection() { Position = Pos });

            return mode == null ? 0 : (EnumChiselMode)mode;
        }

        public int ChiselSize(IPlayer player)
        {
            int mode = (int)ChiselMode(player);
            if (mode == 0) return 1;
            if (mode == 1) return 2;
            if (mode == 2) return 4;
            if (mode == 3) return 8;

            if (mode == 4) return 1;
            if (mode == 5) return 1;
            if (mode == 6) return 1;

            return 0;
        }


        internal void OnBlockInteract(IPlayer byPlayer, BlockSelection blockSel, bool isBreak)
        {
            if (Api.World.Side == EnumAppSide.Client && DetailingMode)
            {
                Cuboidf box = GetOrCreateVoxelSelectionBoxes(byPlayer)[blockSel.SelectionBoxIndex];
                Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));

                UpdateVoxel(byPlayer, byPlayer.InventoryManager.ActiveHotbarSlot, voxelPos, blockSel.Face, isBreak);
            }
        }


        

        internal void UpdateVoxel(IPlayer byPlayer, ItemSlot itemslot, Vec3i voxelPos, BlockFacing facing, bool isBreak)
        {
            if (!Api.World.Claims.TryAccess(byPlayer, Pos, EnumBlockAccessFlags.Use))
            {
                MarkDirty(true, byPlayer);
                return;
            }

            
            EnumChiselMode mode = ChiselMode(byPlayer);

            bool wasChanged = false;

            switch (mode)
            {
                case EnumChiselMode.Rename:
                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;

                    string prevName = BlockName;
                    GuiDialogBlockEntityTextInput dlg = new GuiDialogBlockEntityTextInput(Lang.Get("Block name"), Pos, BlockName, Api as ICoreClientAPI, 500);
                    dlg.OnTextChanged = (text) => BlockName = text;
                    dlg.OnCloseCancel = () => BlockName = prevName;
                    dlg.TryOpen();
                    break;

                case EnumChiselMode.Flip:
                    FlipVoxels(Block.SuggestedHVOrientation(byPlayer, new BlockSelection() { Position = Pos.Copy(), HitPosition = new Vec3d(voxelPos.X/16.0, voxelPos.Y/16.0, voxelPos.Z/16.0) })[0]);
                    wasChanged = true;
                    break;

                case EnumChiselMode.Rotate:
                    RotateModel(byPlayer, isBreak);
                    wasChanged = true;
                    break;


                default:
                    int size = ChiselSize(byPlayer);
                    Vec3i addAtPos = voxelPos.Clone().Add(size * facing.Normali.X, size * facing.Normali.Y, size * facing.Normali.Z);

                    if (isBreak)
                    {
                        wasChanged = SetVoxel(voxelPos, false, byPlayer, nowmaterialIndex);
                    }
                    else
                    {
                        if (addAtPos.X >= 0 && addAtPos.X < 16 && addAtPos.Y >= 0 && addAtPos.Y < 16 && addAtPos.Z >= 0 && addAtPos.Z < 16)
                        {
                            wasChanged = SetVoxel(addAtPos, true, byPlayer, nowmaterialIndex);
                        }
                    }

                    break;
            }


            if (!wasChanged) return;

            if (Api.Side == EnumAppSide.Client)
            {
                RegenMesh();
            }

            RegenSelectionBoxes(byPlayer);
            MarkDirty(true, byPlayer);

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (Api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos, facing, isBreak);
            }

            double posx = Pos.X + voxelPos.X / 16f;
            double posy = Pos.Y + voxelPos.Y / 16f;
            double posz = Pos.Z + voxelPos.Z / 16f;
            Api.World.PlaySoundAt(new AssetLocation("sounds/player/knap" + (Api.World.Rand.Next(2) > 0 ? 1 : 2)), posx, posy, posz, byPlayer, true, 12, 1);

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative && Api.World.Rand.Next(3) == 0)
            {
                itemslot.Itemstack?.Collectible.DamageItem(Api.World, byPlayer.Entity, itemslot);
            }


            if (VoxelCuboids.Count == 0)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                return;
            }
        }


        public void SendUseOverPacket(IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool isBreak)
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
                Pos.X, Pos.Y, Pos.Z,
                (int)1010,
                data
            );
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == (int)EnumSignPacketId.SaveText)
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryReader reader = new BinaryReader(ms);
                    BlockName = reader.ReadString();
                    if (BlockName == null) BlockName = "";
                }
                MarkDirty(true, player);
                // Tell server to save this chunk to disk again
                Api.World.BlockAccessor.GetChunkAtBlockPos(Pos.X, Pos.Y, Pos.Z).MarkModified();
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
                    nowmaterialIndex = reader.ReadByte();
                }

                UpdateVoxel(player, player.InventoryManager.ActiveHotbarSlot, voxelPos, facing, isBreak);
            }
        }



        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos, IPlayer forPlayer = null)
        {
            if (Api.Side == EnumAppSide.Client && DetailingMode)
            {
                if (forPlayer == null) forPlayer = (Api.World as IClientWorldAccessor).Player;

                int nowSize = ChiselSize(forPlayer);
                
                if (prevSize > 0 && prevSize != nowSize)
                {
                    selectionBoxesVoxels = null;
                }

                prevSize = nowSize;

                return GetOrCreateVoxelSelectionBoxes(forPlayer);
            }

            if (selectionBoxes.Length == 0) return new Cuboidf[] { Cuboidf.Default() };

            return selectionBoxes;
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

        public bool SetVoxel(Vec3i voxelPos, bool state, IPlayer byPlayer, byte materialId)
        {           
            int size = ChiselSize(byPlayer);
            bool wasChanged = SetVoxel(voxelPos, state, byPlayer, materialId, size);

            if (!wasChanged) return false;

            if (Api.Side == EnumAppSide.Client && !state)
            {
                Vec3d basepos = Pos
                    .ToVec3d()
                    .Add(voxelPos.X / 16.0, voxelPos.Y / 16.0, voxelPos.Z / 16.0)
                    .Add(size / 4f / 16.0, size / 4f / 16.0, size / 4f / 16.0)
                ;

                int q = size * 5 - 2 + Api.World.Rand.Next(5);
                Block block = Api.World.GetBlock(MaterialIds[materialId]);

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




        public override void RegenSelectionBoxes(IPlayer byPlayer)
        {
            // Create a temporary array first, because the offthread particle system might otherwise access a null collisionbox
            Cuboidf[] selectionBoxesTmp = new Cuboidf[VoxelCuboids.Count];
            CuboidWithMaterial cwm = tmpCuboid;

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], cwm);
                selectionBoxesTmp[i] = cwm.ToCuboidf();
            }
            selectionBoxes = selectionBoxesTmp;


            if (byPlayer != null)
            {
                RegenSelectionVoxelBoxes(false, byPlayer);
            } else
            {
                selectionBoxesVoxels = null;
            }
        }


        public void GenerateSelectionVoxelBoxes(IPlayer byPlayer)
        {
            RegenSelectionVoxelBoxes(true, byPlayer);
        }


        public void RegenSelectionVoxelBoxes(bool mustLoad, IPlayer byPlayer)
        {
            if (selectionBoxesVoxels == null && !mustLoad) return;

            HashSet<Cuboidf> boxes = new HashSet<Cuboidf>();

            int size = ChiselSize(byPlayer);
            if (size <= 0) size = 16;

            float sx = size / 16f;
            float sy = size / 16f;
            float sz = size / 16f;

            CuboidWithMaterial cwm = tmpCuboid;

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], cwm);
                
                for (int x1 = cwm.X1; x1 < cwm.X2; x1 += size)
                {
                    for (int y1 = cwm.Y1; y1 < cwm.Y2; y1 += size)
                    {
                        for (int z1 = cwm.Z1; z1 < cwm.Z2; z1 += size)
                        {
                            float px = (float)Math.Floor((float)x1 / size) * sx;
                            float py = (float)Math.Floor((float)y1 / size) * sy;
                            float pz = (float)Math.Floor((float)z1 / size) * sz;

                            if (px + sx > 1 || py + sy > 1 || pz + sz > 1) continue;

                            boxes.Add(new Cuboidf(px, py, pz, px + sx, py + sy, pz + sz));
                        }
                    }
                }
            }

            selectionBoxesVoxels = boxes.ToArray();
        }

        #endregion


    }
}
