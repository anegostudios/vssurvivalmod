using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockCharcoalPit : Block
    { 
        //Vec3f[] basePos;
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            interactions = ObjectCacheUtil.GetOrCreate(api, "charcoalpitInteractions", () =>
            {
                List<ItemStack> canIgniteStacks = BlockBehaviorCanIgnite.CanIgniteStacks(api, true);

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-firepit-ignite",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "shift",
                        Itemstacks = canIgniteStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) => {
                            BlockEntityCharcoalPit becp = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityCharcoalPit;
                            if (becp?.Lit == false)
                            {
                                return wi.Itemstacks;
                            }
                            return null;
                        }
                    }
                };
            });
        }


        public override EnumIgniteState OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting)
        {
            BlockEntityCharcoalPit becp = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCharcoalPit;
            if (becp == null || becp.Lit) return EnumIgniteState.NotIgnitablePreventDefault;

            return secondsIgniting > 3 ? EnumIgniteState.IgniteNow : EnumIgniteState.Ignitable;
        }

        public override void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            BlockEntityCharcoalPit becp = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCharcoalPit;

            if (becp != null && !becp.Lit) becp.IgniteNow();

            handling = EnumHandling.PreventDefault;
        }


        public override bool ShouldReceiveClientParticleTicks(IWorldAccessor world, IPlayer player, BlockPos pos, out bool isWindAffected)
        {
            bool val = base.ShouldReceiveClientParticleTicks(world, player, pos, out _);
            isWindAffected = true;

            return val;
        }



        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
