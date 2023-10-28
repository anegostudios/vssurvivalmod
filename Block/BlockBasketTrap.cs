using ProperVersion;
using System;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{

    public class BlockBasketTrap : Block
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}