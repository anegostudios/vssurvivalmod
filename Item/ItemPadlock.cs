using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemPadlock : Item
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "padlockInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in api.World.Blocks)
                {
                    if (block.Code == null) continue;

                    if (block.HasBehavior<BlockBehaviorLockable>(true) && block.CreativeInventoryTabs != null && block.CreativeInventoryTabs.Length > 0)
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-lock",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {

            if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position).HasBehavior<BlockBehaviorLockable>(true))
            {
                ModSystemBlockReinforcement modBre = byEntity.World.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();

                IPlayer player = (byEntity as EntityPlayer).Player;

                if (!modBre.IsReinforced(blockSel.Position))
                {
                    (byEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "cannotlock", Lang.Get("ingameerror-cannotlock-notreinforced"));
                    return;
                }

                if (!modBre.TryLock(blockSel.Position, player, this.Code.ToString()))
                {
                    (byEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "cannotlock", Lang.Get("ingameerror-cannotlock"));
                } else
                {
                    //(byEntity.World.Api as ICoreClientAPI)?.ShowChatMessage(Lang.Get("lockapplied"));
                    api.World.PlaySoundAt(new AssetLocation("sounds/tool/padlock.ogg"), player, player, false, 12);


                    slot.TakeOut(1);
                    slot.MarkDirty();
                }

                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }

    }
}
