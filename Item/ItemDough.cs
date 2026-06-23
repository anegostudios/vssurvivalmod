using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    // The functionality for creating pies has been moved to BlockBehaviorPieFormingSurface
    public class ItemDough : Item
    {
        static WorldInteraction[]? interactions = null;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI && interactions == null)
            {
                ItemStack[] tableStacks = api.World.Collectibles
                    .Where(obj => (obj as Block)?.GetBehavior<BlockBehaviorPieFormingSurface>() != null)
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

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions!.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
