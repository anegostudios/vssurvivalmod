using System;
using System.Collections.Generic;
using System.Threading;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class TreeGen : ITreeGenerator
    {
        // "Temporary Values" linked to currently generated tree
        IBlockAccessor blockAccessor;
        TreeGenParams treeGenParams;
        float size;

        List<TreeGenBranch> branchesByDepth = new List<TreeGenBranch>();

        // Tree config
        TreeGenConfig config;
        private readonly ForestFloorSystem forestFloor;

        public TreeGen(TreeGenConfig config, int seed, ForestFloorSystem ffs)
        {
            this.config = config;
            forestFloor = ffs;
        }

        public void GrowTree(IBlockAccessor ba, BlockPos pos, TreeGenParams treeGenParams, IRandom random)
        {
            int treeSubType = random.NextInt(8);

            this.blockAccessor = ba;
            this.treeGenParams = treeGenParams;
            this.size = treeGenParams.size * config.sizeMultiplier + config.sizeVar.nextFloat(1, random);

            pos.Up(config.yOffset);

            TreeGenTrunk[] trunks = config.trunks;

            branchesByDepth.Clear();
            branchesByDepth.Add(null);
            branchesByDepth.AddRange(config.branches);

            forestFloor.ClearOutline();

            TreeGenTrunk trunk = config.trunks[0];
            float trunkHeight = Math.Max(0, trunk.dieAt.nextFloat(1, random));
            float trunkWidthLoss = trunk.WidthLoss(random);
            for (int i = 0; i < trunks.Length; i++)
            {
                trunk = config.trunks[i];

                if (random.NextDouble() <= trunk.probability)
                {
                    branchesByDepth[0] = trunk;

                    growBranch(
                        random,
                        0, pos, treeSubType, trunk.dx, 0f, trunk.dz,
                        trunk.angleVert.nextFloat(1, random),
                        trunk.angleHori.nextFloat(1, random),
                        size * trunk.widthMultiplier,
                        trunkHeight, trunkWidthLoss, trunks.Length > 1
                    );
                }
            }

            if (!treeGenParams.skipForestFloor)
            {
                forestFloor.CreateForestFloor(ba, config, pos, random, treeGenParams.treesInChunkGenerated);
            }
        }


        private void growBranch(IRandom rand, int depth, BlockPos basePos, int treeSubType, float dx, float dy, float dz, float angleVerStart, float angleHorStart, float curWidth, float dieAt, float trunkWidthLoss, bool wideTrunk)
        {
            if (depth > 30) { Console.WriteLine("TreeGen.growBranch() aborted, too many branches!"); return; }

            TreeGenBranch branch = branchesByDepth[Math.Min(depth, branchesByDepth.Count - 1)];

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

            float sinAngleVer, cosAnglerHor, sinAngleHor;

            float currentSequence;

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

                int blockId = branch.getBlockId(rand, curWidth, config.treeBlocks, this, treeSubType);
                if (blockId == 0) return;

                currentPos.Set(basePos.X + dx, basePos.Y + dy, basePos.Z + dz);

                PlaceResumeState state = getPlaceResumeState(currentPos, blockId, wideTrunk);

                if (state == PlaceResumeState.CanPlace)
                {
                    PlaceBlockEtc(blockId, currentPos, rand, dx, dz);
                }
                else if (state == PlaceResumeState.Stop)
                {
                    return;
                }

                reldistance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / totaldistance;

                //  Useful for future debugging if ever required:
                // if (depth == 0) blockAccessor.GetBlock(0).api.Logger.Notification("Tree level " + dy + " cw:" + curWidth + " rd:" + reldistance + " bs:" + branchstart);

                if (reldistance < branchstart) continue;

                if (reldistance > lastreldistance + branchspacing * (1f - reldistance))
                {
                    branchspacing = branch.branchSpacing.nextFloat(1, rand);
                    lastreldistance = reldistance;

                    float branchQuantity = (branch.branchQuantityEvolve != null) ?
                        branch.branchQuantityEvolve.nextFloat(branchQuantityStart, currentSequence) :
                        branch.branchQuantity.nextFloat(1, rand);
                    if (rand.NextDouble() < branchQuantity % 1.0) branchQuantity++;   // Deal with the fractional part, because we are only interested in (int)branchQuantity

                    //  Useful for future debugging if ever required:
                    // if (depth == 0) blockAccessor.GetBlock(0).api.Logger.Notification("Tree making branches: " + branchQuantity);

                    curWidth = GrowBranchesHere((int)branchQuantity, branch, depth + 1, rand, curWidth, branchWidthMulitplierStart, currentSequence, angleHor, dx + trunkOffsetX, dy, dz + trunkOffsetZ, basePos, treeSubType, trunkWidthLoss);
                }
            }
        }

        private float GrowBranchesHere(int branchQuantity, TreeGenBranch branch, int newDepth, IRandom rand, float curWidth, float branchWidthMulitplierStart, float currentSequence, float angleHor, float dx, float dy, float dz, BlockPos basePos, int treeSubType, float trunkWidthLoss)
        {
            float branchWidth;
            float prevHorAngle = 0f;
            float horAngle;
            float minHorangleDist = Math.Min(GameMath.PI / 5, branch.branchHorizontalAngle.var / 5);
            bool first = true;

            while (branchQuantity-- > 0)
            {
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
                }
                else
                {
                    branchWidth = branch.branchWidthMultiplier.nextFloat(curWidth, rand);
                }

                growBranch(
                    rand,
                    newDepth,
                    basePos, treeSubType,
                    dx, dy, dz,
                    branch.branchVerticalAngle.nextFloat(1, rand),
                    horAngle,
                    branchWidth,
                    Math.Max(0, branch.dieAt.nextFloat(1, rand)),
                    trunkWidthLoss, false
                );

                first = false;
                prevHorAngle = angleHor + horAngle;
            }

            return curWidth;
        }

        /// <summary>
        /// Place one tree block and any consequentials from that: maybe apply moss; maybe update the canopy outline for forest floor purposes; maybe place vines
        /// </summary>
        private void PlaceBlockEtc(int blockId, BlockPos currentPos, IRandom rand, float dx, float dz)
        {
            blockAccessor.SetBlock(blockId, currentPos);

            if (blockAccessor.GetBlock(blockId).BlockMaterial == EnumBlockMaterial.Wood && treeGenParams.mossGrowthChance > 0 && config.treeBlocks.mossDecorBlock != null)
            {
                var rnd = rand.NextDouble();
                int faceIndex = treeGenParams.hemisphere == EnumHemisphere.North ? 0 : 2; // Prefer north face on the northern hemisphere, and south otherwise (= the shady spot)
                for (int i = 2; i >= 0; i--)
                {
                    if (rnd > treeGenParams.mossGrowthChance * i) break;
                    var face = BlockFacing.HORIZONTALS[faceIndex % 4];
                    var block = blockAccessor.GetBlock(currentPos.X + face.Normali.X, currentPos.Y, currentPos.Z + face.Normali.Z);
                    if (!block.SideSolid[face.Opposite.Index])
                    {
                        blockAccessor.SetDecor(config.treeBlocks.mossDecorBlock, currentPos, face);
                    }
                    faceIndex += rand.NextInt(4);
                }
            }


            // Update the canopy outline of the tree for this block position
            int idz = (int)(dz + ForestFloorSystem.Range);
            int idx = (int)(dx + ForestFloorSystem.Range);
            if (idz > 1 && idz < ForestFloorSystem.GridRowSize - 2 && idx > 1 && idx < ForestFloorSystem.GridRowSize - 2)
            {
                short[] outline = forestFloor.GetOutline();

                int canopyIndex = idz * ForestFloorSystem.GridRowSize + idx;
                outline[canopyIndex - 2 * ForestFloorSystem.GridRowSize - 2]++;
                outline[canopyIndex - 2 * ForestFloorSystem.GridRowSize - 1]++;  //bias canopy shading towards the North (- z direction) for sun effects
                outline[canopyIndex - 2 * ForestFloorSystem.GridRowSize + 0]++;
                outline[canopyIndex - 2 * ForestFloorSystem.GridRowSize + 1]++;
                outline[canopyIndex - 2 * ForestFloorSystem.GridRowSize + 2]++;
                outline[canopyIndex - ForestFloorSystem.GridRowSize - 2]++;
                outline[canopyIndex - ForestFloorSystem.GridRowSize - 1] += 2;
                outline[canopyIndex - ForestFloorSystem.GridRowSize + 0] += 2;
                outline[canopyIndex - ForestFloorSystem.GridRowSize + 1] += 2;
                outline[canopyIndex - ForestFloorSystem.GridRowSize + 2]++;
                outline[canopyIndex - 2]++;
                outline[canopyIndex - 1] += 2;
                outline[canopyIndex + 0] += 3;
                outline[canopyIndex + 1] += 2;
                outline[canopyIndex + 2]++;
                outline[canopyIndex + ForestFloorSystem.GridRowSize]++;
            }

            if (treeGenParams.vinesGrowthChance > 0 && rand.NextDouble() < treeGenParams.vinesGrowthChance && config.treeBlocks.vinesBlock != null)
            {
                BlockFacing facing = BlockFacing.HORIZONTALS[rand.NextInt(4)];

                BlockPos vinePos = currentPos.AddCopy(facing);
                float cnt = 1 + rand.NextInt(11) * (treeGenParams.vinesGrowthChance + 0.2f);

                while (blockAccessor.GetBlockId(vinePos) == 0 && cnt-- > 0)
                {
                    Block block = config.treeBlocks.vinesBlock;

                    if (cnt <= 0 && config.treeBlocks.vinesEndBlock != null)
                    {
                        block = config.treeBlocks.vinesEndBlock;
                    }

                    block.TryPlaceBlockForWorldGen(blockAccessor, vinePos, facing, rand);
                    vinePos.Down();
                }
            }
        }

        internal bool TriggerRandomOtherBlock(IRandom lcgRandom)
        {
            return lcgRandom.NextDouble() < treeGenParams.otherBlockChance * config.treeBlocks.otherLogChance;
        }


        PlaceResumeState getPlaceResumeState(BlockPos targetPos, int desiredblockId, bool wideTrunk)
        {
            if (targetPos.X < 0 || targetPos.Y < 0 || targetPos.Z < 0 || targetPos.X >= blockAccessor.MapSizeX || targetPos.Y >= blockAccessor.MapSizeY || targetPos.Z >= blockAccessor.MapSizeZ) return PlaceResumeState.Stop;

            int currentblockId = blockAccessor.GetBlockId(targetPos);
            if (currentblockId == -1) return PlaceResumeState.CannotPlace;
            if (currentblockId == 0) return PlaceResumeState.CanPlace;

            Block currentBlock = blockAccessor.GetBlock(currentblockId);
            Block desiredBock = blockAccessor.GetBlock(desiredblockId);

            // For everything except redwood trunks, abort the treegen if it encounters a non-replaceable, non-soil, non-same-tree block.  Redwood trunks continue regardless, otherwise we get 3/4 chunks because of some other random worldgen block near the base etc.
            if ((currentBlock.Fertility == 0 || desiredBock.BlockMaterial != EnumBlockMaterial.Wood) && currentBlock.BlockMaterial != EnumBlockMaterial.Leaves && currentBlock.Replaceable < 6000 && !wideTrunk && !config.treeBlocks.blockIds.Contains(currentBlock.BlockId) /* Allow logs to replace soil */)
            {
                return PlaceResumeState.Stop;
            }

            return (desiredBock.Replaceable > currentBlock.Replaceable) ? PlaceResumeState.CannotPlace : PlaceResumeState.CanPlace;
        }
    }

    public enum PlaceResumeState
    {
        CannotPlace,
        CanPlace,
        Stop
    }
}
