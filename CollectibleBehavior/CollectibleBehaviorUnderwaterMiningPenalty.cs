using Vintagestory.API.Common;

namespace Vintagestory.GameContent;

public class CollectibleBehaviorUnderwaterMiningPenalty(CollectibleObject collObj) : CollectibleBehavior(collObj)
{
    public override float GetMiningSpeed(ItemStack itemstack, BlockSelection blockSel, Block block, IPlayer forPlayer, ref EnumHandling bhHandling)
    {
        if (!forPlayer.Entity.Swimming) return 1f;

        bhHandling = EnumHandling.Handled;
        return (float)forPlayer.Entity.World.Config.GetDecimal("underwaterMiningSpeedMultiplier", 0.1);
    }
}
