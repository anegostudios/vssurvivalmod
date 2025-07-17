using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Passes functions to the <see cref="BEBehaviorJonasHydraulicPump"/> block entity behavior, and therefore requires that.
    /// Uses the code "JonasHydraulicPump". This behavior has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{ "name": "JonasHydraulicPump" }
	///]
    /// </code></example>
    [DocumentAsJson]
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
