using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockBamboo : Block, ITreeGenerator
    {
        Block greenSeg1;
        Block greenSeg2;
        Block greenSeg3;

        Block brownSeg1;
        Block brownSeg2;
        Block brownSeg3;

        Block brownLeaves;
        Block greenLeaves;

        static Random rand = new Random();
        private bool isSegmentWithLeaves;

        Block greenShootBlock;
        Block brownShootBlock;

        public override void OnLoaded(ICoreAPI api)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
            if (sapi != null)
            {
                if (Code.Path.Equals("bamboo-grown-green-segment1"))
                {
                    sapi.RegisterTreeGenerator(new AssetLocation("bamboo-grown-green"), this);
                }
                if (Code.Path.Equals("bamboo-grown-brown-segment1"))
                {
                    sapi.RegisterTreeGenerator(new AssetLocation("bamboo-grown-brown"), this);
                }
            }

            if (greenSeg1 == null)
            {
                IBlockAccessor blockAccess = api.World.BlockAccessor;

                greenSeg1 = blockAccess.GetBlock(new AssetLocation("bamboo-grown-green-segment1"));
                greenSeg2 = blockAccess.GetBlock(new AssetLocation("bamboo-grown-green-segment2"));
                greenSeg3 = blockAccess.GetBlock(new AssetLocation("bamboo-grown-green-segment3"));

                brownSeg1 = blockAccess.GetBlock(new AssetLocation("bamboo-grown-brown-segment1"));
                brownSeg2 = blockAccess.GetBlock(new AssetLocation("bamboo-grown-brown-segment2"));
                brownSeg3 = blockAccess.GetBlock(new AssetLocation("bamboo-grown-brown-segment3"));

                brownLeaves = blockAccess.GetBlock(new AssetLocation("bambooleaves-brown-grown"));
                greenLeaves = blockAccess.GetBlock(new AssetLocation("bambooleaves-green-grown"));

                greenShootBlock = blockAccess.GetBlock(new AssetLocation("sapling-greenbambooshoots-free"));
                brownShootBlock = blockAccess.GetBlock(new AssetLocation("sapling-brownbambooshoots-free"));
            }

            if (this.RandomDrawOffset > 0)
            {
                JsonObject overrider = Attributes?["overrideRandomDrawOffset"];
                if (overrider?.Exists == true) this.RandomDrawOffset = overrider.AsInt(1);
            }

            isSegmentWithLeaves = LastCodePart() == "segment2" || LastCodePart() == "segment3";
        }


        public string Type()
        {
            return LastCodePart(1);
        }

        public Block NextSegment(IBlockAccessor blockAccess)
        {
            
            string part = LastCodePart();

            return Type() == "green" ?
                (part == "segment1" ? greenSeg2 : (part == "segment2" ? greenSeg3 : null)) :
                (part == "segment1" ? brownSeg2 : (part == "segment2" ? brownSeg3 : null))
            ;
        }


        public void GrowTree(IBlockAccessor blockAccessor, BlockPos pos, float sizeModifier = 1, float vineGrowthChance = 0, float forestDensity = 0)
        {
            int quantity = (2 + (int)((1 + rand.NextDouble() * 4) * (1 - forestDensity) * (1 - forestDensity))) * 3 * 3;

            BlockPos npos = pos.Copy();

            sizeModifier = GameMath.Mix(sizeModifier, 1, 0.5f);

            sizeModifier *= 1 + ((float)rand.NextDouble() * 0.5f);

            while (quantity-- > 0)
            {
                float dist = Math.Max(1, pos.DistanceTo(npos) - 2);

                GrowStalk(blockAccessor, npos.UpCopy(), dist, sizeModifier, vineGrowthChance);

                // Potentially grow another one nearby
                npos.Set(pos);
                npos.X += rand.Next(8) - 4;
                npos.Z += rand.Next(8) - 4;

                // Test up to 2 blocks up and down.
                bool foundSuitableBlock = false;
                for (int y = 2; y >= -2; y--)
                {
                    Block block = blockAccessor.GetBlock(npos.X, npos.Y + y, npos.Z);
                    if (block.Fertility > 0)
                    {
                        npos.Y = npos.Y + y;
                        foundSuitableBlock = true;
                        break;
                    }
                }
                if (!foundSuitableBlock) break;
            }
        }

        private void GrowStalk(IBlockAccessor blockAccessor, BlockPos upos, float centerDist, float sizeModifier, float vineGrowthChance)
        {
            Block block = this;
            float heightf = (8 + rand.Next(5)) * sizeModifier;
            heightf = Math.Max(1f, heightf - centerDist);

            int height = (int)heightf;
            int nextSegmentAtHeight = height / 3;

            BlockPos npos = upos.Copy();

            // Bamboo shoots nearby
            foreach (BlockFacing face in BlockFacing.HORIZONTALS)
            {
                if (rand.NextDouble() > 0.75)
                {
                    BlockPos bpos = npos.Set(upos).Add(face);
                    Block shootBlock = block == greenSeg3 ? greenShootBlock : brownShootBlock;

                    var nblock = blockAccessor.GetBlock(bpos);

                    if (nblock.Replaceable >= shootBlock.Replaceable && !nblock.IsLiquid() && blockAccessor.GetBlock(bpos.X, bpos.Y - 1, bpos.Z).Fertility > 0)
                    {
                        blockAccessor.SetBlock(shootBlock.BlockId, bpos);
                    }
                }
            }
            
            if (height < 4)
            {
                block = ((BlockBamboo)block).NextSegment(blockAccessor);
            }

            for (int i = 0; i < height; i++)
            {
                if (!blockAccessor.GetBlock(upos).IsReplacableBy(block)) break;

                blockAccessor.SetBlock(block.BlockId, upos);

                if (nextSegmentAtHeight <= i)
                {
                    block = ((BlockBamboo)block).NextSegment(blockAccessor);
                    nextSegmentAtHeight += height / 3;
                }

                if (block == null) break;

                if (block == greenSeg3 || block == brownSeg3)
                {
                    // segment 3 can generate leaves
                    Block blockLeaves = block == greenSeg3 ? greenLeaves : brownLeaves;

                    foreach (BlockFacing facing in BlockFacing.ALLFACES)
                    {
                        if (facing == BlockFacing.DOWN) continue;
                        float chanceFac = facing == BlockFacing.UP ? 0 : 0.25f;

                        if (rand.NextDouble() > chanceFac)
                        {
                            npos.Set(upos.X + facing.Normali.X, upos.Y + facing.Normali.Y, upos.Z + facing.Normali.Z);

                            if (rand.NextDouble() > 0.33)
                            {
                                BlockPos bpos = npos.DownCopy();

                                if (blockAccessor.GetBlock(bpos).Replaceable >= blockLeaves.Replaceable)
                                {
                                    blockAccessor.SetBlock(blockLeaves.BlockId, bpos);
                                }
                            }

                            if (blockAccessor.GetBlock(npos).Replaceable >= blockLeaves.Replaceable)
                            {
                                blockAccessor.SetBlock(blockLeaves.BlockId, npos);
                            }
                            else continue;

                            // if there's a leaf expand it
                            foreach (BlockFacing facing2 in BlockFacing.ALLFACES)
                            {
                                if (rand.NextDouble() > 0.5)
                                {
                                    npos.Set(upos.X + facing.Normali.X + facing2.Normali.X, upos.Y + facing.Normali.Y + facing2.Normali.Y, upos.Z + facing.Normali.Z + facing2.Normali.Z);

                                    if (blockAccessor.GetBlock(npos).Replaceable >= blockLeaves.Replaceable)
                                    {
                                        blockAccessor.SetBlock(blockLeaves.BlockId, npos);
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }

                upos.Up();
            }
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            if (!this.isSegmentWithLeaves || LastCodePart() != "segment3") return base.GetRandomColor(capi, pos, facing);

            if (Textures == null || Textures.Count == 0) return 0;
            CompositeTexture tex;
            if (!Textures.TryGetValue(facing.Code, out tex))
            {
                tex = Textures.First().Value;
            }
            if (tex?.Baked == null) return 0;

            int color = capi.BlockTextureAtlas.GetRandomColor(tex.Baked.TextureSubId);

            return capi.World.ApplyColorMapOnRgba("climatePlantTint", SeasonColorMap, color, pos.X, pos.Y, pos.Z);
        }


        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Block[] chunkExtBlocks, int extIndex3d)
        {
            if (VertexFlags.LeavesWindWave)
            {
                int leavesNoWaveTileSide = 0;

                // For bamboo poles, only check the block below - unlike leaves, these don't attach to solid blocks on all sides
                Block nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down]];
                if (!nblock.VertexFlags.LeavesWindWave && nblock.SideSolid[TileSideEnum.Up]) leavesNoWaveTileSide = 32;
                else if (nblock is BlockBamboo)
                {
                    // Detect immobile bamboo below

                    nblock = chunkExtBlocks[extIndex3d + TileSideEnum.MoveIndex[TileSideEnum.Down] + 1];
                    if (!nblock.VertexFlags.LeavesWindWave && nblock.SideSolid[TileSideEnum.West]) leavesNoWaveTileSide = 32;
                }


                bool waveoff = (byte)(lightRgbsByCorner[24] >> 24) < 159;  //corresponds with a sunlight level of less than 14
                int groundOffset = 1;

                // Disable swaying if would push into a block to the East
                nblock = chunkExtBlocks[extIndex3d + 1];
                if (!nblock.VertexFlags.LeavesWindWave && nblock.SideSolid[TileSideEnum.West]) waveoff = true;

                if (!waveoff)
                {
                    bool bambooLeavesFound = isSegmentWithLeaves;
                    bool continuousBambooCane = true;
                    int upMoveIndex = TileSideEnum.MoveIndex[TileSideEnum.Up];
                    int nsMoveIndex = TileSideEnum.MoveIndex[TileSideEnum.South];
                    int movedIndex3d = extIndex3d;
                    Block block1;
                    Block block2;
                    for (; groundOffset < 8; groundOffset++)
                    {
                        movedIndex3d -= upMoveIndex;    // move down first because groundOffset starts at 1 (no point checking this block itself!)
                        if (movedIndex3d >= 0)
                        {
                            block1 = chunkExtBlocks[movedIndex3d];
                            block2 = (block1 is BlockBamboo) ? chunkExtBlocks[movedIndex3d + 1] : null;
                        }
                        else
                        {
                            block1 = api.World.BlockAccessor.GetBlock(pos.X, pos.Y - groundOffset, pos.Z);
                            block2 = (block1 is BlockBamboo) ? api.World.BlockAccessor.GetBlock(pos.X + 1, pos.Y - groundOffset, pos.Z) : null;
                        }

                        if (!block1.VertexFlags.LeavesWindWave && block1.SideSolid[TileSideEnum.Up]) break;
                        if (block2 != null && !block2.VertexFlags.LeavesWindWave && block2.SideSolid[TileSideEnum.West]) break;

                        if (block2 == null) continuousBambooCane = false;

                        if (!bambooLeavesFound && continuousBambooCane)
                        {
                            if (block1 is BlockBamboo bam && bam.isSegmentWithLeaves)
                            {
                                bambooLeavesFound = true; continue;
                            }
                        }
                    }


                    int y = pos.Y;
                    continuousBambooCane = true;
                    if (!bambooLeavesFound)
                    {
                        movedIndex3d = extIndex3d;
                        int max = upMoveIndex * (nsMoveIndex - 1);  //this is the index of the top (max Y) layer in the extBlocks

                        do
                        {
                            movedIndex3d += upMoveIndex;
                            y++;

                            block1 = chunkExtBlocks[movedIndex3d];
                            if (block1 is BlockBamboo bam)
                            {
                                bambooLeavesFound = bam.isSegmentWithLeaves;
                            }
                            else
                            {
                                continuousBambooCane = false;
                                break;  // Stop searching once no longer a continuous bamboo cane above
                            }
                        }
                        while (!bambooLeavesFound && movedIndex3d < max);
                    }

                    // Carry on doing the same check even into the next chunk above, as long as there's bamboo here
                    while (!bambooLeavesFound && continuousBambooCane)
                    {
                        block1 = api.World.BlockAccessor.GetBlock(pos.X, ++y, pos.Z);
                        if (block1 is BlockBamboo bam)
                        {
                            bambooLeavesFound = bam.isSegmentWithLeaves;
                        }
                        else
                        {
                            break;  // Stop searching once no longer a continuous bamboo cane above
                        }
                    }

                    if (!bambooLeavesFound) waveoff = true;
                }

                if (VertexFlags.GrassWindWave)
                {
                    // Standard: apply swaying motion (based on this block's VertexFlags)
                    BlockWithLeavesMotion.SetLeaveWaveFlags(sourceMesh, leavesNoWaveTileSide, waveoff, VertexFlags.All, groundOffset);
                }
                else
                {
                    // Segment3: we want the CANES part to sway but the LEAVES part to have full leaves motion
                    SetLeaveWaveFlagsAdaptive(sourceMesh, leavesNoWaveTileSide, waveoff, VertexFlags.All, groundOffset);
                }
            }
        }

        void SetLeaveWaveFlagsAdaptive(MeshData sourceMesh, int leavesNoWaveTileSide, bool waveOff, int leaveWave, int groundOffsetTop)
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

                float fx = sourceMesh.xyz[vertexNum * 3 + 0];
                float fz = sourceMesh.xyz[vertexNum * 3 + 2];
                int x = (int)(fx - 1.5f) >> 1;
                int y = (int)(sourceMesh.xyz[vertexNum * 3 + 1] - 1.5f) >> 1;
                int z = (int)(fz - 1.5f) >> 1;

                int sidesToCheckMask = 1 << TileSideEnum.Up - y | 4 + z * 4 | 2 - x * 6;

                if ((leavesNoWaveTileSide & sidesToCheckMask) == 0)
                {
                    // regular leaf motion for the outer leaf, swaying motion for the inner canes:  here we know that the Segment3 model has outer leaf vertices at 0.1/16 and 15.9/16, this code may break if the leaf model is changed  (we really need per-element VertexFlags in the model)
                    if (fx > 0.0065f && fx < 0.9935f && fz > 0.0065f && fz < 0.9935f) leaveWave |= VertexFlags.FoliageWindWaveBitMask;

                    flag |= leaveWave | (groundOffsetTop == 8 ? 7 : groundOffsetTop + y) << 28;
                }

                sourceMesh.Flags[vertexNum] = flag;
            }
        }
    }
}
