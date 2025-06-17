using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockHenbox : Block
    {
        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            if (Variant["eggCount"] == "empty") return System.Array.Empty<WorldInteraction>();

            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-collect-eggs",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }
    }
}
