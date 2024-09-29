using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockBrake : BlockMPBase
    {
        public bool IsOrientedTo(BlockFacing facing)
        {
            return facing.Code == Variant["side"];
        }


        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            BlockFacing ownFacing = BlockFacing.FromCode(Variant["side"]);
            BlockFacing leftFacing = BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(ownFacing.HorizontalAngleIndex - 1, 4)];
            BlockFacing rightFacing = BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(ownFacing.HorizontalAngleIndex + 1, 4)];

            return face == leftFacing || face == rightFacing;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing[] horVer = Block.SuggestedHVOrientation(byPlayer, blockSel);
            AssetLocation blockCode = CodeWithParts(horVer[0].Code);
            Block orientedBlock = world.BlockAccessor.GetBlock(blockCode);


            BlockFacing ownFacing = BlockFacing.FromCode(orientedBlock.Variant["side"]);

            BlockFacing leftFacing = BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(ownFacing.HorizontalAngleIndex - 1, 4)];
            BlockFacing rightFacing = BlockFacing.HORIZONTALS_ANGLEORDER[GameMath.Mod(ownFacing.HorizontalAngleIndex + 1, 4)];

            BlockPos leftPos = blockSel.Position.AddCopy(leftFacing);
            IMechanicalPowerBlock leftBlock = world.BlockAccessor.GetBlock(leftPos).GetInterface<IMechanicalPowerBlock>(world, leftPos);
            if (leftBlock != null) return DoPlaceMechBlock(world, byPlayer, itemstack, blockSel, orientedBlock, leftBlock, leftFacing);

            BlockPos rightPos = blockSel.Position.AddCopy(rightFacing);
            IMechanicalPowerBlock rightBlock = world.BlockAccessor.GetBlock(rightPos).GetInterface<IMechanicalPowerBlock>(world, rightPos);
            if (rightBlock != null) return DoPlaceMechBlock(world, byPlayer, itemstack, blockSel, orientedBlock, rightBlock, rightFacing);


            BlockFacing frontFacing = ownFacing;
            BlockFacing backFacing = ownFacing.Opposite;
            Block rotBlock = world.GetBlock(orientedBlock.CodeWithVariant("side", leftFacing.Code));

            BlockPos frontPos = blockSel.Position.AddCopy(frontFacing);
            IMechanicalPowerBlock frontBlock = world.BlockAccessor.GetBlock(frontPos).GetInterface<IMechanicalPowerBlock>(world, frontPos);
            if (frontBlock != null) return DoPlaceMechBlock(world, byPlayer, itemstack, blockSel, rotBlock, frontBlock, frontFacing);

            BlockPos backPos = blockSel.Position.AddCopy(backFacing);
            IMechanicalPowerBlock backBlock = world.BlockAccessor.GetBlock(backPos).GetInterface<IMechanicalPowerBlock>(world, backPos);
            if (backBlock != null) return DoPlaceMechBlock(world, byPlayer, itemstack, blockSel, rotBlock, backBlock, backFacing);


            if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
            {
                WasPlaced(world, blockSel.Position, null);
                return true;
            }
            return false;
        }

        private bool DoPlaceMechBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, Block block, IMechanicalPowerBlock connectingBlock, BlockFacing connectingFace)
        {
            if (block.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
            {
                connectingBlock.DidConnectAt(world, blockSel.Position.AddCopy(connectingFace), connectingFace.Opposite);
                WasPlaced(world, blockSel.Position, connectingFace);
                return true;
            }

            return false;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEBehaviorMPAxle bempaxle = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();
            if (bempaxle != null && !BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, bempaxle.Block, pos))
            {
                foreach (BlockFacing face in BlockFacing.HORIZONTALS)
                {
                    BlockPos npos = pos.AddCopy(face);
                    BlockAngledGears blockagears = world.BlockAccessor.GetBlock(npos) as BlockAngledGears;
                    if (blockagears == null) continue;
                    if (blockagears.Facings.Contains(face.Opposite) && blockagears.Facings.Length == 1)
                    {
                        world.BlockAccessor.BreakBlock(npos, null);
                    }
                }
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }


        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {

        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BEBrake bebrake = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEBrake;
            return bebrake?.OnInteract(byPlayer) == true;
        }

    }
}
