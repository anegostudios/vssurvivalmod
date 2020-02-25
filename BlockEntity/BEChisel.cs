using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class CuboidWithMaterial : Cuboidi
    {
        public byte Material;

        internal Cuboidf ToCuboidf()
        {
            return new Cuboidf(X1 / 16f, Y1/ 16f, Z1 / 16f, X2 / 16f, Y2 / 16f, Z2 / 16f);
        }
    }
    
    public struct Voxel : IEquatable<Voxel>
    {
        public byte x;
        public byte y;
        public byte z;

        public Voxel(byte x, byte y, byte z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(Voxel other)
        {
            return x == other.x && y == other.y && z == other.z;
        }
    }

    

    public class BlockEntityChisel : BlockEntity, IBlockEntityRotatable
    {
        static CuboidWithMaterial tmpCuboid = new CuboidWithMaterial();

        // bits 0..3 = xmin
        // bits 4..7 = xmax
        // bits 8..11 = ymin
        // bits 12..15 = ymax
        // bits 16..19 = zmin
        // bits 20..23 = zmas
        // bits 24..31 = materialindex
        public List<uint> VoxelCuboids = new List<uint>();

        /// <summary>
        /// List of block ids for the materials used
        /// </summary>
        public int[] MaterialIds;
        
        
        public MeshData Mesh;
        Cuboidf[] selectionBoxes = new Cuboidf[0];
        Cuboidf[] selectionBoxesVoxels = new Cuboidf[0];
        int prevSize = -1;
        string blockName = "";

        public string BlockName => blockName;

        bool[] emitSideAo = new bool[6] { true, true, true, true, true, true };
        bool[] emitSideAoByFlags = new bool[63];
        bool absorbAnyLight;
        bool[] sideSolid = new bool[6];

        public bool DetailingMode
        {
            get {
                IPlayer player = (Api.World as IClientWorldAccessor).Player;
                ItemSlot slot = player?.InventoryManager?.ActiveHotbarSlot;
                ItemStack stack = slot?.Itemstack;

                return Api.Side == EnumAppSide.Client && stack?.Collectible?.Tool == EnumTool.Chisel; 
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (MaterialIds != null)
            {
                if (api.Side == EnumAppSide.Client) RegenMesh();
                RegenSelectionBoxes(null);
            }
        }

        internal int GetLightAbsorption()
        {
            if (MaterialIds == null || !absorbAnyLight)
            {
                return 0;
            }

            int absorb = 99;

            for (int i = 0; i < MaterialIds.Length; i++)
            {
                Block block = Api.World.GetBlock(MaterialIds[i]);
                absorb = Math.Min(absorb, block.LightAbsorption);
            }

            return absorb;
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


        public bool CanAttachBlockAt(BlockFacing blockFace)
        {
            return sideSolid[blockFace.Index];
        }

        public void WasPlaced(Block block, string blockName)
        {
            bool collBoxCuboid = block.Attributes?["chiselShapeFromCollisionBox"].AsBool(false) == true;

            MaterialIds = new int[] { block.BlockId };

            if (!collBoxCuboid)
            {
                VoxelCuboids.Add(ToCuboid(0, 0, 0, 16, 16, 16, 0));
            } else
            {
                Cuboidf[] collboxes = block.GetCollisionBoxes(Api.World.BlockAccessor, Pos);
                for (int i = 0; i < collboxes.Length; i++)
                {
                    Cuboidf box = collboxes[i];
                    VoxelCuboids.Add(ToCuboid((int)(16*box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1), (int)(16 * box.X2), (int)(16 * box.Y2), (int)(16 * box.Z2), 0));
                }
            }

            this.blockName = blockName;

            updateSideSolidSideAo();
            RegenSelectionBoxes(null);
            if (Api.Side == EnumAppSide.Client && Mesh == null)
            {
                RegenMesh();
            }
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
            EnumChiselMode mode = ChiselMode(byPlayer);

            bool wasChanged = false;

            switch (mode)
            {
                case EnumChiselMode.Rename:
                    IClientWorldAccessor clientWorld = (IClientWorldAccessor)Api.World;

                    string prevName = blockName;
                    GuiDialogBlockEntityTextInput dlg = new GuiDialogBlockEntityTextInput(Lang.Get("Block name"), Pos, blockName, Api as ICoreClientAPI, 500);
                    dlg.OnTextChanged = (text) => blockName = text;
                    dlg.OnCloseCancel = () => blockName = prevName;
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
                        wasChanged = SetVoxel(voxelPos, false, byPlayer);
                    }
                    else
                    {
                        if (addAtPos.X >= 0 && addAtPos.X < 16 && addAtPos.Y >= 0 && addAtPos.Y < 16 && addAtPos.Z >= 0 && addAtPos.Z < 16)
                        {
                            wasChanged = SetVoxel(addAtPos, true, byPlayer);
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
            MarkDirty(true);

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

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative && Api.World.Rand.Next(2) > 0)
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
                    blockName = reader.ReadString();
                    if (blockName == null) blockName = "";
                }
                MarkDirty(true);
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
                }

                UpdateVoxel(player, player.InventoryManager.ActiveHotbarSlot, voxelPos, facing, isBreak);
            }
        }



        internal Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos, IPlayer forPlayer = null)
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

            return selectionBoxes;
        }

        internal Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
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


        void convertToVoxels(out bool[,,] voxels, out byte[,,] materials)
        {
            voxels = new bool[16, 16, 16];
            materials = new byte[16, 16, 16];

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], ref tmpCuboid);

                for (int dx = tmpCuboid.X1; dx < tmpCuboid.X2; dx++)
                {
                    for (int dy = tmpCuboid.Y1; dy < tmpCuboid.Y2; dy++)
                    {
                        for (int dz = tmpCuboid.Z1; dz < tmpCuboid.Z2; dz++)
                        {
                            voxels[dx, dy, dz] = true;
                            materials[dx, dy, dz] = tmpCuboid.Material;
                        }
                    }
                }
            }
        }

        void updateSideSolidSideAo()
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;

            convertToVoxels(out Voxels, out VoxelMaterial);
            RebuildCuboidList(Voxels, VoxelMaterial);
        }


        private void FlipVoxels(BlockFacing frontFacing)
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;

            convertToVoxels(out Voxels, out VoxelMaterial);

            bool[,,] outVoxels = new bool[16, 16, 16];
            byte[,,] outVoxelMaterial = new byte[16, 16, 16];

            // Ok, now we can actually modify the voxel
            for (int dx = 0; dx < 16; dx++)
            {
                for (int dy = 0; dy < 16; dy++)
                {
                    for (int dz = 0; dz < 16; dz++)
                    {
                        outVoxels[dx, dy, dz] = Voxels[frontFacing.Axis == EnumAxis.Z ? 15 - dx : dx, dy, frontFacing.Axis == EnumAxis.X ? 15 - dz : dz];
                        outVoxelMaterial[dx, dy, dz] = VoxelMaterial[frontFacing.Axis == EnumAxis.Z ? 15 - dx : dx, dy, frontFacing.Axis == EnumAxis.X ? 15 - dz : dz];
                    }
                }
            }

            RebuildCuboidList(outVoxels, outVoxelMaterial);
        }

        private void RotateModel(IPlayer byPlayer, bool clockwise)
        {
            List<uint> rotatedCuboids = new List<uint>();

            foreach (var val in this.VoxelCuboids)
            {
                FromUint(val, ref tmpCuboid);
                Cuboidi rotated = tmpCuboid.RotatedCopy(0, clockwise ? 90 : -90, 0, new Vec3d(8, 8, 8));
                tmpCuboid.Set(rotated.X1, rotated.Y1, rotated.Z1, rotated.X2, rotated.Y2, rotated.Z2);
                rotatedCuboids.Add(ToCuboid(tmpCuboid));
            }

            VoxelCuboids = rotatedCuboids;
        }


        public void OnTransformed(ITreeAttribute tree, int byDegrees, EnumAxis? aroundAxis)
        {
            List<uint> rotatedCuboids = new List<uint>();

            VoxelCuboids = new List<uint>((tree["cuboids"] as IntArrayAttribute).AsUint);

            foreach (var val in this.VoxelCuboids)
            {
                FromUint(val, ref tmpCuboid);
                Cuboidi rotated = tmpCuboid.Clone();

                if (aroundAxis == EnumAxis.X)
                {
                    rotated.Y1 = 16 - rotated.Y1;
                    rotated.Y2 = 16 - rotated.Y2;
                }
                if (aroundAxis == EnumAxis.Y)
                {
                    rotated.X1 = 16 - rotated.X1;
                    rotated.X2 = 16 - rotated.X2;
                }
                if (aroundAxis == EnumAxis.Z)
                {
                    rotated.Z1 = 16 - rotated.Z1;
                    rotated.Z2 = 16 - rotated.Z2;
                }

                rotated = rotated.RotatedCopy(0, byDegrees, 0, new Vec3d(8, 8, 8));


                tmpCuboid.Set(rotated.X1, rotated.Y1, rotated.Z1, rotated.X2, rotated.Y2, rotated.Z2);
                rotatedCuboids.Add(ToCuboid(tmpCuboid));
            }

            tree["cuboids"] = new IntArrayAttribute(rotatedCuboids.ToArray());
        }



        public bool SetVoxel(Vec3i voxelPos, bool state, IPlayer byPlayer)
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;

            convertToVoxels(out Voxels, out VoxelMaterial);

            // Ok, now we can actually modify the voxel
            int size = ChiselSize(byPlayer);
            bool wasChanged = false;
            for (int dx = 0; dx < size; dx++)
            {
                for (int dy = 0; dy < size; dy++)
                {
                    for (int dz = 0; dz < size; dz++)
                    {
                        if (voxelPos.X + dx >= 16 || voxelPos.Y + dy >= 16 || voxelPos.Z + dz >= 16) continue;

                        wasChanged |= Voxels[voxelPos.X + dx, voxelPos.Y + dy, voxelPos.Z + dz] != state;

                        Voxels[voxelPos.X + dx, voxelPos.Y + dy, voxelPos.Z + dz] = state;
                    }
                }
            }

            if (!wasChanged) return false;

            RebuildCuboidList(Voxels, VoxelMaterial);

            return true;
        }



        public void SetData(bool[,,] Voxels, byte[,,] VoxelMaterial)
        {
            RebuildCuboidList(Voxels, VoxelMaterial);

            if (Api.Side == EnumAppSide.Client)
            {
                RegenMesh();
            }

            RegenSelectionBoxes(null);
            MarkDirty(true);

            if (VoxelCuboids.Count == 0)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                return;
            }
        }


        #region Side AO 


        public bool DoEmitSideAo(int facing)
        {
            return emitSideAo[facing];
        }

        public bool DoEmitSideAoByFlag(int flag)
        {
            return emitSideAoByFlags[flag];
        }

        #endregion


        private void RebuildCuboidList(bool[,,] Voxels, byte[,,] VoxelMaterial)
        {
            bool[,,] VoxelVisited = new bool[16, 16, 16];
            emitSideAo = new bool[] { true, true, true, true, true, true };
            sideSolid = new bool[] { true, true, true, true, true, true };

            // And now let's rebuild the cuboids with some greedy search algo thing
            VoxelCuboids.Clear();

            int[] edgeVoxelsMissing = new int[6];
            int[] edgeCenterVoxelsMissing = new int[6];

            for (int dx = 0; dx < 16; dx++)
            {
                for (int dy = 0; dy < 16; dy++)
                {
                    for (int dz = 0; dz < 16; dz++)
                    {
                        bool isVoxel = Voxels[dx, dy, dz];

                        // North: Negative Z
                        // East: Positive X
                        // South: Positive Z
                        // West: Negative X
                        // Up: Positive Y
                        // Down: Negative Y
                        if (!isVoxel)
                        {
                            if (dz == 0)
                            {
                                edgeVoxelsMissing[BlockFacing.NORTH.Index]++;
                                if (Math.Abs(dy - 8) < 5 && Math.Abs(dx - 8) < 5) edgeCenterVoxelsMissing[BlockFacing.NORTH.Index]++;
                            }
                            if (dx == 15)
                            {
                                edgeVoxelsMissing[BlockFacing.EAST.Index]++;
                                if (Math.Abs(dy - 8) < 5 && Math.Abs(dz - 8) < 5) edgeCenterVoxelsMissing[BlockFacing.EAST.Index]++;
                            }
                            if (dz == 15)
                            {
                                edgeVoxelsMissing[BlockFacing.SOUTH.Index]++;
                                if (Math.Abs(dy - 8) < 5 && Math.Abs(dx - 8) < 5) edgeCenterVoxelsMissing[BlockFacing.SOUTH.Index]++;
                            }
                            if (dx == 0)
                            {
                                edgeVoxelsMissing[BlockFacing.WEST.Index]++;
                                if (Math.Abs(dy - 8) < 5 && Math.Abs(dz - 8) < 5) edgeCenterVoxelsMissing[BlockFacing.WEST.Index]++;
                            }
                            if (dy == 15)
                            {
                                edgeVoxelsMissing[BlockFacing.UP.Index]++;
                                if (Math.Abs(dz - 8) < 5 && Math.Abs(dx - 8) < 5) edgeCenterVoxelsMissing[BlockFacing.UP.Index]++;
                            }
                            if (dy == 0)
                            {
                                edgeVoxelsMissing[BlockFacing.DOWN.Index]++;
                                if (Math.Abs(dz - 8) < 5 && Math.Abs(dx - 8) < 5) edgeCenterVoxelsMissing[BlockFacing.DOWN.Index]++;
                            }

                            
                            continue;
                        }

                        if (VoxelVisited[dx, dy, dz]) continue;

                        CuboidWithMaterial cub = new CuboidWithMaterial()
                        {
                            Material = VoxelMaterial[dx, dy, dz],
                            X1 = dx, Y1 = dy, Z1 = dz,
                            X2 = dx + 1, Y2 = dy + 1, Z2 = dz + 1
                        };

                        // Try grow this cuboid for as long as we can
                        bool didGrowAny = true;
                        while (didGrowAny)
                        {
                            didGrowAny =
                                TryGrowX(cub, Voxels, VoxelVisited, VoxelMaterial) ||
                                TryGrowY(cub, Voxels, VoxelVisited, VoxelMaterial) ||
                                TryGrowZ(cub, Voxels, VoxelVisited, VoxelMaterial)
                            ;
                        }

                        VoxelCuboids.Add(ToCuboid(cub));
                    }
                }
            }

            bool doEmitSideAo = edgeVoxelsMissing[0] < 64 || edgeVoxelsMissing[1] < 64 || edgeVoxelsMissing[2] < 64 || edgeVoxelsMissing[3] < 64;

            if (absorbAnyLight != doEmitSideAo)
            {
                int preva = GetLightAbsorption();
                absorbAnyLight = doEmitSideAo;
                int nowa = GetLightAbsorption();
                if (preva != nowa)
                {
                    Api.World.BlockAccessor.MarkAbsorptionChanged(preva, nowa, Pos);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                emitSideAo[i] = doEmitSideAo;
                sideSolid[i] = edgeCenterVoxelsMissing[i] < 5;
            }

            this.emitSideAoByFlags = Block.ResolveAoFlags(this.Block, emitSideAo);
        }


        private bool TryGrowX(CuboidWithMaterial cub, bool[,,] voxels, bool[,,] voxelVisited, byte[,,] voxelMaterial)
        {
            if (cub.X2 > 15) return false;

            for (int y = cub.Y1; y < cub.Y2; y++)
            {
                for (int z = cub.Z1; z < cub.Z2; z++)
                {
                    if (!voxels[cub.X2, y, z] || voxelVisited[cub.X2, y, z] || voxelMaterial[cub.X2, y, z] != cub.Material) return false;
                }
            }

            for (int y = cub.Y1; y < cub.Y2; y++)
            {
                for (int z = cub.Z1; z < cub.Z2; z++)
                {
                    voxelVisited[cub.X2, y, z] = true;
                }
            }

            cub.X2++;
            return true;
        }

        private bool TryGrowY(CuboidWithMaterial cub, bool[,,] voxels, bool[,,] voxelVisited, byte[,,] voxelMaterial)
        {
            if (cub.Y2 > 15) return false;

            for (int x = cub.X1; x < cub.X2; x++)
            {
                for (int z = cub.Z1; z < cub.Z2; z++)
                {
                    if (!voxels[x, cub.Y2, z] || voxelVisited[x, cub.Y2, z] || voxelMaterial[x, cub.Y2, z] != cub.Material) return false;
                }
            }

            for (int x = cub.X1; x < cub.X2; x++)
            {
                for (int z = cub.Z1; z < cub.Z2; z++)
                {
                    voxelVisited[x, cub.Y2, z] = true;
                }
            }

            cub.Y2++;
            return true;
        }

        private bool TryGrowZ(CuboidWithMaterial cub, bool[,,] voxels, bool[,,] voxelVisited, byte[,,] voxelMaterial)
        {
            if (cub.Z2 > 15) return false;

            for (int x = cub.X1; x < cub.X2; x++)
            {
                for (int y = cub.Y1; y < cub.Y2; y++)
                {
                    if (!voxels[x, y, cub.Z2] || voxelVisited[x, y, cub.Z2] || voxelMaterial[x, y, cub.Z2] != cub.Material) return false;
                }
            }

            for (int x = cub.X1; x < cub.X2; x++)
            {
                for (int y = cub.Y1; y < cub.Y2; y++)
                {
                    voxelVisited[x, y, cub.Z2] = true;
                }
            }

            cub.Z2++;
            return true;
        }







        public void RegenSelectionBoxes(IPlayer byPlayer)
        {
            selectionBoxes = new Cuboidf[VoxelCuboids.Count];

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], ref tmpCuboid);
                selectionBoxes[i] = tmpCuboid.ToCuboidf();
            }

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

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], ref tmpCuboid);
                
                for (int x1 = tmpCuboid.X1; x1 < tmpCuboid.X2; x1 += size)
                {
                    for (int y1 = tmpCuboid.Y1; y1 < tmpCuboid.Y2; y1 += size)
                    {
                        for (int z1 = tmpCuboid.Z1; z1 < tmpCuboid.Z2; z1 += size)
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

        #region Mesh generation
        public void RegenMesh()
        {
            Mesh = CreateMesh(Api as ICoreClientAPI, VoxelCuboids, MaterialIds);
        }

        public static MeshData CreateMesh(ICoreClientAPI coreClientAPI, List<uint> voxelCuboids, int[] materials)
        {
            MeshData mesh = new MeshData(24, 36, false).WithTints().WithRenderpasses().WithXyzFaces();
            if (voxelCuboids == null || materials == null) return mesh;

            for (int i = 0; i < voxelCuboids.Count; i++)
            {
                FromUint(voxelCuboids[i], ref tmpCuboid);

                Block block = coreClientAPI.World.GetBlock(materials[tmpCuboid.Material]);

                //TextureAtlasPosition tpos = coreClientAPI.BlockTextureAtlas.GetPosition(block, BlockFacing.ALLFACES[0].Code);
                float subPixelPaddingx = coreClientAPI.BlockTextureAtlas.SubPixelPaddingX;
                float subPixelPaddingy = coreClientAPI.BlockTextureAtlas.SubPixelPaddingY;

                MeshData cuboidmesh = genCube(
                    tmpCuboid.X1, tmpCuboid.Y1, tmpCuboid.Z1, 
                    tmpCuboid.X2 - tmpCuboid.X1, tmpCuboid.Y2 - tmpCuboid.Y1, tmpCuboid.Z2 - tmpCuboid.Z1, 
                    coreClientAPI, 
                    coreClientAPI.Tesselator.GetTexSource(block, 0, true),
                    subPixelPaddingx,
                    subPixelPaddingy,
                    (int)block.RenderPass,
                    block.VertexFlags.All
                );

                mesh.AddMeshData(cuboidmesh);
            }

            return mesh;
        }

        public MeshData CreateDecalMesh(ITexPositionSource decalTexSource)
        {
            return CreateDecalMesh(Api as ICoreClientAPI, VoxelCuboids, decalTexSource);
        }

        public static MeshData CreateDecalMesh(ICoreClientAPI coreClientAPI, List<uint> voxelCuboids, ITexPositionSource decalTexSource)
        {
            MeshData mesh = new MeshData(24, 36, false).WithTints().WithRenderpasses().WithXyzFaces();

            for (int i = 0; i < voxelCuboids.Count; i++)
            {
                FromUint(voxelCuboids[i], ref tmpCuboid);

                MeshData cuboidmesh = genCube(
                    tmpCuboid.X1, tmpCuboid.Y1, tmpCuboid.Z1, 
                    tmpCuboid.X2 - tmpCuboid.X1, tmpCuboid.Y2 - tmpCuboid.Y1, tmpCuboid.Z2 - tmpCuboid.Z1, 
                    coreClientAPI, 
                    decalTexSource,
                    0,
                    0,
                    0,
                    0
                );

                mesh.AddMeshData(cuboidmesh);
            }

            return mesh;
        }



        static MeshData genCube(int voxelX, int voxelY, int voxelZ, int width, int height, int length, ICoreClientAPI capi, ITexPositionSource texSource, float subPixelPaddingx, float subPixelPaddingy, int renderpass, int renderFlags)
        {
             MeshData mesh = CubeMeshUtil.GetCube(
                 width / 32f, height / 32f, length / 32f, 
                 new Vec3f(voxelX / 16f, voxelY / 16f, voxelZ / 16f)
            );

            
            float[] sideShadings = CubeMeshUtil.DefaultBlockSideShadingsByFacing;

            mesh.Rgba.Fill((byte)255);

            mesh.Flags = new int[mesh.VerticesCount];
            mesh.Flags.Fill(renderFlags);
            mesh.RenderPasses = new int[mesh.VerticesCount / 4];
            mesh.RenderPassCount = mesh.VerticesCount / 4;
            for (int i = 0; i < mesh.RenderPassCount; i++)
            {
                mesh.RenderPasses[i] = renderpass;
            }
            mesh.Tints = new int[mesh.VerticesCount / 4];
            mesh.TintsCount = mesh.VerticesCount / 4;
            mesh.XyzFaces = new int[mesh.VerticesCount / 4];
            mesh.XyzFacesCount = mesh.VerticesCount / 4;
            

            int k = 0;
            for (int i = 0; i < 6; i++)
            {
                BlockFacing facing = BlockFacing.ALLFACES[i];

                mesh.XyzFaces[i] = i;

                int normal = (VertexFlags.NormalToPackedInt(facing.Normalf.X, facing.Normalf.Y, facing.Normalf.Z) << 15);
                mesh.Flags[i * 4 + 0] |= normal;
                mesh.Flags[i * 4 + 1] |= normal;
                mesh.Flags[i * 4 + 2] |= normal;
                mesh.Flags[i * 4 + 3] |= normal;

                bool isOutside =
                    (
                        (facing == BlockFacing.NORTH && voxelZ == 0) ||
                        (facing == BlockFacing.EAST && voxelX + width == 16) ||
                        (facing == BlockFacing.SOUTH && voxelZ + length == 16) ||
                        (facing == BlockFacing.WEST && voxelX == 0) ||
                        (facing == BlockFacing.UP && voxelY + height == 16) ||
                        (facing == BlockFacing.DOWN && voxelY == 0)
                    )
                ;
                 

                TextureAtlasPosition tpos = isOutside ? texSource[facing.Code] : texSource["inside-" + facing.Code];
                if (tpos == null)
                {
                    tpos = texSource[facing.Code];
                }

                for (int j = 0; j < 2*4; j++)
                {
                    if (j % 2 > 0)
                    {
                        mesh.Uv[k] = tpos.y1 + mesh.Uv[k] * 32f / texSource.AtlasSize.Height - subPixelPaddingy;
                    } else
                    {
                        mesh.Uv[k] = tpos.x1 + mesh.Uv[k] * 32f / texSource.AtlasSize.Width - subPixelPaddingx;
                    }
                    
                    k++;
                }

            }
            
            return mesh;
        }


        static MeshData genQuad(BlockFacing face, int voxelX, int voxelY, int voxelZ, int width, int height, ICoreClientAPI capi, Block block)
        {
            MeshData mesh = CubeMeshUtil.GetCubeFace(face, width / 32f, height / 32f, new Vec3f(voxelX / 16f, voxelY / 16f, voxelZ / 16f));

            float[] sideShadings = CubeMeshUtil.DefaultBlockSideShadingsByFacing;
            int faceIndex = face.Index;

            mesh.Rgba = new byte[16];
            mesh.Rgba.Fill((byte)(255 * sideShadings[faceIndex]));
            for (int j = 3; j < mesh.Rgba.Length; j += 4) mesh.Rgba[j] = (byte)255; // Alpha value

            mesh.Rgba2 = new byte[16];
            mesh.Rgba2.Fill((byte)(255 * sideShadings[faceIndex]));
            for (int j = 3; j < mesh.Rgba2.Length; j += 4) mesh.Rgba2[j] = (byte)255; // Alpha value

            mesh.Flags = new int[4];
            mesh.Flags.Fill(block.VertexFlags.All | face.NormalPackedFlags);
            mesh.RenderPasses = new int[1];
            mesh.RenderPassCount = 1;
            mesh.RenderPasses[0] = (int)block.RenderPass;
            mesh.Tints = new int[1];
            mesh.TintsCount = 1;
            mesh.XyzFaces = new int[] { faceIndex };
            mesh.XyzFacesCount = 1;

            return mesh;
        }


        #endregion


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            MaterialIds = MaterialIdsFromAttributes(tree, worldAccessForResolve);
            blockName = tree.GetString("blockName", "");

            VoxelCuboids = new List<uint>((tree["cuboids"] as IntArrayAttribute).AsUint);

            byte[] sideAo = tree.GetBytes("emitSideAo", new byte[] { 255 });
            if (sideAo.Length > 0)
            {
                emitSideAo[0] = (sideAo[0] & 1) > 0;
                emitSideAo[1] = (sideAo[0] & 2) > 0;
                emitSideAo[2] = (sideAo[0] & 4) > 0;
                emitSideAo[3] = (sideAo[0] & 8) > 0;
                emitSideAo[4] = (sideAo[0] & 16) > 0;
                emitSideAo[5] = (sideAo[0] & 32) > 0;

                absorbAnyLight = emitSideAo[0];
                emitSideAoByFlags = Block.ResolveAoFlags(this.Block, emitSideAo);
            }

            byte[] sideSolid = tree.GetBytes("sideSolid", new byte[] { 255 });
            if (sideSolid.Length > 0)
            {
                this.sideSolid[0] = (sideSolid[0] & 1) > 0;
                this.sideSolid[1] = (sideSolid[0] & 2) > 0;
                this.sideSolid[2] = (sideSolid[0] & 4) > 0;
                this.sideSolid[3] = (sideSolid[0] & 8) > 0;
                this.sideSolid[4] = (sideSolid[0] & 16) > 0;
                this.sideSolid[5] = (sideSolid[0] & 32) > 0;
            }



            if (Api is ICoreClientAPI)
            {
                RegenMesh();
                RegenSelectionBoxes(null);
                MarkDirty(true);
            }
        }


        public static int[] MaterialIdsFromAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            if (tree["materials"] is IntArrayAttribute)
            {
                // Pre 1.8 storage 
                ushort[] values = (tree["materials"] as IntArrayAttribute).AsUShort;

                int[] valuesInt = new int[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    valuesInt[i] = values[i];
                }

                return valuesInt;
            }
            else
            {
                string[] codes = (tree["materials"] as StringArrayAttribute).value;
                int[] ids = new int[codes.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = worldAccessForResolve.GetBlock(new AssetLocation(codes[i])).BlockId;
                }

                return ids;
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            StringArrayAttribute attr = new StringArrayAttribute();
            string[] materialCodes = new string[MaterialIds.Length];
            for (int i = 0; i < MaterialIds.Length; i++)
            {
                materialCodes[i] = Api.World.Blocks[MaterialIds[i]].Code.ToString();
            }
            attr.value = materialCodes;

            tree["materials"] = attr;
            tree["cuboids"] = new IntArrayAttribute(VoxelCuboids.ToArray());

            tree.SetBytes("emitSideAo", new byte[] { (byte)((emitSideAo[0] ? 1 : 0) | (emitSideAo[1] ? 2 : 0) | (emitSideAo[2] ? 4 : 0) | (emitSideAo[3] ? 8 : 0) | (emitSideAo[4] ? 16 : 0) | (emitSideAo[5] ? 32 : 0)) });

            tree.SetBytes("sideSolid", new byte[] { (byte)((sideSolid[0] ? 1 : 0) | (sideSolid[1] ? 2 : 0) | (sideSolid[2] ? 4 : 0) | (sideSolid[3] ? 8 : 0) | (sideSolid[4] ? 16 : 0) | (sideSolid[5] ? 32 : 0)) });

            

            tree.SetString("blockName", blockName);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            //ICoreClientAPI capi = api as ICoreClientAPI;
            if (Mesh == null) return false;

            mesher.AddMeshData(Mesh);
            return true;
        }


        public static uint ToCuboid(int minx, int miny, int minz, int maxx, int maxy, int maxz, int material)
        {
            Debug.Assert(maxx > 0 && maxx > minx);
            Debug.Assert(maxy > 0 && maxy > miny);
            Debug.Assert(maxz > 0 && maxz > minz);
            Debug.Assert(minx < 16);
            Debug.Assert(miny < 16);
            Debug.Assert(minz < 16);

            return (uint)(minx | (miny << 4) | (minz << 8) | ((maxx - 1) << 12) | ((maxy - 1) << 16) | ((maxz - 1) << 20) | (material << 24));
        }

        private uint ToCuboid(CuboidWithMaterial cub)
        {
            return (uint)(cub.X1 | (cub.Y1 << 4) | (cub.Z1 << 8) | ((cub.X2 - 1) << 12) | ((cub.Y2 - 1) << 16) | ((cub.Z2 - 1) << 20) | (cub.Material << 24));
        }


        public static void FromUint(uint val, ref CuboidWithMaterial tocuboid)
        {
            tocuboid.X1 = (int)((val) & 15);
            tocuboid.Y1 = (int)((val >> 4) & 15);
            tocuboid.Z1 = (int)((val >> 8) & 15);
            tocuboid.X2 = (int)(((val) >> 12) & 15) + 1;
            tocuboid.Y2 = (int)(((val) >> 16) & 15) + 1;
            tocuboid.Z2 = (int)(((val) >> 20) & 15) + 1;
            tocuboid.Material = (byte)((val >> 24) & 15);
        }
    }
}
