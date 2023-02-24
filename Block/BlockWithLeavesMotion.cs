using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Common code for several blocks with leaves wind motion, including Leaves (!), Bamboo Leaves, Vines
    /// </summary>
    public class BlockWithLeavesMotion : Block
    {
        public override void OnDecalTesselation(IWorldAccessor world, MeshData decalMesh, BlockPos pos)
        {
            if (VertexFlags.WindMode == EnumWindBitMode.NoWind) return;

            // any bit set to 1 means no Wave on that tileSide
            int sideDisableWindwave = 0;  
            int groundOffset = 0;

            bool enableWind = api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlySunLight) >= 14;

            if (enableWind)
            {
                // Disable motion on any side touching a solid block
                for (int tileSide = 0; tileSide < BlockFacing.NumberOfFaces; tileSide++)
                {
                    BlockFacing facing = BlockFacing.ALLFACES[tileSide];
                    Block nblock = world.BlockAccessor.GetBlock(pos.AddCopy(facing));

                    if (nblock.BlockMaterial != EnumBlockMaterial.Leaves && nblock.SideSolid[BlockFacing.ALLFACES[tileSide].Opposite.Index])
                    {
                        sideDisableWindwave |= (1 << tileSide);
                    }
                }


                groundOffset = 1;
                for (; groundOffset < 8; groundOffset++)
                {
                    Block block = api.World.BlockAccessor.GetBlock(pos.X, pos.Y - groundOffset, pos.Z);
                    if (block.VertexFlags.WindMode == EnumWindBitMode.NoWind && block.SideSolid[BlockFacing.UP.Index])
                    {
                        break;
                    }
                }
            }

            // Tyron 27.11.22: WTF does this code do. origFlags wasn't even assigned here
            /*int[] origFlags;
            if (!windModeByFlagCount.TryGetValue(decalMesh.FlagsCount, out origFlags))
            {
                var flags = windModeByFlagCount[decalMesh.FlagsCount] = new int[decalMesh.FlagsCount];
                for (int i = 0; i < flags.Length; i++) flags[i] = decalMesh.Flags[i] & VertexFlags.WindModeBitsMask;
            }*/

            decalMesh.ToggleWindModeSetWindData(sideDisableWindwave, enableWind, groundOffset);
        }


        //Dictionary<int, int[]> windModeByFlagCount = new Dictionary<int, int[]>();
        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (VertexFlags.WindMode == EnumWindBitMode.NoWind) return;

            // Tyron 27.11.22: WTF does this code do. origFlags wasn't even assigned here
            /*int[] origFlags;
            if (!windModeByFlagCount.TryGetValue(sourceMesh.FlagsCount, out origFlags))
            {
                var flags = windModeByFlagCount[sourceMesh.FlagsCount] = new int[sourceMesh.FlagsCount];
                for (int i = 0; i < flags.Length; i++) flags[i] = sourceMesh.Flags[i] & VertexFlags.WindModeBitsMask;
            }*/

            bool enableWind = (byte)(lightRgbsByCorner[24] >> 24) >= 159;  //corresponds with a sunlight level of less than 14
            int groundOffset = 1;
            int sideDisableWindshear = 0;  //any bit set to 1 means no height-based wind shear on that tileSide (because adjoining solid block)


            if (enableWind)
            {
                for (int tileSide = 0; tileSide < TileSideEnum.SideCount; tileSide++)
                {
                    Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[tileSide]];
                    if (nblock.BlockMaterial != EnumBlockMaterial.Leaves && nblock.SideSolid[TileSideEnum.GetOpposite(tileSide)]) sideDisableWindshear |= (1 << tileSide);
                }

                int downMoveIndex = TileSideEnum.MoveIndex[TileSideEnum.Down];
                int movedIndex3d = extIndex3d + downMoveIndex;  // add downMoveIndex because groundOffset now starts at 1 (no point checking this block itself!)
                Block block;
                for (; groundOffset < 8; groundOffset++)
                {
                    if (movedIndex3d >= 0)
                    {
                        block = chunkExtBlocks[movedIndex3d];
                    }
                    else
                    {
                        block = api.World.BlockAccessor.GetBlock(pos.X, pos.Y - groundOffset, pos.Z);
                    }

                    if (block.VertexFlags.WindMode == EnumWindBitMode.NoWind && block.SideSolid[TileSideEnum.Up])
                    {
                        break;
                    }

                    movedIndex3d += downMoveIndex;
                }
            }

            sourceMesh.ToggleWindModeSetWindData(sideDisableWindshear, enableWind, groundOffset);
        }


        public override int OnInstancedTesselation(int light, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d, out int sideDisableWindwave)
        {
            sideDisableWindwave = 0;  //any bit set to 1 means no Wave on that tileSide
            if (VertexFlags.WindMode == EnumWindBitMode.NoWind) return 0;

            bool enableWind = (byte)(light >> 24) >= 159;  //corresponds with a sunlight level of less than 14
            int groundOffset = 1;

            if (enableWind)
            {
                for (int tileSide = 0; tileSide < TileSideEnum.SideCount; tileSide++)
                {
                    Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[tileSide]];
                    if (nblock.BlockMaterial != EnumBlockMaterial.Leaves && nblock.SideSolid[TileSideEnum.GetOpposite(tileSide)]) sideDisableWindwave |= (1 << tileSide);
                }

                int downMoveIndex = TileSideEnum.MoveIndex[TileSideEnum.Down];
                int movedIndex3d = extIndex3d + downMoveIndex;  // add downMoveIndex because groundOffset now starts at 1 (no point checking this block itself!)
                Block block;
                for (; groundOffset < 8; groundOffset++)
                {
                    if (movedIndex3d >= 0)
                    {
                        block = chunkExtBlocks[movedIndex3d];
                    }
                    else
                    {
                        block = api.World.BlockAccessor.GetBlock(pos.X, pos.Y - groundOffset, pos.Z);
                    }

                    if (block.VertexFlags.WindMode == EnumWindBitMode.NoWind && block.SideSolid[TileSideEnum.Up])
                    {
                        break;
                    }

                    movedIndex3d += downMoveIndex;
                }
            }
            else
            {
                sideDisableWindwave = 0x3f;
            }

            return groundOffset - 1;
        }


    }
}
