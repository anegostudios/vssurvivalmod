using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent.Mechanics
{
    public partial class ModSystemLinkage
    {
        const int collisionUpdateInterval = 20;

        static readonly Cuboidf blockCell = new(0, 0, 0, 1, 1, 1);

        readonly Dictionary<BlockPos, HashSet<BlockPos>> ownersBySpanCell = [];
        readonly Dictionary<BlockPos, BlockPos[]> spanCellsByOwner = [];
        readonly Dictionary<BlockPos, LinkageCollider> colliderByOwner = [];
        readonly Vec3d collisionStartPin = new();
        readonly Vec3d collisionEndPin = new();
        readonly float[] collisionModelMatrix = Mat4f.Create();
        readonly float[] collisionRotationMatrix = Mat3f.Create();
        readonly float[] collisionQuaternion = Quaternionf.Create();

        long collisionTickListenerId;

        sealed class LinkageCollider(long id, int selectionBoxIndex, double length, double halfHeight, double halfWidth, ColliderTransform transform)
        {
            public readonly long Id = id;
            public readonly int SelectionBoxIndex = selectionBoxIndex;
            public readonly double Length = length;
            public readonly double HalfHeight = halfHeight;
            public readonly double HalfWidth = halfWidth;
            public ColliderTransform Transform = transform;
            public bool WasMoving;
        }

        sealed class LinkageSweep(BlockPos origin, RotatedCuboidf[] boxes, Cuboidf bounds)
        {
            public readonly BlockPos Origin = origin;
            public readonly RotatedCuboidf[] Boxes = boxes;
            public readonly Cuboidf Bounds = bounds;
            TransformedCuboidd[]? transformedBoxes;

            public TransformedCuboidd[] TransformedBoxes => transformedBoxes ??= createSweepShapes(this);
        }

        void onBlockPlaced(IServerPlayer byPlayer, int oldBlockId, BlockSelection blockSel, ItemStack withItemStack)
        {
            OnBlockCollisionBoxesChanged(blockSel.Position);
        }

        public void OnBlockCollisionBoxesChanged(BlockPos pos)
        {
            if (api.Side != EnumAppSide.Server
                || !ownersBySpanCell.TryGetValue(pos, out HashSet<BlockPos>? indexedOwners))
            {
                return;
            }

            BlockPos[] ownerPositions = [.. indexedOwners];
            foreach (BlockPos ownerPos in ownerPositions)
            {
                if (!connectionByOwner.TryGetValue(ownerPos, out LinkageConnection? connection)
                    || !tryRefreshSpan(connection, out bool canRemainAttached)
                    || canRemainAttached)
                {
                    continue;
                }

                ILinkageEndpoint? owner = GetEndpoint(ownerPos);
                if (owner != null) returnLinkage(detach(owner, true), null, owner.LinkagePosition);
            }
        }

        // False means the saved endpoints or linkage item could not be resolved.
        bool tryRefreshSpan(LinkageConnection connection, out bool canRemainAttached)
        {
            unindexSpan(connection.OwnerPosition);
            canRemainAttached = true;
            if (!tryGetEndpointSelection(connection.OwnerPosition, connection.OwnerPoint, out LinkageEndpointSelection owner)
                || !tryGetEndpointSelection(connection.TargetPosition, connection.TargetPoint, out LinkageEndpointSelection target))
            {
                return false;
            }
            if ((owner.Endpoint as ILinkageProducer)?.LinkageStack?.Item is not ItemLinkage linkage)
            {
                return false;
            }
            if (owner.Position.Equals(target.Position)
                || owner.Facing.Axis != target.Facing.Axis
                || !linkage.CanAttachTo(owner.Endpoint, owner.Point)
                || !linkage.CanAttachTo(target.Endpoint, target.Point)
                || !isValidLinkageSpan(owner.Position, target.Position, owner.Facing.Axis)
                || !isWithinLinkageRange(owner.Position, target.Position))
            {
                canRemainAttached = false;
                return true;
            }

            HashSet<BlockPos> cells = [];
            canRemainAttached = isSpanClear(owner, target, linkage, cells, out bool hasSweep, GetRodLength(connection));
            if (!hasSweep) return true;

            BlockPos[] indexedCells = [.. cells];
            spanCellsByOwner[connection.OwnerPosition] = indexedCells;
            foreach (BlockPos cell in indexedCells)
            {
                if (!ownersBySpanCell.TryGetValue(cell, out HashSet<BlockPos>? owners))
                {
                    ownersBySpanCell[cell] = owners = [];
                }
                owners.Add(connection.OwnerPosition);
            }

            registerCollider(connection, linkage);
            return true;
        }

        void unindexSpan(BlockPos ownerPos)
        {
            unregisterCollider(ownerPos);
            if (!spanCellsByOwner.Remove(ownerPos, out BlockPos[]? cells)) return;

            foreach (BlockPos cell in cells)
            {
                if (!ownersBySpanCell.TryGetValue(cell, out HashSet<BlockPos>? owners)) continue;

                owners.Remove(ownerPos);
                if (owners.Count == 0) ownersBySpanCell.Remove(cell);
            }
        }

        bool isSpanClear(LinkageEndpointSelection start, LinkageEndpointSelection end, ItemLinkage linkage, HashSet<BlockPos> spanCells, out bool hasSweep, float rodLength = 0)
        {
            hasSweep = tryCreateLinkageSweep(start, end, linkage, rodLength, out LinkageSweep? sweep);
            return sweep != null
                && isSweepClear(sweep, start.Position, end.Position, spanCells)
                && !intersectsRegisteredColliders(sweep);
        }

        bool isPlacementSpanClear(LinkageEndpointSelection start, LinkageEndpointSelection end, ItemLinkage linkage)
        {
            if (!tryCreateLinkageSweep(start, end, linkage, 0, out LinkageSweep? sweep)) return false;

            HashSet<BlockPos> spanCells = [];
            return isSweepClear(sweep, start.Position, end.Position, spanCells)
                && !intersectsRegisteredColliders(sweep)
                && !intersectsExistingLinkage(sweep, spanCells);
        }

        bool tryCreateLinkageSweep(LinkageEndpointSelection start, LinkageEndpointSelection end, ItemLinkage linkage, float rodLength, [NotNullWhen(true)] out LinkageSweep? sweep)
        {
            sweep = null;
            LinkageGeometry.OrderEndpoints(ref start, ref end);
            if (rodLength <= 0) rodLength = GetRodLength(start, end);
            if (rodLength <= 0) return false;

            BlockPos origin = start.Position;
            if (start.Endpoint is not ILinkageProducer producer || end.Endpoint is not ILinkageConsumer consumer) return false;
            LinkageKinematics? kinematics = CreateKinematics(start, end, rodLength);
            if (end.Endpoint is ILinkageProducer && kinematics?.IsValid != true) return false;

            float maxRadius = producer.GetLinkageCrankRadius(start.Point);
            if (end.Endpoint is ILinkageProducer driven) maxRadius = Math.Max(maxRadius, driven.GetLinkageCrankRadius(end.Point));
            // Cover the small gap between sampled crank poses using the connected producers' dimensions.
            float samplePadding = maxRadius * GameMath.PI / LinkageGeometry.SweepSamples;
            RotatedCuboidf[] linkageBoxes = new RotatedCuboidf[LinkageGeometry.SweepSamples];
            Cuboidf bounds = new();
            for (int sample = 0; sample < LinkageGeometry.SweepSamples; sample++)
            {
                float angle = sample * GameMath.TWOPI / LinkageGeometry.SweepSamples;
                start.Endpoint.SetLinkagePin(scratchStartPin, angle, start.Point);
                if (kinematics != null)
                {
                    end.Endpoint.SetLinkagePin(scratchEndPin, (float)kinematics.GetDrivenAngle(angle), end.Point);
                }
                else if (!consumer.TrySetLinkagePinAtAngle(scratchEndPin, start, angle, rodLength, end.Point))
                {
                    return false;
                }

                RotatedCuboidf? linkageBox = createLinkageBox(
                    scratchStartPin,
                    scratchEndPin,
                    origin,
                    start.Facing.Axis,
                    linkage.CollisionHalfHeight,
                    linkage.CollisionHalfWidth,
                    samplePadding
                );
                if (linkageBox == null) return false;

                linkageBoxes[sample] = linkageBox;
                Cuboidf poseBounds = linkageBox.GetAABB();
                if (sample == 0)
                {
                    bounds.Set(poseBounds);
                    continue;
                }

                bounds.X1 = Math.Min(bounds.X1, poseBounds.X1);
                bounds.Y1 = Math.Min(bounds.Y1, poseBounds.Y1);
                bounds.Z1 = Math.Min(bounds.Z1, poseBounds.Z1);
                bounds.X2 = Math.Max(bounds.X2, poseBounds.X2);
                bounds.Y2 = Math.Max(bounds.Y2, poseBounds.Y2);
                bounds.Z2 = Math.Max(bounds.Z2, poseBounds.Z2);
            }

            sweep = new(origin, linkageBoxes, bounds);
            return true;
        }

        bool isSweepClear(LinkageSweep sweep, BlockPos startPos, BlockPos endPos, HashSet<BlockPos> spanCells)
        {
            int minX = sweep.Origin.X + (int)Math.Floor(sweep.Bounds.X1);
            int minY = sweep.Origin.Y + (int)Math.Floor(sweep.Bounds.Y1);
            int minZ = sweep.Origin.Z + (int)Math.Floor(sweep.Bounds.Z1);
            int maxX = sweep.Origin.X + (int)Math.Floor(sweep.Bounds.X2);
            int maxY = sweep.Origin.Y + (int)Math.Floor(sweep.Bounds.Y2);
            int maxZ = sweep.Origin.Z + (int)Math.Floor(sweep.Bounds.Z2);

            IBlockAccessor blockAccessor = api.World.BlockAccessor;
            BlockPos pos = new(startPos.dimension);
            Vec3d offset = new();
            TransformedCuboidd collisionBox = new();
            bool clear = true;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        pos.Set(x, y, z);
                        offset.Set(x - sweep.Origin.X, y - sweep.Origin.Y, z - sweep.Origin.Z);
                        bool intersectsSpan = false;
                        foreach (RotatedCuboidf linkageBox in sweep.Boxes)
                        {
                            if (!linkageBox.IntersectsOrTouches(blockCell, offset)) continue;

                            intersectsSpan = true;
                            break;
                        }
                        if (!intersectsSpan) continue;

                        spanCells.Add(pos.Copy());
                        if (pos.Equals(startPos) || pos.Equals(endPos)) continue;

                        Block block = blockAccessor.GetBlock(pos);
                        Cuboidf[]? collisionBoxes = block.GetCollisionBoxes(blockAccessor, pos);
                        if (collisionBoxes == null) continue;

                        bool obstructed = false;
                        foreach (Cuboidf box in collisionBoxes)
                        {
                            Cuboidd worldBox = box.OffsetCopyDouble(pos.X, pos.Y, pos.Z);
                            if (worldBox is RotatedCuboidd rotated && rotated.IsRotated()) collisionBox.Set(worldBox, rotated.GetRotationMatrix());
                            else collisionBox.Set(worldBox);
                            foreach (TransformedCuboidd linkageBox in sweep.TransformedBoxes)
                            {
                                if (!linkageBox.Intersects(collisionBox)) continue;

                                obstructed = true;
                                break;
                            }
                            if (obstructed) break;
                        }
                        if (!obstructed) continue;

                        clear = false;
                    }
                }
            }
            return clear;
        }

        bool intersectsRegisteredColliders(LinkageSweep sweep)
        {
            List<Collider> colliders = [];
            api.World.CollisionScene.GetColliders(
                sweep.Bounds.OffsetCopyDouble(sweep.Origin.X, sweep.Origin.Y, sweep.Origin.Z),
                sweep.Origin.dimension,
                colliders,
                new CollisionQueryFilter(CollisionLayer.Solid, CollisionLayer.Solid)
            );
            foreach (Collider collider in colliders)
            {
                foreach (TransformedCuboidd box in sweep.TransformedBoxes)
                {
                    if (collider.Intersects(box)) return true;
                }
            }
            return false;
        }

        bool intersectsExistingLinkage(LinkageSweep sweep, HashSet<BlockPos> spanCells)
        {
            HashSet<BlockPos>? candidateOwners = null;
            foreach (BlockPos cell in spanCells)
            {
                if (!ownersBySpanCell.TryGetValue(cell, out HashSet<BlockPos>? owners)) continue;

                candidateOwners ??= [];
                candidateOwners.UnionWith(owners);
            }
            if (candidateOwners == null) return false;

            TransformedCuboidd[] sweepShapes = sweep.TransformedBoxes;
            foreach (BlockPos ownerPos in candidateOwners)
            {
                if (!connectionByOwner.TryGetValue(ownerPos, out LinkageConnection? connection)
                    || !tryGetEndpointSelection(connection.OwnerPosition, connection.OwnerPoint, out LinkageEndpointSelection owner)
                    || !tryGetEndpointSelection(connection.TargetPosition, connection.TargetPoint, out LinkageEndpointSelection target)
                    || (owner.Endpoint as ILinkageProducer)?.LinkageStack?.Item is not ItemLinkage linkage
                    || !tryCreateLinkageSweep(owner, target, linkage, GetRodLength(connection), out LinkageSweep? otherSweep)
                    || !sweepBoundsIntersect(sweep, otherSweep))
                {
                    continue;
                }

                TransformedCuboidd[] otherShapes = otherSweep.TransformedBoxes;
                foreach (TransformedCuboidd shape in sweepShapes)
                {
                    foreach (TransformedCuboidd otherShape in otherShapes)
                    {
                        if (shape.Intersects(otherShape)) return true;
                    }
                }
            }

            return false;
        }

        static bool sweepBoundsIntersect(LinkageSweep first, LinkageSweep second)
        {
            return first.Origin.X + first.Bounds.X2 > second.Origin.X + second.Bounds.X1
                && first.Origin.X + first.Bounds.X1 < second.Origin.X + second.Bounds.X2
                && first.Origin.Y + first.Bounds.Y2 > second.Origin.Y + second.Bounds.Y1
                && first.Origin.Y + first.Bounds.Y1 < second.Origin.Y + second.Bounds.Y2
                && first.Origin.Z + first.Bounds.Z2 > second.Origin.Z + second.Bounds.Z1
                && first.Origin.Z + first.Bounds.Z1 < second.Origin.Z + second.Bounds.Z2;
        }

        static TransformedCuboidd[] createSweepShapes(LinkageSweep sweep)
        {
            TransformedCuboidd[] shapes = new TransformedCuboidd[sweep.Boxes.Length];
            for (int i = 0; i < sweep.Boxes.Length; i++)
            {
                RotatedCuboidd box = new(sweep.Boxes[i], sweep.Origin.X, sweep.Origin.Y, sweep.Origin.Z);
                shapes[i] = new(box, box.GetRotationMatrix());
            }
            return shapes;
        }

        // Cuboid copies the quaternion it is given so every sample of a sweep can share the scratch matrices.
        RotatedCuboidf? createLinkageBox(Vec3d start, Vec3d end, BlockPos origin, EnumAxis axleAxis, float collisionHalfHeight, float collisionHalfWidth, float samplePadding)
        {
            Mat4f.Identity(collisionModelMatrix);
            if (!LinkageGeometry.TrySetRodOrientation(collisionModelMatrix, start, end, axleAxis, out double length)) return null;

            LinkageGeometry.SetRodRotationQuaternion(collisionQuaternion, collisionModelMatrix, collisionRotationMatrix);

            // Keep the OBB local to an endpoint so its float geometry stays precise at large world coordinates.
            Vec3f localStart = new((float)(start.X - origin.X), (float)(start.Y - origin.InternalY), (float)(start.Z - origin.Z));
            float halfHeight = collisionHalfHeight + samplePadding;
            float halfWidth = collisionHalfWidth + samplePadding;
            float endOverhang = LinkageGeometry.RodEndOverhang + samplePadding;
            Cuboidf cuboid = new(localStart.X - endOverhang, localStart.Y - halfHeight, localStart.Z - halfWidth,
                localStart.X + (float)length + endOverhang, localStart.Y + halfHeight, localStart.Z + halfWidth);
            return new RotatedCuboidf(cuboid, localStart, collisionQuaternion);
        }

        void updateColliders(float elapsedSeconds)
        {
            List<BlockPos>? staleOwners = null;
            foreach ((BlockPos ownerPos, LinkageCollider collider) in colliderByOwner)
            {
                if (!connectionByOwner.TryGetValue(ownerPos, out LinkageConnection? connection)
                    || !tryGetCollisionTransform(connection, out ColliderTransform transform))
                {
                    staleOwners ??= [];
                    staleOwners.Add(ownerPos);
                    continue;
                }

                bool moved = !transform.Equals(collider.Transform);
                if (!moved && !collider.WasMoving) continue;

                api.World.CollisionScene.UpdateColliderTransform(collider.Id, ownerPos.dimension, transform, elapsedSeconds);
                collider.Transform = transform;
                collider.WasMoving = moved;
            }

            if (staleOwners == null) return;

            foreach (BlockPos ownerPos in staleOwners) unregisterCollider(ownerPos);
        }

        void registerCollider(LinkageConnection connection, ItemLinkage linkage)
        {
            float rodLength = GetRodLength(connection);
            if (rodLength <= 0 || !tryGetCollisionTransform(connection, out ColliderTransform transform)) return;

            double halfHeight = linkage.CollisionHalfHeight;
            double halfWidth = linkage.CollisionHalfWidth;
            double endOverhang = LinkageGeometry.RodEndOverhang;
            Cuboidd[] localCollisionBoxes = [new(-endOverhang, -halfHeight, -halfWidth, rodLength + endOverhang, halfHeight, halfWidth)];
            var blockAccessor = api.World.BlockAccessor;
            int selectionBoxIndex = blockAccessor.GetBlock(connection.OwnerPosition).GetSelectionBoxes(blockAccessor, connection.OwnerPosition)?.Length ?? 0;

            long colliderId = api.World.CollisionScene.RegisterTransformedCollider(
                connection.OwnerPosition.dimension,
                localCollisionBoxes,
                transform,
                canCarryEntities: true,
                selectionPosition: connection.OwnerPosition,
                selectionBoxIndices: [selectionBoxIndex]
            );
            double length = rodLength + 2 * LinkageGeometry.RodEndOverhang;
            colliderByOwner[connection.OwnerPosition] = new(colliderId, selectionBoxIndex, length, halfHeight, halfWidth, transform);
        }

        RotatedCuboidf createSelectionBox(BlockPos ownerPos, ColliderTransform transform, double length, double halfHeight, double halfWidth)
        {
            Vec3f local = new((float)(transform.X - ownerPos.X), (float)(transform.Y - ownerPos.Y), (float)(transform.Z - ownerPos.Z));
            float endOverhang = LinkageGeometry.RodEndOverhang;
            Cuboidf box = new(local.X - endOverhang, local.Y - (float)halfHeight, local.Z - (float)halfWidth,
                local.X + (float)length - endOverhang, local.Y + (float)halfHeight, local.Z + (float)halfWidth);
            collisionQuaternion[0] = (float)transform.QuaternionX;
            collisionQuaternion[1] = (float)transform.QuaternionY;
            collisionQuaternion[2] = (float)transform.QuaternionZ;
            collisionQuaternion[3] = (float)transform.QuaternionW;
            return new RotatedCuboidf(box, local, collisionQuaternion);
        }

        public Cuboidf[] GetSelectionBoxes(BlockPos ownerPos, Cuboidf[] baseBoxes)
        {
            if (!colliderByOwner.TryGetValue(ownerPos, out LinkageCollider? collider)
                || collider.SelectionBoxIndex != baseBoxes.Length)
            {
                return baseBoxes;
            }

            Cuboidf[] boxes = new Cuboidf[baseBoxes.Length + 1];
            Array.Copy(baseBoxes, boxes, baseBoxes.Length);
            boxes[collider.SelectionBoxIndex] = createSelectionBox(ownerPos, collider.Transform, collider.Length, collider.HalfHeight, collider.HalfWidth);
            return boxes;
        }

        public bool IsLinkageSelection(BlockSelection blockSel)
        {
            return colliderByOwner.TryGetValue(blockSel.Position, out LinkageCollider? collider)
                && collider.SelectionBoxIndex == blockSel.SelectionBoxIndex;
        }

        bool tryGetLinkageSelectionPoint(BlockSelection blockSel, out int point)
        {
            point = -1;
            if (!IsLinkageSelection(blockSel)
                || !connectionByOwner.TryGetValue(blockSel.Position, out LinkageConnection? connection))
            {
                return false;
            }

            point = connection.OwnerPoint;
            return true;
        }

        bool tryGetCollisionTransform(LinkageConnection connection, out ColliderTransform transform)
        {
            transform = default;
            if (!tryGetEndpointSelection(connection.OwnerPosition, connection.OwnerPoint, out LinkageEndpointSelection start)
                || !tryGetEndpointSelection(connection.TargetPosition, connection.TargetPoint, out LinkageEndpointSelection end)
                || start.Facing.Axis != end.Facing.Axis)
            {
                return false;
            }

            LinkageGeometry.OrderEndpoints(ref start, ref end);
            start.Endpoint.SetLinkagePin(collisionStartPin, start.Endpoint.LinkageAngleRad, start.Point);
            end.Endpoint.SetLinkagePin(collisionEndPin, end.Endpoint.LinkageAngleRad, end.Point);

            Mat4f.Identity(collisionModelMatrix);
            if (!LinkageGeometry.TrySetRodOrientation(collisionModelMatrix, collisionStartPin, collisionEndPin, start.Facing.Axis, out _)) return false;

            LinkageGeometry.SetRodRotationQuaternion(collisionQuaternion, collisionModelMatrix, collisionRotationMatrix);
            double localY = collisionStartPin.Y - connection.OwnerPosition.dimension * BlockPos.DimensionBoundary;
            transform = new(collisionStartPin.X, localY, collisionStartPin.Z, collisionQuaternion[0], collisionQuaternion[1], collisionQuaternion[2], collisionQuaternion[3]);
            return true;
        }

        void unregisterCollider(BlockPos ownerPos)
        {
            if (!colliderByOwner.Remove(ownerPos, out LinkageCollider? collider)) return;

            api.World.CollisionScene.UnregisterCollider(collider.Id);
        }

        void clearCollisionState()
        {
            foreach (LinkageCollider collider in colliderByOwner.Values)
            {
                api.World.CollisionScene.UnregisterCollider(collider.Id);
            }
            colliderByOwner.Clear();
            ownersBySpanCell.Clear();
            spanCellsByOwner.Clear();
        }
    }
}
