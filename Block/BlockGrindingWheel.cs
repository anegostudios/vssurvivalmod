using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockGrindingWheel : BlockMPBase
    {
        public BlockFacing Facing;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Facing = BlockFacing.FromCode(Variant["side"]);
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            WasPlaced(world, blockPos, null);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position).OnInteractStep(secondsUsed, byPlayer, blockSel);
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position).OnInteractStop(secondsUsed, byPlayer, blockSel);
            base.OnBlockInteractStop(secondsUsed, world, byPlayer, blockSel);
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            GetBlockEntity<BlockEntityGrindingWheel>(blockSel.Position).OnInteractStop(secondsUsed, byPlayer, blockSel);
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }


        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forblock)
        {
            return face.GetCW() == Facing || face.GetCCW() == Facing;
        }
    }
}
