using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockAngledGears : BlockMPBase
    {
        public bool IsDeadEnd()
        {
            return LastCodePart().Length == 1;
        }

        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = LastCodePart();

            return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
        }

        public override bool HasConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (IsDeadEnd())
            {
                BlockFacing nowFace = BlockFacing.FromFirstLetter(LastCodePart()[0]);
                if (nowFace.IsAdjacent(face))
                {
                    return true;
                }
            }

            return IsOrientedTo(face);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (IsDeadEnd())
            {
                BlockFacing nowFace = BlockFacing.FromFirstLetter(LastCodePart()[0]);
                if (nowFace.IsAdjacent(face))
                {
                    AssetLocation loc = new AssetLocation(FirstCodePart() + "-" + LastCodePart() + face.Code[0]);
                    Block toPlaceBlock = world.GetBlock(loc);

                    if (toPlaceBlock == null)
                    {
                        loc = new AssetLocation(FirstCodePart() + "-" + face.Code[0] + LastCodePart());
                        toPlaceBlock = world.GetBlock(loc);
                    }


                    MechanicalNetwork nw = GetNetwork(world, pos);

                    world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, pos);

                    BEMPBase be = (world.BlockAccessor.GetBlockEntity(pos) as BEMPBase);
                    be.SetNetwork(nw);
                }
            }
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                BlockPos pos = blockSel.Position.AddCopy(face);
                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                if (block != null)
                {
                    if (block.HasConnectorAt(world, pos, face.GetOpposite())) {

                        Block toPlaceBlock = world.GetBlock(new AssetLocation(FirstCodePart() + "-" + face.Code[0]));
                        world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);

                        block.DidConnectAt(world, pos, face.GetOpposite());
                        WasPlaced(world, blockSel.Position, face, block);

                        return true;
                    }
                }
            }

            failureCode = "requiresaxle";

            return false;
        }


        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string orients = LastCodePart();

            BlockFacing[] facings;
            facings = orients.Length == 1 ? new BlockFacing[] { BlockFacing.FromFirstLetter(orients[0]) } : new BlockFacing[] { BlockFacing.FromFirstLetter(orients[0]), BlockFacing.FromFirstLetter(orients[1]) };

            List<BlockFacing> lostFacings = new List<BlockFacing>();

            foreach (BlockFacing facing in facings)
            {
                BlockPos npos = pos.AddCopy(facing);
                IMechanicalPowerBlock nblock = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;

                if (nblock == null || !nblock.HasConnectorAt(world, npos, facing.GetOpposite()))
                {
                    lostFacings.Add(facing);
                }
            }

            if (lostFacings.Count == orients.Length)
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            if (lostFacings.Count > 0)
            {
                MechanicalNetwork nw = GetNetwork(world, pos);

                orients = orients.Replace("" + lostFacings[0].Code[0], "");
                Block toPlaceBlock = world.GetBlock(new AssetLocation(FirstCodePart() + "-" + orients));
                world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, pos);

                BEMPBase be = (world.BlockAccessor.GetBlockEntity(pos) as BEMPBase);
                be.SetNetwork(nw);
            }

        }

    }
}
