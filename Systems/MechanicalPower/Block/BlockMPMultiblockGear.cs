using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// An invisible "fake" block used to fill the space taken by the large gear - provides collision boxes and prevents other blocks from being placed here
    /// </summary>
    public class BlockMPMultiblockGear : BlockMPMultiblockBase
    {
        const float selectionThickness = 0.375f;
        const float collisionThickness = 0.25f;

        public static Cuboidf[]? GetOrientedRimBoxes(Block principalBlock, bool collision)
        {
            if (principalBlock is not BlockLargeGear3m largeGear) return null;

            float thickness = collision ? collisionThickness : selectionThickness;
            Cuboidf box = Cuboidf.Default();
            box.Expand(BlockLargeGear3m.GetStubFacing(largeGear), thickness - 1f);   // Shrink from the stub side, the disc sits on the opposite face
            return [box];
        }

        Cuboidf[]? getOrientedRimBoxes(IBlockAccessor blockAccessor, BlockPos pos, bool collision)
        {
            var principalPos = blockAccessor.GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            return principalPos == null ? null : GetOrientedRimBoxes(blockAccessor.GetBlock(principalPos), collision);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return getOrientedRimBoxes(blockAccessor, pos, false) ?? base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return getOrientedRimBoxes(blockAccessor, pos, true) ?? base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override bool IsReplacableBy(Block block)
        {
            return block is BlockAngledGears || base.IsReplacableBy(block);
        }

        public bool IsReplacableByGear(IWorldAccessor world, BlockPos pos)
        {
            var principalPos = world.BlockAccessor.GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            if (principalPos == null) return true;
            return (world.BlockAccessor.GetBlockEntity(principalPos) as IGearAcceptor)?.CanAcceptGear(pos) != false;
        }

        public BlockEntity? GearPlaced(IWorldAccessor world, BlockPos pos)
        {
            var principalPos = world.BlockAccessor.GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            if (principalPos == null) return null;

            BlockEntity? be = world.BlockAccessor.GetBlockEntity(principalPos);
            if (be is IGearAcceptor beg) beg.AddGear(pos);
            else world.Logger.Notification("no gear acceptor");
            return be;
        }

        public static void OnGearDestroyed(IWorldAccessor world, BlockPos pos, char orient)
        {
            BlockPos posCenter = pos.AddCopy(BlockFacing.FromFirstLetter(orient).Opposite);
            if (world.BlockAccessor.GetBlockEntity(posCenter) is IGearAcceptor beg)
            {
                beg.RemoveGearAt(pos);
                world.BlockAccessor.SetBlock(world.GetBlock("mpmultiblockwood")!.BlockId, pos);
                (world.BlockAccessor.GetBlockEntity(pos) as BEMPMultiblock)?.Principal = posCenter;
            }
            else world.Logger.Notification($"no LG found at {posCenter} from {pos}");
        }
    }
}
