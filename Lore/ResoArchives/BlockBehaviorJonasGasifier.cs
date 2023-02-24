using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorJonasGasifier : BlockBehavior
    {
        public BlockBehaviorJonasGasifier(Block block) : base(block)
        {
        }

        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "gasifierBlockInteractions", () =>
            {
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, false);

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-addcoal",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = new ItemStack[] { new ItemStack(api.World.GetItem(new AssetLocation("charcoal")), 2) }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-forge-ignite",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            var bef = api.World.BlockAccessor.GetBlockEntity(bs.Position)?.GetBehavior<BEBehaviorJonasGasifier>();
                            if (bef!= null && bef.HasFuel && !bef.Lit)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    }
                };
            });
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling));
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            var beh = block.GetBEBehavior<BEBehaviorJonasGasifier>(blockSel.Position);
            beh?.Interact(byPlayer, blockSel);
            handling = EnumHandling.PreventDefault;
            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }
    }
}
