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

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forblock)
        {
            return face == Facing;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
        }
    }
}
