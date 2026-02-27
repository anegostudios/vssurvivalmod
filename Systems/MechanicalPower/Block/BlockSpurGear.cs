using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockSpurGear : BlockMPBase
    {
        protected BlockFacing Orientation; 

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Orientation = BlockFacing.FromFirstLetter(Variant["orientation"]);
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            if (Orientation.IsHorizontal)
            {
                return face == Orientation || (forBlock == this && (face == Orientation.GetCCW() || face == Orientation.GetCW() || face == BlockFacing.UP || face == BlockFacing.DOWN));
            } else
            {
                return face == Orientation || (forBlock == this && face.IsHorizontal);
            }
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.GetBlock(CodeWithVariant("orientation", "s")));
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing targetFace = blockSel.Face.Opposite;
            BlockPos pos = blockSel.Position.AddCopy(targetFace);
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            IMechanicalPowerBlock neighbour = be?.Block as IMechanicalPowerBlock;

            BEBehaviorMPAxle bempaxle = be?.GetBehavior<BEBehaviorMPAxle>();
            if (bempaxle == null || !(bempaxle.Block as BlockMPBase).HasMechPowerConnectorAt(world, pos, targetFace, this))
            {
                failureCode = "requiresaxle";
                return false;
            }
            if (bempaxle != null && !BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, neighbour as Block, pos))
            {
                failureCode = "axlemusthavesupport";
                return false;
            }

            if (blockSel.Face.IsVertical) return false; // Don't allow this for now

            BlockSpurGear toPlaceBlock = world.GetBlock(CodeWithVariant("orientation", targetFace.Code[0] + "")) as BlockSpurGear;
            world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);

            var selfBeh = GetBEBehavior<BEBehaviorMPBase>(blockSel.Position);
            var exits = selfBeh.GetMechPowerExits(new MechPowerPath() { OutFacing = targetFace });

            List<BlockFacing> possiblyNetworklessCandidates = new List<BlockFacing>();
            foreach (var exit in exits)
            {
                var npos = blockSel.Position.AddCopy(exit.OutFacing);
                var neibBlock = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
                neibBlock?.DidConnectAt(world, blockSel.Position, exit.OutFacing.Opposite);
                if (neibBlock != null)
                {
                    if (!selfBeh.tryConnect(exit.OutFacing))
                    {
                        // We might be trying to connect to a side which is has no power node, which means it has no network.
                        // We first need to connect to a network, before we can connect our neighbours, so lets try to connect these again
                        possiblyNetworklessCandidates.Add(exit.OutFacing);
                    }
                }
            }

            // Looks like we managed to connect
            if (selfBeh.Network != null)
            {
                foreach (var face in possiblyNetworklessCandidates) selfBeh.tryConnect(face);
            }


            return true;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            var nblock = world.BlockAccessor.GetBlock(pos.AddCopy(Orientation));
            if (!(nblock is BlockMPBase) || nblock.SideIsSolid(world.BlockAccessor, pos, Orientation.Opposite.Index))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }


        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }
    }
}
