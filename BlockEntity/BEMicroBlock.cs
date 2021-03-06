﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

    

    public class BlockEntityMicroBlock : BlockEntity, IBlockEntityRotatable
    {
        protected static ThreadLocal<CuboidWithMaterial> tmpCuboidTL = new ThreadLocal<CuboidWithMaterial>(() => new CuboidWithMaterial());
        protected static CuboidWithMaterial tmpCuboid => tmpCuboidTL.Value;


        // bits 0..3 = xmin
        // bits 4..7 = xmax
        // bits 8..11 = ymin
        // bits 12..15 = ymax
        // bits 16..19 = zmin
        // bits 20..23 = zmas
        // bits 24..31 = materialindex
        public List<uint> VoxelCuboids = new List<uint>();

        public int SnowLevel = 0;
        public int PrevSnowLevel = 0;
        public int snowLayerBlockId;
        public List<uint> SnowCuboids = new List<uint>();
        public List<uint> GroundSnowCuboids = new List<uint>();

        /// <summary>
        /// List of block ids for the materials used
        /// </summary>
        public int[] MaterialIds;
        
        
        public MeshData Mesh;
        public MeshData SnowMesh;
        protected Cuboidf[] selectionBoxes = new Cuboidf[0];
        protected Cuboidf[] selectionBoxesVoxels = new Cuboidf[0];
        protected int prevSize = -1;

        public string BlockName { get; set; } = "";

        protected int emitSideAo = 0x3F;
        protected bool absorbAnyLight;
        public bool[] sideSolid = new bool[6];
        protected byte nowmaterialIndex;

        public float sizeRel=1;



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (MaterialIds != null)
            {
                //if (api.Side == EnumAppSide.Client) RegenMesh();
                //RegenSelectionBoxes(null);
            }

            SnowLevel = (int)Block.snowLevel;
            snowLayerBlockId = (Block as BlockMicroBlock)?.snowLayerBlockId ?? 0;
        }

        public int GetLightAbsorption()
        {
            if (MaterialIds == null || !absorbAnyLight || Api == null)
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



        public bool CanAttachBlockAt(BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            if (attachmentArea == null)
            {
                return sideSolid[blockFace.Index];
            } else
            {
                HashSet<XYZ> req = new HashSet<XYZ>();
                for (int x = attachmentArea.X1; x <= attachmentArea.X2; x++)
                {
                    for (int y = attachmentArea.Y1; y <= attachmentArea.Y2; y++)
                    {
                        for (int z = attachmentArea.Z1; z <= attachmentArea.Z2; z++)
                        {
                            XYZ vec;

                            switch (blockFace.Index)
                            {
                                case 0: vec = new XYZ(x, y, 0); break; // N
                                case 1: vec = new XYZ(15, y, z); break; // E
                                case 2: vec = new XYZ(x, y, 15); break; // S
                                case 3: vec = new XYZ(0, y, z); break; // W
                                case 4: vec = new XYZ(x, 15, z); break; // U
                                case 5: vec = new XYZ(x, 0, z); break; // D
                                default: vec = new XYZ(0, 0, 0); break;
                            }

                            req.Add(vec);
                        }
                    }
                }

                CuboidWithMaterial cwm = tmpCuboid;

                for (int i = 0; i < VoxelCuboids.Count; i++)
                {
                    FromUint(VoxelCuboids[i], cwm);

                    for (int x = cwm.X1; x < cwm.X2; x++)
                    {
                        for (int y = cwm.Y1; y < cwm.Y2; y++)
                        {
                            for (int z = cwm.Z1; z < cwm.Z2; z++)
                            {
                                // Early exit
                                if (x != 0 && x != 15 && y != 0 && y != 15 && z != 0 && z != 15) continue;

                                req.Remove(new XYZ(x, y, z));
                            }
                        }
                    }
                }

                return req.Count == 0;
            }
        }



        public void WasPlaced(Block block, string blockName)
        {
            bool collBoxCuboid = block.Attributes?.IsTrue("chiselShapeFromCollisionBox") == true;

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
                    VoxelCuboids.Add(ToCuboid((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1), (int)(16 * box.X2), (int)(16 * box.Y2), (int)(16 * box.Z2), 0));
                }
            }

            this.BlockName = blockName;

            updateSideSolidSideAo();
            RegenSelectionBoxes(null);
            if (Api.Side == EnumAppSide.Client && Mesh == null)
            {
                RegenMesh();
            }
        }




        public void SetNowMaterial(byte index)
        {
            nowmaterialIndex = (byte)GameMath.Clamp(index, 0, MaterialIds.Length - 1);
        }



        public virtual Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos, IPlayer forPlayer = null)
        {
            if (selectionBoxes.Length == 0) return new Cuboidf[] { Cuboidf.Default() };
            return selectionBoxes;
        }

        public Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return selectionBoxes;
        }




        #region Voxel math


        protected void convertToVoxels(out bool[,,] voxels, out byte[,,] materials)
        {
            voxels = new bool[16, 16, 16];
            materials = new byte[16, 16, 16];
            CuboidWithMaterial cwm = tmpCuboid;

            

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], cwm);

                for (int dx = cwm.X1; dx < cwm.X2; dx++)
                {
                    for (int dy = cwm.Y1; dy < cwm.Y2; dy++)
                    {
                        for (int dz = cwm.Z1; dz < cwm.Z2; dz++)
                        {
                            voxels[dx, dy, dz] = true;
                            materials[dx, dy, dz] = cwm.Material;
                        }
                    }
                }
            }
        }

        protected void updateSideSolidSideAo()
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;

            convertToVoxels(out Voxels, out VoxelMaterial);
            RebuildCuboidList(Voxels, VoxelMaterial);
        }


        protected void FlipVoxels(BlockFacing frontFacing)
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

        protected void RotateModel(IPlayer byPlayer, bool clockwise)
        {
            List<uint> rotatedCuboids = new List<uint>();
            CuboidWithMaterial cwm = tmpCuboid;
            Vec3d axis = new Vec3d(8, 8, 8);

            foreach (var val in VoxelCuboids)
            {
                FromUint(val, cwm);
                Cuboidi rotated = cwm.RotatedCopy(0, clockwise ? 90 : -90, 0, axis);
                cwm.Set(rotated.X1, rotated.Y1, rotated.Z1, rotated.X2, rotated.Y2, rotated.Z2);
                rotatedCuboids.Add(ToCuboid(cwm));
            }
            VoxelCuboids = rotatedCuboids;

            rotatedCuboids = new List<uint>();
            foreach (var val in SnowCuboids)
            {
                FromUint(val, cwm);
                Cuboidi rotated = cwm.RotatedCopy(0, clockwise ? 90 : -90, 0, axis);
                cwm.Set(rotated.X1, rotated.Y1, rotated.Z1, rotated.X2, rotated.Y2, rotated.Z2);
                rotatedCuboids.Add(ToCuboid(cwm));
            }

            SnowCuboids = rotatedCuboids;

            rotatedCuboids = new List<uint>();
            foreach (var val in GroundSnowCuboids)
            {
                FromUint(val, cwm);
                Cuboidi rotated = cwm.RotatedCopy(0, clockwise ? 90 : -90, 0, axis);
                cwm.Set(rotated.X1, rotated.Y1, rotated.Z1, rotated.X2, rotated.Y2, rotated.Z2);
                rotatedCuboids.Add(ToCuboid(cwm));
            }

            GroundSnowCuboids = rotatedCuboids;
        }


        public void OnTransformed(ITreeAttribute tree, int byDegrees, EnumAxis? aroundAxis)
        {
            uint[] cuboidValues = (tree["cuboids"] as IntArrayAttribute)?.AsUint;
            VoxelCuboids = cuboidValues == null ? new List<uint>(0) : new List<uint>(cuboidValues);

            // Rotations from rotate schematic:
            if (aroundAxis == null && byDegrees == 90 || byDegrees == -90)
            {
                uint[] snowcuboidValues = (tree["snowcuboids"] as IntArrayAttribute)?.AsUint;
                uint[] groundsnowvalues = (tree["groundSnowCuboids"] as IntArrayAttribute)?.AsUint;
                SnowCuboids = snowcuboidValues == null ? new List<uint>(0) : new List<uint>(snowcuboidValues);
                GroundSnowCuboids = groundsnowvalues == null ? new List<uint>(0) : new List<uint>(groundsnowvalues);

                this.RotateModel(null, byDegrees < 0);

                tree["cuboids"] = new IntArrayAttribute(VoxelCuboids.ToArray());
                if (SnowCuboids.Count > 0)
                {
                    tree["snowcuboids"] = new IntArrayAttribute(SnowCuboids.ToArray());
                }
                if (GroundSnowCuboids.Count > 0)
                {
                    tree["groundSnowCuboids"] = new IntArrayAttribute(GroundSnowCuboids.ToArray());
                }

                return;
            }


            List<uint> rotatedCuboids = new List<uint>();
            Vec3d axis = new Vec3d(8, 8, 8);
            CuboidWithMaterial cwm = tmpCuboid;

            foreach (var val in VoxelCuboids)
            {
                FromUint(val, cwm);
                Cuboidi rotated = cwm.Clone();

                if (aroundAxis == EnumAxis.X)
                {
                    rotated.X1 = 16 - rotated.X1;
                    rotated.X2 = 16 - rotated.X2;
                }
                if (aroundAxis == EnumAxis.Y)
                {
                    rotated.Y1 = 16 - rotated.Y1;
                    rotated.Y2 = 16 - rotated.Y2;
                }
                if (aroundAxis == EnumAxis.Z)
                {
                    rotated.Z1 = 16 - rotated.Z1;
                    rotated.Z2 = 16 - rotated.Z2;
                }

                rotated = rotated.RotatedCopy(0, byDegrees, 0, axis);


                cwm.Set(rotated.X1, rotated.Y1, rotated.Z1, rotated.X2, rotated.Y2, rotated.Z2);
                rotatedCuboids.Add(ToCuboid(cwm));
            }

            tree["cuboids"] = new IntArrayAttribute(rotatedCuboids.ToArray());
            tree.RemoveAttribute("snowcuboids");   //can't sensibly rotate the current snow layer on X-Z axis rotations
            tree.RemoveAttribute("groundSnowCuboids");
        }



        public bool SetVoxel(Vec3i voxelPos, bool state, IPlayer byPlayer, byte materialId, int size)
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;

            convertToVoxels(out Voxels, out VoxelMaterial);

            // Ok, now we can actually modify the voxel
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

                        if (state)
                        {
                            VoxelMaterial[voxelPos.X + dx, voxelPos.Y + dy, voxelPos.Z + dz] = materialId;
                        }
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
            return (emitSideAo & (1 << facing)) != 0;
        }

        public bool DoEmitSideAoByFlag(int flag)
        {
            return (emitSideAo & flag) != 0;
        }

        #endregion


        protected void RebuildCuboidList(bool[,,] Voxels, byte[,,] VoxelMaterial)
        {
            bool[,,] VoxelVisited = new bool[16, 16, 16];
            emitSideAo = 0x3F;
            sideSolid = new bool[] { true, true, true, true, true, true };
            float voxelCount = 0;

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
                        } else
                        {
                            voxelCount++;
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
                            didGrowAny = false;
                            didGrowAny |= TryGrowX(cub, Voxels, VoxelVisited, VoxelMaterial);
                            didGrowAny |= TryGrowY(cub, Voxels, VoxelVisited, VoxelMaterial);
                            didGrowAny |= TryGrowZ(cub, Voxels, VoxelVisited, VoxelMaterial);
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
                sideSolid[i] = edgeCenterVoxelsMissing[i] < 5;
            }
            emitSideAo = doEmitSideAo ? 0x3F : 0;

            this.sizeRel = voxelCount / (16f * 16f * 16f);

            buildSnowCuboids(Voxels);
        }

        void buildSnowCuboids(bool[,,] Voxels) 
        {
            SnowCuboids.Clear();
            GroundSnowCuboids.Clear();

            //if (SnowLevel > 0) - always generate this
            {
                bool[,] snowVoxelVisited = new bool[16, 16];

                for (int dx = 0; dx < 16; dx++)
                {
                    for (int dz = 0; dz < 16; dz++)
                    {
                        if (snowVoxelVisited[dx, dz]) continue;

                        for (int dy = 15; dy >= 0; dy--)
                        {
                            bool ground = dy == 0;
                            bool solid = ground || Voxels[dx, dy, dz];

                            if (solid)
                            {
                                CuboidWithMaterial cub = new CuboidWithMaterial()
                                {
                                    Material = 0,
                                    X1 = dx,
                                    Y1 = dy,
                                    Z1 = dz,
                                    X2 = dx + 1,
                                    Y2 = dy + 1,
                                    Z2 = dz + 1
                                };

                                // Try grow this cuboid for as long as we can
                                bool didGrowAny = true;
                                while (didGrowAny)
                                {
                                    didGrowAny = false;
                                    didGrowAny |= TrySnowGrowX(cub, Voxels, snowVoxelVisited);
                                    didGrowAny |= TrySnowGrowZ(cub, Voxels, snowVoxelVisited);
                                }

                                if (ground)
                                {
                                    GroundSnowCuboids.Add(ToCuboid(cub));
                                }
                                else
                                {
                                    SnowCuboids.Add(ToCuboid(cub));
                                }

                                break;
                            }
                        }
                    }
                }
            }
        }


        protected bool TryGrowX(CuboidWithMaterial cub, bool[,,] voxels, bool[,,] voxelVisited, byte[,,] voxelMaterial)
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

        protected bool TryGrowY(CuboidWithMaterial cub, bool[,,] voxels, bool[,,] voxelVisited, byte[,,] voxelMaterial)
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

        protected bool TryGrowZ(CuboidWithMaterial cub, bool[,,] voxels, bool[,,] voxelVisited, byte[,,] voxelMaterial)
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




        #region Snowgrow

        protected bool TrySnowGrowX(CuboidWithMaterial cub, bool[,,] voxels, bool[,] voxelVisited)
        {
            if (cub.X2 > 15) return false;

            for (int z = cub.Z1; z < cub.Z2; z++)
            {
                if (!voxels[cub.X2, cub.Y1, z] || voxelVisited[cub.X2, z] || (cub.Y2 < 15 && voxels[cub.X2, cub.Y2, z])) return false;
            }

            for (int z = cub.Z1; z < cub.Z2; z++)
            {
                voxelVisited[cub.X2, z] = true;
            }

            cub.X2++;
            return true;
        }

        protected bool TrySnowGrowZ(CuboidWithMaterial cub, bool[,,] voxels, bool[,] voxelVisited)
        {
            if (cub.Z2 > 15) return false;

            for (int x = cub.X1; x < cub.X2; x++)
            {
                // Stop if
                // "Floor" is gone, already visited, or there's a voxel above
                if (!voxels[x, cub.Y1, cub.Z2] || voxelVisited[x, cub.Z2] || (cub.Y2 < 15 && voxels[x, cub.Y2, cub.Z2])) return false;
            }

            for (int x = cub.X1; x < cub.X2; x++)
            {
                voxelVisited[x, cub.Z2] = true;
            }

            cub.Z2++;
            return true;
        }



        #endregion



        public virtual void RegenSelectionBoxes(IPlayer byPlayer)
        {
            // Create a temporary array first, because the offthread particle system might otherwise access a null collisionbox
            Cuboidf[] selectionBoxesTmp = new Cuboidf[VoxelCuboids.Count];
            CuboidWithMaterial cwm = tmpCuboid;

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], cwm);
                selectionBoxesTmp[i] = cwm.ToCuboidf();
            }
            this.selectionBoxes = selectionBoxesTmp;
        }



        #endregion

        #region Mesh generation
        public void RegenMesh()
        {
            Mesh = CreateMesh(Api as ICoreClientAPI, VoxelCuboids, MaterialIds, Pos);
            GenSnowMesh();
        }

        private void GenSnowMesh()
        {
            if (SnowCuboids.Count > 0 && SnowLevel > 0)
            {
                SnowMesh = CreateMesh(Api as ICoreClientAPI, SnowCuboids, new int[] { snowLayerBlockId }, Pos);
                SnowMesh.Translate(0, 1 / 16f, 0);

                if (Api.World.BlockAccessor.GetBlock(Pos.DownCopy()).SideSolid[BlockFacing.UP.Index])
                {
                    SnowMesh.AddMeshData(CreateMesh(Api as ICoreClientAPI, GroundSnowCuboids, new int[] { snowLayerBlockId }, Pos));
                }
            }
            else
            {
                SnowMesh = null;
            }
        }

        public void RegenMesh(ICoreClientAPI capi)
        {
            Mesh = CreateMesh(capi, VoxelCuboids, MaterialIds, Pos);
        }

        public static MeshData CreateMesh(ICoreClientAPI coreClientAPI, List<uint> voxelCuboids, int[] materials, BlockPos posForRnd = null)
        {
            MeshData mesh = new MeshData(24, 36, false).WithColorMaps().WithRenderpasses().WithXyzFaces();
            if (voxelCuboids == null || materials == null) return mesh;
            CuboidWithMaterial cwm = tmpCuboid;

            for (int i = 0; i < voxelCuboids.Count; i++)
            {
                FromUint(voxelCuboids[i], cwm);

                Block block = coreClientAPI.World.GetBlock(materials[cwm.Material]);

                float subPixelPaddingx = coreClientAPI.BlockTextureAtlas.SubPixelPaddingX;
                float subPixelPaddingy = coreClientAPI.BlockTextureAtlas.SubPixelPaddingY;

                int altNum = 0;

                if (block.HasAlternates && posForRnd != null)
                {
                    int altcount = 0;
                    foreach (var val in block.Textures)
                    {
                        BakedCompositeTexture bct = val.Value.Baked;
                        if (bct.BakedVariants == null) continue;
                        altcount = Math.Max(altcount, bct.BakedVariants.Length);
                    }

                    altNum = block.RandomizeAxes == EnumRandomizeAxes.XYZ ? GameMath.MurmurHash3Mod(posForRnd.X, posForRnd.Y, posForRnd.Z, altcount) : GameMath.MurmurHash3Mod(posForRnd.X, 0, posForRnd.Z, altcount);
                }

                MeshData cuboidmesh = genCube(
                    cwm.X1, cwm.Y1, cwm.Z1,
                    cwm.X2 - cwm.X1, cwm.Y2 - cwm.Y1, cwm.Z2 - cwm.Z1, 
                    coreClientAPI, 
                    coreClientAPI.Tesselator.GetTexSource(block, altNum, true),
                    subPixelPaddingx,
                    subPixelPaddingy,
                    block
                );

                mesh.AddMeshData(cuboidmesh);
            }

            return mesh;
        }


        // Incomplete greedy mesh impl from 
        // https://0fps.net/2012/06/30/meshing-in-a-minecraft-game/
        MeshData GreedyMesh(bool[,,] voxels, byte[,,] materials, int[] dims)
        {
            // Sweep over 3-axes
            var mesh = new MeshData();

            for (var d = 0; d < 3; ++d)
            {
                int i, j, k, l, w, h
                  , u = (d + 1) % 3
                  , v = (d + 2) % 3;

                int[] x = new int[] { 0, 0, 0 };
                int[] q = new int[] { 0, 0, 0 };
                bool[] mask = new bool[dims[u] * dims[v]];

                q[d] = 1;

                for (x[d] = -1; x[d] < dims[d];)
                {
                    // Compute mask
                    var n = 0;
                    for (x[v] = 0; x[v] < dims[v]; ++x[v])
                        for (x[u] = 0; x[u] < dims[u]; ++x[u])
                        {
                            mask[n++] =
                              (0 <= x[d] ? voxels[x[0], x[1], x[2]] : false) !=
                              (x[d] < dims[d] - 1 ? voxels[x[0] + q[0], x[1] + q[1], x[2] + q[2]] : false);
                        }

                    // Increment x[d]
                    ++x[d];

                    // Generate mesh for mask using lexicographic ordering
                    n = 0;

                    for (j = 0; j < dims[v]; ++j)
                    {
                        for (i = 0; i < dims[u];)
                        {
                            if (mask[n])
                            {
                                // Compute width
                                for (w = 1; mask[n + w] && i + w < dims[u]; ++w)
                                {
                                }

                                // Compute height (this is slightly awkward
                                var done = false;
                                for (h = 1; j + h < dims[v]; ++h)
                                {
                                    for (k = 0; k < w; ++k)
                                    {
                                        if (!mask[n + k + h * dims[u]])
                                        {
                                            done = true;
                                            break;
                                        }
                                    }
                                    if (done)
                                    {
                                        break;
                                    }
                                }

                                // Add quad
                                x[u] = i; x[v] = j;
                                int[] du = new int[] { 0, 0, 0 };
                                int[] dv = new int[] { 0, 0, 0 };
                                du[u] = w;
                                dv[v] = h;

                                mesh.AddVertex(x[0], x[1], x[2]);
                                mesh.AddVertex(x[0] + du[0], x[1] + du[1], x[2] + du[2]);
                                mesh.AddVertex(x[0] + du[0] + dv[0], x[1] + du[1] + dv[1], x[2] + du[2] + dv[2]);
                                mesh.AddVertex(x[0] + dv[0], x[1] + dv[1], x[2] + dv[2]);


                                // Zero-out mask
                                for (l = 0; l < h; ++l)
                                {
                                    for (k = 0; k < w; ++k)
                                    {
                                        mask[n + k + l * dims[u]] = false;
                                    }
                                }

                                // Increment counters and continue
                                i += w; n += w;
                            }
                            else
                            {
                                ++i; ++n;
                            }
                        }
                    }
                }
            }


            Block block = Api.World.GetBlock(MaterialIds[0]);

            short renderpass = (short)block.RenderPass;
            int renderFlags = block.VertexFlags.All;

            mesh.Flags = new int[mesh.VerticesCount];
            mesh.Flags.Fill(renderFlags);
            mesh.RenderPassesAndExtraBits = new short[mesh.VerticesCount / 4];
            mesh.RenderPassCount = mesh.VerticesCount / 4;
            for (int i = 0; i < mesh.RenderPassCount; i++)
            {
                mesh.RenderPassesAndExtraBits[i] = renderpass;
            }

            mesh.ColorMapIdsCount = mesh.VerticesCount / 4;
            mesh.ClimateColorMapIds = new byte[mesh.VerticesCount / 4];
            mesh.SeasonColorMapIds = new byte[mesh.VerticesCount / 4];

            mesh.XyzFaces = new byte[mesh.VerticesCount / 4];
            mesh.XyzFacesCount = mesh.VerticesCount / 4;

            return mesh;
        }


        public MeshData CreateDecalMesh(ITexPositionSource decalTexSource)
        {
            return CreateDecalMesh(Api as ICoreClientAPI, VoxelCuboids, decalTexSource);
        }

        public static MeshData CreateDecalMesh(ICoreClientAPI coreClientAPI, List<uint> voxelCuboids, ITexPositionSource decalTexSource)
        {
            MeshData mesh = new MeshData(24, 36, false).WithColorMaps().WithRenderpasses().WithXyzFaces();

            CuboidWithMaterial cwm = tmpCuboid;

            for (int i = 0; i < voxelCuboids.Count; i++)
            {
                FromUint(voxelCuboids[i], cwm);

                MeshData cuboidmesh = genCube(
                    cwm.X1, cwm.Y1, cwm.Z1,
                    cwm.X2 - cwm.X1, cwm.Y2 - cwm.Y1, cwm.Z2 - cwm.Z1,
                    coreClientAPI,
                    decalTexSource,
                    0,
                    0,
                    coreClientAPI.World.GetBlock(0)
                );

                mesh.AddMeshData(cuboidmesh);
            }

            return mesh;
        }



        protected static MeshData genCube(int voxelX, int voxelY, int voxelZ, int width, int height, int length, ICoreClientAPI capi, ITexPositionSource texSource, float subPixelPaddingx, float subPixelPaddingy, Block block)
        {
            short renderpass = (short)block.RenderPass;
            int renderFlags = block.VertexFlags.All;

             MeshData mesh = CubeMeshUtil.GetCube(
                 width / 32f, height / 32f, length / 32f, 
                 new Vec3f(voxelX / 16f, voxelY / 16f, voxelZ / 16f)
            );

            
            mesh.Rgba.Fill((byte)255);
            mesh.Flags = new int[mesh.VerticesCount];
            mesh.Flags.Fill(renderFlags);
            mesh.RenderPassesAndExtraBits = new short[mesh.VerticesCount / 4];
            mesh.RenderPassCount = mesh.VerticesCount / 4;
            for (int i = 0; i < mesh.RenderPassCount; i++)
            {
                mesh.RenderPassesAndExtraBits[i] = renderpass;
            }

            mesh.ColorMapIdsCount = mesh.VerticesCount / 4;
            mesh.ClimateColorMapIds = new byte[mesh.VerticesCount / 4];
            mesh.SeasonColorMapIds = new byte[mesh.VerticesCount / 4];

            mesh.XyzFaces = new byte[mesh.VerticesCount / 4];
            mesh.XyzFacesCount = mesh.VerticesCount / 4;
            

            int k = 0;
            for (int i = 0; i < 6; i++)
            {
                BlockFacing facing = BlockFacing.ALLFACES[i];

                mesh.XyzFaces[i] = facing.MeshDataIndex;

                int normal = facing.NormalPackedFlags;
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
                if (tpos == null && block.Textures.Count > 0)
                {
                    tpos = texSource[block.Textures.First().Key];
                }
                if (tpos == null)
                {
                    tpos = capi.BlockTextureAtlas.UnknownTexturePosition;
                }

                float texWidth = tpos.x2 - tpos.x1;
                float texHeight = tpos.y2 - tpos.y1;

                for (int j = 0; j < 2*4; j++)
                {
                    if (j % 2 > 0)
                    {
                        mesh.Uv[k] = tpos.y1 + mesh.Uv[k] * texHeight - subPixelPaddingy;
                    } else
                    {
                        mesh.Uv[k] = tpos.x1 + mesh.Uv[k] * texWidth - subPixelPaddingx;
                    }
                    
                    k++;
                }

            }
            
            return mesh;
        }




        #endregion


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            MaterialIds = MaterialIdsFromAttributes(tree, worldAccessForResolve);
            BlockName = tree.GetString("blockName", "");

            uint[] values = (tree["cuboids"] as IntArrayAttribute)?.AsUint;
            // When loaded from json
            if (values == null)
            {
                values = (tree["cuboids"] as LongArrayAttribute)?.AsUint;
            }
            if (values == null)
            {
                values = new uint[] { ToCuboid(0,0,0, 16, 16, 16, 0) };
            }
            VoxelCuboids = new List<uint>(values);

            uint[] snowvalues = (tree["snowcuboids"] as IntArrayAttribute)?.AsUint;
            uint[] groundsnowvalues = (tree["groundSnowCuboids"] as IntArrayAttribute)?.AsUint;
            if (snowvalues != null && groundsnowvalues != null)
            {
                SnowCuboids = new List<uint>(snowvalues);
                GroundSnowCuboids = new List<uint>(groundsnowvalues);
            } else
            {
                bool[,,] Voxels;
                byte[,,] VoxelMaterial;
                convertToVoxels(out Voxels, out VoxelMaterial);
                buildSnowCuboids(Voxels);
            }

            byte[] sideAo = tree.GetBytes("emitSideAo", new byte[] { 255 });
            if (sideAo.Length > 0)
            {
                emitSideAo = sideAo[0];

                absorbAnyLight = emitSideAo != 0;
            }

            byte[] sideSolid = tree.GetBytes("sideSolid", new byte[] { 255 });
            if (sideSolid.Length > 0)
            {
                GameMath.BoolsFromInt(this.sideSolid, sideSolid[0]);
            }


            if (worldAccessForResolve.Side == EnumAppSide.Client)
            {
                RegenMesh(worldAccessForResolve.Api as ICoreClientAPI);
            }

            RegenSelectionBoxes(null);
        }


        public static int[] MaterialIdsFromAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            if (tree["materials"] is IntArrayAttribute)
            {
                // Pre 1.8 storage and Post 1.13-pre.2 storage
                int[] ids = (tree["materials"] as IntArrayAttribute).value;

                int[] valuesInt = new int[ids.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    valuesInt[i] = ids[i];
                }

                return valuesInt;
            }
            else
            {
                if (!(tree["materials"] is StringArrayAttribute))
                {
                    return new int[] { worldAccessForResolve.GetBlock(new AssetLocation("rock-granite")).Id };
                }

                string[] codes = (tree["materials"] as StringArrayAttribute).value;
                int[] ids = new int[codes.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    Block block = worldAccessForResolve.GetBlock(new AssetLocation(codes[i]));
                    if (block == null)
                    {
                        block = worldAccessForResolve.GetBlock(new AssetLocation(codes[i] + "-free")); // pre 1.13 blocks

                        if (block == null)
                        {
                            block = worldAccessForResolve.GetBlock(new AssetLocation("rock-granite"));
                        }
                    }

                    ids[i] = block.BlockId;
                }

                return ids;
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            IntArrayAttribute attr = new IntArrayAttribute();
            attr.value = MaterialIds;

            if (attr.value != null)
            {
                tree["materials"] = attr;
            }

            
            tree["cuboids"] = new IntArrayAttribute(VoxelCuboids.ToArray());

            if (SnowCuboids.Count > 0)
            {
                tree["snowcuboids"] = new IntArrayAttribute(SnowCuboids.ToArray());
            }
            if (GroundSnowCuboids.Count > 0)
            {
                tree["groundSnowCuboids"] = new IntArrayAttribute(GroundSnowCuboids.ToArray());
            }

            tree.SetBytes("emitSideAo", new byte[] { (byte) emitSideAo });

            tree.SetBytes("sideSolid", new byte[] { (byte) GameMath.IntFromBools(sideSolid) });

            tree.SetString("blockName", BlockName);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Mesh == null) return false;

            mesher.AddMeshData(Mesh);

            Block = Api.World.BlockAccessor.GetBlock(Pos);
            SnowLevel = (int)Block.snowLevel;
            if (PrevSnowLevel != SnowLevel || SnowMesh == null)
            {
                GenSnowMesh();
                PrevSnowLevel = SnowLevel;
            }

            mesher.AddMeshData(SnowMesh);

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

        protected uint ToCuboid(CuboidWithMaterial cub)
        {
            return (uint)(cub.X1 | (cub.Y1 << 4) | (cub.Z1 << 8) | ((cub.X2 - 1) << 12) | ((cub.Y2 - 1) << 16) | ((cub.Z2 - 1) << 20) | (cub.Material << 24));
        }


        public static void FromUint(uint val, CuboidWithMaterial tocuboid)
        {
            tocuboid.X1 = (int)((val) & 15);
            tocuboid.Y1 = (int)((val >> 4) & 15);
            tocuboid.Z1 = (int)((val >> 8) & 15);
            tocuboid.X2 = (int)(((val) >> 12) & 15) + 1;
            tocuboid.Y2 = (int)(((val) >> 16) & 15) + 1;
            tocuboid.Z2 = (int)(((val) >> 20) & 15) + 1;
            tocuboid.Material = (byte)((val >> 24) & 15);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed);

            for (int i = 0; i < MaterialIds.Length; i++)
            {
                AssetLocation code;
                if (oldBlockIdMapping.TryGetValue(MaterialIds[i], out code))
                {
                    Block block = worldForNewMappings.GetBlock(code);
                    if (block == null)
                    {
                        worldForNewMappings.Logger.Warning("Cannot load chiseled block id mapping @ {1}, block code {0} not found block registry. Will not display correctly.", code, Pos);
                        continue;
                    }

                    MaterialIds[i] = block.Id;
                } else
                {
                    worldForNewMappings.Logger.Warning("Cannot load chiseled block id mapping @ {1}, block id {0} not found block registry. Will not display correctly.", MaterialIds[i], Pos);
                }
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

            for (int i = 0; i < MaterialIds.Length; i++)
            {
                Block block = Api.World.GetBlock(MaterialIds[i]);
                blockIdMapping[MaterialIds[i]] = block.Code;
            }
        }
    }



    struct XYZ : IEquatable<XYZ>
    {
        public int X;
        public int Y;
        public int Z;

        public XYZ(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(XYZ other)
        {
            return other.X == X && other.Y == Y && other.Z == Z;
        }
    }
}
