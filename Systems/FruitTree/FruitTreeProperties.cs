using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public delegate void FruitingStateChangeDelegate(EnumFruitTreeState nowState);

    public enum EnumTreeCycleType
    {
        Deciduous,
        Evergreen
    }

    public class FruitTreeTypeProperties
    {
        public NatFloat VernalizationHours = NatFloat.createUniform(100, 10);
        public NatFloat VernalizationTemp = NatFloat.createUniform(1, 1);
        public NatFloat FloweringDays = NatFloat.createUniform(3, 1.5f);
        public NatFloat FruitingDays = NatFloat.createUniform(6, 1.5f);
        public NatFloat RipeDays = NatFloat.createUniform(3, 1.5f);
        public NatFloat GrowthStepDays = NatFloat.createUniform(2, 0.5f);
        public BlockDropItemStack[] FruitStacks;
        public EnumTreeCycleType CycleType = EnumTreeCycleType.Deciduous;

        public NatFloat DieBelowTemp = NatFloat.createUniform(-20, -5);

        // For decidious trees
        public NatFloat LeaveDormancyTemp = NatFloat.createUniform(20, 0);
        public NatFloat EnterDormancyTemp = NatFloat.createUniform(-2, 0);

        // For evergreen trees
        public NatFloat LooseLeavesBelowTemp = NatFloat.createUniform(0, 0);
        public NatFloat BlossomAtYearRel = NatFloat.createUniform(0.4f, 0);

        public NatFloat RootSizeMul = NatFloat.createUniform(1f, 0);

        public float CuttingRootingChance = 0.25f;
        public float CuttingGraftChance = 0.5f;
    }


    public class FruitTreeProperties
    {
        public float VernalizationHours;
        public float VernalizationTemp;
        public float FloweringDays;
        public float FruitingDays;
        public float RipeDays;
        public float GrowthStepDays;
        public float LeaveDormancyTemp;
        public float EnterDormancyTemp;
        public float DieBelowTemp;
        public BlockDropItemStack[] FruitStacks;
        public double lastCheckAtTotalDays = 0;
        public double lastStateChangeTotalDays = 0;
        public double vernalizedHours = 0;
        public EnumTreeCycleType CycleType = EnumTreeCycleType.Deciduous;
        public float LooseLeavesBelowTemp = -10;
        public float BlossomAtYearRel = 0.3f;
        public float RootSizeMul;


        public event FruitingStateChangeDelegate OnFruitingStateChange;
        protected EnumFruitTreeState state = EnumFruitTreeState.Young;

        public EnumFruitTreeState State
        {
            get
            {
                return state;
            }
            set
            {
                bool changed = state != value;
                state = value;

                if (changed) OnFruitingStateChange?.Invoke(state);
            }
        }


        public void FromTreeAttributes(ITreeAttribute tree)
        {
            State = (EnumFruitTreeState)tree.GetInt("rootFruitTreeState", (int)EnumFruitTreeState.Empty);
            lastCheckAtTotalDays = tree.GetDouble("lastCheckAtTotalDays");
            vernalizedHours = tree.GetDouble("vernalizedHours");

            FloweringDays = tree.GetFloat("floweringDays");
            FruitingDays = tree.GetFloat("fruitingDays");
            RipeDays = tree.GetFloat("ripeDays");
            GrowthStepDays = tree.GetFloat("growthStepDays");
            RootSizeMul = tree.GetFloat("rootSizeMul");

            DieBelowTemp = tree.GetFloat("dieBelowTemp");
            CycleType = (EnumTreeCycleType)tree.GetInt("cycleType");

            if (CycleType == EnumTreeCycleType.Deciduous)
            {
                VernalizationHours = tree.GetFloat("vernalizationHours");
                VernalizationTemp = tree.GetFloat("vernalizationTemp");
            }

            if (CycleType == EnumTreeCycleType.Evergreen)
            {
                LooseLeavesBelowTemp = tree.GetFloat("looseLeavesBelowTemp");
                BlossomAtYearRel = tree.GetFloat("blossomAtYearRel");
            }
        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetInt("rootFruitTreeState", (int)State);
            tree.SetDouble("lastCheckAtTotalDays", lastCheckAtTotalDays);
            tree.SetDouble("vernalizedHours", vernalizedHours);

            tree.SetFloat("floweringDays", FloweringDays);
            tree.SetFloat("fruitingDays", FruitingDays);
            tree.SetFloat("ripeDays", RipeDays);
            tree.SetFloat("growthStepDays", GrowthStepDays);
            tree.SetFloat("rootSizeMul", RootSizeMul);
            
            tree.SetFloat("dieBelowTemp", DieBelowTemp);
            tree.SetInt("cycleType", (int)CycleType);

            if (CycleType == EnumTreeCycleType.Deciduous)
            {
                tree.SetFloat("vernalizationHours", VernalizationHours);
                tree.SetFloat("vernalizationTemp", VernalizationTemp);
            }
            if (CycleType == EnumTreeCycleType.Evergreen)
            {
                tree.SetFloat("looseLeavesBelowTemp", LooseLeavesBelowTemp);
                tree.SetFloat("blossomAtYearRel", BlossomAtYearRel);
            }
        }
    }
}
 