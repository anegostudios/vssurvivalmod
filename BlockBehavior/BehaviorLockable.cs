using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorLockable : BlockBehavior
    {
        public BlockBehaviorLockable(Block block) : base(block)
        {
            
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            ModSystemBlockReinforcement bre = world.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            if (bre.IsLockedForInteract(blockSel.Position, byPlayer))
            {
                if (world.Side == EnumAppSide.Client)
                {
                    (world.Api as ICoreClientAPI).TriggerIngameError(this, "locked", Lang.Get("ingameerror-locked"));
                }

                handling = EnumHandling.PreventSubsequent;
                return false;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel, ref handling);
        }

    }
}
