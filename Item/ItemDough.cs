using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

#nullable enable

namespace Vintagestory.GameContent
{
    public class ItemDough : Item
    {
        static WorldInteraction[]? interactions = null;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI && interactions == null)
            {
                ItemStack[] tableStacks = api.World.Collectibles
                    .Where(obj => (obj as Block)?.Attributes?.IsTrue("pieFormingSurface") == true)
                    .Select(obj => new ItemStack(obj))
                    .ToArray();

                interactions = [
                    new ()
                    {
                        ActionLangCode = "heldhelp-makepie",
                        Itemstacks = tableStacks,
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                    }
                ];
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null)
            {
                var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
                if (block.Attributes?.IsTrue("pieFormingSurface") == true)
                {
                    if (slot.StackSize >= 2)
                    {
                        (api.World.GetBlock(new AssetLocation("pie-raw")) as BlockPie)?.TryPlacePie(byEntity, blockSel);
                    }
                    else
                    {
                        (api as ICoreClientAPI)?.TriggerIngameError(this, "notpieable", Lang.Get("Need at least 2 dough"));
                    }

                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions!.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
