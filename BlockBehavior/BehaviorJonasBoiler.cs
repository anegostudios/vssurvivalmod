﻿using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Provides interaction for the JonasBoilerDoor block entity behavior.
    /// Uses the "JonasBoilerDoor" code. This behavior has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{ "name": "JonasBoilerDoor" }
	///]
    /// </code></example>
    [DocumentAsJson]
    public class BlockBehaviorJonasBoilerDoor : BlockBehavior
    {
        

        public BlockBehaviorJonasBoilerDoor(Block block) : base(block)
        {
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            var beh = block.GetBEBehavior<BEBehaviorJonasBoilerDoor>(blockSel.Position);
            beh?.Interact(byPlayer, blockSel);
            handling = EnumHandling.PreventDefault;
            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
        {
            if (blockSel == null) return false;

            
            return true;
        }

    }
}
