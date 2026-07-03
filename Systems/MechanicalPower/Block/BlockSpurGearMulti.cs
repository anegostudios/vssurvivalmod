using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// Multi-disc spur gear hub: one cell carrying up to six gear discs, one per face,
    /// each mounted on its own supported axle, all coupled as one network node.
    /// </summary>
    public class BlockSpurGearMulti : BlockMPBase
    {
        Cuboidf[][] discBoxes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            Cuboidf horiz = new Cuboidf(0.125f, 0f, 0.625f, 0.85f, 1f, 1f);
            Cuboidf vert = new Cuboidf(0.125f, 0f, 0.125f, 0.875f, 0.375f, 0.875f);
            Vec3d center = new Vec3d(0.5, 0.5, 0.5);

            discBoxes = new Cuboidf[6][];
            discBoxes[BlockFacing.NORTH.Index] = new[] { horiz.RotatedCopy(0, 180, 0, center) };
            discBoxes[BlockFacing.EAST.Index] = new[] { horiz.RotatedCopy(0, 90, 0, center) };
            discBoxes[BlockFacing.SOUTH.Index] = new[] { horiz.RotatedCopy(0, 0, 0, center) };
            discBoxes[BlockFacing.WEST.Index] = new[] { horiz.RotatedCopy(0, 270, 0, center) };
            discBoxes[BlockFacing.UP.Index] = new[] { vert.RotatedCopy(180, 0, 0, center) };
            discBoxes[BlockFacing.DOWN.Index] = new[] { vert.RotatedCopy(0, 0, 0, center) };
        }

        static BEBehaviorMPSpurGear GetGearBehavior(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSpurGear>();
        }

        static Block GearItemBlock(IWorldAccessor world, Block from)
        {
            return world.GetBlock(new AssetLocation(from.Code.Domain, "spurgear-s"))
                ?? world.GetBlock(new AssetLocation("spurgear-s"));
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            return GetGearBehavior(world.BlockAccessor, pos)?.HasDisc(face) == true;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(GearItemBlock(world, this));
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            int count = GetGearBehavior(world.BlockAccessor, pos)?.DiscCount ?? 0;
            if (count <= 0) return new ItemStack[0];

            return new[] { new ItemStack(GearItemBlock(world, this), count) };
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var beh = GetGearBehavior(blockAccessor, pos);
            if (beh == null || beh.DiscCount == 0) return base.GetSelectionBoxes(blockAccessor, pos);

            List<Cuboidf> boxes = new List<Cuboidf>();
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (beh.HasDisc(face)) boxes.AddRange(discBoxes[face.Index]);
            }

            return boxes.ToArray();
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        /// <summary>
        /// Grows a disc on the cell: a plain gear morphs to the multi block with both discs
        /// set; an existing multi gains one bit.
        /// </summary>
        public static bool TryAddDisc(IWorldAccessor world, BlockPos gearPos, BlockFacing requestedFace, ref string failureCode, IPlayer byPlayer = null)
        {
            Block current = world.BlockAccessor.GetBlock(gearPos);
            BEBehaviorMPSpurGear currentBeh = GetGearBehavior(world.BlockAccessor, gearPos);

            BlockFacing existingOrientation = null;
            if (current is BlockSpurGearMulti)
            {
                if (currentBeh == null) return false;
            }
            else if (current is BlockSpurGear && current.Code.PathStartsWith("spurgear-"))
            {
                existingOrientation = BlockFacing.FromFirstLetter(current.Variant["orientation"]);
            }
            else
            {
                failureCode = "requiresaxle";
                return false;
            }

            Block multiBlock = world.GetBlock(new AssetLocation(current.Code.Domain, "spurgearmulti-s"))
                ?? world.GetBlock(new AssetLocation("spurgearmulti-s"));
            if (!(multiBlock is BlockSpurGearMulti multiMech))
            {
                failureCode = "requiresaxle";
                return false;
            }

            BlockFacing face = null;
            BlockEntity axleBe = null;
            string bestFailure = null;

            // Try requested face first, then scan all others
            for (int i = -1; i < BlockFacing.ALLFACES.Length; i++)
            {
                BlockFacing candidate = i < 0 ? requestedFace : BlockFacing.ALLFACES[i];
                if (candidate == null || (i >= 0 && candidate == requestedFace)) continue;

                if (current is BlockSpurGearMulti && currentBeh.HasDisc(candidate)) continue;
                if (existingOrientation != null && candidate == existingOrientation) continue;

                if (TryGetAddableAxle(world, gearPos, candidate, multiMech, out axleBe, out string candidateFailure))
                {
                    face = candidate;
                    break;
                }

                if (candidateFailure != null && (bestFailure == null || candidateFailure == "axlemusthavesupport"))
                    bestFailure = candidateFailure;
            }

            if (face == null)
            {
                failureCode = bestFailure ?? "requiresaxle";
                return false;
            }

            if (current is BlockSpurGearMulti)
            {
                currentBeh.SetDisc(face, true);
            }
            else
            {
                Exchange(world, gearPos, multiBlock);
                var beh = GetGearBehavior(world.BlockAccessor, gearPos);
                if (beh == null) return false;
                beh.SetDisc(existingOrientation, true);
                beh.SetDisc(face, true);
            }

            BlockPos axlePos = gearPos.AddCopy(face);
            (world.BlockAccessor.GetBlock(axlePos) as IMechanicalPowerBlock)?.DidConnectAt(world, axlePos, face.Opposite);
            world.BlockAccessor.GetBlockEntity(gearPos)?.GetBehavior<BEBehaviorMPBase>()?.tryConnect(face);

            if (byPlayer != null && face != requestedFace)
            {
                (byPlayer as IServerPlayer)?.SendMessage(
                    0, "Disc placed on " + face.Code + " face (aimed face unavailable)", EnumChatType.Notification);
            }

            return true;
        }

        static bool TryGetAddableAxle(IWorldAccessor world, BlockPos gearPos, BlockFacing face, BlockSpurGearMulti multiMech, out BlockEntity axleBe, out string failureCode)
        {
            BlockPos axlePos = gearPos.AddCopy(face);
            axleBe = world.BlockAccessor.GetBlockEntity(axlePos);

            if (axleBe?.GetBehavior<BEBehaviorMPAxle>() == null
                || !(axleBe.Block is BlockMPBase axleBlock)
                || !axleBlock.HasMechPowerConnectorAt(world, axlePos, face.Opposite, multiMech))
            {
                failureCode = "requiresaxle";
                return false;
            }
            if (!BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, axleBe.Block, axlePos))
            {
                failureCode = "axlemusthavesupport";
                return false;
            }

            failureCode = null;
            return true;
        }

        internal static void Exchange(IWorldAccessor world, BlockPos pos, Block toBlock)
        {
            world.BlockAccessor.ExchangeBlock(toBlock.BlockId, pos);
            BEBehaviorMPBase bemp = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>();
            if (bemp != null)
            {
                bemp.SetOrientations();
                bemp.Shape = toBlock.Shape;
                bemp.Blockentity.MarkDirty(true);
            }
        }

        bool DiscHasConnector(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            BlockPos npos = pos.AddCopy(face);
            Block nblockRaw = world.BlockAccessor.GetBlock(npos);
            if (!(nblockRaw is BlockMPBase nblock)) return false;

            bool hasConnector = nblock.HasMechPowerConnectorAt(world, npos, face.Opposite, this);
            BEBehaviorMPBase nbeh = world.BlockAccessor.GetBlockEntity(npos)?.GetBehavior<BEBehaviorMPBase>();
            return hasConnector && nbeh?.disconnected != true;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            var beh = GetGearBehavior(world.BlockAccessor, pos);
            if (beh == null)
            {
                base.OnNeighbourBlockChange(world, pos, neibpos);
                return;
            }

            Block gearItem = GearItemBlock(world, this);
            bool removedAny = false;
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (!beh.HasDisc(face) || DiscHasConnector(world, pos, face)) continue;

                beh.SetDisc(face, false);
                removedAny = true;
                world.SpawnItemEntity(new ItemStack(gearItem), pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            int count = beh.DiscCount;
            if (count == 0)
            {
                if (removedAny) world.BlockAccessor.BreakBlock(pos, null);
                return;
            }
            if (count > 1) return;

            // One disc left: shrink back to the plain spur gear
            BlockFacing last = null;
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (beh.HasDisc(face)) { last = face; break; }
            }

            Block plain = world.GetBlock(new AssetLocation(Code.Domain, "spurgear-" + last.Code[0]))
                ?? world.GetBlock(new AssetLocation("spurgear-" + last.Code[0]));
            if (!(plain is BlockMPBase plainMech)) return;

            beh.SetDisc(last, false);
            Exchange(world, pos, plain);

            BlockPos lastNpos = pos.AddCopy(last);
            (world.BlockAccessor.GetBlock(lastNpos) as IMechanicalPowerBlock)?.DidConnectAt(world, lastNpos, last.Opposite);
            plainMech.WasPlaced(world, pos, last);
        }
    }
}
