using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class TreeGen : ITreeGenerator
    {
        // "Temporary Values" linked to currently generated tree
        IBlockAccessor api;
        BlockPos pos;
        float size;
        float vineGrowthChance; // 0..1
        Random rand;
        List<TreeGenBranch> branchesByDepth = new List<TreeGenBranch>();


        // Tree config
        TreeGenConfig config;

        

        public TreeGen(TreeGenConfig config, int seed)
        {
            this.config = config;
            rand = new Random(seed);
        }

        public void GrowTree(IBlockAccessor api, BlockPos pos, float sizeModifier = 1f, float vineGrowthChance = 0, float forestDensity = 0)
        {
            this.pos = pos;
            this.api = api;
            this.size = sizeModifier * config.sizeMultiplier;
            this.vineGrowthChance = vineGrowthChance;

            pos.Up(config.yOffset);
            
            TreeGenTrunk[] trunks = config.trunks;

            branchesByDepth.Clear();
            branchesByDepth.Add(null);
            branchesByDepth.AddRange(config.branches);

            for (int i = 0; i < trunks.Length; i++)
            {
                TreeGenTrunk trunk = config.trunks[i];

                if (rand.NextDouble() <= trunk.probability)
                {
                    branchesByDepth[0] = trunk;

                    growBranch(
                        0, pos, trunk.dx, 0f, trunk.dz,
                        trunk.angleVert.nextFloat(1, rand),
                        trunk.angleHori.nextFloat(1, rand),
                        size * trunk.widthMultiplier,
                        Math.Max(0, trunk.dieAt.nextFloat(1, rand))
                    );
                }
            }
        }



    

        

        private void growBranch(int depth, BlockPos pos, float dx, float dy, float dz, float angleVerStart, float angleHorStart, float curWidth, float dieAt)
        {
            if (depth > 30) { Console.WriteLine("TreeGen.growBranch() aborted, too many branches!"); return; }

            TreeGenBranch branch = branchesByDepth[Math.Min(depth, branchesByDepth.Count - 1)];


            float branchspacing = branch.branchSpacing.nextFloat(1, rand);
            float branchstart = branch.branchStart.nextFloat(1, rand);
            float branchQuantityStart = branch.branchQuantity.nextFloat(1, rand);
            float branchWidthMulitplierStart = branch.branchWidthMultiplier.nextFloat(1, rand);

            float reldistance = 0, lastreldistance = 0;
            float totaldistance = curWidth / branch.widthloss;

            int iteration = 0;
            float sequencesPerIteration = 1f / (curWidth / branch.widthloss);

            
            float ddrag = 0, angleVer = 0, angleHor = 0;

            // we want to place around the trunk/branch => offset the coordinates when growing stuff from the base
            float trunkOffsetX = 0, trunkOffsetZ = 0, trunkOffsetY = 0;

            BlockPos currentPos;

            float branchQuantity, branchWidth;
            float sinAngleVer, cosAnglerHor, sinAngleHor;

            float currentSequence;

            while (curWidth > 0 && iteration++ < 5000)
            {
                curWidth -= branch.widthloss;
                
                currentSequence = sequencesPerIteration * (iteration - 1);

                if (curWidth < dieAt) break;

                angleVer = branch.angleVertEvolve.nextFloat(angleVerStart, currentSequence);
                angleHor = branch.angleHoriEvolve.nextFloat(angleHorStart, currentSequence);

                sinAngleVer = GameMath.FastSin(angleVer);
                cosAnglerHor = GameMath.FastCos(angleHor);
                sinAngleHor = GameMath.FastSin(angleHor);

                trunkOffsetX = Math.Max(-0.5f, Math.Min(0.5f, 0.7f * sinAngleVer * cosAnglerHor));
                trunkOffsetY = Math.Max(-0.5f, Math.Min(0.5f, 0.7f * cosAnglerHor)) + 0.5f;
                trunkOffsetZ = Math.Max(-0.5f, Math.Min(0.5f, 0.7f * sinAngleVer * sinAngleHor));

                ddrag = branch.gravityDrag * GameMath.FastSqrt(dx * dx + dz * dz);

                dx += sinAngleVer * cosAnglerHor / Math.Max(1, Math.Abs(ddrag));
                dy += Math.Min(1, Math.Max(-1, GameMath.FastCos(angleVer) - ddrag));
                dz += sinAngleVer * sinAngleHor / Math.Max(1, Math.Abs(ddrag));

                ushort blockId = getBlockId(curWidth);
                if (blockId == 0) return;

                currentPos = pos.AddCopy(dx, dy, dz);

                PlaceResumeState state = getPlaceResumeState(currentPos, blockId);

                if (state == PlaceResumeState.CanPlace)
                {
                    api.SetBlock(blockId, currentPos);

                    if (vineGrowthChance > 0 && rand.NextDouble() < vineGrowthChance && config.treeBlocks.vinesBlock != null)
                    {
                        BlockFacing facing = BlockFacing.HORIZONTALS[rand.Next(4)];

                        BlockPos vinePos = currentPos.AddCopy(facing);
                        float cnt = 1 + rand.Next(11) * (vineGrowthChance + 0.2f);

                        while (api.GetBlockId(vinePos) == 0 && cnt-- > 0)
                        {
                            Block block = config.treeBlocks.vinesBlock;

                            if (cnt <= 0 && config.treeBlocks.vinesEndBlock != null)
                            {
                                block = config.treeBlocks.vinesEndBlock;
                            }

                            block.TryPlaceBlockForWorldGen(api, vinePos, facing);
                            vinePos.Down();
                        }
                    }
                } else
                {
                    if (state == PlaceResumeState.Stop)
                    {
                        return;
                    }
                }

                reldistance = GameMath.FastSqrt(dx * dx + dy * dy + dz * dz) / totaldistance;               

                
                if (reldistance < branchstart) continue;

                if (reldistance > lastreldistance + branchspacing * (1f - reldistance))
                {
                    branchspacing = branch.branchSpacing.nextFloat(1, rand);
                    lastreldistance = reldistance;

                    if (branch.branchQuantityEvolve != null)
                    {
                        branchQuantity = branch.branchQuantityEvolve.nextFloat(branchQuantityStart, currentSequence);
                    } else
                    {
                        branchQuantity = branch.branchQuantity.nextFloat(1, rand);
                    }

                    float prevHorAngle = 0f;
                    float horAngle;
                    float minHorangleDist = Math.Min(GameMath.PI / 10, branch.branchHorizontalAngle.var / 5);
                    

                    bool first = true;

                    while (branchQuantity-- > 0)
                    {
                        if (branchQuantity < 1 && rand.NextDouble() < branchQuantity) break;

                        curWidth *= branch.branchWidthLossMul;

                        horAngle = branch.branchHorizontalAngle.nextFloat(1, rand);

                        int tries = 5;
                        while (!first && Math.Abs(horAngle - prevHorAngle) < minHorangleDist && tries-- > 0)
                        {
                            horAngle = branch.branchHorizontalAngle.nextFloat(1, rand);
                        }

                        if (branch.branchWidthMultiplierEvolve != null)
                        {
                            branchWidth = curWidth * branch.branchWidthMultiplierEvolve.nextFloat(branchWidthMulitplierStart, currentSequence);
                        } else
                        {
                            branchWidth = branch.branchWidthMultiplier.nextFloat(curWidth, rand);
                        }

                        growBranch(
                            depth + 1, 
                            pos, dx + trunkOffsetX, dy, dz + trunkOffsetZ, 
                            branch.branchVerticalAngle.nextFloat(1, rand), 
                            angleHor + branch.branchHorizontalAngle.nextFloat(1, rand), 
                            branchWidth,
                            Math.Max(0, branch.dieAt.nextFloat(1, rand))
                        );

                        first = false;
                        prevHorAngle = horAngle;
                    }
                }
            }
        }


        
        public ushort getBlockId(float width)
        {
            return
                width < 0.1f ? config.treeBlocks.leavesBlockId : (
                    width < 0.3f ? config.treeBlocks.leavesBranchyBlockId : config.treeBlocks.logBlockId
                )
            ;
        }

        


        PlaceResumeState getPlaceResumeState(BlockPos targetPos, ushort desiredblockId)
        {
            if (targetPos.X < 0 || targetPos.Y < 0 || targetPos.Z < 0 || targetPos.X >= api.MapSizeX || targetPos.Y >= api.MapSizeY || targetPos.Z >= api.MapSizeZ) return PlaceResumeState.Stop;

            // Should be like this but seems to work just fine anyway? o.O
            //int currentblockId = (api is IBulkBlockAccessor) ? ((IBulkBlockAccessor)api).GetStagedBlockId(targetPos) : api.GetBlockId(targetPos);
            int currentblockId = api.GetBlockId(targetPos);
            if (currentblockId == -1) return PlaceResumeState.CannotPlace;
            if (currentblockId == 0) return PlaceResumeState.CanPlace;

            Block currentBlock = api.GetBlock(currentblockId);
            Block desiredBock = api.GetBlock(desiredblockId);

            if (currentBlock.Replaceable < 6000 && !config.treeBlocks.blockIds.Contains(currentBlock.BlockId) && (desiredBock.BlockMaterial != EnumBlockMaterial.Wood || currentBlock.Fertility == 0) /* Allow logs to replace soil */)
            {
                return PlaceResumeState.Stop;
            }

            return (desiredBock.Replaceable > currentBlock.Replaceable) ? PlaceResumeState.CannotPlace : PlaceResumeState.CanPlace;
        }
    }

    enum PlaceResumeState
    {
        CannotPlace,
        CanPlace,
        Stop
    }
}
