using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class FruitTreeRootBH : BlockEntityBehavior
    {
        public int BlocksGrown = 0;
        public int BlocksRemoved = 0;
        public double TreePlantedTotalDays;
        protected double LastRootTickTotalDays;

        public Dictionary<string, FruitTreeProperties> propsByType = new Dictionary<string, FruitTreeProperties>();

        RoomRegistry roomreg;

        BlockEntity be => Blockentity;

        BlockEntityFruitTreeBranch bebr => be as BlockEntityFruitTreeBranch;
        ItemStack parentPlantStack;

        BlockFruitTreeBranch blockBranch;

        double stateUpdateIntervalDays = 1 / 3.0;

        public double nonFloweringYoungDays = 30;

        public bool IsYoung => Api?.World.Calendar.TotalDays - TreePlantedTotalDays < nonFloweringYoungDays;


        public FruitTreeRootBH(BlockEntity blockentity, ItemStack parentPlantStack) : base(blockentity)
        {
            this.parentPlantStack = parentPlantStack;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (api.Side == EnumAppSide.Server)
            {
                Blockentity.RegisterGameTickListener(onRootTick, 5000, api.World.Rand.Next(5000));
            }

            roomreg = api.ModLoader.GetModSystem<RoomRegistry>();

            blockBranch = be.Block as BlockFruitTreeBranch;

            RegisterTreeType(bebr.TreeType);

            double totalDays = api.World.Calendar.TotalDays;

            if (TreePlantedTotalDays == 0)
            {
                TreePlantedTotalDays = totalDays;
                LastRootTickTotalDays = totalDays;
            }
            else
            {
                // In case this block was imported from another older world. In that case lastCheckAtTotalDays would be a future date.
                TreePlantedTotalDays = Math.Min(TreePlantedTotalDays, totalDays);
                LastRootTickTotalDays = Math.Min(LastRootTickTotalDays, totalDays);
            }
        }

        public void RegisterTreeType(string treeType)
        {
            if (treeType == null) return;
            if (propsByType.ContainsKey(treeType)) return;

            FruitTreeTypeProperties typeProps;
            if (!blockBranch.TypeProps.TryGetValue(bebr.TreeType, out typeProps))
            {
                Api.Logger.Error("Missing fruitTreeProperties for dynamic tree of type '" + bebr.TreeType + "', will use default values.");
                typeProps = new FruitTreeTypeProperties();
            }

            var rnd = Api.World.Rand;
            var props = propsByType[treeType] = new FruitTreeProperties()
            {
                EnterDormancyTemp = typeProps.EnterDormancyTemp.nextFloat(1, rnd),
                LeaveDormancyTemp = typeProps.LeaveDormancyTemp.nextFloat(1, rnd),
                FloweringDays = typeProps.FloweringDays.nextFloat(1, rnd),
                FruitingDays = typeProps.FruitingDays.nextFloat(1, rnd),
                RipeDays = typeProps.RipeDays.nextFloat(1, rnd),
                GrowthStepDays = typeProps.GrowthStepDays.nextFloat(1, rnd),
                DieBelowTemp = typeProps.DieBelowTemp.nextFloat(1, rnd),
                FruitStacks = typeProps.FruitStacks,
                CycleType = typeProps.CycleType,
                VernalizationHours = typeProps.VernalizationHours?.nextFloat(1, rnd) ?? 0,
                VernalizationTemp = typeProps.VernalizationTemp?.nextFloat(1, rnd) ?? 0,
                BlossomAtYearRel = typeProps.BlossomAtYearRel?.nextFloat(1, rnd) ?? 0,
                LooseLeavesBelowTemp = typeProps.LooseLeavesBelowTemp?.nextFloat(1, rnd) ?? 0,
                RootSizeMul = typeProps.RootSizeMul?.nextFloat(1, rnd) ?? 0
            };

            // Genetic variability
            if (parentPlantStack != null)
            {
                props.EnterDormancyTemp = typeProps.EnterDormancyTemp.ClampToRange(props.EnterDormancyTemp + parentPlantStack.Attributes?.GetFloat("enterDormancyTempDiff") ?? 0);
                props.LeaveDormancyTemp = typeProps.LeaveDormancyTemp.ClampToRange(props.LeaveDormancyTemp + parentPlantStack.Attributes?.GetFloat("leaveDormancyTempDiff") ?? 0);
                props.FloweringDays = typeProps.FloweringDays.ClampToRange(props.FloweringDays + parentPlantStack.Attributes?.GetFloat("floweringDaysDiff") ?? 0);
                props.FruitingDays = typeProps.FruitingDays.ClampToRange(props.FruitingDays + parentPlantStack.Attributes?.GetFloat("fruitingDaysDiff") ?? 0);
                props.RipeDays = typeProps.RipeDays.ClampToRange(props.RipeDays + parentPlantStack.Attributes?.GetFloat("ripeDaysDiff") ?? 0);
                props.GrowthStepDays = typeProps.GrowthStepDays.ClampToRange(props.GrowthStepDays + parentPlantStack.Attributes?.GetFloat("growthStepDaysDiff") ?? 0);
                props.DieBelowTemp = typeProps.DieBelowTemp.ClampToRange(props.DieBelowTemp + parentPlantStack.Attributes?.GetFloat("dieBelowTempDiff") ?? 0);
                props.VernalizationHours = typeProps.VernalizationHours.ClampToRange(props.VernalizationHours + parentPlantStack.Attributes?.GetFloat("vernalizationHoursDiff") ?? 0);
                props.VernalizationTemp = typeProps.VernalizationTemp.ClampToRange(props.VernalizationTemp + parentPlantStack.Attributes?.GetFloat("vernalizationTempDiff") ?? 0);
                props.BlossomAtYearRel = typeProps.BlossomAtYearRel.ClampToRange(props.BlossomAtYearRel + parentPlantStack.Attributes?.GetFloat("blossomAtYearRelDiff") ?? 0);
                props.LooseLeavesBelowTemp = typeProps.LooseLeavesBelowTemp.ClampToRange(props.LooseLeavesBelowTemp + parentPlantStack.Attributes?.GetFloat("looseLeavesBelowTempDiff") ?? 0);
                props.RootSizeMul = typeProps.RootSizeMul.ClampToRange(props.RootSizeMul + parentPlantStack.Attributes?.GetFloat("rootSizeMulDiff") ?? 0);
            }
        }

        private void onRootTick(float dt)
        {
            double totalDays = Api.World.Calendar.TotalDays;

            if (totalDays - LastRootTickTotalDays < stateUpdateIntervalDays) return;

            int prevIntDays = -99;
            float temp = 0;

            while (totalDays - LastRootTickTotalDays >= stateUpdateIntervalDays)
            {
                foreach (var val in propsByType)
                {
                    var props = val.Value;

                    if (props.State == EnumFruitTreeState.Dead) continue;

                    // Avoid reading the same temp over and over again
                    if (prevIntDays != (int)LastRootTickTotalDays)
                    {
                        // For roughly daily average temps
                        
                        double midday = (int)LastRootTickTotalDays + 0.5;
                        temp = Api.World.BlockAccessor.GetClimateAt(be.Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, midday).Temperature;
                        prevIntDays = (int)LastRootTickTotalDays;
                    }

                    double yearrel = (LastRootTickTotalDays % Api.World.Calendar.DaysPerYear) / Api.World.Calendar.DaysPerYear;

                    if (props.DieBelowTemp > temp)
                    {
                        props.State = EnumFruitTreeState.Dead;
                        props.lastStateChangeTotalDays = Api.World.Calendar.TotalDays;
                        break;
                    }

                    if (props.State == EnumFruitTreeState.Dormant && props.CycleType == EnumTreeCycleType.Deciduous)
                    {
                        updateVernalizedHours(props, temp);

                        if (props.vernalizedHours > props.VernalizationHours)
                        {
                            props.State = EnumFruitTreeState.DormantVernalized;
                            props.lastStateChangeTotalDays = LastRootTickTotalDays;
                        }
                    }
                    else if (props.State == EnumFruitTreeState.DormantVernalized)
                    {
                        if (temp >= 20 || (temp > 15 && LastRootTickTotalDays - props.lastCheckAtTotalDays > 3))
                        {
                            props.State = EnumFruitTreeState.Flowering;
                            props.lastStateChangeTotalDays = LastRootTickTotalDays;
                        }
                    }
                    else if (props.CycleType == EnumTreeCycleType.Evergreen && (props.State == EnumFruitTreeState.Empty || props.State == EnumFruitTreeState.Young))
                    {
                        if (props.State == EnumFruitTreeState.Young && (LastRootTickTotalDays - TreePlantedTotalDays) < nonFloweringYoungDays) continue;

                        double yearRel = (LastRootTickTotalDays / Api.World.Calendar.DaysPerYear) % 1.0;
                        if (Math.Abs(yearRel - props.BlossomAtYearRel) < 0.125)
                        {
                            props.State = EnumFruitTreeState.Flowering;
                            props.lastCheckAtTotalDays = LastRootTickTotalDays;
                        }
                    }
                    else if (props.State == EnumFruitTreeState.Flowering && props.lastStateChangeTotalDays + props.FloweringDays < LastRootTickTotalDays)
                    {
                        props.State = EnumFruitTreeState.Fruiting;
                        props.lastStateChangeTotalDays = LastRootTickTotalDays;

                        if (temp < props.EnterDormancyTemp) props.State = EnumFruitTreeState.Empty;
                    }
                    else if (props.State == EnumFruitTreeState.Fruiting && props.lastStateChangeTotalDays + props.FruitingDays < LastRootTickTotalDays)
                    {
                        props.State = EnumFruitTreeState.Ripe;
                        props.lastStateChangeTotalDays = LastRootTickTotalDays;
                    }
                    else if (props.State == EnumFruitTreeState.Ripe && props.lastStateChangeTotalDays + props.RipeDays < LastRootTickTotalDays)
                    {
                        props.State = EnumFruitTreeState.Empty;
                        props.lastStateChangeTotalDays = LastRootTickTotalDays;
                    }
                    else if (props.CycleType == EnumTreeCycleType.Deciduous && (props.State == EnumFruitTreeState.Young || props.State == EnumFruitTreeState.Empty) && temp < props.EnterDormancyTemp)
                    {
                        props.State = EnumFruitTreeState.EnterDormancy;
                        props.lastStateChangeTotalDays = LastRootTickTotalDays;
                    }
                    else if (props.CycleType == EnumTreeCycleType.Deciduous && props.State == EnumFruitTreeState.EnterDormancy && props.lastStateChangeTotalDays + 3 < LastRootTickTotalDays)
                    {
                        props.State = EnumFruitTreeState.Dormant;
                        props.lastStateChangeTotalDays = LastRootTickTotalDays;
                    }
                }

                
                LastRootTickTotalDays += stateUpdateIntervalDays;
            }
        }


        public double GetCurrentStateProgress(string treeType)
        {
            if (Api == null) return 0;
            if (propsByType.TryGetValue(treeType, out var val))
            {
                switch (val.State)
                {
                    case EnumFruitTreeState.Dormant:
                        return 0;
                    case EnumFruitTreeState.Flowering:
                        return (Api.World.Calendar.TotalDays - val.lastStateChangeTotalDays) / val.FloweringDays;
                    case EnumFruitTreeState.Fruiting:
                        return (Api.World.Calendar.TotalDays - val.lastStateChangeTotalDays) / val.FruitingDays;
                    case EnumFruitTreeState.Ripe:
                        return (Api.World.Calendar.TotalDays - val.lastStateChangeTotalDays) / val.RipeDays;
                    case EnumFruitTreeState.Empty:
                        return 0;
                }
            }

            return 0;
        }


        void updateVernalizedHours(FruitTreeProperties props, float temp) {
            if (Api.World.BlockAccessor.GetRainMapHeightAt(be.Pos) > be.Pos.Y) // Fast pre-check
            {
                Room room = roomreg?.GetRoomForPosition(be.Pos);
                int roomness = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;

                if (roomness > 0) temp += 5;
            }

            if (temp <= props.VernalizationTemp)
            {
                props.vernalizedHours += stateUpdateIntervalDays * Api.World.Calendar.HoursPerDay;
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            var subtree = tree.GetTreeAttribute("dynproprs");
            if (subtree == null) return;

            foreach (var val in subtree)
            {
                propsByType[val.Key] = new FruitTreeProperties();
                propsByType[val.Key].FromTreeAttributes(val.Value as ITreeAttribute);
            }

            LastRootTickTotalDays = tree.GetDouble("lastRootTickTotalDays");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            var subtree = new TreeAttribute();
            tree["dynproprs"] = subtree;

            tree.SetDouble("lastRootTickTotalDays", LastRootTickTotalDays);

            foreach (var val in propsByType)
            {
                var proptree = new TreeAttribute();
                val.Value.ToTreeAttributes(proptree);
                subtree[val.Key] = proptree;
            }
        }
    }
}
