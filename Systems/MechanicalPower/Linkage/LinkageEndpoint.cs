using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public interface ILinkageEndpoint
    {
        BlockPos LinkagePosition { get; }
        float LinkageAngleRad { get; }

        bool IsValidLinkagePoint(int pointIndex);
        int GetLinkagePointAt(BlockSelection blockSel);
        bool CanAttachLinkage(ItemLinkage linkage, int point);
        BlockFacing GetLinkageFacing(int point);
        void SetLinkagePin(Vec3d pin, float angle, int point);

        /// <summary>
        /// Writes the same pin in its unanimated reference pose, used to measure the fixed rod length.
        /// </summary>
        void SetLinkagePinRestingPose(Vec3d pin, int point);
    }

    /// <summary>
    /// A mechanical crank that can drive a linkage. Pin angles turn around the negative X or Z axis.
    /// Crank radii are in blocks and phases are in radians.
    /// </summary>
    public interface ILinkageProducer : ILinkageEndpoint, IMechanicalPowerDevice
    {
        ItemStack? LinkageStack { get; }
        float LinkageRodLength { get; }

        bool TryGetLinkageTarget(BlockPos target, out int ownerPoint, out int targetPoint);
        void SetLinkage(BlockPos target, ItemStack stack, int ownerPoint, int targetPoint, float rodLength);
        void SetLinkageRodLength(float rodLength);
        ItemStack? TakeLinkage();

        /// <summary>
        /// Point used to start network discovery when no linkage is attached.
        /// </summary>
        int DefaultLinkagePoint { get; }

        /// <summary>
        /// Signed local speed in mechanical network units, before applying IsRotationReversed().
        /// </summary>
        float LinkageSpeed { get; }
        LinkageAngleTransform? LinkageAngleTransform { get; }
        bool LinkageGearingConflict { get; set; }

        /// <summary>
        /// Writes the crank center in world coordinates, in the same plane as its pin.
        /// </summary>
        void SetLinkageCrankCenter(Vec3d center, int point);
        float GetLinkageCrankRadius(int point);
        float GetLinkageCrankPhase(int point);
    }

    public interface ILinkageConsumer : ILinkageEndpoint
    {
        bool TrySetLinkagePinAtAngle(Vec3d pin, LinkageEndpointSelection drive, float driveAngle, float rodLength, int point);
        bool CanFollowLinkage(LinkageEndpointSelection other, int point);

        /// <summary>
        /// Returns resistance at the driving crank, or null to keep the producer's own resistance.
        /// The crank radius is in blocks.
        /// </summary>
        float? GetLinkageResistance(float crankRadius, int pointIndex);

        /// <summary>
        /// Networked consumers join and propagate the driving network here. Terminal consumers stop discovery.
        /// Returns false only when discovery must wait for missingChunkPos to load.
        /// </summary>
        bool JoinLinkageNetwork(ICoreAPI api, MechanicalNetwork network, LinkageEndpointSelection drive, int point, out Vec3i? missingChunkPos)
        {
            missingChunkPos = null;
            return true;
        }
    }

    public readonly struct LinkageEndpointSelection(ILinkageEndpoint endpoint, int point)
    {
        public ILinkageEndpoint Endpoint { get; } = endpoint;
        public int Point { get; } = point;
        public BlockPos Position => Endpoint.LinkagePosition;
        public BlockFacing Facing => Endpoint.GetLinkageFacing(Point);
    }

    public sealed class LinkageConnection(BlockPos ownerPosition, int ownerPoint, BlockPos targetPosition, int targetPoint)
    {
        public BlockPos OwnerPosition { get; } = ownerPosition.Copy();
        public BlockPos TargetPosition { get; } = targetPosition.Copy();
        public int OwnerPoint { get; } = ownerPoint;
        public int TargetPoint { get; } = targetPoint;

        /// <summary>
        /// Resting rod length, filled the first time both endpoints resolve.
        /// Reindexing creates a new connection, so the cached length cannot outlive its endpoints.
        /// </summary>
        public float RodLength;
        internal LinkageKinematics? ForwardKinematics;
        internal LinkageKinematics? ReverseKinematics;
    }

    public static class LinkageGeometry
    {
        public const int SweepSamples = 36;
        public const float RodEndOverhang = 2f / 16;

        // Draw and collide from one canonical producer so both systems use the same phase and orientation.
        public static void OrderEndpoints(ref LinkageEndpointSelection start, ref LinkageEndpointSelection end)
        {
            bool startDrives = start.Endpoint is ILinkageProducer && end.Endpoint is ILinkageConsumer;
            bool endDrives = end.Endpoint is ILinkageProducer && start.Endpoint is ILinkageConsumer;
            if (startDrives != endDrives)
            {
                if (endDrives) (start, end) = (end, start);
                return;
            }

            float startRadius = (start.Endpoint as ILinkageProducer)?.GetLinkageCrankRadius(start.Point) ?? float.PositiveInfinity;
            float endRadius = (end.Endpoint as ILinkageProducer)?.GetLinkageCrankRadius(end.Point) ?? float.PositiveInfinity;
            int radiusOrder = startRadius.CompareTo(endRadius);
            if (radiusOrder > 0 || radiusOrder == 0 && isAfter(start.Position, end.Position)) (start, end) = (end, start);
        }

        /// <summary>
        /// Writes the rod's local axes into the rotation columns of a 4x4 matrix.
        /// Local X then follows the rod and local Y follows the unsigned axle axis.
        /// </summary>
        public static bool TrySetRodOrientation(float[] matrix, Vec3d start, Vec3d end, EnumAxis axleAxis, out double length)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double dz = end.Z - start.Z;
            length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length <= 0) return false;

            double xAxisX = dx / length;
            double xAxisY = dy / length;
            double xAxisZ = dz / length;
            double yAxisX = axleAxis == EnumAxis.X ? 1 : 0;
            double yAxisY = 0;
            double yAxisZ = axleAxis == EnumAxis.Z ? 1 : 0;
            double axialProjection = yAxisX * xAxisX + yAxisZ * xAxisZ;
            yAxisX -= xAxisX * axialProjection;
            yAxisY -= xAxisY * axialProjection;
            yAxisZ -= xAxisZ * axialProjection;
            double yAxisLength = Math.Sqrt(yAxisX * yAxisX + yAxisY * yAxisY + yAxisZ * yAxisZ);
            if (yAxisLength < 0.0001) return false;

            yAxisX /= yAxisLength;
            yAxisY /= yAxisLength;
            yAxisZ /= yAxisLength;
            double zAxisX = xAxisY * yAxisZ - xAxisZ * yAxisY;
            double zAxisY = xAxisZ * yAxisX - xAxisX * yAxisZ;
            double zAxisZ = xAxisX * yAxisY - xAxisY * yAxisX;

            matrix[0] = (float)xAxisX;
            matrix[1] = (float)xAxisY;
            matrix[2] = (float)xAxisZ;
            matrix[4] = (float)yAxisX;
            matrix[5] = (float)yAxisY;
            matrix[6] = (float)yAxisZ;
            matrix[8] = (float)zAxisX;
            matrix[9] = (float)zAxisY;
            matrix[10] = (float)zAxisZ;
            return true;
        }

        /// <summary>
        /// Converts the column-major rod orientation to the row-major matrix expected by Quaternionf.FromMat3().
        /// </summary>
        public static void SetRodRotationQuaternion(float[] quaternion, float[] orientation, float[] rotationMatrix)
        {
            rotationMatrix[0] = orientation[0];
            rotationMatrix[1] = orientation[4];
            rotationMatrix[2] = orientation[8];
            rotationMatrix[3] = orientation[1];
            rotationMatrix[4] = orientation[5];
            rotationMatrix[5] = orientation[9];
            rotationMatrix[6] = orientation[2];
            rotationMatrix[7] = orientation[6];
            rotationMatrix[8] = orientation[10];
            Quaternionf.Normalize(quaternion, Quaternionf.FromMat3(quaternion, rotationMatrix));
        }

        private static bool isAfter(BlockPos first, BlockPos second)
        {
            if (first.X != second.X) return first.X > second.X;
            if (first.InternalY != second.InternalY) return first.InternalY > second.InternalY;
            return first.Z > second.Z;
        }
    }

    public sealed class LinkageEndpointState
    {
        const string linkageOffsetKey = "linkageOffset";
        const string linkageOwnerPointKey = "linkageOwnerPoint";
        const string linkageTargetPointKey = "linkageTargetPoint";
        const string linkageStackKey = "linkageStack";
        const string linkageRodLengthKey = "linkageRodLength";

        Vec3i? targetOffset;
        int ownerPoint;
        int targetPoint;
        ItemStack? linkageStack;
        float rodLength;

        public ItemStack? LinkageStack => linkageStack?.Item is ItemLinkage ? linkageStack : null;
        public float RodLength => rodLength;

        public bool TryGetTarget(BlockPos ownerPos, BlockPos target, out int storedOwnerPoint, out int storedTargetPoint)
        {
            storedOwnerPoint = ownerPoint;
            storedTargetPoint = targetPoint;
            if (targetOffset == null || LinkageStack == null) return false;

            target.Set(ownerPos).Add(targetOffset);
            return true;
        }

        public void Set(BlockPos ownerPos, BlockPos target, ItemStack stack, int ownerPoint, int targetPoint, float rodLength)
        {
            targetOffset = new(target.X - ownerPos.X, target.Y - ownerPos.Y, target.Z - ownerPos.Z);
            this.ownerPoint = ownerPoint;
            this.targetPoint = targetPoint;
            linkageStack = stack.Clone();
            linkageStack.StackSize = 1;
            this.rodLength = rodLength;
        }

        public void SetRodLength(float rodLength)
        {
            this.rodLength = rodLength;
        }

        public ItemStack? Take()
        {
            ItemStack? stack = linkageStack;
            targetOffset = null;
            ownerPoint = 0;
            targetPoint = 0;
            linkageStack = null;
            rodLength = 0;
            return stack;
        }

        public void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
        {
            targetOffset = tree.GetVec3i(linkageOffsetKey);
            ownerPoint = tree.GetInt(linkageOwnerPointKey);
            targetPoint = tree.GetInt(linkageTargetPointKey);
            linkageStack = tree.GetItemstack(linkageStackKey);
            rodLength = tree.GetFloat(linkageRodLengthKey);
            linkageStack?.ResolveBlockOrItem(world);

            if (targetOffset == null || linkageStack == null)
            {
                Take();
            }
        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            if (targetOffset == null || LinkageStack == null)
            {
                tree.RemoveAttribute(linkageOffsetKey);
                tree.RemoveAttribute(linkageOwnerPointKey);
                tree.RemoveAttribute(linkageTargetPointKey);
                tree.RemoveAttribute(linkageStackKey);
                tree.RemoveAttribute(linkageRodLengthKey);
                return;
            }

            tree.SetVec3i(linkageOffsetKey, targetOffset);
            tree.SetInt(linkageOwnerPointKey, ownerPoint);
            tree.SetInt(linkageTargetPointKey, targetPoint);
            tree.SetItemstack(linkageStackKey, linkageStack);
            tree.SetFloat(linkageRodLengthKey, rodLength);
        }

        public void OnLoadCollectibleMappings(IWorldAccessor world, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, bool resolveImports)
        {
            if (linkageStack == null) return;

            if (!linkageStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, world)
                || linkageStack.Item is not ItemLinkage)
            {
                Take();
                return;
            }

            linkageStack.Collectible.OnLoadCollectibleMappings(world, new DummySlot(linkageStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }

        public void OnStoreCollectibleMappings(IWorldAccessor world, Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            ItemStack? stack = LinkageStack;
            stack?.Collectible.OnStoreCollectibleMappings(world, new DummySlot(stack), blockIdMapping, itemIdMapping);
        }

        public static void OnTransformed(ITreeAttribute tree, int degreeRotation, EnumAxis? flipAxis)
        {
            Vec3i? offset = tree.GetVec3i(linkageOffsetKey);
            if (offset == null) return;

            if (flipAxis == EnumAxis.X) offset.X = -offset.X;
            if (flipAxis == EnumAxis.Y) offset.Y = -offset.Y;
            if (flipAxis == EnumAxis.Z) offset.Z = -offset.Z;

            (offset.X, offset.Z) = degreeRotation switch
            {
                90 => (-offset.Z, offset.X),
                180 => (-offset.X, -offset.Z),
                270 => (offset.Z, -offset.X),
                _ => (offset.X, offset.Z)
            };

            tree.SetVec3i(linkageOffsetKey, offset);
        }
    }
}
