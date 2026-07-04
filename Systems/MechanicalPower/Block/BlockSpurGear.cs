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

        bool IsHubVariant()
        {
            return Code?.PathStartsWith("spurgearhub-") == true;
        }

        bool IsSameSpurGearVariant(Block block)
        {
            string path = block?.Code?.Path;
            if (path == null || (!path.StartsWith("spurgear-") && !path.StartsWith("spurgearhub-"))) return false;
            return block is BlockSpurGear && block.Variant?["orientation"] == Variant?["orientation"];
        }

        Block GetSpurGearVariant(IWorldAccessor world, bool hub, BlockFacing orientation)
        {
            string code = (hub ? "spurgearhub-" : "spurgear-") + orientation.Code[0];
            return world.GetBlock(new AssetLocation(Code.Domain, code)) ?? world.GetBlock(new AssetLocation(code));
        }

        void ExchangeTo(IWorldAccessor world, BlockPos pos, Block toBlock)
        {
            world.BlockAccessor.ExchangeBlock(toBlock.BlockId, pos);

            BEBehaviorMPBase bemp = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>();
            if (bemp != null)
            {
                bemp.SetOrientations();
                bemp.Shape = toBlock.Shape;
                bemp.Blockentity.MarkDirty();
            }
        }

        public bool IsHubAxleFace(BlockFacing face)
        {
            return face == Orientation.Opposite;
        }

        public bool TryAddHubAxle(IWorldAccessor world, BlockPos pos, BlockFacing connectedOnFace = null)
        {
            if (IsHubVariant()) return true;
            if (!(GetSpurGearVariant(world, true, Orientation) is BlockMPBase hubBlock)) return false;

            ExchangeTo(world, pos, hubBlock);
            ReconnectAxis(world, pos, hubBlock, connectedOnFace);
            return true;
        }

        void ReconnectAxis(IWorldAccessor world, BlockPos pos, BlockMPBase ownBlock, BlockFacing connectedOnFace)
        {
            if (connectedOnFace == Orientation || connectedOnFace == Orientation.Opposite)
            {
                TryReconnectFace(world, pos, ownBlock, connectedOnFace);
                TryReconnectFace(world, pos, ownBlock, connectedOnFace.Opposite);
                return;
            }

            TryReconnectFace(world, pos, ownBlock, Orientation);
            TryReconnectFace(world, pos, ownBlock, Orientation.Opposite);
        }

        void TryReconnectFace(IWorldAccessor world, BlockPos pos, BlockMPBase ownBlock, BlockFacing face)
        {
            BlockPos npos = pos.AddCopy(face);
            IMechanicalPowerBlock neighbour = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
            if (neighbour == null) return;
            if (!neighbour.HasMechPowerConnectorAt(world, npos, face.Opposite, ownBlock)) return;
            if (!ownBlock.HasMechPowerConnectorAt(world, pos, face, neighbour as BlockMPBase)) return;

            neighbour.DidConnectAt(world, npos, face.Opposite);
            ownBlock.WasPlaced(world, pos, face);
        }

        bool AxisEndHolds(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            BlockPos npos = pos.AddCopy(face);
            return world.BlockAccessor.GetBlock(npos) is BlockMPBase nblock
                && nblock.HasMechPowerConnectorAt(world, npos, face.Opposite, this)
                && HasMechPowerConnectorAt(world, pos, face, nblock);
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            if (face == Orientation || face == Orientation.Opposite) return true;
            return IsSameSpurGearVariant(forBlock);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(GetSpurGearVariant(world, false, BlockFacing.SOUTH));
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                if (failureCode != "notreplaceable" || blockSel.Face == null) return false;

                Block blockAtSelection = world.BlockAccessor.GetBlock(blockSel.Position);
                if (blockAtSelection is BlockSpurGear || blockAtSelection is BlockSpurGearMulti)
                {
                    BlockFacing discFace = blockSel.Face.Opposite;
                    return BlockSpurGearMulti.TryAddDisc(world, blockSel.Position, discFace, ref failureCode, byPlayer);
                }

                if (!TryMoveSelectionToReplaceableNeighbour(world, byPlayer, blockSel, ref failureCode))
                {
                    return false;
                }
            }

            if (TryRedirectToNeighbourMulti(world, byPlayer, blockSel, ref failureCode))
            {
                return true;
            }

            BlockFacing targetFace = null;
            Block toPlaceBlock = null;
            BlockFacing clickedFace = blockSel.Face.Opposite;
            bool unsupportedAxleSeen = false;

            for (int i = -1; i < BlockFacing.ALLFACES.Length; i++)
            {
                BlockFacing face = i < 0 ? clickedFace : BlockFacing.ALLFACES[i];
                if (i >= 0 && face == clickedFace) continue;

                Block candidateBlock = GetSpurGearVariant(world, false, face) ?? world.GetBlock(CodeWithVariant("orientation", face.Code[0] + ""));
                if (!(candidateBlock is BlockMPBase candidateMechBlock)) continue;

                BlockPos npos = blockSel.Position.AddCopy(face);
                BlockEntity nbe = world.BlockAccessor.GetBlockEntity(npos);
                if (!(nbe?.Block is BlockMPBase neighbourBlock)) continue;
                if (!neighbourBlock.HasMechPowerConnectorAt(world, npos, face.Opposite, candidateMechBlock)) continue;
                if (nbe.GetBehavior<BEBehaviorMPAxle>() == null) continue;
                if (!BEBehaviorMPAxle.IsAttachedToBlock(world.BlockAccessor, nbe.Block, npos))
                {
                    unsupportedAxleSeen = true;
                    continue;
                }

                targetFace = face;
                toPlaceBlock = candidateBlock;
                break;
            }

            if (targetFace == null)
            {
                failureCode = unsupportedAxleSeen ? "axlemusthavesupport" : "requiresaxle";
                return false;
            }

            world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, blockSel.Position);

            var selfBeh = GetBEBehavior<BEBehaviorMPBase>(blockSel.Position);
            var exits = selfBeh.GetMechPowerExits(new MechPowerPath() { OutFacing = targetFace });

            List<BlockFacing> possiblyNetworklessCandidates = new List<BlockFacing>();
            foreach (var exit in exits)
            {
                var npos = blockSel.Position.AddCopy(exit.OutFacing);
                var neibBlock = world.BlockAccessor.GetBlock(npos) as IMechanicalPowerBlock;
                neibBlock?.DidConnectAt(world, npos, exit.OutFacing.Opposite);
                if (neibBlock != null && !selfBeh.tryConnect(exit.OutFacing))
                {
                    possiblyNetworklessCandidates.Add(exit.OutFacing);
                }
            }

            if (selfBeh.Network != null)
            {
                foreach (var face in possiblyNetworklessCandidates) selfBeh.tryConnect(face);
            }

            return true;
        }

        bool TryRedirectToNeighbourMulti(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            BlockFacing clickedFace = blockSel.Face;
            if (clickedFace == null) return false;

            BlockPos pos = blockSel.Position;
            BlockFacing scanDir = clickedFace.Opposite;
            for (int depth = 1; depth <= 2; depth++)
            {
                BlockPos candidatePos = pos.AddCopy(scanDir, depth);
                Block candidate = world.BlockAccessor.GetBlock(candidatePos);

                if (candidate is BlockSpurGear || candidate is BlockSpurGearMulti)
                {
                    BlockFacing discFace = scanDir.Opposite;
                    return BlockSpurGearMulti.TryAddDisc(world, candidatePos, discFace, ref failureCode, byPlayer);
                }

                if (candidate.BlockMaterial != EnumBlockMaterial.Wood && !(candidate is BlockMPBase))
                {
                    break;
                }
            }

            return false;
        }

        bool TryMoveSelectionToReplaceableNeighbour(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            BlockPos originalPos = blockSel.Position.Copy();
            BlockFacing originalFace = blockSel.Face;
            bool originalDidOffset = blockSel.DidOffset;

            for (int i = -1; i < BlockFacing.ALLFACES.Length; i++)
            {
                BlockFacing face = i < 0 ? originalFace : BlockFacing.ALLFACES[i];
                if (face == null || (i >= 0 && face == originalFace)) continue;

                blockSel.Position = originalPos.AddCopy(face);
                blockSel.Face = face;
                blockSel.DidOffset = true;

                string offsetFailureCode = null;
                if (CanPlaceBlock(world, byPlayer, blockSel, ref offsetFailureCode))
                {
                    failureCode = offsetFailureCode;
                    return true;
                }
            }

            blockSel.Position = originalPos;
            blockSel.Face = originalFace;
            blockSel.DidOffset = originalDidOffset;
            return false;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            var nblock = world.BlockAccessor.GetBlock(pos.AddCopy(Orientation));
            bool frontHolds = nblock is BlockMPBase && !nblock.SideIsSolid(world.BlockAccessor, pos, Orientation.Opposite.Index);

            if (frontHolds)
            {
                base.OnNeighbourBlockChange(world, pos, neibpos);
                return;
            }

            bool hubHolds = IsHubVariant() && AxisEndHolds(world, pos, Orientation.Opposite);
            if (!hubHolds)
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }
    }
}
