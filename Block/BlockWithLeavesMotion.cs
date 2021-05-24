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
            if (VertexFlags.LeavesWindWave)
            {
                int leavesNoWaveTileSide = 0;  //any bit set to 1 means no Wave on that tileSide

                //Disable motion on any side touching a solid block
                for (int tileSide = 0; tileSide < BlockFacing.NumberOfFaces; tileSide++)
                {
                    BlockFacing facing = BlockFacing.ALLFACES[tileSide];
                    Block nblock = world.BlockAccessor.GetBlock(pos.AddCopy(facing));

                    if (nblock.BlockMaterial != EnumBlockMaterial.Leaves && nblock.SideSolid[BlockFacing.ALLFACES[tileSide].Opposite.Index]) leavesNoWaveTileSide |= (1 << tileSide);
                }

                int groundOffset = 0;

                bool waveoff = api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlySunLight) < 14;

                //int yrain = api.World.BlockAccessor.GetRainMapHeightAt(pos);

                // This is too expensive. We need more efficient system for this
                // Small optimization: Don't need to do a room search when exposed to the rain map
                /*if (pos.Y == yrain || (pos.Y + 1 == yrain && api.World.BlockAccessor.GetBlock(pos.UpCopy()).BlockMaterial == EnumBlockMaterial.Leaves))
                {
                    waveoff = false;
                }
                else
                {

                    if (roomreg != null)
                    {
                        Room room = roomreg.GetRoomForPosition(pos);

                        waveoff = ((float)room.SkylightCount / room.NonSkylightCount) < 0.1f;
                    }
                }*/

                if (!waveoff)
                {
                    groundOffset = 1;
                    for (; groundOffset < 8; groundOffset++)
                    {
                        Block block = api.World.BlockAccessor.GetBlock(pos.X, pos.Y - groundOffset, pos.Z);
                        if (!block.VertexFlags.LeavesWindWave && block.SideSolid[BlockFacing.UP.Index])
                        {
                            break;
                        }
                    }
                }

                SetLeaveWaveFlags(decalMesh, leavesNoWaveTileSide, waveoff, VertexFlags.LeavesWindWaveBitMask, groundOffset);
            }
        }


        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (VertexFlags.LeavesWindWave)
            {
                int leavesNoWaveTileSide = 0;  //any bit set to 1 means no Wave on that tileSide
                for (int tileSide = 0; tileSide < TileSideEnum.SideCount; tileSide++)
                {
                    Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[tileSide]];
                    if (nblock.BlockMaterial != EnumBlockMaterial.Leaves && nblock.SideSolid[TileSideEnum.GetOpposite(tileSide)]) leavesNoWaveTileSide |= (1 << tileSide);
                    //  this piece of maths is equivalent to BlockFacing.ALLFACES[tileSide].Opposite.Index ^^^^^^
                }

                bool waveoff = (byte)(lightRgbsByCorner[24] >> 24) < 159;  //corresponds with a sunlight level of less than 14
                int groundOffset = 1;


                // This is too expensive. We need more efficient system for this


                /*int yrain = api.World.BlockAccessor.GetRainMapHeightAt(pos);

                // Small optimization: Don't need to do a room search when exposed to the rain map
                if (pos.Y == yrain || (pos.Y + 1 == yrain && api.World.Blocks[chunkExtIds[extIndex3d + TileSideEnum.MoveIndex[5]]].BlockMaterial == EnumBlockMaterial.Leaves))
                {
                    waveoff = false;
                }
                else
                {

                    if (roomreg != null)
                    {
                        Room room = roomreg.GetRoomForPosition(pos);

                        waveoff = ((float)room.SkylightCount / room.NonSkylightCount) < 0.1f;
                    }
                }*/

                if (!waveoff)
                {
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

                        if (!block.VertexFlags.LeavesWindWave && block.SideSolid[TileSideEnum.Up])
                        {
                            break;
                        }

                        movedIndex3d += downMoveIndex;
                    }
                }

                SetLeaveWaveFlags(sourceMesh, leavesNoWaveTileSide, waveoff, VertexFlags.LeavesWindWaveBitMask, groundOffset);
            }
        }


        public static void SetLeaveWaveFlags(MeshData sourceMesh, int leavesNoWaveTileSide, bool waveOff, int leaveWave, int groundOffsetTop)
        {
            int clearFlags = VertexFlags.clearWaveBits;
            int verticesCount = sourceMesh.VerticesCount;

            if (waveOff)
            {
                // shorter return path, and no need to test off in every iteration of the loop in the other code path
                for (int vertexNum = 0; vertexNum < verticesCount; vertexNum++)
                {
                    sourceMesh.Flags[vertexNum] &= clearFlags;
                }
                return;
            }

            for (int vertexNum = 0; vertexNum < verticesCount; vertexNum++)
            {
                int flag = sourceMesh.Flags[vertexNum] & clearFlags;

                if (leavesNoWaveTileSide == 0)
                {
                    // Simplified path if allowed to wave on all 6 sides
                    flag |= leaveWave | ( groundOffsetTop == 8 ? 7 : groundOffsetTop + ((int)(sourceMesh.xyz[vertexNum * 3 + 1] - 1.5f) >> 1) ) << 28;
                }
                else
                {
                    // Is there some pretty math formula for this? :<  - yes :)

                    // The calculation (int)(x - 1.5f) will be either -2, -1 or 0 - this works reliably unless a vertex is positioned greater than +40/16 or less than -24/16 in which case this code may produce surprising waves (but no leaf block vertex is anywhere close to these limits, even if rotated: a basic leaf model is the widest I know of, some vertices are at coordinates 24/16 or -8/16)
                    // The arithmetic right bit shift converts -2 to -1, while also preserving -1 -> -1 and leaving a value of 0 unchanged: a useful performance trick to avoid conditional jumps.

                    int x = (int)(sourceMesh.xyz[vertexNum * 3 + 0] - 1.5f) >> 1;
                    int y = (int)(sourceMesh.xyz[vertexNum * 3 + 1] - 1.5f) >> 1;
                    int z = (int)(sourceMesh.xyz[vertexNum * 3 + 2] - 1.5f) >> 1;
                    // The result is x == -1 for WEST side vertices, x == 0 for EAST side vertices.  etc...

                    int sidesToCheckMask = 1 << TileSideEnum.Up - y | 4 + z * 3 | 2 - x * 6;
                    // How this works:
                    //     high (+y) vertex -> y == 0 -> UP;  low vertex -> y == -1 -> DOWN
                    //     southerly (+z) vertex -> z == 0 -> SOUTH;  northerly vertex -> z == -1 -> NORTH
                    //     easterly (+x) vertex -> x == 0 -> EAST;  westerly vertex -> x == -1 -> WEST

                    if ((leavesNoWaveTileSide & sidesToCheckMask) == 0)
                    {
                        flag |= leaveWave | (groundOffsetTop == 8 ? 7 : groundOffsetTop + y) << 28;
                    }
                }

                sourceMesh.Flags[vertexNum] = flag;
            }
        }

    }
}
