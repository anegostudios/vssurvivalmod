using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockTicker : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityTicker bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityTicker;
            if (bec?.OnInteract(byPlayer) == false)
            {
                return base.OnBlockInteractStart(world, byPlayer, blockSel);
            }

            return true;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "Set (requires Creative mode)",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }

    }
}
