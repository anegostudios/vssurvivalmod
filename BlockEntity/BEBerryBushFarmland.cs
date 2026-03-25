using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityWildFruitingBush : BlockEntityFastForwardGrowth
    {
        protected override void beginIntervalledUpdate(out FarmlandFastForwardUpdate onInterval, out FarmlandUpdateEnd onEnd)
        {
            base.beginIntervalledUpdate(out onInterval, out onEnd);

            var baseOnInterval = onInterval;

            var bhfb = GetBehavior<BEBehaviorFruitingBush>();
            if (bhfb == null) return;

            var callback = bhfb.onUpdate();
            onInterval = (hourIntervall, conds, lightGrowthSpeedFactor, growthPaused) =>
            {
                baseOnInterval?.Invoke(hourIntervall, conds, lightGrowthSpeedFactor, growthPaused);
                callback?.Invoke(hourIntervall, conds, lightGrowthSpeedFactor, growthPaused);
            };
        }

        protected override int RainHeightOffset => 0;
    }


    public class BlockEntityBerryBushFarmland : BlockEntitySoilNutrition
    {
        protected double lastOccupiedTotalDays;
        protected bool HasBerryBush => Api.World.BlockAccessor.GetBlockEntity(upPos)?.GetBehavior<BEBehaviorFruitingBush>() != null;

        protected override void beginIntervalledUpdate(out FarmlandFastForwardUpdate onInterval, out FarmlandUpdateEnd onEnd)
        {
            base.beginIntervalledUpdate(out onInterval, out onEnd);

            var baseOnInterval = onInterval;

            var bhfb = Api.World.BlockAccessor.GetBlockEntity(upPos)?.GetBehavior<BEBehaviorFruitingBush>();
            if (bhfb == null)
            {
                if (Api.World.Calendar.TotalDays - lastOccupiedTotalDays > Api.World.Calendar.DaysPerYear)
                {
                    Api.World.BlockAccessor.RemoveBlockEntity(Pos);
                }
                return;
            }

            lastOccupiedTotalDays = Api.World.Calendar.TotalDays;

            var callback = bhfb.onUpdate();
            onInterval = (hourIntervall, conds, lightGrowthSpeedFactor, growthPaused) =>
            {
                baseOnInterval?.Invoke(hourIntervall, conds, lightGrowthSpeedFactor, growthPaused);
                callback?.Invoke(hourIntervall, conds, lightGrowthSpeedFactor, growthPaused);
            };
        }

        protected override bool ConsidersMoistureLevels => false;
        protected override int RainHeightOffset => HasBerryBush ? 1 : 0;
        protected override bool RecoverFertility => !HasBerryBush;

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            lastOccupiedTotalDays = tree.GetDouble("lastOccupiedTotalDays");
            base.FromTreeAttributes(tree, worldForResolving);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetDouble("lastOccupiedTotalDays", lastOccupiedTotalDays);
            base.ToTreeAttributes(tree);
        }

    }
}
