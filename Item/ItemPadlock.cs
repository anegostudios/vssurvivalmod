using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ItemPadlock : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {

            if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position).HasBehavior<BlockBehaviorLockable>())
            {
                ModSystemBlockReinforcement modBre = byEntity.World.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();

                IPlayer player = (byEntity as EntityPlayer).Player;

                if (!modBre.TryLock(blockSel.Position, player, this.Code.ToString()))
                {
                    (byEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "cannotlock", Lang.Get("ingameerror-cannotlock"));
                } else
                {
                    (byEntity.World.Api as ICoreClientAPI)?.ShowChatMessage(Lang.Get("lockapplied"));
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}
