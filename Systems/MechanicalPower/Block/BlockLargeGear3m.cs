using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockLargeGear3m : BlockMPBase
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            //TODO - change model, start with half axle but extend axle upwards to full block height if connection above
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            if (face == BlockFacing.UP || face == BlockFacing.DOWN) return true;
            return (world.BlockAccessor.GetBlockEntity(pos) is BELargeGear3m beg) && beg.HasGearAt(world.Api, pos.AddCopy(face));
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            List<BlockPos> smallGears = new List<BlockPos>();
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode, smallGears))
            {
                return false;
            }

            bool ok = base.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
            if (ok)
            {
                int dx, dz;
                BlockEntity beOwn = world.BlockAccessor.GetBlockEntity(blockSel.Position);
                List<BlockFacing> connections = new List<BlockFacing>();

                foreach(var smallGear in smallGears)
                {
                    dx = smallGear.X - blockSel.Position.X;
                    dz = smallGear.Z - blockSel.Position.Z;
                    char orient = 'n';
                    if (dx == 1) orient = 'e';
                    else if (dx == -1) orient = 'w';
                    else if (dz == 1) orient = 's';
                    BlockMPBase toPlaceBlock = world.GetBlock(new AssetLocation("angledgears-" + orient + orient)) as BlockMPBase;
                    BlockFacing bf = BlockFacing.FromFirstLetter(orient);
                    toPlaceBlock.ExchangeBlockAt(world, smallGear);
                    toPlaceBlock.DidConnectAt(world, smallGear, bf.Opposite);
                    connections.Add(bf);
                    //IGearAcceptor beg = beOwn as IGearAcceptor;
                    //if (beg == null) world.Logger.Error("large gear wrong block entity type - not a gear acceptor");
                    //beg?.AddGear(smallGear);
                }
                PlaceFakeBlocks(world, blockSel.Position, smallGears);

                BEBehaviorMPBase beMechBase = beOwn?.GetBehavior<BEBehaviorMPBase>();
                BlockPos pos = blockSel.Position.DownCopy();
                if (world.BlockAccessor.GetBlock(pos) is IMechanicalPowerBlock block && block.HasMechPowerConnectorAt(world, pos, BlockFacing.UP))
                {
                    block.DidConnectAt(world, pos, BlockFacing.UP);
                    connections.Add(BlockFacing.DOWN);
                }
                else
                {
                    pos = blockSel.Position.UpCopy();
                    block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
                    if (block != null && block.HasMechPowerConnectorAt(world, pos, BlockFacing.DOWN))
                    {
                        block.DidConnectAt(world, pos, BlockFacing.DOWN);
                        connections.Add(BlockFacing.UP);
                    }
                }

                foreach (BlockFacing face in connections)
                {
                    beMechBase?.WasPlaced(face);
                }
            }
            return ok;
        }

        private void PlaceFakeBlocks(IWorldAccessor world, BlockPos pos, List<BlockPos> skips)
        {
            Block toPlaceBlock = world.GetBlock(new AssetLocation("mpmultiblockwood"));
            BlockPos tmpPos = new BlockPos();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    bool toSkip = false;
                    foreach (var skipPos in skips)
                    {
                        if (pos.X + dx == skipPos.X && pos.Z + dz == skipPos.Z)
                        {
                            toSkip = true;
                            break;
                        }
                    }
                    if (toSkip) continue;
                    tmpPos.Set(pos.X + dx, pos.Y, pos.Z + dz);
                    world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, tmpPos);
                    BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(tmpPos) as BEMPMultiblock;
                    if (be != null) be.Principal = pos;
                }
            }
        }

        private bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode, List<BlockPos> smallGears)
        {
            if (!base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;
            BlockPos pos = blockSel.Position;

            BlockPos tmpPos = new BlockPos();
            BlockSelection bs = blockSel.Clone();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    tmpPos.Set(pos.X + dx, pos.Y, pos.Z + dz);
                    if (dx == 0 || dz == 0)
                    {
                        BlockAngledGears bg = world.BlockAccessor.GetBlock(tmpPos) as BlockAngledGears;
                        if (bg != null)
                        {
                            smallGears.Add(tmpPos.Copy());
                            continue;
                        }
                    }
                    bs.Position = tmpPos;
                    if (!base.CanPlaceBlock(world, byPlayer, bs, ref failureCode)) return false;
                }
            }

            return true;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.SelectionBoxIndex == 0)
            {
                // If the player is trying to place blocks against the central axle upper part, assume the player wants to place the block above
                blockSel.Face = BlockFacing.UP;
            }
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            base.OnBlockRemoved(world, pos);
            BlockPos tmpPos = new BlockPos();

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    tmpPos.Set(pos.X + dx, pos.Y, pos.Z + dz);

                    //Destroy any fake blocks; revert small gears to their normal peg gear type
                    BEMPMultiblock be = world.BlockAccessor.GetBlockEntity(tmpPos) as BEMPMultiblock;
                    if (be != null && pos.Equals(be.Principal))
                    {
                        be.Principal = null;  //signal to BlockMPMultiblockWood that it can be broken normally without triggering this in a loop
                        world.BlockAccessor.SetBlock(0, tmpPos);
                    }
                    else
                    {
                        BlockAngledGears smallgear = world.BlockAccessor.GetBlock(tmpPos) as BlockAngledGears;
                        if (smallgear != null) smallgear.ToPegGear(world, tmpPos);
                    }
                }
            }
        }
    }
}
