using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockDisplayCase : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "displayCaseInteractions", () =>
            {
                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        MouseButton = EnumMouseButton.Right,
                        ActionLangCode = "blockhelp-displaycase-place",
                    },
                    new WorldInteraction()
                    {
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand=true,
                        ActionLangCode = "blockhelp-displaycase-remove",
                    }
                };
            });

            if (capi != null)
            {
                capi.Event.RegisterEventBusListener(OnEventBusEvent);
            }
        }

        private void OnEventBusEvent(string eventName, ref EnumHandling handling, IAttribute data)
        {
            if (eventName == "oncloseedittransforms")
            {
                ObjectCacheUtil.GetOrCreate(api, "meshesDisplay-" + "displaycase", () => new Dictionary<string, MeshData>()).Clear();
            }
        }

        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityDisplayCase bedc = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityDisplayCase;
            if (bedc != null) return bedc.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
