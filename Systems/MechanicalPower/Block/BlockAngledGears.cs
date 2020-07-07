using System;
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

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
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

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.GetBlock(new AssetLocation("angledgears-s")));  
        }


        public Block getGearBlock(IWorldAccessor world, bool cageGear, BlockFacing facing, BlockFacing adjFacing = null)
        {
            if (adjFacing == null)
            {
                char orient = facing.Code[0];
                return world.GetBlock(new AssetLocation(FirstCodePart() + (cageGear ? "-" + orient + orient : "-" + orient)));
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
                    Block toPlaceBlock = getGearBlock(world, false, Facings[0], face);
                    MechanicalNetwork nw = GetNetwork(world, pos);

                    (toPlaceBlock as BlockMPBase).ExchangeBlockAt(world, pos);

                    BEBehaviorMPBase be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>();
                    be?.JoinNetwork(nw);
                }
            }
        }

        public bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode, Block blockExisting)
        {
            BlockMPMultiblockWood testMultiblock = blockExisting as BlockMPMultiblockWood;
            if (testMultiblock != null && !testMultiblock.IsReplacableByGear(world, blockSel.Position))
            {
                failureCode = "notreplaceable";
                return false;
            }
            return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            Block blockExisting = world.BlockAccessor.GetBlock(blockSel.Position);
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode, blockExisting))
            {
                return false;
            }

            BlockFacing firstFace = null;
            BlockFacing secondFace = null;
            BlockMPMultiblockWood largeGearEdge = blockExisting as BlockMPMultiblockWood;
            bool validLargeGear = false;
            if (largeGearEdge != null)
            {
                BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMPMultiblock;
                if (be != null) validLargeGear = be.Centre != null;
            }

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (validLargeGear && (face == BlockFacing.UP || face == BlockFacing.DOWN)) continue;
                BlockPos pos = blockSel.Position.AddCopy(face);
                IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                if (block != null && block.HasMechPowerConnectorAt(world, pos, face.GetOpposite()))
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
                BlockPos firstPos = blockSel.Position.AddCopy(firstFace);
                BlockEntity be = world.BlockAccessor.GetBlockEntity(firstPos);
                IMechanicalPowerBlock neighbour = be?.Block as IMechanicalPowerBlock;

                BEBehaviorMPAxle bempaxle = be?.GetBehavior<BEBehaviorMPAxle>();
                if (bempaxle != null && !bempaxle.IsAttachedToBlock())
                {
                    failureCode = "axlemusthavesupport";
                    return false;
                }

                if (validLargeGear) largeGearEdge.GearPlaced(world, blockSel.Position);

                Block toPlaceBlock = getGearBlock(world, validLargeGear, firstFace, secondFace);
                world.BlockAccessor.RemoveBlockEntity(blockSel.Position);  //## needed in 1.12, but not with new chunk BlockEntity Dictionary in 1.13
                world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);
                BlockEntity be1 = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                
                neighbour.DidConnectAt(world, firstPos, firstFace.GetOpposite());

                if (secondFace != null)
                {
                    BlockPos secondPos = blockSel.Position.AddCopy(secondFace);
                    neighbour = world.BlockAccessor.GetBlock(secondPos) as IMechanicalPowerBlock;
                    neighbour.DidConnectAt(world, secondPos, secondFace.GetOpposite());
                }

                WasPlaced(world, blockSel.Position, firstFace);
                return true;
            }

            failureCode = "requiresaxle";

            return false;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            string orients = Orientation;
            if (orients.Length == 2 && orients[0] == orients[1]) orients = "" + orients[0];

            BlockFacing[] facings;
            facings = orients.Length == 1 ? new BlockFacing[] { BlockFacing.FromFirstLetter(orients[0]) } : new BlockFacing[] { BlockFacing.FromFirstLetter(orients[0]), BlockFacing.FromFirstLetter(orients[1]) };

            List<BlockFacing> lostFacings = new List<BlockFacing>();

            foreach (BlockFacing facing in facings)
            {
                BlockPos npos = pos.AddCopy(facing);
                IMechanicalPowerBlock nblock = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;

                if (nblock == null || !nblock.HasMechPowerConnectorAt(world, npos, facing.GetOpposite()) || world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>()?.disconnected == true)
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
                (toPlaceBlock as BlockMPBase).ExchangeBlockAt(world, pos);

                //BEBehaviorMPBase be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>(); 
                //be.JoinNetwork(nw);
                //## TODO  check for adjacent valid facings, similar to TryPlaceBlock
            }

        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            bool preventDefault = false;
            //world.Logger.Notification("-- smallgear calling OBR");

            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockRemoved(world, pos, ref handled);
                if (handled == EnumHandling.PreventSubsequent) return;
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
            }

            if (preventDefault) return;

            world.BlockAccessor.RemoveBlockEntity(pos);
            //world.Logger.Notification("-- smallgear calling OBR 2");

            if (world.Side == EnumAppSide.Server)
            {
                //For large gear usage, allow an angled gear when broken to be replaced by a dummy block if appropriate
                string orient = Variant["orientation"];
                if (orient.Length == 2 && orient[1] == orient[0])
                {
                    //world.Logger.Notification("-- smallgear calling OGD");
                    BlockMPMultiblockWood.OnGearDestroyed(world, pos, orient[0]);
                }
            }
        }

        internal void ToPegGear(IWorldAccessor world, BlockPos pos)
        {
            string orient = Variant["orientation"];
            if (orient.Length == 2 && orient[1] == orient[0])
            {
                Block toPlaceBlock = world.GetBlock(new AssetLocation(FirstCodePart() + "-" + orient[0]));
                world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, pos);
                //world.Logger.Notification("-- bepg " + world.BlockAccessor.GetBlockEntity(pos).GetType().FullName);
                //#TODO: do a firstface/second face axle check as in TryPlaceBlock()
            }
        }
    }
}
