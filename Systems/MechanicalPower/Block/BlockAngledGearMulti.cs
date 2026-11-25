using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    // This block holds up to six angled gears in one cell.
    // Gears mesh across axes, and three-axis clusters are always jammed.
    public class BlockAngledGearMulti : BlockMPBase
    {
        Cuboidf[][] gearBoxSets;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            gearBoxSets = ObjectCacheUtil.GetOrCreate(api, "angledGearMultiBoxSets", createGearBoxSets);
        }

        static Cuboidf[][] createGearBoxSets()
        {
            Cuboidf horiz = new Cuboidf(0.125f, 0f, 0.625f, 0.85f, 1f, 1f);
            Cuboidf vert = new Cuboidf(0.125f, 0f, 0.125f, 0.875f, 0.375f, 0.875f);
            Vec3d center = new Vec3d(0.5, 0.5, 0.5);

            Cuboidf[] gearBoxes = new Cuboidf[6];
            gearBoxes[BlockFacing.NORTH.Index] = horiz.RotatedCopy(0, 180, 0, center);
            gearBoxes[BlockFacing.EAST.Index] = horiz.RotatedCopy(0, 90, 0, center);
            gearBoxes[BlockFacing.SOUTH.Index] = horiz.RotatedCopy(0, 0, 0, center);
            gearBoxes[BlockFacing.WEST.Index] = horiz.RotatedCopy(0, 270, 0, center);
            gearBoxes[BlockFacing.DOWN.Index] = vert.RotatedCopy(0, 0, 0, center);
            gearBoxes[BlockFacing.UP.Index] = vert.RotatedCopy(180, 0, 0, center);

            Cuboidf[][] gearBoxSets = new Cuboidf[64][];
            for (int mask = 1; mask < gearBoxSets.Length; mask++)
            {
                int count = 0;
                for (int i = 0; i < 6; i++) if ((mask & (1 << i)) != 0) count++;

                Cuboidf[] boxes = new Cuboidf[count];
                int index = 0;
                for (int i = 0; i < 6; i++)
                {
                    if ((mask & (1 << i)) != 0) boxes[index++] = gearBoxes[i];
                }
                gearBoxSets[mask] = boxes;
            }

            return gearBoxSets;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            int gearFaces = getGearBeh(blockAccessor, pos)?.GearFaces ?? 0;
            return gearFaces == 0 ? base.GetSelectionBoxes(blockAccessor, pos) : gearBoxSets[gearFaces];
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return GetSelectionBoxes(blockAccessor, pos);
        }

        static BEBehaviorMPAngledGears getGearBeh(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return blockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPAngledGears>();
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
        {
            // Angled gears only engage perpendicular gears inside this block.
            if (forBlock is BlockAngledGears || forBlock is BlockAngledGearMulti) return false;
            return HasCompatibleConnector(forBlock) && getGearBeh(world.BlockAccessor, pos)?.HasGear(face) == true;
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (BlockAngledGears.TryHandleGearInHand(world, byPlayer, blockSel)) return true;
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return getGearBeh(world.BlockAccessor, pos)?.GetFirstGearDrop();
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            return getGearBeh(world.BlockAccessor, pos)?.GetGearDrops() ?? [];
        }

        // This does not fall back to another face when the requested face is invalid.
        public static bool TryAddGear(IWorldAccessor world, BlockPos gearPos, BlockFacing requestedFace, ItemStack gearStack, ref string failureCode)
        {
            if (requestedFace == null || gearStack?.Block is not BlockAngledGears gearBlock)
            {
                failureCode = "requiresaxle";
                return false;
            }

            Block current = world.BlockAccessor.GetBlock(gearPos);
            BEBehaviorMPAngledGears currentBeh = getGearBeh(world.BlockAccessor, gearPos);

            BlockFacing existingFace = null;
            ItemStack existingStack = null;
            BlockAngledGearMulti multiMech;
            if (current is BlockAngledGearMulti currentMulti)
            {
                multiMech = currentMulti;
                if (currentBeh == null) return false;
                if (currentBeh.HasGear(requestedFace))
                {
                    failureCode = "notreplaceable";
                    return false;
                }
            }
            else if (current is BlockAngledGears ag)
            {
                multiMech = ag.GetMultiGearBlock(world);
                if (multiMech == null)
                {
                    failureCode = "requiresaxle";
                    return false;
                }

                string orient = ag.Orientation;
                // Large-gear cage variants are not standalone single gears.
                if (orient.Length != 1)
                {
                    failureCode = "notreplaceable";
                    return false;
                }
                existingFace = BlockFacing.FromFirstLetter(orient[0]);
                existingStack = ag.OnPickBlock(world, gearPos);
                if (existingFace == requestedFace)
                {
                    failureCode = "notreplaceable";
                    return false;
                }
            }
            else
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (!multiMech.HasCompatibleConnector(gearBlock))
            {
                failureCode = "incompatibleconnector";
                return false;
            }

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (!face.IsAdjacent(requestedFace)) continue;

                BlockAngledGears existingGear = existingFace == null ? currentBeh.GetGearBlock(face) : face == existingFace ? (BlockAngledGears)current : null;
                if (existingGear?.CanMeshWith(gearBlock) == false)
                {
                    failureCode = "incompatibleangledgears";
                    return false;
                }
            }

            if (!tryGetAxle(world, gearPos, requestedFace, multiMech, out _, out failureCode)) return false;

            if (current is BlockAngledGearMulti)
            {
                if (!currentBeh.TryAddGear(requestedFace, gearStack))
                {
                    failureCode = "notreplaceable";
                    return false;
                }
                RefreshPowerConnections(world, gearPos, requestedFace);
            }
            else
            {
                long prevNetworkId = currentBeh?.NetworkId ?? 0;
                BlockFacing prevPropDir = currentBeh?.GetPropagationDirection();

                currentBeh?.LeaveNetwork();

                Exchange(world, gearPos, multiMech);
                BEBehaviorMPAngledGears beh = getGearBeh(world.BlockAccessor, gearPos);
                if (beh == null) return false;
                if (!beh.HasGear(existingFace)) beh.TryAddGear(existingFace, existingStack);
                beh.TryAddGear(requestedFace, gearStack);
                RefreshPowerConnections(world, gearPos, requestedFace, prevNetworkId, prevPropDir);
            }

            BlockPos axlePos = gearPos.AddCopy(requestedFace);
            (world.BlockAccessor.GetBlock(axlePos) as IMechanicalPowerBlock)?.DidConnectAt(world, axlePos, requestedFace.Opposite);

            return true;
        }

        public static void RefreshPowerConnections(IWorldAccessor world, BlockPos gearPos, BlockFacing preferredFace = null, long prevNetworkId = 0, BlockFacing prevPropDir = null)
        {
            BEBehaviorMPAngledGears beh = getGearBeh(world.BlockAccessor, gearPos);
            if (beh == null) return;

            if (prevNetworkId == 0) prevNetworkId = beh.NetworkId;
            prevPropDir ??= beh.GetPropagationDirection();

            List<BlockFacing> faces = getOrderedConnectionFaces(world, gearPos, beh, preferredFace, prevNetworkId, prevPropDir);

            beh.LeaveNetwork();
            beh.CheckEngagedByClutch(world.BlockAccessor, false);

            foreach (BlockFacing face in faces)
            {
                if (beh.HasGear(face) && beh.tryConnect(face)) break;
            }
        }

        static List<BlockFacing> getOrderedConnectionFaces(IWorldAccessor world, BlockPos gearPos, BEBehaviorMPAngledGears beh, BlockFacing preferredFace, long prevNetworkId, BlockFacing prevPropDir)
        {
            List<BlockFacing> faces = [];
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                if (beh.HasGear(face)) faces.Add(face);
            }

            BlockMPBase forBlock = beh.Block as BlockMPBase;
            int[] scores = new int[BlockFacing.ALLFACES.Length];
            BlockPos neighbourPos = new BlockPos(gearPos.dimension);
            foreach (BlockFacing face in faces)
            {
                neighbourPos.Set(gearPos).Add(face);
                scores[face.Index] = getConnectionScore(world, neighbourPos, forBlock, face, preferredFace, prevNetworkId, prevPropDir);
            }
            faces.Sort((a, b) => scores[b.Index].CompareTo(scores[a.Index]));

            return faces;
        }

        static int getConnectionScore(IWorldAccessor world, BlockPos neighbourPos, BlockMPBase forBlock, BlockFacing face, BlockFacing preferredFace, long prevNetworkId, BlockFacing prevPropDir)
        {
            IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(neighbourPos) as IMechanicalPowerBlock;
            MechanicalNetwork network = block?.HasMechPowerConnectorAt(world, neighbourPos, face.Opposite, forBlock) == true
                ? block.GetNetwork(world, neighbourPos)
                : null;
            if (network == null || !network.Valid) return 0;

            int score = 1000;
            if (Math.Abs(network.Speed) > 0.0001f || Math.Abs(network.TotalAvailableTorque) > 0.0001f)
            {
                score += 10000;
            }
            if (prevNetworkId != 0 && network.networkId == prevNetworkId) score += 5000;
            if (prevPropDir != null && (face == prevPropDir || face == prevPropDir.Opposite))
            {
                score += 1000;
            }
            if (preferredFace != null && face == preferredFace) score += 500;
            return score;
        }

        public static bool TryPlaceGearOnAxle(IWorldAccessor world, IPlayer byPlayer, ItemStack gearStack, BlockPos axlePos, BlockFacing clickedFace, ref string failureCode)
        {
            if (gearStack?.Block is not BlockAngledGears)
            {
                failureCode = "requiresaxle";
                return false;
            }

            BlockEntity axleBe = world.BlockAccessor.GetBlockEntity(axlePos);
            if (axleBe?.GetBehavior<BEBehaviorMPAxle>() == null || axleBe.Block is not BlockMPBase axleBlock)
            {
                failureCode = "requiresaxle";
                return false;
            }

            List<BlockFacing> axleFaces = getAxleConnectorFaces(world, axlePos, axleBlock, clickedFace);
            BlockPos gearPos = new(axlePos.dimension);

            foreach (BlockFacing axleFace in axleFaces)
            {
                gearPos.Set(axlePos).Add(axleFace);
                Block current = world.BlockAccessor.GetBlock(gearPos);
                if ((current is BlockAngledGearMulti || current is BlockAngledGears)
                    && TryAddGear(world, gearPos, axleFace.Opposite, gearStack, ref failureCode))
                {
                    return true;
                }
            }

            foreach (BlockFacing axleFace in axleFaces)
            {
                gearPos.Set(axlePos).Add(axleFace);
                if (tryPlaceNewGear(world, byPlayer, gearStack, gearPos, axleFace.Opposite, ref failureCode))
                {
                    return true;
                }
            }

            failureCode ??= "requiresaxle";
            return false;
        }

        static List<BlockFacing> getAxleConnectorFaces(IWorldAccessor world, BlockPos axlePos, BlockMPBase axleBlock, BlockFacing clickedFace)
        {
            List<BlockFacing> faces = [];
            addAxleFace(world, axlePos, axleBlock, clickedFace, faces);
            addAxleFace(world, axlePos, axleBlock, clickedFace?.Opposite, faces);

            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                addAxleFace(world, axlePos, axleBlock, face, faces);
            }

            return faces;
        }

        static void addAxleFace(IWorldAccessor world, BlockPos axlePos, BlockMPBase axleBlock, BlockFacing face, List<BlockFacing> faces)
        {
            if (face == null || faces.Contains(face)) return;
            if (axleBlock.HasMechPowerConnectorAt(world, axlePos, face, null)) faces.Add(face);
        }

        static bool tryPlaceNewGear(IWorldAccessor world, IPlayer byPlayer, ItemStack gearStack, BlockPos gearPos, BlockFacing gearFace, ref string failureCode)
        {
            BlockAngledGears gearBlock = (BlockAngledGears)gearStack.Block;
            Block current = world.BlockAccessor.GetBlock(gearPos);
            if (current is BlockAngledGearMulti || current is BlockAngledGears)
            {
                return TryAddGear(world, gearPos, gearFace, gearStack, ref failureCode);
            }

            if (current is BlockMPMultiblockGear largeGearEdge)
            {
                return tryPlaceCageGear(world, gearStack, gearPos, gearFace, largeGearEdge, ref failureCode);
            }

            BlockAngledGearMulti multiMech = gearBlock.GetMultiGearBlock(world);
            if (multiMech == null)
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (!tryGetAxle(world, gearPos, gearFace, multiMech, out _, out failureCode)) return false;

            BlockSelection gearSel = new BlockSelection() { Position = gearPos, Face = gearFace.Opposite };
            if (!gearBlock.CanPlaceBlock(world, byPlayer, gearSel, ref failureCode, current)) return false;

            world.BlockAccessor.SetBlock(multiMech.BlockId, gearPos);

            BEBehaviorMPAngledGears beh = getGearBeh(world.BlockAccessor, gearPos);
            beh.TryAddGear(gearFace, gearStack);
            RefreshPowerConnections(world, gearPos, gearFace);

            BlockPos axlePos = gearPos.AddCopy(gearFace);
            (world.BlockAccessor.GetBlock(axlePos) as IMechanicalPowerBlock)?.DidConnectAt(world, axlePos, gearFace.Opposite);

            return true;
        }

        static bool tryPlaceCageGear(IWorldAccessor world, ItemStack gearStack, BlockPos gearPos, BlockFacing gearFace, BlockMPMultiblockGear largeGearEdge, ref string failureCode)
        {
            BlockAngledGears gearBlock = (BlockAngledGears)gearStack.Block;
            if (!largeGearEdge.IsReplacableByGear(world, gearPos))
            {
                failureCode = "notreplaceable";
                return false;
            }

            Block toPlaceBlock = gearBlock.GetGearBlock(world, true, gearFace);
            if (toPlaceBlock is not BlockMPBase)
            {
                failureCode = "requiresaxle";
                return false;
            }

            if (!tryGetAxle(world, gearPos, gearFace, gearBlock, out _, out failureCode)) return false;

            BlockEntity largeGearBE = largeGearEdge.GearPlaced(world, gearPos);
            world.BlockAccessor.SetBlock(toPlaceBlock.BlockId, gearPos, gearStack);

            BEBehaviorMPAngledGears beAngledGear = getGearBeh(world.BlockAccessor, gearPos);
            if (largeGearBE?.GetBehavior<BEBehaviorMPBase>() is BEBehaviorMPLargeGear3m largeGear)
            {
                beAngledGear.AddToLargeGearNetwork(largeGear, gearFace);
            }

            BlockPos axlePos = gearPos.AddCopy(gearFace);
            (world.BlockAccessor.GetBlock(axlePos) as IMechanicalPowerBlock)?.DidConnectAt(world, axlePos, gearFace.Opposite);
            beAngledGear.newlyPlaced = true;
            beAngledGear.tryConnect(gearFace);
            beAngledGear.newlyPlaced = false;

            return true;
        }

        static bool tryGetAxle(IWorldAccessor world, BlockPos gearPos, BlockFacing face, BlockMPBase forBlock, out BlockEntity axleBe, out string failureCode)
        {
            BlockPos axlePos = gearPos.AddCopy(face);
            axleBe = world.BlockAccessor.GetBlockEntity(axlePos);
            if (axleBe?.GetBehavior<BEBehaviorMPAxle>() == null
                || axleBe.Block is not BlockMPBase axleBlock
                || !axleBlock.HasMechPowerConnectorAt(world, axlePos, face.Opposite, forBlock))
            {
                failureCode = "requiresaxle";
                return false;
            }
            failureCode = null;
            return true;
        }

        public static void Exchange(IWorldAccessor world, BlockPos pos, Block toBlock)
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

        bool hasGearConnector(IWorldAccessor world, BlockPos neighbourPos, BlockFacing face)
        {
            if (world.BlockAccessor.GetChunkAtBlockPos(neighbourPos) == null) return true;
            if (world.BlockAccessor.GetBlock(neighbourPos) is not BlockMPBase neighbourBlock) return false;
            return neighbourBlock.HasMechPowerConnectorAt(world, neighbourPos, face.Opposite, this);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEBehaviorMPAngledGears beh = getGearBeh(world.BlockAccessor, pos);
            if (beh == null)
            {
                base.OnNeighbourBlockChange(world, pos, neibpos);
                return;
            }

            List<MechanicalNetwork> networks = null;
            bool removedAny = false;
            BlockPos neighbourPos = pos.Copy();
            foreach (BlockFacing face in BlockFacing.ALLFACES)
            {
                face.IterateThruFacingOffsets(neighbourPos);
                if (!beh.HasGear(face) || hasGearConnector(world, neighbourPos, face)) continue;
                networks ??= beh.GetConnectedNetworks();
                ItemStack drop = beh.RemoveGear(face);
                removedAny = true;
                if (world.Side == EnumAppSide.Server && drop != null)
                {
                    world.SpawnItemEntity(drop, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }

            int count = beh.GearCount;
            if (count == 0)
            {
                if (removedAny)
                {
                    beh.RebuildNetworks(networks);
                    world.BlockAccessor.BreakBlock(pos, null);
                }
                return;
            }
            if (removedAny)
            {
                beh.CheckEngagedByClutch(world.BlockAccessor, false);
                beh.RebuildNetworks(networks);
                if (beh.Network == null) RefreshPowerConnections(world, pos);
            }
            beh.CheckEngagedByClutch(world.BlockAccessor, true);
        }
    }
}
