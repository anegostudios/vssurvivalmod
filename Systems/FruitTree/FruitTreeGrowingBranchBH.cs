using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class FruitTreeGrowingBranchBH : BlockEntityBehavior
    {
        int callbackTimeMs = 20000;
        public float VDrive = 0;
        public float HDrive = 0;

        Block stemBlock;
        BlockFruitTreeBranch branchBlock;
        BlockFruitTreeFoliage leavesBlock;
        long listenerId;

        BlockEntityFruitTreeBranch ownBe => Blockentity as BlockEntityFruitTreeBranch;

        public FruitTreeGrowingBranchBH(BlockEntity blockentity) : base(blockentity)
        {
#if DEBUG
            callbackTimeMs = 1000;
#endif
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (Api.Side == EnumAppSide.Server)
            {
                listenerId = Blockentity.RegisterGameTickListener(OnTick, callbackTimeMs + Api.World.Rand.Next(callbackTimeMs));
            }

            stemBlock = Api.World.GetBlock(ownBe.Block.CodeWithVariant("type", "stem"));
            branchBlock = Api.World.GetBlock(ownBe.Block.CodeWithVariant("type", "branch")) as BlockFruitTreeBranch;
            leavesBlock = Api.World.GetBlock(AssetLocation.Create(ownBe.Block.Attributes["foliageBlock"].AsString(), ownBe.Block.Code.Domain)) as BlockFruitTreeFoliage;

            if (ownBe.Block == leavesBlock) ownBe.PartType = EnumTreePartType.Leaves;
            if (ownBe.Block == branchBlock) ownBe.PartType = EnumTreePartType.Branch;

            if (ownBe.lastGrowthAttemptTotalDays == 0)
            {
                ownBe.lastGrowthAttemptTotalDays = api.World.Calendar.TotalDays;
            }
        }


        protected void OnTick(float dt)
        {
            // Not fully set up yet in BlockEntityFruitTreePart
            if (ownBe.RootOff == null) return;

            var rootBe = Api.World.BlockAccessor.GetBlockEntity(ownBe.Pos.AddCopy(ownBe.RootOff)) as BlockEntityFruitTreeBranch;
            if (rootBe == null)
            {
                if (Api.World.Rand.NextDouble() < 0.25)
                {
                    Api.World.BlockAccessor.BreakBlock(ownBe.Pos, null);
                }
                return;
            }

            double totalDays = Api.World.Calendar.TotalDays;

            if (ownBe.GrowTries > 60 || ownBe.FoliageState == EnumFoliageState.Dead)
            {
                ownBe.lastGrowthAttemptTotalDays = totalDays;
                return;
            }

            
            ownBe.lastGrowthAttemptTotalDays = Math.Max(ownBe.lastGrowthAttemptTotalDays, totalDays - Api.World.Calendar.DaysPerYear * 4); // Don't simulate more than 4 years

            if (totalDays - ownBe.lastGrowthAttemptTotalDays < 0.5) return;

            double hoursPerDay = Api.World.Calendar.HoursPerDay;


            FruitTreeProperties props = null;

            if (ownBe.TreeType == null)
            {
                Api.World.BlockAccessor.SetBlock(0, ownBe.Pos);
                return;
            }

            var rootBh = rootBe.GetBehavior<FruitTreeRootBH>();
            if (rootBh?.propsByType.TryGetValue(ownBe.TreeType, out props) != true)
            {
                return;
            }
            if (ownBe.FoliageState == EnumFoliageState.Dead)
            {
                ownBe.UnregisterGameTickListener(listenerId);
                return;
            }


            double growthStepDays = props.GrowthStepDays;


            while (totalDays - ownBe.lastGrowthAttemptTotalDays > growthStepDays)
            {
                // Get midday temperature for testing (which is roughly the daily average)
                float temp = Api.World.BlockAccessor.GetClimateAt(ownBe.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, (int)ownBe.lastGrowthAttemptTotalDays + hoursPerDay/2f).Temperature;
                if (temp < 12)
                {
                    ownBe.lastGrowthAttemptTotalDays += growthStepDays;
                    continue;
                }

                TryGrow();
                ownBe.lastGrowthAttemptTotalDays += growthStepDays;
                ownBe.GrowTries++;
            }
        }

        public void OnNeighbourBranchRemoved(BlockFacing facing)
        {
            if ((ownBe.SideGrowth & (1<<facing.Index)) > 0)
            {
                ownBe.GrowTries = Math.Min(55, ownBe.GrowTries - 5);
                HDrive++;
            }
        }

        private void TryGrow()
        {
            var rnd = Api.World.Rand;

            if (ownBe.TreeType == "" || ownBe.TreeType == null)
            {
                return;
            }

            switch (ownBe.PartType)
            {
                case EnumTreePartType.Stem:
                    Block upBlock2 = Api.World.BlockAccessor.GetBlock(ownBe.Pos.UpCopy());
                    if (upBlock2.Id == 0)
                    {
                        TryGrowTo(EnumTreePartType.Leaves, BlockFacing.UP, 1);
                        ownBe.GrowTries /= 2;
                    } else
                    {
                        if (upBlock2 == leavesBlock)
                        {
                            TryGrowTo(EnumTreePartType.Branch, BlockFacing.UP, 1);
                        } else
                        {
                            var faces = ((BlockFacing[])BlockFacing.ALLFACES.Clone()).Shuffle(Api.World.Rand);
                            foreach (var facing in faces)
                            {
                                if ((ownBe.SideGrowth & (1 << facing.Index)) > 0)
                                {
                                    Block nblock = Api.World.BlockAccessor.GetBlock(ownBe.Pos.AddCopy(facing));

                                    if (nblock == leavesBlock)
                                    {
                                        TryGrowTo(EnumTreePartType.Branch, facing, 1, 1);
                                    } else
                                    {
                                        if (nblock.Id == 0) TryGrowTo(EnumTreePartType.Leaves, facing, 1);
                                    }
                                }
                            }
                        }
                    }
                    break;
                    
                case EnumTreePartType.Cutting:
                    if (ownBe.FoliageState == EnumFoliageState.Dead || ownBe.GrowTries < 1) break;

                    var rootBe = Api.World.BlockAccessor.GetBlockEntity(ownBe.Pos.AddCopy(ownBe.RootOff)) as BlockEntityFruitTreeBranch;

                    var rootBh = rootBe.GetBehavior<FruitTreeRootBH>();
                    if (rootBh == null) break;

                    double rndval = Api.World.Rand.NextDouble();
                    bool survived =
                        (ownBe.GrowthDir.IsVertical && branchBlock.TypeProps[ownBe.TreeType].CuttingRootingChance >= rndval)
                        || (ownBe.GrowthDir.IsHorizontal && branchBlock.TypeProps[ownBe.TreeType].CuttingGraftChance >= rndval)
                    ;

                    //survived = true;

                    if (survived)
                    {
                        Api.World.BlockAccessor.ExchangeBlock(branchBlock.Id, ownBe.Pos);
                        ownBe.GrowTries+=4;
                        ownBe.PartType = EnumTreePartType.Branch;
                        rootBh.propsByType[ownBe.TreeType].State = EnumFruitTreeState.Young;
                        TryGrowTo(EnumTreePartType.Leaves, ownBe.GrowthDir);
                        ownBe.MarkDirty(true);
                    } else
                    {
                        rootBh.propsByType[ownBe.TreeType].State = EnumFruitTreeState.Dead;
                        ownBe.FoliageState = EnumFoliageState.Dead;
                        ownBe.MarkDirty(true);
                    }
                    break;

                case EnumTreePartType.Branch:
                    Block upBlock = Api.World.BlockAccessor.GetBlock(ownBe.Pos.UpCopy());

                    if (ownBe.GrowthDir == BlockFacing.UP)
                    {
                        if (ownBe.GrowTries > 5 && upBlock == leavesBlock && VDrive > 0)
                        {
                            TryGrowTo(EnumTreePartType.Branch, BlockFacing.UP);
                            TryGrowTo(EnumTreePartType.Leaves, BlockFacing.UP, 2);
                            return;
                        }

                        bool growStem = ownBe.GrowTries > 20 && upBlock == branchBlock && ownBe.Height < 3;
                        bool growThinBranch = ownBe.GrowTries > 20 && upBlock == branchBlock && ownBe.Height >= 3 && rnd.NextDouble() < 0.05;
                        bool grownBranches = growStem || growThinBranch;

                        if (grownBranches)
                        {
                            if (growStem)
                            {
                                Api.World.BlockAccessor.ExchangeBlock(stemBlock.Id, ownBe.Pos);
                                ownBe.PartType = EnumTreePartType.Stem;
                                ownBe.MarkDirty(true);
                            }

                            for (int i = 0; i < 4; i++)
                            {
                                var face = BlockFacing.HORIZONTALS[i];
                                var npos = ownBe.Pos.AddCopy(face);
                                var nBlock = Api.World.BlockAccessor.GetBlock(npos);
                                if (nBlock == leavesBlock)
                                {
                                    if (ownBe.Height >= 2 && rnd.NextDouble() < 0.6 && HDrive > 0)
                                    {
                                        if (TryGrowTo(EnumTreePartType.Branch, face))
                                        {
                                            ownBe.SideGrowth |= (1 << i);
                                            ownBe.MarkDirty(true);
                                            TryGrowTo(EnumTreePartType.Leaves, face, 2);
                                        }
                                    }
                                    else
                                    {
                                        bool hasBranch = false;
                                        foreach (var nface in BlockFacing.HORIZONTALS)
                                        {
                                            hasBranch |= Api.World.BlockAccessor.GetBlock(npos.AddCopy(nface)) == branchBlock;
                                        }
                                        if (!hasBranch)
                                        {
                                            Api.World.BlockAccessor.SetBlock(0, npos);
                                        }
                                    }
                                }
                            }


                            return;
                        }

                        if (upBlock.IsReplacableBy(leavesBlock))
                        {
                            TryGrowTo(EnumTreePartType.Leaves, BlockFacing.UP);
                            return;
                        }

                        if (ownBe.Height > 0)
                        {
                            BlockFacing facing = BlockFacing.HORIZONTALS[rnd.Next(4)];
                            TryGrowTo(EnumTreePartType.Leaves, facing);
                        }
                    }
                    else
                    {
                        if (rnd.NextDouble() > 0.5)
                        {
                            var dir = ownBe.GrowthDir;
                            var nblock = Api.World.BlockAccessor.GetBlock(ownBe.Pos.AddCopy(dir));

                            TryGrowTo(nblock == leavesBlock && HDrive > 0 ? EnumTreePartType.Branch : EnumTreePartType.Leaves, dir);
                        }
                        else
                        {
                            int k = 0;
                            for (int i = 0; i < 5; i++)
                            {
                                BlockFacing facing = BlockFacing.ALLFACES[i];
                                if (rnd.NextDouble() < 0.4 && k < 2)
                                {
                                    if (TryGrowTo(EnumTreePartType.Leaves, facing))
                                    {
                                        ownBe.MarkDirty(true);
                                    }
                                    k++;
                                }
                            }
                        }
                    }

                    break;
            }
        }


        private bool TryGrowTo(EnumTreePartType partType, BlockFacing facing, int len = 1, float? hdrive = null)
        {
            var pos = ownBe.Pos.AddCopy(facing, len);
            var block = stemBlock;
            if (partType == EnumTreePartType.Branch) block = branchBlock;
            if (partType == EnumTreePartType.Leaves) block = leavesBlock;

            var nblock = Api.World.BlockAccessor.GetBlock(pos);

            bool replaceable =
                (partType == EnumTreePartType.Leaves && nblock.IsReplacableBy(leavesBlock)) ||
                (partType == EnumTreePartType.Branch && nblock == leavesBlock) ||
                (partType == EnumTreePartType.Stem && nblock == branchBlock)
            ;
            if (!replaceable) return false;

            var rootPos = ownBe.Pos.AddCopy(ownBe.RootOff);
            var rootBe = Api.World.BlockAccessor.GetBlockEntity(rootPos) as BlockEntityFruitTreeBranch;
            if (rootBe == null) return false;

            var bh = rootBe.GetBehavior<FruitTreeRootBH>();
            if (bh != null) bh.BlocksGrown++;


            Api.World.BlockAccessor.SetBlock(block.Id, pos);

            var beb = Api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeBranch;
            var beh = beb?.GetBehavior<FruitTreeGrowingBranchBH>();
            if (beh != null)
            {
                beh.VDrive = VDrive - (facing.IsVertical ? 1 : 0);

                float hd = hdrive == null ? HDrive - (facing.IsHorizontal ? 1 : 0) : (float)hdrive;
                beh.HDrive = hd;
                beb.ParentOff = facing.Normali.Clone();
                beb.lastGrowthAttemptTotalDays = ownBe.lastGrowthAttemptTotalDays;
            }


            var be = Api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreePart;
            if (be != null)
            {
                if (partType != EnumTreePartType.Stem)
                {
                    be.FoliageState = EnumFoliageState.Plain;
                }
                be.GrowthDir = facing;
                be.TreeType = ownBe.TreeType;
                be.PartType = partType;
                be.RootOff = (rootPos - pos).ToVec3i();
                be.Height = ownBe.Height + facing.Normali.Y;
                be.OnGrown();
            }

            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (ownBe.PartType == EnumTreePartType.Cutting)
            {
                dsc.AppendLine(ownBe.FoliageState == EnumFoliageState.Dead ? "<font color=\"#ff8080\">" + Lang.Get("Dead tree cutting") + "</font>" : Lang.Get("Establishing tree cutting"));

                if (ownBe.FoliageState != EnumFoliageState.Dead && branchBlock.TypeProps.TryGetValue(ownBe.TreeType, out var typeprops))
                {
                    dsc.AppendLine(Lang.Get("{0}% survival chance", 100 * (ownBe.GrowthDir.IsVertical ? typeprops.CuttingRootingChance : typeprops.CuttingGraftChance)));
                }
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            VDrive = tree.GetFloat("vdrive");
            HDrive = tree.GetFloat("hdrive");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("vdrive", VDrive);
            tree.SetFloat("hdrive", HDrive);
        }

    }
}
