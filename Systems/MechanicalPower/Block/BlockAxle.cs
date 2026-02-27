using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{

    public class BlockAxle : BlockMPBase
    {
        Cuboidf[] fullBox = [Cuboidf.Default()];
        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (GetBEBehavior<BlockEntityBehaviorCoverable>(pos)?.WallStack != null) return fullBox;
            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (GetBEBehavior<BlockEntityBehaviorCoverable>(pos)?.WallStack != null) return fullBox;
            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = LastCodePart();

            return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            return IsOrientedTo(face);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                BlockPos pos = blockSel.Position.AddCopy(face);

                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                if (block != null)
                {
                    BlockFacing faceOpposite = face.Opposite;
                    if (block.HasMechPowerConnectorAt(world, pos, faceOpposite, this))
                    {
                        AssetLocation loc = CodeWithVariant("rotation", "" + faceOpposite.Code[0] + face.Code[0]);
                        Block toPlaceBlock = world.GetBlock(loc);
                        if (toPlaceBlock == null)
                        {
                            loc = CodeWithVariant("rotation", "" + face.Code[0] + faceOpposite.Code[0]);
                            toPlaceBlock = world.GetBlock(loc);
                        }

                        if (toPlaceBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
                        {
                            block.DidConnectAt(world, pos, faceOpposite);
                            WasPlaced(world, blockSel.Position, face);

                            // Test for connection on opposite side as well
                            pos = blockSel.Position.AddCopy(faceOpposite);
                            block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                            if (block != null && block.HasMechPowerConnectorAt(world, pos, face, this))
                            {
                                block.DidConnectAt(world, pos, face);
                                WasPlaced(world, blockSel.Position, faceOpposite);
                            }

                            return true;
                        }
                    }
                }
            }


            if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
            {
                WasPlaced(world, blockSel.Position, null);
                return true;
            }
            return false;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEBehaviorMPAxle bempaxle = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAxle>();
            if (bempaxle != null && !BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, bempaxle.Block, pos))
            {
                bool connected = false;
                foreach (BlockFacing face in BlockFacing.ALLFACES)
                {
                    BlockPos npos = pos.AddCopy(face);
                    IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
                    bool prevConnected = connected;
                    if (block != null && block.HasMechPowerConnectorAt(world, pos, face.Opposite, this) && world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>()?.disconnected == false) connected = true;
                    BlockAngledGears blockagears = block as BlockAngledGears;
                    if (blockagears == null) continue;
                    if (blockagears.Facings.Contains(face.Opposite) && blockagears.Facings.Length == 1)
                    {
                        world.BlockAccessor.BreakBlock(npos, null);
                        connected = prevConnected;  //undo connected = true in this situation
                    }
                }
                if (!connected)
                {
                    world.BlockAccessor.BreakBlock(pos, null);
                }
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }


        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            
        }
    }
}
