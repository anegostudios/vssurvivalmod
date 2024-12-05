using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

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
                    Block block = api.World.BlockAccessor.GetBlockBelow(pos, groundOffset);
                    if (block.VertexFlags.WindMode == EnumWindBitMode.NoWind && block.SideSolid[BlockFacing.UP.Index])
                    {
                        break;
                    }
                }
            }

            decalMesh.ToggleWindModeSetWindData(sideDisableWindwave, enableWind, groundOffset);
        }


        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (VertexFlags.WindMode == EnumWindBitMode.NoWind) return;

            bool enableWind = (lightRgbsByCorner[24] >> 24 & 0xff) >= 159;  // Corresponds with a sunlight level of less than 14
            int groundOffset = 1;
            int sideDisableWindshear = 0;  // Any bit set to 1 means no height-based wind shear on that tileSide (because adjoining solid block)


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
                        block = api.World.BlockAccessor.GetBlockBelow(pos, groundOffset);
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




    }
}
