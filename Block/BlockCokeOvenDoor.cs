using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCokeOvenDoor : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockPos pos = blockSel.Position;

            if (Variant["state"] == "closed")
            {
                world.BlockAccessor.ExchangeBlock(world.GetBlock(CodeWithVariant("state", "opened")).Id, pos);
                if (world.Side == EnumAppSide.Server) world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                world.PlaySoundAt(new AssetLocation("sounds/block/cokeovendoor-open"), pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true);
            } else
            {
                world.BlockAccessor.ExchangeBlock(world.GetBlock(CodeWithVariant("state", "closed")).Id, pos);
                if (world.Side == EnumAppSide.Server) world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                world.PlaySoundAt(new AssetLocation("sounds/block/cokeovendoor-close"), pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true);
            }

            return true;
        }


    }
}
