using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public partial class BlockEntityMicroBlock : BlockEntity, IRotatable, IAcceptsDecor, IMaterialExchangeable
    {
        protected static ThreadLocal<CuboidWithMaterial[]> tmpCuboidTL = new ThreadLocal<CuboidWithMaterial[]>(() =>
        {
            var val = new CuboidWithMaterial[16 * 16 * 16];
            for (int i = 0; i < val.Length; i++) val[i] = new CuboidWithMaterial();
            return val;
        });
        protected static CuboidWithMaterial[] tmpCuboids => tmpCuboidTL.Value;

        protected static uint[] defaultOriginalVoxelCuboids => new uint[] { ToUint(0, 0, 0, 16, 16, 16, 0) };

        // bits 0..3 = xmin
        // bits 4..7 = xmax
        // bits 8..11 = ymin
        // bits 12..15 = ymax
        // bits 16..19 = zmin
        // bits 20..23 = zmas
        // bits 24..31 = materialindex
        public List<uint> VoxelCuboids = new List<uint>();
        protected uint[] originalCuboids;
        public uint[] OriginalVoxelCuboids => originalCuboids == null ? defaultOriginalVoxelCuboids : originalCuboids;

        [Obsolete("Use BlockIds instead")]
        public int[] MaterialIds => BlockIds;
        /// <summary>
        /// List of block ids for the materials used in this microblock
        /// </summary>
        public int[] BlockIds;
        /// <summary>
        /// List of decor block ids per block face, i.e. can only ever be null or be a 6 length array
        /// </summary>
        public int[] DecorIds = null;

        protected int[] BlockIdsRotated;
        protected int[] DecorIdsRotated;
        public int rotated;

        public MeshData Mesh;
        protected Cuboidf[] selectionBoxesNoMeta = null;
        protected Cuboidf[] selectionBoxes = new Cuboidf[0];
        protected Cuboidf[] selectionBoxesVoxels = new Cuboidf[0];
        protected int prevSize = -1;

        public string BlockName { get; set; } = "";

        protected int emitSideAo = 0x3F;
        protected bool absorbAnyLight;
        public bool[] sidecenterSolid = new bool[6];
        public bool[] sideAlmostSolid = new bool[6];
        protected short rotationY;
        public float sizeRel = 1;
        protected int totalVoxels;
        protected bool withColorMapData;

        /// <summary>
        /// A value from 0..1 describing how % of the full block is still left
        /// </summary>
        public float VolumeRel => totalVoxels / (16f * 16f * 16f);


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
        }


        public virtual void WasPlaced(Block block, string blockName)
        {
            bool collBoxCuboid = block.Attributes?.IsTrue("chiselShapeFromCollisionBox") == true;

            BlockIds = new int[] { block.BlockId };

            if (!collBoxCuboid)
            {
                VoxelCuboids.Add(ToUint(0, 0, 0, 16, 16, 16, 0));
            }
            else
            {
                Cuboidf[] collboxes = block.GetCollisionBoxes(Api.World.BlockAccessor, Pos);

                originalCuboids = new uint[collboxes.Length];

                for (int i = 0; i < collboxes.Length; i++)
                {
                    Cuboidf box = collboxes[i];
                    var uintbox = ToUint((int)(16 * box.X1), (int)(16 * box.Y1), (int)(16 * box.Z1), (int)(16 * box.X2), (int)(16 * box.Y2), (int)(16 * box.Z2), 0);
                    VoxelCuboids.Add(uintbox);
                    originalCuboids[i] = uintbox;
                }
            }

            this.BlockName = blockName;

            RebuildCuboidList();
            RegenSelectionBoxes(Api.World, null);
            if (Api.Side == EnumAppSide.Client && Mesh == null)
            {
                MarkMeshDirty();
            }
        }

        public override void OnBlockRemoved()
        {
            UpdateNeighbors(this);
            base.OnBlockRemoved();
        }



        #region Get block properties

        public byte[] GetLightHsv(IBlockAccessor ba)
        {
            int[] matids = BlockIds;
            byte[] hsv = new byte[3];
            int q = 0;

            if (matids == null) return hsv;

            for (int i = 0; i < matids.Length; i++)
            {
                Block block = ba.GetBlock(matids[i]);
                if (block != null && block.LightHsv[2] > 0)
                {
                    hsv[0] += block.LightHsv[0];
                    hsv[1] += block.LightHsv[1];
                    hsv[2] += block.LightHsv[2]; // Should take into account the amount of used voxels, but then we need to pass the old light hsv to the relighting engine or we'll get lighting bugs
                    q++;
                }
            }

            if (q == 0) return hsv;

            hsv[0] = (byte)(hsv[0] / q);
            hsv[1] = (byte)(hsv[1] / q);
            hsv[2] = (byte)(hsv[2] / q);

            return hsv;
        }


        public BlockSounds GetSounds()
        {
            var mbsounds = (Block as BlockMicroBlock).MBSounds.Value;
            mbsounds.Init(this, Block);
            return mbsounds;
        }

        public int GetLightAbsorption()
        {
            if (BlockIds == null || !absorbAnyLight || Api == null)
            {
                return 0;
            }

            int absorb = 99;

            for (int i = 0; i < BlockIds.Length; i++)
            {
                Block block = Api.World.GetBlock(BlockIds[i]);
                absorb = Math.Min(absorb, block.LightAbsorption);
            }

            return absorb;
        }

        public bool CanAttachBlockAt(BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            if (attachmentArea == null)
            {
                return sidecenterSolid[blockFace.Index];
            }
            else
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

                CuboidWithMaterial cwm = tmpCuboids[0];

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

        public virtual Cuboidf[] GetSelectionBoxes(IBlockAccessor world, BlockPos pos, IPlayer forPlayer = null)
        {
            if (selectionBoxesNoMeta != null)
            {
                if (Api.Side == EnumAppSide.Client && (Api as ICoreClientAPI).Settings.Bool["renderMetaBlocks"] == false)
                {
                    return selectionBoxesNoMeta;
                }
                if (Api.Side == EnumAppSide.Server) return null;
            }

            if (selectionBoxes.Length == 0) return new Cuboidf[] { Cuboidf.Default() };
            return selectionBoxes;
        }

        public Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return selectionBoxes;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            dsc.AppendLine(GetPlacedBlockName());

            if (forPlayer?.CurrentBlockSelection?.Face != null && BlockIds != null)
            {
                Block block = Api.World.GetBlock(BlockIds[0]);
                var mat = block.BlockMaterial;
                if (mat == EnumBlockMaterial.Ore || mat == EnumBlockMaterial.Stone || mat == EnumBlockMaterial.Soil || mat == EnumBlockMaterial.Ceramic)
                {
                    if (sideAlmostSolid[forPlayer.CurrentBlockSelection.Face.Index] || sideAlmostSolid[forPlayer.CurrentBlockSelection.Face.Opposite.Index] && VolumeRel >= 0.5f)
                    {
                        dsc.AppendLine(Lang.Get("Insulating block face"));
                    }
                }
            }

        }

        public string GetPlacedBlockName()
        {
            return GetPlacedBlockName(Api, VoxelCuboids, BlockIds, BlockName);
        }

        public static string GetPlacedBlockName(ICoreAPI api, List<uint> voxelCuboids, int[] blockIds, string blockName)
        {
            if ((blockName == null || blockName == "") && blockIds != null)
            {
                int mblockid = getMajorityMaterial(voxelCuboids, blockIds);
                Block majorityBlock = api.World.Blocks[mblockid];
                return majorityBlock.GetHeldItemName(new ItemStack(majorityBlock));
            }
            else
            {
                return blockName.Substring(blockName.IndexOf('\n') + 1);
            }
        }

        public int GetMajorityMaterialId(ActionBoolReturn<int> filterblockId = null)
        {
            return getMajorityMaterial(VoxelCuboids, BlockIds, filterblockId);
        }

        public static int getMajorityMaterial(List<uint> voxelCuboids, int[] blockIds, ActionBoolReturn<int> filterblockId = null)
        {
            Dictionary<int, int> volumeByBlockid = new Dictionary<int, int>();
            CuboidWithMaterial cwm = new CuboidWithMaterial();
            for (int i = 0; i < voxelCuboids.Count; i++)
            {
                FromUint(voxelCuboids[i], cwm);
                var blockid = blockIds[cwm.Material];

                if (volumeByBlockid.ContainsKey(blockid))
                {
                    volumeByBlockid[blockid] += cwm.SizeXYZ;
                }
                else
                {
                    volumeByBlockid[blockid] = cwm.SizeXYZ;
                }
            }

            if (volumeByBlockid.Count == 0) return 0;

            if (filterblockId != null)
            {
                volumeByBlockid = volumeByBlockid.Where(vbb => filterblockId?.Invoke(vbb.Key) == true).ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            
            if (volumeByBlockid.Count == 0) return 0;

            var mblockid = volumeByBlockid.MaxBy(vbb => vbb.Value).Key;
            return mblockid;
        }


        #endregion

        #region Voxel math


        public void ConvertToVoxels(out bool[,,] voxels, out byte[,,] materials)
        {
            voxels = new bool[16, 16, 16];
            materials = new byte[16, 16, 16];
            CuboidWithMaterial cwm = tmpCuboids[0];

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

        public void RebuildCuboidList()
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;

            ConvertToVoxels(out Voxels, out VoxelMaterial);
            RebuildCuboidList(Voxels, VoxelMaterial);
        }


        public void FlipVoxels(BlockFacing frontFacing)
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;

            ConvertToVoxels(out Voxels, out VoxelMaterial);

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

        public void TransformList(int degrees, EnumAxis? flipAroundAxis, List<uint> list)
        {
            CuboidWithMaterial cwm = tmpCuboids[0];
            Vec3d axis = new Vec3d(8, 8, 8);

            for (int i = 0; i < list.Count; i++)
            {
                uint val = list[i];
                FromUint(val, cwm);

                if (flipAroundAxis == EnumAxis.X)
                {
                    cwm.X1 = 16 - cwm.X1;
                    cwm.X2 = 16 - cwm.X2;
                }
                if (flipAroundAxis == EnumAxis.Y)
                {
                    cwm.Y1 = 16 - cwm.Y1;
                    cwm.Y2 = 16 - cwm.Y2;
                }
                if (flipAroundAxis == EnumAxis.Z)
                {
                    cwm.Z1 = 16 - cwm.Z1;
                    cwm.Z2 = 16 - cwm.Z2;
                }

                Cuboidi rotated = cwm.RotatedCopy(0, -degrees, 0, axis); // Not sure why its negative

                cwm.Set(rotated.X1, rotated.Y1, rotated.Z1, rotated.X2, rotated.Y2, rotated.Z2);
                list[i] = ToUint(cwm);
            }
        }

        public void RotateModel(int degrees, EnumAxis? flipAroundAxis)
        {
            TransformList(degrees, flipAroundAxis, VoxelCuboids);

            foreach (var val in Behaviors)
            {
                if (val is IMicroblockBehavior bebhmicroblock)
                {
                    bebhmicroblock.RotateModel(degrees, flipAroundAxis);
                }
            }

            if (flipAroundAxis != null)
            {
                if (originalCuboids != null)
                {
                    var origCubs = new List<uint>(originalCuboids);
                    TransformList(degrees, flipAroundAxis, origCubs);
                    originalCuboids = origCubs.ToArray();
                }

                int shift = -degrees / 90;
                bool[] prevSolid = (bool[])sidecenterSolid.Clone();
                bool[] prevAlmostSolid = (bool[])sideAlmostSolid.Clone();

                for (int i = 0; i < 4; i++)
                {
                    sidecenterSolid[i] = prevSolid[GameMath.Mod(i + shift, 4)];
                    sideAlmostSolid[i] = prevAlmostSolid[GameMath.Mod(i + shift, 4)];
                }
            }

            Api?.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);

            rotationY = (short)((rotationY + degrees) % 360);
        }


        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int byDegrees,
            Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping,
            EnumAxis? flipAroundAxis)
        {
            uint[] cuboidValues = (tree["cuboids"] as IntArrayAttribute)?.AsUint;
            VoxelCuboids = cuboidValues == null ? new List<uint>(0) : new List<uint>(cuboidValues);

            RotateModel(byDegrees, flipAroundAxis);

            tree["cuboids"] = new IntArrayAttribute(VoxelCuboids.ToArray());
            
            var materialIds = (tree["materials"] as IntArrayAttribute)?.value;
            if (materialIds != null)
            {
                var newMaterialIds = new int[materialIds.Length];
                for (var i = 0; i < materialIds.Length; i++)
                {
                    var materialId = materialIds[i];

                    if (oldBlockIdMapping.TryGetValue(materialId, out var code))
                    {
                        var block = worldAccessor.GetBlock(code);
                        if (block != null)
                        {
                            var assetLocation = block.GetRotatedBlockCode(byDegrees);
                            var newBlock = worldAccessor.GetBlock(assetLocation);
                            newMaterialIds[i] = newBlock.Id;
                        }
                        else
                        {
                            newMaterialIds[i] = materialId;
                            worldAccessor.Logger.Warning("Cannot load chiseled block id mapping for rotation @ {1}, block id {0} not found block registry. Will not display correctly.", code, Pos);
                        }
                    }
                    else
                    {
                        // when we rotate block in worldedit multiple times the the new blockid form a previous rotation
                        // won't be in the oldBlockIdMapping but the id is still valid so just use it if the block exists
                        newMaterialIds[i] = materialId;
                        if (materialId >= worldAccessor.Blocks.Count)
                        {
                            worldAccessor.Logger.Warning("Cannot load chiseled block id mapping for rotation @ {1}, block code {0} not found block registry. Will not display correctly.", materialId, Pos);
                        }
                    }
                }

                tree["materials"] = new IntArrayAttribute(newMaterialIds);
            }

            foreach (var val in Behaviors)
            {
                if (val is IRotatable bhrot)
                {
                    bhrot.OnTransformed(worldAccessor ,tree, byDegrees, oldBlockIdMapping, oldItemIdMapping, flipAroundAxis);
                }
            }
        }


        public int GetVoxelMaterialAt(Vec3i voxelPos)
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;
            ConvertToVoxels(out Voxels, out VoxelMaterial);

            if (Voxels[voxelPos.X, voxelPos.Y, voxelPos.Z])
            {
                return BlockIds[VoxelMaterial[voxelPos.X, voxelPos.Y, voxelPos.Z]];
            }

            return 0;
        }

        public bool SetVoxel(Vec3i voxelPos, bool state, byte materialId, int size)
        {
            bool[,,] Voxels;
            byte[,,] VoxelMaterial;
            ConvertToVoxels(out Voxels, out VoxelMaterial);

            // Ok, now we can actually modify the voxel
            bool wasChanged = false;

            int endx = voxelPos.X + size;
            int endy = voxelPos.Y + size;
            int endz = voxelPos.Z + size;
            for (int x = voxelPos.X; x < endx; x++)
            {
                for (int y = voxelPos.Y; y < endy; y++)
                {
                    for (int z = voxelPos.Z; z < endz; z++)
                    {
                        if (x >= 16 || y >= 16 || z >= 16) continue;
                        if (state)
                        {
                            wasChanged |= !Voxels[x, y, z] || VoxelMaterial[x, y, z] != materialId;
                            Voxels[x, y, z] = true;
                            VoxelMaterial[x, y, z] = materialId;
                        }
                        else
                        {
                            wasChanged |= Voxels[x, y, z];
                            Voxels[x, y, z] = false;
                        }
                    }
                }
            }

            if (!wasChanged) return false;

            RebuildCuboidList(Voxels, VoxelMaterial);
            Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);

            return true;
        }


        public void BeginEdit(out bool[,,] voxels, out byte[,,] voxelMaterial)
        {
            ConvertToVoxels(out voxels, out voxelMaterial);
        }

        public void EndEdit(bool[,,] voxels, byte[,,] voxelMaterial)
        {
            RebuildCuboidList(voxels, voxelMaterial);
            Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
        }



        public void SetData(bool[,,] Voxels, byte[,,] VoxelMaterial)
        {
            RebuildCuboidList(Voxels, VoxelMaterial);

            if (Api.Side == EnumAppSide.Client)
            {
                //RegenMesh();
                MarkMeshDirty();
            }

            RegenSelectionBoxes(Api.World, null);
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


        public override void HistoryStateRestore()
        {
            RebuildCuboidList();
            MarkDirty(true);
        }

        protected void RebuildCuboidList(bool[,,] Voxels, byte[,,] VoxelMaterial)
        {
            bool[,,] VoxelVisited = new bool[16, 16, 16];
            emitSideAo = 0x3F;
            sidecenterSolid = new bool[] { true, true, true, true, true, true };
            float voxelCount = 0;

            // And now let's rebuild the cuboids with some greedy search algo thing
            List<uint> voxelCuboids = new List<uint>();

            int[] edgeVoxelsMissing = new int[6];
            int[] edgeCenterVoxelsMissing = new int[6];

            byte[] lightshv = GetLightHsv(Api.World.BlockAccessor);

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
                        else
                        {
                            voxelCount++;
                        }

                        if (VoxelVisited[dx, dy, dz]) continue;

                        CuboidWithMaterial cub = new CuboidWithMaterial()
                        {
                            Material = VoxelMaterial[dx, dy, dz],
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
                            didGrowAny |= TryGrowX(cub, Voxels, VoxelVisited, VoxelMaterial);
                            didGrowAny |= TryGrowY(cub, Voxels, VoxelVisited, VoxelMaterial);
                            didGrowAny |= TryGrowZ(cub, Voxels, VoxelVisited, VoxelMaterial);
                        }

                        voxelCuboids.Add(ToUint(cub));
                    }
                }
            }

            this.VoxelCuboids = voxelCuboids;

            bool doEmitSideAo = edgeVoxelsMissing[0] < 64 || edgeVoxelsMissing[1] < 64 || edgeVoxelsMissing[2] < 64 || edgeVoxelsMissing[3] < 64 || edgeVoxelsMissing[4] < 64 || edgeVoxelsMissing[5] < 64;

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

            int emitFlags = 0;
            for (int i = 0; i < 6; i++)
            {
                sidecenterSolid[i] = edgeCenterVoxelsMissing[i] < 5;
                if ((sideAlmostSolid[i] = edgeVoxelsMissing[i] <= 32)) emitFlags += 1 << i;
            }
            //if (emitFlags != 0x3F)
            //{
            //    if (emitFlags == 0x3E && emitFlags == 0x3D && emitFlags == 0x3B && emitFlags == 0x37) emitFlags = 0x3F;  // If only one side missing, treat as full cube
            //    else emitFlags &= 0x30;
            //}
            emitSideAo = lightshv[2] < 10 && doEmitSideAo ? emitFlags : 0;

            if (BlockIds.Length == 1 && Api.World.GetBlock(BlockIds[0]).RenderPass == EnumChunkRenderPass.Meta)
            {
                emitSideAo = 0;
            }

            this.sizeRel = voxelCount / (16f * 16f * 16f);

            foreach (var val in Behaviors)
            {
                if (val is IMicroblockBehavior bebhmicroblock)
                {
                    bebhmicroblock.RebuildCuboidList(Voxels, VoxelMaterial);
                }
            }

            if (DisplacesLiquid())
            {
                Api.World.BlockAccessor.SetBlock(0, Pos, BlockLayersAccess.Fluid);
            }
        }

        public bool DisplacesLiquid()
        {
            return sideAlmostSolid[0] && sideAlmostSolid[1] && sideAlmostSolid[2] && sideAlmostSolid[3] && sideAlmostSolid[5];
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



        public virtual void RegenSelectionBoxes(IWorldAccessor worldForResolve, IPlayer byPlayer)
        {
            // Create a temporary array first, because the offthread particle system might otherwise access a null collisionbox
            Cuboidf[] selectionBoxesTmp = new Cuboidf[VoxelCuboids.Count];
            CuboidWithMaterial cwm = tmpCuboids[0];

            List<Cuboidf> selBoxesNoMeta = null;
            bool hasMetaBlock = false;
            for (int i = 0; i < BlockIds.Length; i++) hasMetaBlock |= worldForResolve.Blocks[BlockIds[i]].RenderPass == EnumChunkRenderPass.Meta;
            if (hasMetaBlock) selBoxesNoMeta = new List<Cuboidf>();


            totalVoxels = 0;

            for (int i = 0; i < VoxelCuboids.Count; i++)
            {
                FromUint(VoxelCuboids[i], cwm);
                selectionBoxesTmp[i] = cwm.ToCuboidf();
                totalVoxels += cwm.Volume;

                if (hasMetaBlock && worldForResolve.Blocks[BlockIds[cwm.Material]].RenderPass != EnumChunkRenderPass.Meta) selBoxesNoMeta.Add(selectionBoxesTmp[i]);
            }

            selectionBoxes = selectionBoxesTmp;
            selectionBoxesNoMeta = selBoxesNoMeta?.ToArray();
        }



        #endregion

        #region De-/Serialization

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            BlockIds = MaterialIdsFromAttributes(tree, worldAccessForResolve);
            DecorIds = (int[])(tree["decorIds"] as IntArrayAttribute)?.value.Clone();
            BlockName = tree.GetString("blockName", null);

            int rot = tree.GetInt("rotation", 0);
            rotationY = (short)(((rot >> 10) & 0b1111111111) - 360); // 10 bits is enough for -360 .. 360

            VoxelCuboids = new List<uint>(GetVoxelCuboids(tree));

            byte[] sideAo = tree.GetBytes("emitSideAo", new byte[] { 255 });
            if (sideAo.Length > 0)
            {
                emitSideAo = sideAo[0];

                absorbAnyLight = emitSideAo != 0;
            }

            byte[] sideSolid = tree.GetBytes("sideSolid", new byte[] { 255 });
            if (sideSolid.Length > 0)
            {
                GameMath.BoolsFromInt(this.sidecenterSolid, sideSolid[0]);
            }

            byte[] sideAlmostSolid = tree.GetBytes("sideAlmostSolid", new byte[] { 255 });
            if (sideAlmostSolid.Length > 0)
            {
                GameMath.BoolsFromInt(this.sideAlmostSolid, sideAlmostSolid[0]);
            }

            if (tree.HasAttribute("originalCuboids"))
            {
                originalCuboids = (tree["originalCuboids"] as IntArrayAttribute)?.AsUint;
            }

            if (worldAccessForResolve.Side == EnumAppSide.Client)
            {
                if (Api != null)
                {
                    Mesh = GenMesh();
                    Api.World.BlockAccessor.MarkBlockModified(Pos); // not sure why this one is needed
                }

                const int chunksize = GlobalConstants.ChunkSize;
                int lx = Pos.X % chunksize;
                int lz = Pos.X % chunksize;

                // Update neighours only when
                // a) Microblock was modified, i.e. api is not null and something triggered a resend of data
                if (Api != null)
                {
                    UpdateNeighbors(this);
                }
                // b) Microblock was loaded and is at the edge of a chunk
                else if (lx == 0 || lx == chunksize - 1 || lz == 0 || lz == chunksize - 1)
                {
                    if (lx == 0) UpdateNeighbour(worldAccessForResolve, Pos, BlockFacing.WEST);
                    if (lz == 0) UpdateNeighbour(worldAccessForResolve, Pos, BlockFacing.NORTH);
                    if (lx == chunksize - 1) UpdateNeighbour(worldAccessForResolve, Pos, BlockFacing.EAST);
                    if (lz == chunksize - 1) UpdateNeighbour(worldAccessForResolve, Pos, BlockFacing.SOUTH);
                }
            }
            else
            {
                // From 1.15.0 until 1.15.5 we forgot to store sideAlmostSolid
                if (!tree.HasAttribute("sideAlmostSolid"))
                {
                    if (Api == null) this.Api = worldAccessForResolve.Api; // Needed for LightHsv property, I hope this does not break things >.>
                    RebuildCuboidList();
                }
            }

            RegenSelectionBoxes(worldAccessForResolve, null);
        }

        public static uint[] GetVoxelCuboids(ITreeAttribute tree)
        {
            uint[] values = (tree["cuboids"] as IntArrayAttribute)?.AsUint;
            // When loaded from json
            if (values == null)
            {
                values = (tree["cuboids"] as LongArrayAttribute)?.AsUint;
            }
            if (values == null)
            {
                values = new uint[] { ToUint(0, 0, 0, 16, 16, 16, 0) };
            }

            return values;
        }
        public static int[] MaterialIdsFromAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            if (tree["materials"] is IntArrayAttribute)
            {
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
                // Old storage format

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

            if (BlockIds != null)
            {
                tree["materials"] = new IntArrayAttribute(BlockIds);
            }
            if (DecorIds != null)
            {
                tree["decorIds"] = new IntArrayAttribute(DecorIds);
            }

            tree.SetInt("rotation", (rotationY + 360) << 10);
            tree["cuboids"] = new IntArrayAttribute(VoxelCuboids.ToArray());
            tree.SetBytes("emitSideAo", new byte[] { (byte)emitSideAo });
            tree.SetBytes("sideSolid", new byte[] { (byte)GameMath.IntFromBools(sidecenterSolid) });
            tree.SetBytes("sideAlmostSolid", new byte[] { (byte)GameMath.IntFromBools(sideAlmostSolid) });
            tree.SetString("blockName", BlockName);

            if (originalCuboids != null)
            {
                tree["originalCuboids"] = new IntArrayAttribute(originalCuboids);
            }
        }


        public static uint ToUint(int minx, int miny, int minz, int maxx, int maxy, int maxz, int material)
        {
            Debug.Assert(maxx > 0 && maxx > minx);
            Debug.Assert(maxy > 0 && maxy > miny);
            Debug.Assert(maxz > 0 && maxz > minz);
            Debug.Assert(minx < 16);
            Debug.Assert(miny < 16);
            Debug.Assert(minz < 16);

            return (uint)(minx | (miny << 4) | (minz << 8) | ((maxx - 1) << 12) | ((maxy - 1) << 16) | ((maxz - 1) << 20) | (material << 24));
        }

        public static uint ToUint(CuboidWithMaterial cub)
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
            tocuboid.Material = (byte)((val >> 24) & 0xff);
        }


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);

            for (int i = 0; i < BlockIds.Length; i++)
            {
                AssetLocation code;
                if (oldBlockIdMapping != null && oldBlockIdMapping.TryGetValue(BlockIds[i], out code))
                {
                    Block block = worldForNewMappings.GetBlock(code);
                    if (block == null)
                    {
                        worldForNewMappings.Logger.Warning("Cannot load chiseled block id mapping @ {1}, block code {0} not found block registry. Will not display correctly.", code, Pos);
                        continue;
                    }

                    BlockIds[i] = block.Id;
                }
                else
                {
                    // if a schematic gets rotated this BlockId is already the correct one, but if we cant find it either something is wrong
                    var block = worldForNewMappings.GetBlock(BlockIds[i]);
                    if (block == null)
                    {
                        worldForNewMappings.Logger.Warning("Cannot load chiseled block id mapping @ {1}, block id {0} not found block registry. Will not display correctly.", BlockIds[i], Pos);
                    }
                }
            }

            if (DecorIds != null)
            {
                for (int i = 0; i < DecorIds.Length; i++)
                {
                    AssetLocation code;
                    if (oldBlockIdMapping.TryGetValue(DecorIds[i], out code))
                    {
                        Block block = worldForNewMappings.GetBlock(code);
                        if (block == null)
                        {
                            worldForNewMappings.Logger.Warning("Cannot load chiseled decor block id mapping @ {1}, block code {0} not found block registry. Will not display correctly.", code, Pos);
                            continue;
                        }

                        DecorIds[i] = block.Id;
                    }
                    else
                    {
                        worldForNewMappings.Logger.Warning("Cannot load chiseled decor block id mapping @ {1}, block id {0} not found block registry. Will not display correctly.", DecorIds[i], Pos);
                    }
                }
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

            for (int i = 0; i < BlockIds.Length; i++)
            {
                Block block = Api.World.GetBlock(BlockIds[i]);
                blockIdMapping[BlockIds[i]] = block.Code;
            }

            if (DecorIds != null)
            {
                for (int i = 0; i < DecorIds.Length; i++)
                {
                    Block block = Api.World.GetBlock(DecorIds[i]);
                    blockIdMapping[DecorIds[i]] = block.Code;
                }
            }
        }


        public virtual bool RemoveMaterial(Block block)
        {
            if (BlockIds.Contains(block.Id))
            {
                int index = BlockIds.IndexOf(block.Id);
                BlockIds = BlockIds.Remove(block.Id);

                for (int i = 0; i < VoxelCuboids.Count; i++)
                {
                    var material = (int)((VoxelCuboids[i] >> 24) & 0xFu);
                    if (index == material)
                    {
                        VoxelCuboids.RemoveAt(i);
                        i--;
                    }
                }

                ShiftMaterialIndicesAt(index);
                return true;
            }

            return false;
        }

        private void ShiftMaterialIndicesAt(int index)
        {
            for (int j = 0; j < VoxelCuboids.Count; j++)
            {
                var material = (VoxelCuboids[j] >> 24) & 0xFu;
                if (material >= index)
                {
                    VoxelCuboids[index] = (uint)((VoxelCuboids[j] & ~(255 << 24)) | ((material-1) << 24));
                }
            }
        }

        #endregion

        #region Mesh generation

        public MeshData GenMesh()
        {
            if (BlockIds == null) return null;
            GenRotatedMaterialIds();

            var mesh = CreateMesh(Api as ICoreClientAPI, VoxelCuboids, BlockIdsRotated, DecorIdsRotated, OriginalVoxelCuboids, Pos);

            foreach (var val in Behaviors)
            {
                if (val is IMicroblockBehavior bebhmicroblock)
                {
                    bebhmicroblock.RegenMesh();
                }
            }
            withColorMapData = false;
            for (int i = 0; !withColorMapData && i < BlockIds.Length; i++) withColorMapData |= Api.World.Blocks[BlockIds[i]].ClimateColorMapResolved != null;

            return mesh;
        }

        private void GenRotatedMaterialIds()
        {
            if (rotationY == 0)
            {
                // Early exit in the most common case where there is no rotation
                BlockIdsRotated = BlockIds;
                DecorIdsRotated = DecorIds;
                return;
            }

            if (BlockIdsRotated == null || BlockIdsRotated.Length < BlockIds.Length) BlockIdsRotated = new int[BlockIds.Length];
            for (var i = 0; i < BlockIds.Length; i++)
            {
                int id = BlockIds[i];
                Block block = Api.World.GetBlock(id);
                var rotatedBlockCode = block.GetRotatedBlockCode(rotationY);
                Block rotatedBlock = rotatedBlockCode == null ? null : Api.World.GetBlock(rotatedBlockCode);
                BlockIdsRotated[i] = rotatedBlock == null ? id : rotatedBlock.Id;
            }

            if (DecorIds != null)
            {
                if (DecorIdsRotated == null || DecorIdsRotated.Length < DecorIds.Length) DecorIdsRotated = new int[DecorIds.Length];
                for (var i = 0; i < 4; i++)
                {
                    DecorIdsRotated[i] = DecorIds[GameMath.Mod(i + rotationY/90, 4)];
                }

                DecorIdsRotated[4] = DecorIds[4];
                DecorIdsRotated[5] = DecorIds[5];
            }
        }

        public void RegenMesh(ICoreClientAPI capi)
        {
            GenRotatedMaterialIds();
            Mesh = CreateMesh(capi, VoxelCuboids, BlockIdsRotated, DecorIdsRotated, OriginalVoxelCuboids, Pos);
        }


        public void MarkMeshDirty()
        {
            Mesh = null;
        }

        // Contributed by Xytabich#5684 on discord
        // Under MIT license (evidence is in email inbox of tyron, jan 18 2023)

        public const int EXT_VOXELS_PER_SIDE = 18;// 16 per self and one for each neighbor
        public const int EXT_VOXELS_SQ = EXT_VOXELS_PER_SIDE * EXT_VOXELS_PER_SIDE;

        [ThreadStatic]
        public static VoxelInfo[] tmpVoxels;
        [ThreadStatic]
        public static RefList<VoxelMaterial> tmpBlockMaterials;
        [ThreadStatic]
        public static RefList<VoxelMaterial> tmpDecorMaterials;

        private static readonly SizeConverter ConvertPlaneX = ConvertPlaneXImpl;
        private static readonly SizeConverter ConvertPlaneY = ConvertPlaneYImpl;
        private static readonly SizeConverter ConvertPlaneZ = ConvertPlaneZImpl;

        private static readonly int[] shiftOffsetByFace = new int[6] { 0, 1, 1, 0, 1, 0 };

        private static RefList<VoxelMaterial> getOrCreateBlockMatRefList()
        {
            return tmpBlockMaterials ?? (tmpBlockMaterials = new RefList<VoxelMaterial>());
        }
        private static RefList<VoxelMaterial> getOrCreateDecorMatRefList()
        {
            return tmpDecorMaterials ?? (tmpDecorMaterials = new RefList<VoxelMaterial>());
        }

        private static VoxelInfo[] getOrCreateCuboidInfoArray()
        {
            return tmpVoxels ?? (tmpVoxels = new VoxelInfo[EXT_VOXELS_PER_SIDE * EXT_VOXELS_PER_SIDE * EXT_VOXELS_PER_SIDE]);
        }


        public static MeshData CreateMesh(ICoreClientAPI capi, List<uint> voxelCuboids, int[] blockIds, int[] decorIds, BlockPos posForRnd = null, uint[] originalCuboids = null)
        {
            return CreateMesh(capi, voxelCuboids, blockIds, decorIds, originalCuboids ?? defaultOriginalVoxelCuboids, posForRnd);
        }

        public unsafe static MeshData CreateMesh(ICoreClientAPI capi, List<uint> voxelCuboids, int[] blockIds, int[] decorIds, uint[] originalVoxelCuboids, BlockPos pos = null)
        {
            var mesh = new MeshData(24, 36).WithColorMaps().WithRenderpasses().WithXyzFaces();

            if (voxelCuboids == null || blockIds == null)
            {
                return mesh;
            }

            var blockMatList = getOrCreateBlockMatRefList();
            blockMatList.Clear();

            var voxels = getOrCreateCuboidInfoArray();
            // Clear array of VoxelInfo structs
            fixed (VoxelInfo* ptr = voxels)
            {
                Unsafe.InitBlockUnaligned(ptr, 255, (uint)(sizeof(VoxelInfo) * voxels.Length)); // 255 means "-1" for material field

                if (pos != null)
                {
                    FetchNeighborVoxels(capi, blockMatList, ptr, pos);
                }
            }

            bool hasTopSoil = false;
            int matIndex = blockMatList.Count;
            for (int i = 0; i < blockIds.Length; i++)
            {
                var block = capi.World.GetBlock(blockIds[i]);
                blockMatList.Add(VoxelMaterial.FromBlock(capi, block, pos, true));

                hasTopSoil |= block.RenderPass == EnumChunkRenderPass.TopSoil;
            }
            if (hasTopSoil) mesh.CustomFloats = new CustomMeshDataPartFloat() { InterleaveOffsets = new int[] { 0 }, InterleaveSizes = new int[] { 2 }, InterleaveStride = 8 };

            RefList<VoxelMaterial> decorMatList = null;

            if (decorIds != null)
            {
                decorMatList = getOrCreateDecorMatRefList();
                decorMatList.Clear();
                for (int i = 0; i < decorIds.Length; i++)
                {
                    decorMatList.Add(decorIds[i] == 0 ? noMat : VoxelMaterial.FromBlock(capi, capi.World.GetBlock(decorIds[i]), pos, true));
                }
            }


            // stackalloc to force the array onto the stack, not the heap
            // original bounds info are required to figure out if we need to use inside or outside texture
            int* origVoxelBounds = stackalloc int[6];
            FromUint(originalVoxelCuboids[0],
                out origVoxelBounds[BlockFacing.indexWEST], out origVoxelBounds[BlockFacing.indexDOWN], out origVoxelBounds[BlockFacing.indexNORTH],
                out origVoxelBounds[BlockFacing.indexEAST], out origVoxelBounds[BlockFacing.indexUP], out origVoxelBounds[BlockFacing.indexSOUTH],
                out _
            );
            

            var count = voxelCuboids.Count;
            fixed (VoxelInfo* voxelsPtr = voxels)
            {
                int x0, y0, z0, x1, y1, z1, material;
                for (int i = 0; i < count; i++)
                {
                    FromUint(voxelCuboids[i], out x0, out y0, out z0, out x1, out y1, out z1, out material);
                    FillCuboidEdges(voxelsPtr, x0, y0, z0, x1, y1, z1, matIndex + material);
                }

                GenFaceInfo genFaceInfo = default;
                genFaceInfo.capi = capi;
                genFaceInfo.targetMesh = mesh;
                genFaceInfo.originalBounds = origVoxelBounds;
                genFaceInfo.subPixelPaddingx = capi.BlockTextureAtlas.SubPixelPaddingX;
                genFaceInfo.subPixelPaddingy = capi.BlockTextureAtlas.SubPixelPaddingY;

                GenPlaneInfo genPlaneInfo = default;
                genPlaneInfo.blockMaterials = blockMatList;
                genPlaneInfo.decorMaterials = decorMatList;
                genPlaneInfo.voxels = voxelsPtr;

                for (int i = 0; i < count; i++)
                {
                    FromUint(voxelCuboids[i], out x0, out y0, out z0, out x1, out y1, out z1, out material);
                    genPlaneInfo.materialIndex = matIndex + material;
                    GenCuboidMesh(ref genFaceInfo, ref genPlaneInfo, x0, y0, z0, x1, y1, z1);
                }
            }

            return mesh;
        }


        public MeshData CreateDecalMesh(ITexPositionSource decalTexSource)
        {
            return CreateDecalMesh(Api as ICoreClientAPI, VoxelCuboids, decalTexSource, OriginalVoxelCuboids);
        }
        public unsafe static MeshData CreateDecalMesh(ICoreClientAPI capi, List<uint> voxelCuboids, ITexPositionSource decalTexSource, uint[] originalVoxelCuboids)
        {
            var mesh = new MeshData(24, 36).WithColorMaps().WithRenderpasses().WithXyzFaces();
            if (voxelCuboids == null)
            {
                return mesh;
            }

            var matList = getOrCreateBlockMatRefList();
            matList.Clear();
            matList.Add(VoxelMaterial.FromTexSource(capi, decalTexSource, true));

            int* origVoxelBounds = stackalloc int[6];
            FromUint(originalVoxelCuboids[0],
                out origVoxelBounds[BlockFacing.indexWEST], out origVoxelBounds[BlockFacing.indexDOWN], out origVoxelBounds[BlockFacing.indexNORTH],
                out origVoxelBounds[BlockFacing.indexEAST], out origVoxelBounds[BlockFacing.indexUP], out origVoxelBounds[BlockFacing.indexSOUTH],
                out _
            );

            var count = voxelCuboids.Count;
            var voxels = getOrCreateCuboidInfoArray();
            fixed (VoxelInfo* ptr = voxels)
            {
                Unsafe.InitBlockUnaligned(ptr, 255, (uint)(sizeof(VoxelInfo) * voxels.Length));

                int x0, y0, z0, x1, y1, z1;
                for (int i = 0; i < count; i++)
                {
                    FromUint(voxelCuboids[i], out x0, out y0, out z0, out x1, out y1, out z1, out _);
                    FillCuboidEdges(ptr, x0, y0, z0, x1, y1, z1, 0);
                }

                GenFaceInfo genFaceInfo = default;
                genFaceInfo.capi = capi;
                genFaceInfo.targetMesh = mesh;
                genFaceInfo.originalBounds = origVoxelBounds;
                genFaceInfo.subPixelPaddingx = capi.BlockTextureAtlas.SubPixelPaddingX;
                genFaceInfo.subPixelPaddingy = capi.BlockTextureAtlas.SubPixelPaddingY;

                GenPlaneInfo genPlaneInfo = default;
                genPlaneInfo.blockMaterials = matList;
                genPlaneInfo.voxels = ptr;
                genPlaneInfo.materialIndex = 0;

                for (int i = 0; i < count; i++)
                {
                    FromUint(voxelCuboids[i], out x0, out y0, out z0, out x1, out y1, out z1, out _);
                    GenCuboidMesh(ref genFaceInfo, ref genPlaneInfo, x0, y0, z0, x1, y1, z1);
                }
            }

            return mesh;
        }
    

        // Load in voxel material data from adjacent microblocks
        private static unsafe void FetchNeighborVoxels(ICoreClientAPI capi, RefList<VoxelMaterial> matList, VoxelInfo* voxels, BlockPos pos)
        {
            var ba = capi.World.BlockAccessor;
            foreach (var face in BlockFacing.ALLFACES)
            {
                var blockPos = pos.AddCopy(face);
                if (ba.GetBlockEntity(blockPos) is BlockEntityMicroBlock bm)
                {
                    var materials = bm.BlockIds;
                    var voxCuboids = bm.VoxelCuboids;
                    if (materials == null || voxCuboids == null) continue;
                    var voxelCuboids = voxCuboids;

                    int matOffset = matList.Count;
                    foreach (var id in materials)
                    {
                        matList.Add(VoxelMaterial.FromBlock(capi, capi.World.GetBlock(id), blockPos, true));
                    }

                    int x0, y0, z0, x1, y1, z1, material;
                    for (int i = 0; i < voxelCuboids.Count; i++)
                    {
                        FromUint(voxelCuboids[i], out x0, out y0, out z0, out x1, out y1, out z1, out material);
                        if (material >= materials.Length) break;

                        FillCuboidFace(voxels, x0, y0, z0, x1, y1, z1, matOffset + material, face);
                    }
                }
            }
        }

        private static unsafe void FillCuboidFace(VoxelInfo* cuboids, int x0, int y0, int z0, int x1, int y1, int z1, int material, BlockFacing face)
        {
            switch (face.Index)
            {
                case 0:
                    if (z1 != 16) return;
                    break;
                case 1:
                    if (x0 != 0) return;
                    break;
                case 2:
                    if (z0 != 0) return;
                    break;
                case 3:
                    if (x1 != 16) return;
                    break;
                case 4:
                    if (y0 != 0) return;
                    break;
                case 5:
                    if (y1 != 16) return;
                    break;
            }

            // offset by 1, due to neighbors
            x0++;
            x1++;
            y0++;
            y1++;
            z0++;
            z1++;

            y0 *= EXT_VOXELS_PER_SIDE;
            y1 *= EXT_VOXELS_PER_SIDE;
            z0 *= EXT_VOXELS_SQ;
            z1 *= EXT_VOXELS_SQ;

            switch (face.Index)
            {
                case 0:
                    FillPlane(cuboids, material, x0, x1, 1, y0, y1, EXT_VOXELS_PER_SIDE, 0);
                    break;
                case 1:
                    FillPlane(cuboids, material, y0, y1, EXT_VOXELS_PER_SIDE, z0, z1, EXT_VOXELS_SQ, EXT_VOXELS_PER_SIDE - 1);
                    break;
                case 2:
                    FillPlane(cuboids, material, x0, x1, 1, y0, y1, EXT_VOXELS_PER_SIDE, (EXT_VOXELS_PER_SIDE - 1) * EXT_VOXELS_SQ);
                    break;
                case 3:
                    FillPlane(cuboids, material, y0, y1, EXT_VOXELS_PER_SIDE, z0, z1, EXT_VOXELS_SQ, 0);
                    break;
                case 4:
                    FillPlane(cuboids, material, x0, x1, 1, z0, z1, EXT_VOXELS_SQ, (EXT_VOXELS_PER_SIDE - 1) * EXT_VOXELS_PER_SIDE);
                    break;
                case 5:
                    FillPlane(cuboids, material, x0, x1, 1, z0, z1, EXT_VOXELS_SQ, 0);
                    break;
            }
        }

        // Populate the voxel array with materials for given cuboid, but only the outer 6 planes of this cuboid, not its insides
        public static unsafe void FillCuboidEdges(VoxelInfo* cuboids, int x0, int y0, int z0, int x1, int y1, int z1, int material)
        {
            // offset by 1, due to extended size array (18x18x18)
            x0++;
            x1++;
            y0++;
            y1++;
            z0++;
            z1++;

            y0 *= EXT_VOXELS_PER_SIDE;
            y1 *= EXT_VOXELS_PER_SIDE;
            z0 *= EXT_VOXELS_SQ;
            z1 *= EXT_VOXELS_SQ;

            FillPlane(cuboids, material, x0, x1, 1, y0, y1, EXT_VOXELS_PER_SIDE, z0);
            FillPlane(cuboids, material, x0, x1, 1, y0, y1, EXT_VOXELS_PER_SIDE, z1 - EXT_VOXELS_SQ);

            FillPlane(cuboids, material, x0, x1, 1, z0, z1, EXT_VOXELS_SQ, y0);
            FillPlane(cuboids, material, x0, x1, 1, z0, z1, EXT_VOXELS_SQ, y1 - EXT_VOXELS_PER_SIDE);

            FillPlane(cuboids, material, y0, y1, EXT_VOXELS_PER_SIDE, z0, z1, EXT_VOXELS_SQ, x0);
            FillPlane(cuboids, material, y0, y1, EXT_VOXELS_PER_SIDE, z0, z1, EXT_VOXELS_SQ, x1 - 1);
        }

        public static unsafe void FillPlane(VoxelInfo* ptr, int value, int fromX, int toX, int stepX, int fromY, int toY, int stepY, int z)
        {
            for (int x = fromX; x < toX; x += stepX)
            {
                for (int y = fromY; y < toY; y += stepY)
                {
                    ptr[x + y + z].Material = value;
                }
            }
        }


        static VoxelMaterial noMat = new VoxelMaterial();
        public static unsafe void GenCuboidMesh(ref GenFaceInfo genFaceInfo, ref GenPlaneInfo genPlaneInfo, int x0, int y0, int z0, int x1, int y1, int z1)
        {
            // offset by 1, due to extended size array (18x18x18)
            x0++;
            x1++;
            y0++;
            y1++;
            z0++;
            z1++;

            y0 *= EXT_VOXELS_PER_SIDE;
            y1 *= EXT_VOXELS_PER_SIDE;
            z0 *= EXT_VOXELS_SQ;
            z1 *= EXT_VOXELS_SQ;

            

            genFaceInfo.SetInfo(ConvertPlaneX, BlockFacing.indexWEST, genPlaneInfo.decorMaterials == null ? noMat : genPlaneInfo.decorMaterials[BlockFacing.indexWEST]);
            genPlaneInfo.SetCoords(z0, z1, EXT_VOXELS_SQ, y0, y1, EXT_VOXELS_PER_SIDE, x0, x0 - 1);
            genPlaneInfo.GenPlaneMesh(ref genFaceInfo);

            genFaceInfo.SetInfo(ConvertPlaneX, BlockFacing.indexEAST, genPlaneInfo.decorMaterials == null ? noMat : genPlaneInfo.decorMaterials[BlockFacing.indexEAST]);
            genPlaneInfo.SetCoords(z0, z1, EXT_VOXELS_SQ, y0, y1, EXT_VOXELS_PER_SIDE, x1 - 1, x1);
            genPlaneInfo.GenPlaneMesh(ref genFaceInfo);

            genFaceInfo.SetInfo(ConvertPlaneY, BlockFacing.indexDOWN, genPlaneInfo.decorMaterials == null ? noMat : genPlaneInfo.decorMaterials[BlockFacing.indexDOWN]);
            genPlaneInfo.SetCoords(x0, x1, 1, z0, z1, EXT_VOXELS_SQ, y0, y0 - EXT_VOXELS_PER_SIDE);
            genPlaneInfo.GenPlaneMesh(ref genFaceInfo);

            genFaceInfo.SetInfo(ConvertPlaneY, BlockFacing.indexUP, genPlaneInfo.decorMaterials == null ? noMat : genPlaneInfo.decorMaterials[BlockFacing.indexUP]);
            genPlaneInfo.SetCoords(x0, x1, 1, z0, z1, EXT_VOXELS_SQ, y1 - EXT_VOXELS_PER_SIDE, y1);
            genPlaneInfo.GenPlaneMesh(ref genFaceInfo);

            genFaceInfo.SetInfo(ConvertPlaneZ, BlockFacing.indexNORTH, genPlaneInfo.decorMaterials == null ? noMat : genPlaneInfo.decorMaterials[BlockFacing.indexNORTH]);
            genPlaneInfo.SetCoords(x0, x1, 1, y0, y1, EXT_VOXELS_PER_SIDE, z0, z0 - EXT_VOXELS_SQ);
            genPlaneInfo.GenPlaneMesh(ref genFaceInfo);

            genFaceInfo.SetInfo(ConvertPlaneZ, BlockFacing.indexSOUTH, genPlaneInfo.decorMaterials == null ? noMat : genPlaneInfo.decorMaterials[BlockFacing.indexSOUTH]);
            genPlaneInfo.SetCoords(x0, x1, 1, y0, y1, EXT_VOXELS_PER_SIDE, z1 - EXT_VOXELS_SQ, z1);
            genPlaneInfo.GenPlaneMesh(ref genFaceInfo);
        }

        public static void FromUint(uint val, out int x0, out int y0, out int z0, out int x1, out int y1, out int z1, out int material)
        {
            x0 = (int)(val & 0xF);
            y0 = (int)((val >> 4) & 0xF);
            z0 = (int)((val >> 8) & 0xF);
            x1 = (int)(((val >> 12) & 0xF) + 1);
            y1 = (int)(((val >> 16) & 0xF) + 1);
            z1 = (int)(((val >> 20) & 0xF) + 1);
            material = (int)((val >> 24) & 0xFu);
        }

        private static bool isMergableMaterial(int selfMat, int otherMat, RefList<VoxelMaterial> materials)
        {
            if (selfMat == otherMat) return true;
            if (otherMat >= 0)
            {
                if (materials[selfMat].BlockId == materials[otherMat].BlockId) return true;

                bool selfOpaque = true;
                switch (materials[selfMat].RenderPass)
                {
                    case EnumChunkRenderPass.Liquid:
                    case EnumChunkRenderPass.OpaqueNoCull:
                    case EnumChunkRenderPass.BlendNoCull:
                        return false;
                    case EnumChunkRenderPass.Meta:
                    case EnumChunkRenderPass.TopSoil:
                    case EnumChunkRenderPass.Transparent:
                        selfOpaque = false;
                        break;
                }
                bool otherOpaque = true;
                switch (materials[otherMat].RenderPass)
                {
                    case EnumChunkRenderPass.Meta:
                    case EnumChunkRenderPass.TopSoil:
                    case EnumChunkRenderPass.Transparent:
                    case EnumChunkRenderPass.BlendNoCull:
                        otherOpaque = false;
                        break;
                }
                if (selfOpaque & otherOpaque) return true;
                if (selfOpaque) return false;
                return otherOpaque | materials[selfMat].CullBetweenTransparents;
            }
            return false;
        }

        private static void ConvertPlaneXImpl(int width, int height, out float sx, out float sy, out float sz)
        {
            const float V2F = 1f / 32f;
            sx = V2F;
            sy = height * V2F;
            sz = width * V2F;
        }

        private static void ConvertPlaneYImpl(int width, int height, out float sx, out float sy, out float sz)
        {
            const float V2F = 1f / 32f;
            sx = width * V2F;
            sy = V2F;
            sz = height * V2F;
        }

        private static void ConvertPlaneZImpl(int width, int height, out float sx, out float sy, out float sz)
        {
            const float V2F = 1f / 32f;
            sx = width * V2F;
            sy = height * V2F;
            sz = V2F;
        }

        [StructLayout(LayoutKind.Sequential, Size = 8)]
        public struct VoxelInfo
        {
            public int Material;
            public ushort MainIndex;
            public byte Size;
            public bool CullFace;
        }

        public static void UpdateNeighbors(BlockEntityMicroBlock bm)
        {
            if (bm.Api == null || bm.Api.Side != EnumAppSide.Client) return;

            var pos = bm.Pos;
            var world = bm.Api.World;
            foreach (var face in BlockFacing.ALLFACES)
            {
                UpdateNeighbour(world, pos, face);
            }
        }

        private static void UpdateNeighbour(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (!(world.BlockAccessor.GetBlockEntity(pos.AddCopy(face)) is BlockEntityMicroBlock be))
            {
                return;
            }

            if (be.BlockIds == null || be.VoxelCuboids == null) return;
            be.MarkMeshDirty();
            be.MarkDirty(true);
        }


        public unsafe ref struct GenFaceInfo
        {
            // Args
            public ICoreClientAPI capi;
            public MeshData targetMesh;
            public SizeConverter converter;
            public int* originalBounds;
            public int posPacked;
            public int width;
            public int length;
            public int face;
            public BlockFacing facing;

            public float subPixelPaddingx;
            public float subPixelPaddingy;
            public int flags;

            public float texWidth;
            public float texHeight;
            public TextureAtlasPosition tpos;
            public TextureAtlasPosition topsoiltpos;
            public TextureAtlasPosition decortpos;
            VoxelMaterial decorMat;

            // Locals
            public fixed int xyz[3];
            public float posX, posY, posZ;
            public float centerX, centerY, centerZ;
            public float halfSizeX, halfSizeY, halfSizeZ;

            public fixed float uScaleByAxis[3];
            public fixed float vScaleByAxis[3];
            public fixed float uOffsetByAxis[3];
            public fixed float vOffsetByAxis[3];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetInfo(SizeConverter converter, int face, VoxelMaterial decorMat)
            {
                this.decorMat = decorMat;
                this.face = face;
                this.facing = BlockFacing.ALLFACES[face];
                this.converter = converter;
            }

            public void GenFace(in VoxelMaterial blockMat)
            {
                xyz[2] = posPacked / EXT_VOXELS_SQ;
                posPacked -= xyz[2] * EXT_VOXELS_SQ;

                xyz[1] = posPacked / EXT_VOXELS_PER_SIDE;
                posPacked -= xyz[1] * EXT_VOXELS_PER_SIDE;

                xyz[2]--;
                xyz[1]--;
                xyz[0] = posPacked - 1;

                converter(width, length, out halfSizeX, out halfSizeY, out halfSizeZ);

                const float V2F = 1f / 16f;
                posX = xyz[0] * V2F;
                posY = xyz[1] * V2F;
                posZ = xyz[2] * V2F;
                centerX = posX + halfSizeX;
                centerY = posY + halfSizeY;
                centerZ = posZ + halfSizeZ;
                
                InitMaterial(blockMat, decorMat);
                InitUV();

                int start = (targetMesh.IndicesCount > 0) ? (targetMesh.Indices[targetMesh.IndicesCount - 1] + 1) : 0;
                int vertOffset = face * 4;
                for (int i = 0; i < 4; i++)
                {
                    int ind = vertOffset + i;
                    if (targetMesh.VerticesCount >= targetMesh.VerticesMax)
                    {
                        targetMesh.GrowVertexBuffer();
                    }
                    int index = targetMesh.XyzCount;
                    targetMesh.xyz[index] = CubeMeshUtil.CubeVertices[ind * 3] * halfSizeX + centerX;
                    targetMesh.xyz[index + 1] = CubeMeshUtil.CubeVertices[ind * 3 + 1] * halfSizeY + centerY;
                    targetMesh.xyz[index + 2] = CubeMeshUtil.CubeVertices[ind * 3 + 2] * halfSizeZ + centerZ;

                    GetScaledUV(ind * 2, out float u, out float v);
                    index = targetMesh.UvCount;
                    targetMesh.Uv[index] = tpos.x1 + u * texWidth - subPixelPaddingx;
                    targetMesh.Uv[index + 1] = tpos.y1 + v * texHeight - subPixelPaddingy;

                    targetMesh.Flags[targetMesh.VerticesCount++] = flags;

                    if (targetMesh.CustomFloats != null)
                    {
                        if (topsoiltpos == null) targetMesh.CustomFloats.Add(0, 0);
                        else targetMesh.CustomFloats.Add(topsoiltpos.x1 + u * texWidth - subPixelPaddingx, topsoiltpos.y1 + v * texHeight - subPixelPaddingy);
                    }
                }

                int faceOffset = face * 6;
                for (int i = 0; i < 6; i++)
                {
                    targetMesh.AddIndex(start + CubeMeshUtil.CubeVertexIndices[faceOffset + i] - vertOffset);
                }
                targetMesh.AddXyzFace(facing.MeshDataIndex);
                targetMesh.AddTextureId(tpos.atlasTextureId);
                targetMesh.AddColorMapIndex(blockMat.ClimateMapIndex, blockMat.SeasonMapIndex);
                targetMesh.AddRenderPass((short)blockMat.RenderPass);
                

                if (decortpos != null)
                {
                    start = (targetMesh.IndicesCount > 0) ? (targetMesh.Indices[targetMesh.IndicesCount - 1] + 1) : 0;
                    vertOffset = face * 4;
                    texWidth = decortpos.x2 - decortpos.x1;
                    texHeight = decortpos.y2 - decortpos.y1;

                    for (int i = 0; i < 4; i++)
                    {
                        int ind = vertOffset + i;
                        if (targetMesh.VerticesCount >= targetMesh.VerticesMax)
                        {
                            targetMesh.GrowVertexBuffer();
                        }
                        int index = targetMesh.XyzCount;

                        float xg = 1 + Math.Abs(BlockFacing.ALLNORMALI[face].X * 0.01f);
                        float yg = 1 + Math.Abs(BlockFacing.ALLNORMALI[face].Y * 0.01f);
                        float zg = 1 + Math.Abs(BlockFacing.ALLNORMALI[face].Z * 0.01f);

                        targetMesh.xyz[index] = CubeMeshUtil.CubeVertices[ind * 3] * halfSizeX * xg + centerX;
                        targetMesh.xyz[index + 1] = CubeMeshUtil.CubeVertices[ind * 3 + 1] * halfSizeY * yg + centerY;
                        targetMesh.xyz[index + 2] = CubeMeshUtil.CubeVertices[ind * 3 + 2] * halfSizeZ * zg + centerZ;

                        GetScaledUV(ind * 2, out float u, out float v);
                        index = targetMesh.UvCount;
                        targetMesh.Uv[index] = decortpos.x1 + u * texWidth - subPixelPaddingx;
                        targetMesh.Uv[index + 1] = decortpos.y1 + v * texHeight - subPixelPaddingy;

                        targetMesh.Flags[targetMesh.VerticesCount++] = flags;
                    }

                    if (targetMesh.CustomFloats != null)
                    {
                        targetMesh.CustomFloats.Add(0, 0, 0, 0, 0, 0, 0, 0);
                    }

                    faceOffset = face * 6;
                    for (int i = 0; i < 6; i++)
                    {
                        targetMesh.AddIndex(start + CubeMeshUtil.CubeVertexIndices[faceOffset + i] - vertOffset);
                    }
                    targetMesh.AddXyzFace(facing.MeshDataIndex);
                    targetMesh.AddTextureId(decortpos.atlasTextureId);
                    targetMesh.AddColorMapIndex(decorMat.ClimateMapIndex, decorMat.SeasonMapIndex);
                    targetMesh.AddRenderPass((short)decorMat.RenderPass);
                }

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void InitMaterial(in VoxelMaterial material, in VoxelMaterial decorMat)
            {
                bool isOutside = originalBounds[face] == xyz[(int)facing.Axis] + shiftOffsetByFace[face];
                tpos = isOutside ? material.Texture[face] : material.TextureInside[face];
                topsoiltpos = material.TextureTopSoil;

                decortpos = decorMat.Texture?[face] ?? null;

                texWidth = tpos.x2 - tpos.x1;
                texHeight = tpos.y2 - tpos.y1;
            }

            [MethodImpl]
            private void InitUV()
            {
                uScaleByAxis[0] = halfSizeZ;
                uScaleByAxis[1] = halfSizeX;
                uScaleByAxis[2] = halfSizeX;

                vScaleByAxis[0] = halfSizeY;
                vScaleByAxis[1] = halfSizeZ;
                vScaleByAxis[2] = halfSizeY;

                uOffsetByAxis[0] = posZ;
                uOffsetByAxis[1] = posX;
                uOffsetByAxis[2] = posX;

                vOffsetByAxis[0] = posY;
                vOffsetByAxis[1] = posZ;
                vOffsetByAxis[2] = posY;
            }

            private void GetScaledUV(int fIndex, out float u, out float v)
            {
                int axis = (int)facing.Axis;
                switch (facing.Index)
                {
                    case 0:
                        u = CubeMeshUtil.CubeUvCoords[fIndex] * 2f * uScaleByAxis[axis] + (1f - 2f * uScaleByAxis[axis]) - uOffsetByAxis[axis];
                        v = (1f - CubeMeshUtil.CubeUvCoords[fIndex + 1]) * 2f * vScaleByAxis[axis] + (1f - 2f * vScaleByAxis[axis]) - vOffsetByAxis[axis];
                        break;
                    case 1:
                        u = CubeMeshUtil.CubeUvCoords[fIndex] * 2f * uScaleByAxis[axis] + (1f - 2f * uScaleByAxis[axis]) - uOffsetByAxis[axis];
                        v = (1f - CubeMeshUtil.CubeUvCoords[fIndex + 1]) * 2f * vScaleByAxis[axis] + (1f - 2f * vScaleByAxis[axis]) - vOffsetByAxis[axis];
                        break;
                    case 2:
                        u = CubeMeshUtil.CubeUvCoords[fIndex] * 2f * uScaleByAxis[axis] + uOffsetByAxis[axis];
                        v = (1f - CubeMeshUtil.CubeUvCoords[fIndex + 1]) * 2f * vScaleByAxis[axis] + (1f - 2f * vScaleByAxis[axis]) - vOffsetByAxis[axis];
                        break;
                    case 3:
                        u = CubeMeshUtil.CubeUvCoords[fIndex] * 2f * uScaleByAxis[axis] + uOffsetByAxis[axis];
                        v = (1f - CubeMeshUtil.CubeUvCoords[fIndex + 1]) * 2f * vScaleByAxis[axis] + (1f - 2f * vScaleByAxis[axis]) - vOffsetByAxis[axis];
                        break;
                    case 4:
                        u = (1f - CubeMeshUtil.CubeUvCoords[fIndex]) * 2f * uScaleByAxis[axis] + (1f - 2f * uScaleByAxis[axis]) - uOffsetByAxis[axis];
                        v = CubeMeshUtil.CubeUvCoords[fIndex + 1] * 2f * vScaleByAxis[axis] + (1f - 2f * vScaleByAxis[axis]) - vOffsetByAxis[axis];
                        break;
                    case 5:
                        u = CubeMeshUtil.CubeUvCoords[fIndex] * 2f * uScaleByAxis[axis] + (1f - 2f * uScaleByAxis[axis]) - uOffsetByAxis[axis];
                        v = (1f - CubeMeshUtil.CubeUvCoords[fIndex + 1]) * 2f * vScaleByAxis[axis] + vOffsetByAxis[axis];
                        break;
                    default: throw new Exception();
                }
            }
        }

        /// <summary>
        /// Is coordinate agnostic, can iterate over any of the 6 planes
        /// </summary>
        public unsafe ref struct GenPlaneInfo
        {
            public RefList<VoxelMaterial> blockMaterials;
            public RefList<VoxelMaterial> decorMaterials;

            public VoxelInfo* voxels;
            public int materialIndex;
            
            // Premultiplied values
            public int fromA;
            public int toA;
            public int fromB;
            public int toB;
            public int c;

            public int stepA;
            public int stepB;
            public int faceOffsetZ;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetCoords(int fromA, int toA, int stepA, int fromB, int toY, int stepY, int c, int faceOffsetZ)
            {
                this.fromA = fromA;
                this.toA = toA;
                this.stepA = stepA;
                this.fromB = fromB;
                this.toB = toY;
                this.stepB = stepY;
                this.c = c;
                this.faceOffsetZ = faceOffsetZ;
            }

            public void GenPlaneMesh(ref GenFaceInfo faceGenInfo)
            {
                int a, b, sizeB, index;
                bool mergable;

                // Grow mergable face in A dimension
                for (a = fromA; a < toA; a += stepA)
                {
                    sizeB = 1;
                    // We are using premultiplied values so no need for (x * size + y) * size + z
                    index = a + fromB + c; 

                    mergable = isMergableMaterial(materialIndex, voxels[a + fromB + faceOffsetZ].Material, blockMaterials);
                    for (b = fromB + stepB; b < toB; b += stepB)
                    {
                        if (isMergableMaterial(materialIndex, voxels[a + b + faceOffsetZ].Material, blockMaterials) == mergable)
                        {
                            sizeB++;
                            voxels[a + b + c].MainIndex = (ushort)index;
                        }
                        else
                        {
                            voxels[index].Size = (byte)sizeB;
                            voxels[index].CullFace = mergable;
                            voxels[index].MainIndex = (ushort)index;
                            sizeB = 1;
                            index = a + b + c;
                            mergable = !mergable;
                        }
                    }

                    voxels[index].Size = (byte)sizeB;
                    voxels[index].CullFace = mergable;
                    // MainIndex points to the first voxel of the same material of a given 1d segment inside a plane
                    voxels[index].MainIndex = (ushort)index;
                }

                ref readonly var blockMat = ref blockMaterials[materialIndex];
                faceGenInfo.flags = faceGenInfo.facing.NormalPackedFlags | blockMat.Flags;

                // Grow mergable face in B dimension
                for (b = fromB; b < toB; b += stepB)
                {
                    sizeB = 1;
                    index = fromA + b + c;
                    ref var curVoxel = ref voxels[index];
                    int sizeA = curVoxel.Size;
                    int prevIndex = index;
                    mergable = curVoxel.CullFace;
                    bool skip = curVoxel.MainIndex != index;

                    for (a = fromA + stepA; a < toA; a += stepA)
                    {
                        index = a + b + c;
                        curVoxel = ref voxels[index];
                        if (curVoxel.MainIndex != index)
                        {
                            if (skip) continue;
                            skip = true;
                        }
                        else
                        {
                            if (skip)
                            {
                                skip = false;
                                sizeB = 1;
                                prevIndex = index;
                                sizeA = curVoxel.Size;
                                mergable = curVoxel.CullFace;
                                continue;
                            }
                            else if (mergable == curVoxel.CullFace && sizeA == curVoxel.Size)
                            {
                                sizeB++;
                                continue;
                            }
                        }

                        if (!mergable)
                        {
                            faceGenInfo.posPacked = prevIndex;
                            faceGenInfo.width = sizeB;
                            faceGenInfo.length = sizeA;
                            faceGenInfo.GenFace(blockMat);
                        }
                        sizeB = 1;
                        prevIndex = index;
                        sizeA = curVoxel.Size;
                        mergable = curVoxel.CullFace;
                    }

                    if (!mergable && !skip)
                    {
                        faceGenInfo.posPacked = prevIndex;
                        faceGenInfo.width = sizeB;
                        faceGenInfo.length = sizeA;
                        faceGenInfo.GenFace(blockMat);
                    }
                }
            }
        }

        public delegate void SizeConverter(int width, int height, out float sx, out float sy, out float sz);

        #endregion

        #region Worldgen

        public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid, Block layerBlock, bool resolveImports)
        {
            base.OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);

            if (replaceBlocks != null)
            {
                if (BlockName != null && BlockName.Length > 0)
                {
                    string oldMajorityName = GetPlacedBlockName(api, VoxelCuboids, BlockIds, null);
                    if (oldMajorityName == BlockName) BlockName = null;    // Clear the old BlockName if auto-generated, so that it gets regenerated because the result of auto-generation may change after materials are replaced
                }

                Dictionary<int, int> replaceByBlock;
                for (int i = 0; i < BlockIds.Length; i++)
                {
                    if (replaceBlocks.TryGetValue(BlockIds[i], out replaceByBlock))
                    {
                        int newBlockId;
                        if (replaceByBlock.TryGetValue(centerrockblockid, out newBlockId))
                        {
                            BlockIds[i] = blockAccessor.GetBlock(newBlockId).Id;
                        }
                    }
                }
            }

            int newMatIndex = -1;
            int len = BlockIds.Length;
            for (int i = 0; i < len; i++)
            {
                if (BlockIds[i] == BlockMicroBlock.BlockLayerMetaBlockId)
                {
                    for (int j = 0; j < VoxelCuboids.Count; j++)
                    {
                        uint matindex = (VoxelCuboids[j] >> 24) & 0xff;
                        if (matindex == i)
                        {
                            if (layerBlock == null) { VoxelCuboids.RemoveAt(j); j--; }
                            else
                            {
                                if (newMatIndex < 0)
                                {
                                    BlockIds = BlockIds.Append(layerBlock.Id);
                                    newMatIndex = BlockIds.Length - 1;
                                }

                                VoxelCuboids[j] = (VoxelCuboids[j] & clearMaterialMask) | ((uint)newMatIndex << 24);
                            } 
                        }
                    }
                }
            }
        }
        const int clearMaterialMask = ~(0xff << 24);

        #endregion

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Mesh == null)
            {
                Mesh = GenMesh();
            }

            base.OnTesselation(mesher, tesselator);
            if (withColorMapData)
            {
                var cmapdata = (Api as ICoreClientAPI).World.GetColorMapData(Api.World.Blocks[BlockIds[0]], Pos.X, Pos.Y, Pos.Z);
                mesher.AddMeshData(Mesh, cmapdata);
            }
            else
            {
                mesher.AddMeshData(Mesh);
            }
            
            Block = Api.World.BlockAccessor.GetBlock(Pos);

            return true;
        }

        public void SetDecor(Block blockToPlace, BlockPos pos, BlockFacing face)
        {
            if (DecorIds == null) DecorIds = new int[6];

            int rotfaceindex = face.IsVertical ? face.Index : BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(face.HorizontalAngleIndex + rotationY / 90, 4)].Index;

            DecorIds[rotfaceindex] = blockToPlace.Id;
            MarkDirty(true);
        }

        public bool ExchangeWith(ItemSlot fromSlot, ItemSlot toSlot)
        {
            var fromBlock = fromSlot.Itemstack?.Block;
            var toBlock = toSlot.Itemstack?.Block;
            if (fromBlock == null || toBlock == null) return false;

            bool exchanged = false;

            for (int i = 0; i < BlockIds.Length; i++) 
            {
                if (BlockIds[i] == fromBlock.Id)
                {
                    BlockIds[i] = toBlock.Id;
                    exchanged = true;
                }
            }

            RegenSelectionBoxes(Api.World, null);
            MarkDirty(true, null);

            return exchanged;
        }
    }
}
