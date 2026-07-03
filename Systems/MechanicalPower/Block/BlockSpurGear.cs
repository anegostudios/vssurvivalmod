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
            // Original: face == Orientation || side-by-side matching spur gears
            // Added: hub variant accepts both ends of the rotation axis (drive side + continuation)
            if (face == Orientation) return true;
            if (face == Orientation.Opposite && IsHubVariant()) return true;
            if (forBlock == this && face != Orientation.Opposite) return true;

            // Multi-disc block requesting connection on any face with a supported axle
            if (forBlock is BlockSpurGearMulti) return face == Orientation || face == Orientation.Opposite;

            return false;
        }

        bool IsHubVariant()
        {
            return Code.Path.StartsWith("spurgearhub-");
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

            // If clicking on an existing spur gear, redirect to multi-disc growth
            Block targetBlock = world.BlockAccessor.GetBlock(blockSel.Position);
            if (targetBlock is BlockSpurGear || targetBlock is BlockSpurGearMulti)
            {
                if (BlockSpurGearMulti.TryAddDisc(world, blockSel.Position, blockSel.Face.Opposite, ref failureCode, byPlayer))
                {
                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
                        byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    }
                    return true;
                }
                // Fall through to normal placement if adding a disc failed
            }

            // Scan all 6 faces for a valid axle, prioritizing the clicked face
            BlockFacing targetFace = blockSel.Face.Opposite;
            BlockPos targetPos = blockSel.Position.AddCopy(targetFace);
            BlockEntity targetBe = world.BlockAccessor.GetBlockEntity(targetPos);

            if (!TryFindAxle(world, blockSel.Position, targetFace, out targetFace, out targetPos, out targetBe, ref failureCode))
            {
                return false;
            }

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

        /// <summary>
        /// Searches for a valid axle to attach the gear to, starting with the preferred face
        /// and falling through to all other faces if that one fails.
        /// </summary>
        bool TryFindAxle(IWorldAccessor world, BlockPos gearPos, BlockFacing preferred, out BlockFacing foundFace, out BlockPos foundPos, out BlockEntity foundBe, ref string failureCode)
        {
            // Try preferred face first
            if (CheckAxleFace(world, gearPos, preferred, out foundPos, out foundBe))
            {
                foundFace = preferred;
                return true;
            }

            // Scan remaining faces
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (face == preferred) continue;
                if (CheckAxleFace(world, gearPos, face, out foundPos, out foundBe))
                {
                    foundFace = face;
                    return true;
                }
            }

            foundFace = preferred;
            foundPos = null;
            foundBe = null;
            failureCode = "requiresaxle";
            return false;
        }

        bool CheckAxleFace(IWorldAccessor world, BlockPos gearPos, BlockFacing face, out BlockPos axlePos, out BlockEntity axleBe)
        {
            axlePos = gearPos.AddCopy(face);
            axleBe = world.BlockAccessor.GetBlockEntity(axlePos);

            BEBehaviorMPAxle bempaxle = axleBe?.GetBehavior<BEBehaviorMPAxle>();
            if (bempaxle == null) return false;
            if (!(bempaxle.Block as BlockMPBase).HasMechPowerConnectorAt(world, axlePos, face, this)) return false;
            if (!BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, bempaxle.Block as Block, axlePos)) return false;

            return true;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            var nblock = world.BlockAccessor.GetBlock(pos.AddCopy(Orientation));
            if (!(nblock is BlockMPBase) || nblock.SideIsSolid(world.BlockAccessor, pos, Orientation.Opposite.Index))
            {
                // Before breaking, check if the hub-side axle still supports us
                if (IsHubVariant())
                {
                    var hubBlock = world.BlockAccessor.GetBlock(pos.AddCopy(Orientation.Opposite));
                    if (hubBlock is BlockMPBase) return; // Hub axle holds the gear
                }

                world.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            base.OnNeighbourBlockChange(world, pos, neibpos);
        }


        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }
    }
}
