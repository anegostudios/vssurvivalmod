using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class ItemOar : Item
    {
        public override string GetHeldTpIdleAnimation(ItemSlot activeHotbarSlot, Entity forEntity, EnumHand hand)
        {
            return (forEntity as EntityAgent)?.MountedOn == null ? base.GetHeldTpIdleAnimation(activeHotbarSlot, forEntity, hand) : null;
        }

    }
}
