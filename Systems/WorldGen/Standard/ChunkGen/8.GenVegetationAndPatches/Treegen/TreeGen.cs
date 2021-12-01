using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class TreeGen : ITreeGenerator
    {
        // "Temporary Values" linked to currently generated tree
        IBlockAccessor api;
        float size;
        float vineGrowthChance; // 0..1
        float otherBlockChance; // 0..1

        static ThreadLocal<Random> rand = new ThreadLocal<Random>(() => new Random(Environment.TickCount));

        List<TreeGenBranch> branchesByDepth = new List<TreeGenBranch>();
        ThreadLocal<LCGRandom> lcgrandTL;

        // Tree config
        TreeGenConfig config;
        private readonly ForestFloorSystem forestFloor;


        public TreeGen(TreeGenConfig config, int seed, ForestFloorSystem ffs)
        {
            this.config = config;
            this.forestFloor = ffs;
            lcgrandTL = new ThreadLocal<LCGRandom>(() => new LCGRandom(seed));
        }

        public void GrowTree(IBlockAccessor api, BlockPos pos, bool isShrubLayer, float sizeModifier = 1f, float vineGrowthChance = 0, float otherBlockChance = 1f, int treesInChunkGenerated = 0)
        {
            Random rnd = rand.Value;
            lcgrandTL.Value.InitPositionSeed(pos.X, pos.Z);

            this.api = api;
            this.size = sizeModifier * config.sizeMultiplier + config.sizeVar.nextFloat(1, rnd);
            this.vineGrowthChance = vineGrowthChance;
            this.otherBlockChance = otherBlockChance;

            pos.Up(config.yOffset);

            TreeGenTrunk[] trunks = config.trunks;

            branchesByDepth.Clear();
            branchesByDepth.Add(null);
            branchesByDepth.AddRange(config.branches);

            forestFloor.ClearOutline();

            TreeGenTrunk trunk = config.trunks[0];
            float trunkHeight = Math.Max(0, trunk.dieAt.nextFloat(1, rnd));
            float trunkWidthLoss = trunk.WidthLoss(rnd);
            for (int i = 0; i < trunks.Length; i++)
            {
                trunk = config.trunks[i];

                if (rnd.NextDouble() <= trunk.probability)
                {
                    branchesByDepth[0] = trunk;

                    growBranch(
                        rnd,
                        0, pos, trunk.dx, 0f, trunk.dz,
                        trunk.angleVert.nextFloat(1, rnd),
                        trunk.angleHori.nextFloat(1, rnd),
                        size * trunk.widthMultiplier,
                        trunkHeight, trunkWidthLoss, trunks.Length > 1
                    );
                }
            }

            if (!isShrubLayer)
            {
                forestFloor.CreateForestFloor(api, config, pos, lcgrandTL.Value, treesInChunkGenerated);
            }
        }


        private void growBranch(Random rand, int depth, BlockPos pos, float dx, float dy, float dz, float angleVerStart, float angleHorStart, float curWidth, float dieAt, float trunkWidthLoss, bool wideTrunk)
        {
            if (depth > 30) { Console.WriteLine("TreeGen.growBranch() aborted, too many branches!"); return; }

            TreeGenBranch branch = branchesByDepth[Math.Min(depth, branchesByDepth.Count - 1)];
            short[] outline = forestFloor.GetOutline();

            float widthloss = depth == 0 ? trunkWidthLoss : branch.WidthLoss(rand);
            float widthlossCurve = branch.widthlossCurve;
            float branchspacing = branch.branchSpacing.nextFloat(1, rand);
            float branchstart = branch.branchStart.nextFloat(1, rand);
            float branchQuantityStart = branch.branchQuantity.nextFloat(1, rand);
            float branchWidthMulitplierStart = branch.branchWidthMultiplier.nextFloat(1, rand);

            float reldistance, lastreldistance = 0;
            float totaldistance = curWidth / widthloss;

            int iteration = 0;
            float sequencesPerIteration = 1f / (curWidth / widthloss);

            
            float ddrag, angleVer, angleHor;

            // we want to place around the trunk/branch => offset the coordinates when growing stuff from the base
            float trunkOffsetX, trunkOffsetZ;

            BlockPos currentPos = new BlockPos();

            float branchQuantity, branchWidth;
            float sinAngleVer, cosAnglerHor, sinAngleHor;

            float currentSequence;

            LCGRandom lcgrand = lcgrandTL.Value;

            while (curWidth > 0 && iteration++ < 5000)
            {
                curWidth -= widthloss;
                if (widthlossCurve + curWidth / 20 < 1f) widthloss *= (widthlossCurve + curWidth / 20);
                
                currentSequence = sequencesPerIteration * (iteration - 1);

                if (curWidth < dieAt) break;

                angleVer = branch.angleVertEvolve.nextFloat(angleVerStart, currentSequence);
                angleHor = branch.angleHoriEvolve.nextFloat(angleHorStart, currentSequence);

                sinAngleVer = GameMath.FastSin(angleVer);
                cosAnglerHor = GameMath.FastCos(angleHor);
                sinAngleHor = GameMath.FastSin(angleHor);

                trunkOffsetX = Math.Max(-0.5f, Math.Min(0.5f, 0.7f * sinAngleVer * cosAnglerHor));
                trunkOffsetZ = Math.Max(-0.5f, Math.Min(0.5f, 0.7f * sinAngleVer * sinAngleHor));

                ddrag = branch.gravityDrag * (float)Math.Sqrt(dx * dx + dz * dz);

                dx += sinAngleVer * cosAnglerHor / Math.Max(1, Math.Abs(ddrag));
                dy += Math.Min(1, Math.Max(-1, GameMath.FastCos(angleVer) - ddrag));
                dz += sinAngleVer * sinAngleHor / Math.Max(1, Math.Abs(ddrag));

                int blockId = branch.getBlockId(curWidth, config.treeBlocks, this);
                if (blockId == 0) return;

                currentPos.Set(pos.X + dx, pos.Y + dy, pos.Z + dz);

                PlaceResumeState state = getPlaceResumeState(currentPos, blockId, wideTrunk);

                if (state == PlaceResumeState.CanPlace)
                {
                    api.SetBlock(blockId, currentPos);

                    // Update the canopy outline of the tree for this block position
                    int idz = (int)(dz + 16);
                    int idx = (int)(dx + 16);
                    if (idz > 1 && idz < 31 && idx > 1 && idx < 31)
                    {
                        int canopyIndex = idz * 33 + idx;
                        outline[canopyIndex - 68]++;
                        outline[canopyIndex - 67]++;  //bias canopy shading towards the North (- z direction) for sun effects
                        outline[canopyIndex - 66]++;
                        outline[canopyIndex - 65]++;
                        outline[canopyIndex - 64]++;
                        outline[canopyIndex - 35]++;
                        outline[canopyIndex - 34] += 2;
                        outline[canopyIndex - 33] += 2;
                        outline[canopyIndex - 32] += 2;
                        outline[canopyIndex - 31]++;
                        outline[canopyIndex - 2]++;
                        outline[canopyIndex - 1] += 2;
                        outline[canopyIndex + 0] += 3;
                        outline[canopyIndex + 1] += 2;
                        outline[canopyIndex + 2]++;
                        outline[canopyIndex + 33]++;
                    }

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

                            block.TryPlaceBlockForWorldGen(api, vinePos, facing, lcgrand);
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

                reldistance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / totaldistance;               

                
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
                    float minHorangleDist = Math.Min(GameMath.PI / 5, branch.branchHorizontalAngle.var / 5);
                    

                    bool first = true;

                    while (branchQuantity-- > 0)
                    {
                        if (branchQuantity < 1 && rand.NextDouble() < branchQuantity) break;

                        curWidth *= branch.branchWidthLossMul;

                        horAngle = angleHor + branch.branchHorizontalAngle.nextFloat(1, rand);

                        int tries = 10;
                        while (!first && Math.Abs(horAngle - prevHorAngle) < minHorangleDist && tries-- > 0)
                        {
                            float newAngle = angleHor + branch.branchHorizontalAngle.nextFloat(1, rand);
                            if (Math.Abs(horAngle - prevHorAngle) < Math.Abs(newAngle - prevHorAngle))
                            {
                                horAngle = newAngle;
                            }
                        }

                        if (branch.branchWidthMultiplierEvolve != null)
                        {
                            branchWidth = curWidth * branch.branchWidthMultiplierEvolve.nextFloat(branchWidthMulitplierStart, currentSequence);
                        } else
                        {
                            branchWidth = branch.branchWidthMultiplier.nextFloat(curWidth, rand);
                        }

                        growBranch(
                            rand,
                            depth + 1, 
                            pos, dx + trunkOffsetX, dy, dz + trunkOffsetZ, 
                            branch.branchVerticalAngle.nextFloat(1, rand), 
                            horAngle,
                            branchWidth,
                            Math.Max(0, branch.dieAt.nextFloat(1, rand)),
                            trunkWidthLoss, false
                        );

                        first = false;
                        prevHorAngle = angleHor + horAngle;
                    }
                }
            }
        }


        internal bool TriggerRandomOtherBlock()
        {
            return rand.Value.NextDouble() < otherBlockChance * config.treeBlocks.otherLogChance;
        }


        PlaceResumeState getPlaceResumeState(BlockPos targetPos, int desiredblockId, bool wideTrunk)
        {
            if (targetPos.X < 0 || targetPos.Y < 0 || targetPos.Z < 0 || targetPos.X >= api.MapSizeX || targetPos.Y >= api.MapSizeY || targetPos.Z >= api.MapSizeZ) return PlaceResumeState.Stop;

            // Should be like this but seems to work just fine anyway? o.O
            //int currentblockId = (api is IBulkBlockAccessor) ? ((IBulkBlockAccessor)api).GetStagedBlockId(targetPos) : api.GetBlockId(targetPos);
            int currentblockId = api.GetBlockId(targetPos);
            if (currentblockId == -1) return PlaceResumeState.CannotPlace;
            if (currentblockId == 0) return PlaceResumeState.CanPlace;

            Block currentBlock = api.GetBlock(currentblockId);
            Block desiredBock = api.GetBlock(desiredblockId);

            // For everything except redwood trunks, abort the treegen if it encounters a non-replaceable, non-soil, non-same-tree block.  Redwood trunks continue regardless, otherwise we get 3/4 chunks because of some other random worldgen block near the base etc.
            if ((currentBlock.Fertility == 0 || desiredBock.BlockMaterial != EnumBlockMaterial.Wood) && currentBlock.Replaceable < 6000 && !wideTrunk && !config.treeBlocks.blockIds.Contains(currentBlock.BlockId) /* Allow logs to replace soil */)
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
