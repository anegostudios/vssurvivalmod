using System;
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

            IMechanicalPowerBlock leftBlock = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(leftFacing)) as IMechanicalPowerBlock;
            if (leftBlock != null) return DoPlaceMechBlock(world, byPlayer, itemstack, blockSel, orientedBlock, leftBlock, leftFacing);

            IMechanicalPowerBlock rightBlock = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(rightFacing)) as IMechanicalPowerBlock;
            if (rightBlock != null) return DoPlaceMechBlock(world, byPlayer, itemstack, blockSel, orientedBlock, rightBlock, rightFacing);


            BlockFacing frontFacing = ownFacing;
            BlockFacing backFacing = ownFacing.Opposite;
            Block rotBlock = world.GetBlock(orientedBlock.CodeWithVariant("side", leftFacing.Code));

            IMechanicalPowerBlock frontBlock = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(frontFacing)) as IMechanicalPowerBlock;
            if (frontBlock != null) return DoPlaceMechBlock(world, byPlayer, itemstack, blockSel, rotBlock, frontBlock, frontFacing);

            IMechanicalPowerBlock backBlock = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(backFacing)) as IMechanicalPowerBlock;
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
            if (bempaxle != null && !bempaxle.IsAttachedToBlock())
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
