using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorJonasHydraulicPump : BlockBehavior
    {
        public BlockBehaviorJonasHydraulicPump(Block block) : base(block)
        {
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            var beh = block.GetBEBehavior<BEBehaviorJonasHydraulicPump>(blockSel.Position);
            beh?.Interact(byPlayer, blockSel);
            handling = EnumHandling.PreventDefault;
            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }

    }
}
