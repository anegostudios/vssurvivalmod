using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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
