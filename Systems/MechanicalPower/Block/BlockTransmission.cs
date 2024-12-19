using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockTransmission : BlockMPBase
    {
        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = LastCodePart();

            return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return IsOrientedTo(face);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            foreach (BlockFacing face in BlockFacing.HORIZONTALS)
            {
                BlockPos pos = blockSel.Position.AddCopy(face);

                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos).GetInterface<IMechanicalPowerBlock>(world, pos);
                if (block != null)
                {
                    BlockFacing faceOpposite = face.Opposite;
                    if (block.HasMechPowerConnectorAt(world, pos, faceOpposite))
                    {
                        AssetLocation loc;
                        if (face == BlockFacing.EAST || face == BlockFacing.WEST)
                        {
                            loc = new AssetLocation(FirstCodePart() + "-we");
                        }
                        else
                        {
                            loc = new AssetLocation(FirstCodePart() + "-ns");
                        }
                        Block toPlaceBlock = world.GetBlock(loc);

                        if (toPlaceBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
                        {
                            block.DidConnectAt(world, pos, faceOpposite);
                            WasPlaced(world, blockSel.Position, face);

                            //Test for connection on opposite side as well
                            pos = blockSel.Position.AddCopy(faceOpposite);
                            block = world.BlockAccessor.GetBlock(pos).GetInterface<IMechanicalPowerBlock>(world, pos);
                            if (block != null && block.HasMechPowerConnectorAt(world, pos, face))
                            {
                                block.DidConnectAt(world, pos, face);
                                WasPlaced(world, blockSel.Position, faceOpposite);
                            }

                            return true;
                        }
                    }
                }
            }

            //no mech power connectors adjacent
            if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
            {
                WasPlaced(world, blockSel.Position, null);
                return true;
            }

            return false;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {

        }

        public override MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            BEBehaviorMPTransmission be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPTransmission>();
            if (be == null || !be.engaged) return null;
            return be.Network;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEBehaviorMPTransmission be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPTransmission>();
            be?.CheckEngaged(world.BlockAccessor, true);
        }
    }
}
