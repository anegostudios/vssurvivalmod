using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockLabeledChest : BlockGenericTypedContainer, ITexPositionSource
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

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
                        ActionLangCode = "blockhelp-chest-open",
                        MouseButton = EnumMouseButton.Right
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-sign-write",
                        HotKeyCode = "sneak",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacksList.ToArray()
                    }
                };
            });
        }



        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions;
        }
    }
}
