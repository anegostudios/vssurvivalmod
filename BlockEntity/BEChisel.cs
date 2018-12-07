using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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

    

    public class BlockEntityChisel : BlockEntity, IBlockShapeSupplier, IBlockEntityRotatable
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
        public ushort[] MaterialIds;
        
        
        public MeshData Mesh;
        Cuboidf[] selectionBoxes = new Cuboidf[0];
        Cuboidf[] selectionBoxesVoxels = new Cuboidf[0];
        int prevSize = -1;

        public bool DetailingMode
        {
            get { return api.Side == EnumAppSide.Client && (api.World as IClientWorldAccessor).Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible?.Tool == EnumTool.Chisel; }
        }

        public int ChiselMode(IPlayer player)
        {
            IItemSlot slot = player?.InventoryManager?.ActiveHotbarSlot;
            int? mode = slot?.Itemstack?.Collectible.GetToolMode(slot, player, new BlockSelection() { Position = pos });

            return mode == null ? 0 : (int)mode;
        }

        public int ChiselSize(IPlayer player)
        {
            int mode = ChiselMode(player);
            if (mode == 0) return 1;
            if (mode == 1) return 2;
            if (mode == 2) return 4;
            if (mode == 3) return 8;
            if (mode == 4) return 16;
            return 0;
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


        public void WasPlaced(Block block)
        {
            MaterialIds = new ushort[] { block.BlockId };
            VoxelCuboids.Add(ToCuboid(0, 0, 0, 16, 16, 16, 0));

            if (api.Side == EnumAppSide.Client && Mesh == null)
            {
                RegenMesh();
            }

            RegenSelectionBoxes(null);
        }


        public static uint ToCuboid(int minx, int miny, int minz, int maxx, int maxy, int maxz, int material)
        {
            Debug.Assert(maxx > 0 && maxx > minx);
            Debug.Assert(maxy > 0 && maxy > miny);
            Debug.Assert(maxz > 0 && maxz > minz);
            Debug.Assert(minx < 16);
            Debug.Assert(miny < 16);
            Debug.Assert(minz < 16);

            return (uint)(minx | (miny << 4) | (minz << 8) | ((maxx-1) << 12) | ((maxy-1) << 16) | ((maxz-1) << 20) | (material << 24));
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
        

        internal void OnBlockInteract(IPlayer byPlayer, BlockSelection blockSel, bool isBreak)
        {
            if (api.World.Side == EnumAppSide.Client && DetailingMode)
            {
                Cuboidf box = GetOrCreateVoxelSelectionBoxes(byPlayer)[blockSel.SelectionBoxIndex];
                Vec3i voxelPos = new Vec3i((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1));

                UpdateVoxel(byPlayer, byPlayer.InventoryManager.ActiveHotbarSlot, voxelPos, blockSel.Face, isBreak);
            }
        }


        internal void UpdateVoxel(IPlayer byPlayer, ItemSlot itemslot, Vec3i voxelPos, BlockFacing facing, bool isBreak)
        {
            int mode = ChiselMode(byPlayer);

            bool wasChanged = false;

            if (mode == 4)
            {
                RotateModel(byPlayer, isBreak);
                wasChanged = true;
            } else
            {
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
            }


            if (!wasChanged) return;

            if (api.Side == EnumAppSide.Client)
            {
                RegenMesh();
            }

            RegenSelectionBoxes(byPlayer);
            MarkDirty(true);

            // Send a custom network packet for server side, because
            // serverside blockselection index is inaccurate
            if (api.Side == EnumAppSide.Client)
            {
                SendUseOverPacket(byPlayer, voxelPos, facing, isBreak);
            }

            double posx = pos.X + voxelPos.X / 16f;
            double posy = pos.Y + voxelPos.Y / 16f;
            double posz = pos.Z + voxelPos.Z / 16f;
            api.World.PlaySoundAt(new AssetLocation("sounds/player/knap" + (api.World.Rand.Next(2) > 0 ? 1 : 2)), posx, posy, posz, byPlayer, true, 12, 1);

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                itemslot.Itemstack?.Collectible.DamageItem(api.World, byPlayer.Entity, itemslot);
            }


            if (VoxelCuboids.Count == 0)
            {
                api.World.BlockAccessor.SetBlock(0, pos);
                return;
            }
        }


        private void RotateModel(IPlayer byPlayer, bool clockwise)
        {
            List<uint> rotatedCuboids = new List<uint>();

            foreach (var val in this.VoxelCuboids)
            {
                FromUint(val, ref tmpCuboid);
                Cuboidi rotated = tmpCuboid.RotatedCopy(0, clockwise ? 90 : -90, 0, new Vec3d(8,8,8));
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

            ((ICoreClientAPI)api).Network.SendBlockEntityPacket(
                pos.X, pos.Y, pos.Z,
                (int)1000,
                data
            );
        }


        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            if (packetid == 1000)
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
            if (api.Side == EnumAppSide.Client && DetailingMode)
            {
                if (forPlayer == null) forPlayer = (api.World as IClientWorldAccessor).Player;

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



        public bool SetVoxel(Vec3i voxelPos, bool state, IPlayer byPlayer)
        {
            bool[,,] Voxels = new bool[16, 16, 16];
            byte[,,] VoxelMaterial = new byte[16, 16, 16];

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], ref tmpCuboid);

                for (int dx = tmpCuboid.X1; dx < tmpCuboid.X2; dx++)
                {
                    for (int dy = tmpCuboid.Y1; dy < tmpCuboid.Y2; dy++)
                    {
                        for (int dz = tmpCuboid.Z1; dz < tmpCuboid.Z2; dz++)
                        {
                            Voxels[dx, dy, dz] = true;
                            VoxelMaterial[dx, dy, dz] = tmpCuboid.Material;
                        }
                    }
                }
            }


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

            if (api.Side == EnumAppSide.Client)
            {
                RegenMesh();
            }

            RegenSelectionBoxes(null);
            MarkDirty(true);

            if (VoxelCuboids.Count == 0)
            {
                api.World.BlockAccessor.SetBlock(0, pos);
                return;
            }
        }



        private void RebuildCuboidList(bool[,,] Voxels, byte[,,] VoxelMaterial)
        {
            bool[,,] VoxelVisited = new bool[16, 16, 16];

            // And now let's rebuild the cuboids with some greedy search algo thing
            VoxelCuboids.Clear();

            for (int dx = 0; dx < 16; dx++)
            {
                for (int dy = 0; dy < 16; dy++)
                {
                    for (int dz = 0; dz < 16; dz++)
                    {
                        if (VoxelVisited[dx, dy, dz] || !Voxels[dx, dy, dz]) continue;

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


        public void RegenMesh()
        {
            Mesh = CreateMesh(api as ICoreClientAPI, VoxelCuboids, MaterialIds);
        }

        public static MeshData CreateMesh(ICoreClientAPI coreClientAPI, List<uint> voxelCuboids, ushort[] materials)
        {
            MeshData mesh = new MeshData(24, 36, false).WithTints().WithRenderpasses().WithXyzFaces();
            if (voxelCuboids == null || materials == null) return mesh;

            for (int i = 0; i < voxelCuboids.Count; i++)
            {
                FromUint(voxelCuboids[i], ref tmpCuboid);

                Block block = coreClientAPI.World.GetBlock(materials[tmpCuboid.Material]);

                //TextureAtlasPosition tpos = coreClientAPI.BlockTextureAtlas.GetPosition(block, BlockFacing.ALLFACES[0].Code);
                float subPixelPadding = coreClientAPI.BlockTextureAtlas.SubPixelPadding;

                MeshData cuboidmesh = genCube(
                    tmpCuboid.X1, tmpCuboid.Y1, tmpCuboid.Z1, 
                    tmpCuboid.X2 - tmpCuboid.X1, tmpCuboid.Y2 - tmpCuboid.Y1, tmpCuboid.Z2 - tmpCuboid.Z1, 
                    coreClientAPI, 
                    coreClientAPI.Tesselator.GetTexSource(block, 0, true),
                    subPixelPadding,
                    (int)block.RenderPass,
                    block.VertexFlags.All
                );

                mesh.AddMeshData(cuboidmesh);
            }

            return mesh;
        }

        public MeshData CreateDecalMesh(ITexPositionSource decalTexSource)
        {
            return CreateDecalMesh(api as ICoreClientAPI, VoxelCuboids, decalTexSource);
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
                    0
                );

                mesh.AddMeshData(cuboidmesh);
            }

            return mesh;
        }



        static MeshData genCube(int voxelX, int voxelY, int voxelZ, int width, int height, int length, ICoreClientAPI capi, ITexPositionSource texSource, float subPixelPadding, int renderpass, int renderFlags)
        {
             MeshData mesh = CubeMeshUtil.GetCube(
                 width / 32f, height / 32f, length / 32f, 
                 new Vec3f(voxelX / 16f, voxelY / 16f, voxelZ / 16f)
            );

            
            float[] sideShadings = CubeMeshUtil.DefaultBlockSideShadingsByFacing;

            for (int i = 0; i < mesh.Rgba.Length; i+=4)
            {
                int faceIndex = i / 4 / 4;  // 4 rgba per vertex, 4 vertices per face

                byte b = (byte)(255 * sideShadings[faceIndex]);
                mesh.Rgba[i + 0] = mesh.Rgba[i + 1] = mesh.Rgba[i + 2] = b;
            }

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
                mesh.XyzFaces[i] = i;

                BlockFacing facing = BlockFacing.ALLFACES[i];

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
                    mesh.Uv[k] = (j % 2 > 0 ? tpos.y1 : tpos.x1) + mesh.Uv[k] * 32f / texSource.AtlasSize - subPixelPadding;
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
            mesh.Flags.Fill(0);
            mesh.RenderPasses = new int[1];
            mesh.RenderPassCount = 1;
            mesh.RenderPasses[0] = (int)block.RenderPass;
            mesh.Tints = new int[1];
            mesh.TintsCount = 1;
            mesh.XyzFaces = new int[] { faceIndex };
            mesh.XyzFacesCount = 1;

            return mesh;
        }





        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            if (tree["materials"] is IntArrayAttribute)
            {
                // Pre 1.8 storage 
                MaterialIds = (tree["materials"] as IntArrayAttribute).AsUShort;
            } else
            {
                string[] codes = (tree["materials"] as StringArrayAttribute).value;
                MaterialIds = new ushort[codes.Length];
                for (int i = 0; i < MaterialIds.Length; i++)
                {
                    MaterialIds[i] = worldAccessForResolve.GetBlock(new AssetLocation(codes[i])).BlockId;
                }
            }
             
            VoxelCuboids = new List<uint>((tree["cuboids"] as IntArrayAttribute).AsUint);

            if (api is ICoreClientAPI)
            {
                RegenMesh();
                RegenSelectionBoxes(null);
                MarkDirty(true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            StringArrayAttribute attr = new StringArrayAttribute();
            string[] materialCodes = new string[MaterialIds.Length];
            for (int i = 0; i < MaterialIds.Length; i++)
            {
                materialCodes[i] = api.World.Blocks[MaterialIds[i]].Code.ToString();
            }
            attr.value = materialCodes;

            tree["materials"] = attr;
            tree["cuboids"] = new IntArrayAttribute(VoxelCuboids.ToArray());
        }


        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            //ICoreClientAPI capi = api as ICoreClientAPI;
            if (Mesh == null) return false;

            mesher.AddMeshData(Mesh);
            return true;
        }
    }
}
