using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockMPConsumer : BlockMPBase
    {
        public BlockFacing Facing;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Facing = BlockFacing.FromCode(Variant["side"]);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            bool ok = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);

            if (ok)
            {
                var facing = BlockFacing.FromCode(api.World.BlockAccessor.GetBlock(blockSel.Position).Variant["side"]);
                tryConnect(world, byPlayer, blockSel.Position, facing);
            }

            return ok;
        }


        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forblock)
        {
            return face == Facing;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
        }
    }
}
