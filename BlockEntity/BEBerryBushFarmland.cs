using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    // TODO: Delete self when barren for too long
    public class BlockEntityBerryBushFarmland : BlockEntitySoilNutrition
    {
        protected bool HasBerryBush => Api.World.BlockAccessor.GetBlockEntity(upPos)?.GetBehavior<BEBehaviorFruitingBush>() != null;

        protected override FarmlandFastForwardUpdate onUpdate()
        {
            var bhfb = Api.World.BlockAccessor.GetBlockEntity(upPos)?.GetBehavior<BEBehaviorFruitingBush>();
            if (bhfb == null) return null;

            return bhfb.onUpdate();
        }


        protected override int RainHeightOffset => HasBerryBush ? 1 : 0;

        protected override bool RecoverFertility => !HasBerryBush;

    }
}
