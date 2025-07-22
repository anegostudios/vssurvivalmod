using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockLabeledChest : BlockGenericTypedContainer
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            PlacedPriorityInteract = true;

            if (api.Side != EnumAppSide.Client) return;

            interactions = ObjectCacheUtil.GetOrCreate(api, "signBlockInteractions", () =>
            {
                List<ItemStack> stacksList = new List<ItemStack>();

                foreach (CollectibleObject collectible in api.World.Collectibles)
                {
                    if (collectible.Attributes?["pigment"].Exists == true)
                    {
                        stacksList.Add(new ItemStack(collectible));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-sign-write",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacksList.ToArray()
                    }
                };
            });
        }



        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
