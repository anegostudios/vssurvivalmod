using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// An invisible "fake" block used to fill the space taken by the large bellows - provides collision boxes and prevents other blocks from being placed here
    /// </summary>
    public class BlockMPMultiblockBellows : BlockMPMultiblockBase
    {
        const int lowerSelectionBoxIndex = 0;
        const int rearHandleSelectionBoxIndex = 1;

        static readonly Vec3d selectionRotationOrigin = new(0.5, 0, 0.5);

        public int GetLinkagePoint(int selectionBoxIndex)
        {
            return selectionBoxIndex switch
            {
                lowerSelectionBoxIndex => BlockEntityMechPoweredBellows.LowerLinkagePoint,
                rearHandleSelectionBoxIndex => BlockEntityMechPoweredBellows.RearHandleLinkagePoint,
                _ => -1
            };
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var principalPos = blockAccessor.GetBlockEntity<BEMPMultiblock>(pos)?.Principal;
            if (principalPos == null || blockAccessor.GetBlockEntity(principalPos) is not BlockEntityBellows bellows)
            {
                return base.GetSelectionBoxes(blockAccessor, pos);
            }

            float rotateY = bellows.Block.Shape.rotateY;
            Cuboidf[] boxes = SelectionBoxes;
            if (rotateY != 0)
            {
                boxes = new Cuboidf[SelectionBoxes.Length];
                for (int i = 0; i < boxes.Length; i++)
                {
                    boxes[i] = SelectionBoxes[i].RotatedCopy(0, rotateY, 0, selectionRotationOrigin);
                }
            }
            return GetBehavior<BlockBehaviorLinkageEndpoint>()?.GetSelectionBoxes(pos, boxes) ?? boxes;
        }

        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer? byPlayer, float dropQuantityMultiplier = 1f)
        {
            BlockSelection? selection = byPlayer?.CurrentBlockSelection;
            if (selection != null && GetBehavior<BlockBehaviorLinkageEndpoint>()?.IsLinkageSelection(selection) == true) return;

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }
    }
}
