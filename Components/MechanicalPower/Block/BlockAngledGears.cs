using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockAngledGears : BlockMPBase
    {
        public string Orientation => Variant["orientation"];

        public BlockFacing[] Facings
        {
            get
            {
                string dirs = Orientation;
                BlockFacing[] facings = new BlockFacing[dirs.Length];
                for (int i = 0; i < dirs.Length; i++)
                {
                    facings[i] = BlockFacing.FromFirstLetter(dirs[i]);
                }

                return facings;
            }
        }

        public bool IsDeadEnd()
        {
            return Orientation.Length == 1;
        }

        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = Orientation;

            return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
        }

        public override bool HasConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (IsDeadEnd())
            {
                BlockFacing nowFace = BlockFacing.FromFirstLetter(Orientation[0]);
                if (nowFace.IsAdjacent(face))
                {
                    return true;
                }
            }

            return IsOrientedTo(face);
        }


        public Block getGearBlock(IWorldAccessor world, BlockFacing facing, BlockFacing adjFacing = null)
        {
            if (adjFacing == null)
            {
                return world.GetBlock(new AssetLocation(FirstCodePart() + "-" + facing.Code[0]));
            }

            AssetLocation loc = new AssetLocation(FirstCodePart() + "-" + adjFacing.Code[0] + facing.Code[0]);
            Block toPlaceBlock = world.GetBlock(loc);

            if (toPlaceBlock == null)
            {
                loc = new AssetLocation(FirstCodePart() + "-" + facing.Code[0] + adjFacing.Code[0]);
                toPlaceBlock = world.GetBlock(loc);
            }

            return toPlaceBlock;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (IsDeadEnd())
            {
                BlockFacing nowFace = BlockFacing.FromFirstLetter(Orientation[0]);
                if (nowFace.IsAdjacent(face))
                {
                    Block toPlaceBlock = getGearBlock(world, Facings[0], face);
                    MechanicalNetwork nw = GetNetwork(world, pos);

                    (toPlaceBlock as BlockMPBase).ExchangeBlockAt(world, pos);

                    BEBehaviorMPBase be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>();
                    be.JoinNetwork(nw);
                }
            }
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing firstFace = null;
            BlockFacing secondFace = null;

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                BlockPos pos = blockSel.Position.AddCopy(face);
                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                if (block != null && block.HasConnectorAt(world, pos, face.GetOpposite()))
                {
                    if (firstFace == null)
                    {
                        firstFace = face;
                    } else
                    {
                        if (face.IsAdjacent(firstFace))
                        {
                            secondFace = face;
                            break;
                        }
                    }
                }
            }


            if (firstFace != null)
            {
                Block toPlaceBlock = getGearBlock(world, firstFace, secondFace);
                world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);

                BlockPos firstPos = blockSel.Position.AddCopy(firstFace);
                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(firstPos) as IMechanicalPowerBlock;
                block.DidConnectAt(world, firstPos, firstFace.GetOpposite());

                if (secondFace != null)
                {
                    BlockPos secondPos = blockSel.Position.AddCopy(secondFace);
                    block = world.BlockAccessor.GetBlock(secondPos) as IMechanicalPowerBlock;
                    block.DidConnectAt(world, secondPos, secondFace.GetOpposite());
                }

                WasPlaced(world, blockSel.Position, firstFace, block);
                return true;
            }

            failureCode = "requiresaxle";

            return false;
        }


        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string orients = Orientation;

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

                BEBehaviorMPBase be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>(); 
                be.JoinNetwork(nw);
            }

        }

    }
}
