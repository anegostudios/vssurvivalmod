using Vintagestory.API.Client;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockHenbox : Block
    {
        public string NestType;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            NestType = Attributes?["nestType"]?.AsString();
            if (NestType == null) api.Logger.Warning("BlockHenbox " + Code + " nestType attribute not set, defaulting to \"ground\"");
            NestType ??= "ground";
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            var blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityHenBox;
            if (blockEntity != null) {
                return blockEntity.OnInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            var blockEntity = world.BlockAccessor.GetBlockEntity(selection.Position) as BlockEntityHenBox;
            if (blockEntity == null || blockEntity.CountEggs() == 0) return System.Array.Empty<WorldInteraction>();

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
