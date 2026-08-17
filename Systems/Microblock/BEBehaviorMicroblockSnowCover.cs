using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BEBehaviorMicroblockSnowCover : BlockEntityBehavior, IRotatable, IMicroblockBehavior
    {
        public int SnowLevel = 0;
        public int PrevSnowLevel = 0;
        public int snowLayerBlockId;
        public List<uint> SnowCuboids = new List<uint>();
        public List<uint> GroundSnowCuboids = new List<uint>();
        public MeshData SnowMesh;

        BlockEntityMicroBlock beMicroBlock;


        public BEBehaviorMicroblockSnowCover(BlockEntity blockentity) : base(blockentity)
        {
            beMicroBlock = blockentity as BlockEntityMicroBlock;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            SnowLevel = (int)Block.snowLevel;
            snowLayerBlockId = (Block as BlockMicroBlock)?.snowLayerBlockId ?? 0;
        }


        public void RotateModel(int degrees, EnumAxis? flipAroundAxis)
        {
            // Snow falls off if you flip around the block
            if (flipAroundAxis != null)
            {
                SnowCuboids = new List<uint>();
                GroundSnowCuboids = new List<uint>();
                SnowLevel = 0;
                if (Api != null) Api.World.BlockAccessor.ExchangeBlock((Block as BlockMicroBlock).notSnowCovered.Id, Pos);
            }
            else
            {
                beMicroBlock.TransformList(degrees, flipAroundAxis, SnowCuboids);
                beMicroBlock.TransformList(degrees, flipAroundAxis, GroundSnowCuboids);
            }
        }


        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int byDegrees, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAroundAxis)
        {
            uint[] snowcuboidValues = (tree["snowcuboids"] as IntArrayAttribute)?.AsUint;
            SnowCuboids = snowcuboidValues == null ? new List<uint>(0) : new List<uint>(snowcuboidValues);
            uint[] groundsnowvalues = (tree["groundSnowCuboids"] as IntArrayAttribute)?.AsUint;
            GroundSnowCuboids = groundsnowvalues == null ? new List<uint>(0) : new List<uint>(groundsnowvalues);

            tree["snowcuboids"] = new IntArrayAttribute(SnowCuboids.ToArray());
            tree["groundSnowCuboids"] = new IntArrayAttribute(GroundSnowCuboids.ToArray());
        }


        CuboidWithMaterial[] aboveCuboids = null;


        void buildSnowCuboids(BoolArray16x16x16 Voxels)
        {
            List<uint> snowCuboids = new List<uint>();
            List<uint> groundSnowCuboids = new List<uint>();

            var aboveBe = Api?.World.BlockAccessor.GetBlockEntity(Pos.UpCopy()) as BlockEntityMicroBlock;
            CuboidWithMaterial[] newAboveCuboids = null;

            // Replaced bool[16,16] heap allocation with stackalloc Span<bool> to avoid per-call GC pressure.
            // Also flattened the 2D voxel/visited access into single-dimension indices for better cache locality.
            Span<bool> snowVoxelVisited = stackalloc bool[256];

            for (int dy = 15; dy >= 0; dy--)
            {
                int yOffset = dy * 16; // flat index base for the entire Y plane within Voxels

                for (int dx = 0; dx < 16; dx++)
                {
                    int flatBase = dx * 256 + yOffset;     // base for Voxels.GetFlat(flatBase + dz)
                    int visitedRowBase = dx * 16;           // base for snowVoxelVisited[dx*16+dz]

                    for (int dz = 0; dz < 16; dz++)
                    {
                        int visitedIdx = visitedRowBase + dz;
                        if (snowVoxelVisited[visitedIdx]) continue;

                        bool voxelHere = Voxels.GetFlat(flatBase + dz);
                        bool ground = dy == 0 && !voxelHere;
                        bool search = ground || voxelHere;

                        if (!search) continue;

                        if (dy == 15 && aboveBe != null && newAboveCuboids == null)
                        {
                            newAboveCuboids = new CuboidWithMaterial[aboveBe.VoxelCuboids.Count];
                            for (int i = 0; i < newAboveCuboids.Length; i++)
                            {
                                BlockEntityMicroBlock.FromUint(aboveBe.VoxelCuboids[i], newAboveCuboids[i] = new CuboidWithMaterial());
                            }
                            // removed the now-empty loop that iterated aboveCuboids and called Contains() without using the result
                        }

                        CuboidWithMaterial cub = new CuboidWithMaterial()
                        {
                            Material = 0,
                            X1 = dx,
                            Y1 = dy,
                            Z1 = dz,
                            X2 = dx + 0,
                            Y2 = dy + 1,
                            Z2 = dz + 0
                        };

                        bool didGrowAny = true;
                        while (didGrowAny)
                        {
                            didGrowAny = false;
                            didGrowAny |= TrySnowableSurfaceGrowX(cub, Voxels, snowVoxelVisited, ground);
                            didGrowAny |= TrySnowableSurfaceGrowZ(cub, Voxels, snowVoxelVisited, ground);
                        }

                        if (cub.SizeX == 0 || cub.SizeZ == 0) continue;

                        for (int z = cub.Z1; z < cub.Z2; z++)
                        {
                            for (int x = cub.X1; x < cub.X2; x++)
                            {
                                snowVoxelVisited[x * 16 + z] = true; // flat index instead of [x, z]
                            }
                        }

                        if (ground)
                        {
                            groundSnowCuboids.Add(BlockEntityMicroBlock.ToUint(cub));
                        }
                        else
                        {
                            snowCuboids.Add(BlockEntityMicroBlock.ToUint(cub));
                        }

                        break;
                    }
                }
            }

            this.aboveCuboids = newAboveCuboids;
            this.GroundSnowCuboids = groundSnowCuboids;
            this.SnowCuboids = snowCuboids;
        }


        private void GenSnowMesh()
        {
            if (beMicroBlock != null)
            {
                beMicroBlock.ConvertToVoxels(out BoolArray16x16x16 Voxels, out byte[,,] VoxelMaterial);
                buildSnowCuboids(Voxels);
            }

            if (SnowCuboids.Count > 0 && SnowLevel > 0)
            {
                SnowMesh = BlockEntityMicroBlock.CreateMesh(Api as ICoreClientAPI, SnowCuboids, new int[] { snowLayerBlockId }, null, 0, beMicroBlock.OriginalVoxelCuboids, Pos);
                SnowMesh.Translate(0, 1 / 16f, 0);
                SnowMesh.Scale(new Vec3f(0.5f, 0, 0.5f), 0.999f, 1, 0.999f);

                if (Api.World.BlockAccessor.IsSideSolid(Pos.X, Pos.Y - 1, Pos.Z, BlockFacing.UP))
                {
                    SnowMesh.AddMeshData(BlockEntityMicroBlock.CreateMesh(Api as ICoreClientAPI, GroundSnowCuboids, new int[] { snowLayerBlockId }, null, 0, beMicroBlock.OriginalVoxelCuboids, Pos));
                }
            }
            else
            {
                SnowMesh = null;
            }
        }


        #region Snowgrow

        // Replaced bool[,] voxelVisited with Span<bool> to match stackalloc allocation in buildSnowCuboids.
        // Also switched from multi-dimensional Voxels[x,y,z] to flat GetFlat(idx) access for better memory layout.
        protected bool TrySnowableSurfaceGrowX(CuboidWithMaterial cub, BoolArray16x16x16 voxels, Span<bool> voxelVisited, bool ground)
        {
            if (cub.X2 > 15) return false;

            int x2 = cub.X2;
            int xBase = x2 * 256;
            int yOffset = cub.Y1 * 16;
            int aboveLen = aboveCuboids?.Length ?? 0;

            for (int z = cub.Z1; z < cub.Z2; z++)
            {
                int zz = Math.Min(15, z);

                if (aboveLen > 0)
                {
                    for (int i = 0; i < aboveLen; i++) if (aboveCuboids[i].Contains(x2, 0, zz)) return false;
                }

                int idx = xBase + yOffset + zz; // flat index instead of voxels[x2, cub.Y1, z]
                if (voxels.GetFlat(idx) == ground
                    || voxelVisited[x2 * 16 + zz]       // flat visited access
                    || (cub.Y1 < 15 && voxels.GetFlat(idx + 16))) // Y+1 is just +16 in flat layout
                    return false;
            }

            cub.X2++;
            return true;
        }

        protected bool TrySnowableSurfaceGrowZ(CuboidWithMaterial cub, BoolArray16x16x16 voxels, Span<bool> voxelVisited, bool ground) // Span<bool> instead of bool[,]
        {
            if (cub.Z2 > 15) return false;

            int z2 = cub.Z2;
            int yOffset = cub.Y1 * 16;
            int aboveLen = aboveCuboids?.Length ?? 0;

            for (int x = cub.X1; x < cub.X2; x++)
            {
                int xx = Math.Min(15, x);

                if (aboveLen > 0)
                {
                    for (int i = 0; i < aboveLen; i++) if (aboveCuboids[i].Contains(xx, 0, z2)) return false;
                }

                int idx = xx * 256 + yOffset + z2; //flat index instead of voxels[x, cub.Y1, cub.Z2]
                if (voxels.GetFlat(idx) == ground
                    || voxelVisited[xx * 16 + z2]       // flat visited access
                    || (cub.Y1 < 15 && voxels.GetFlat(idx + 16))) // Y+1 is just +16 in flat layout
                    return false;
            }

            cub.Z2++;
            return true;
        }




        #endregion


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            SnowLevel = (int)Block.snowLevel;
            if (SnowLevel == 0)
            {
                var abovebe = Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy()) as BlockEntityMicroBlock;
                if (abovebe != null && abovebe.Block.snowLevel > 0 && abovebe.VolumeRel < 1 / 16f)
                {
                    SnowLevel = (int)abovebe.Block.snowLevel;
                }

                if (SnowLevel == 0) return false;
            }

            if (PrevSnowLevel != SnowLevel || SnowMesh == null)
            {
                GenSnowMesh();
                PrevSnowLevel = SnowLevel;
            }

            mesher.AddMeshData(SnowMesh);

            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            uint[] snowvalues = (tree["snowcuboids"] as IntArrayAttribute)?.AsUint;
            uint[] groundsnowvalues = (tree["groundSnowCuboids"] as IntArrayAttribute)?.AsUint;
            if (snowvalues != null && groundsnowvalues != null)
            {
                SnowCuboids = new List<uint>(snowvalues);
                GroundSnowCuboids = new List<uint>(groundsnowvalues);
            }
            else
            {
                this.SnowMesh = null;
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            if (SnowCuboids.Count > 0)
            {
                tree["snowcuboids"] = new IntArrayAttribute(SnowCuboids.ToArray());
            }
            if (GroundSnowCuboids.Count > 0)
            {
                tree["groundSnowCuboids"] = new IntArrayAttribute(GroundSnowCuboids.ToArray());
            }
        }

        public void RebuildCuboidList(BoolArray16x16x16 voxels, byte[,,] voxelMaterial)
        {
            buildSnowCuboids(voxels);
        }

        public void RegenMesh()
        {
            SnowLevel = (int)Block.snowLevel;
            if (SnowLevel == 0)
            {
                var abovebe = Api.World.BlockAccessor.GetBlockEntity(Pos.Up()) as BlockEntityMicroBlock;
                Pos.Down();  // reverse the Pos.Up() without creating a new BlockPos object
                if (abovebe != null && abovebe.Block.snowLevel > 0 && abovebe.VolumeRel < 1 / 16f)
                {
                    SnowLevel = (int)abovebe.Block.snowLevel;
                }

                if (SnowLevel == 0) return;
            }

            GenSnowMesh();
        }
    }
}
