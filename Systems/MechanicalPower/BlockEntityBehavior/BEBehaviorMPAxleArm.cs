using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPAxleArm : BEBehaviorMPConsumer, ILinkageProducer, ILinkageConsumer, IRotatable
    {
        const string crankHoleKey = "crankHole";

        readonly LinkageEndpointState linkage = new();

        int maxLinkagePinSize;
        int crankHole;
        BlockFacing? facing;
        ModSystemLinkage linkageSystem = null!;

        public BlockPos LinkagePosition => Position;
        public float LinkageAngleRad => AngleRad;
        public int DefaultLinkagePoint => 0;
        public float LinkageSpeed => CurrentSpeed;
        LinkageAngleTransform? ILinkageProducer.LinkageAngleTransform => AngleTransform;
        bool ILinkageProducer.LinkageGearingConflict { get => GearingConflict; set => GearingConflict = value; }
        public ItemStack? LinkageStack => linkage.LinkageStack;
        public float LinkageRodLength => linkage.RodLength;
        public int CrankHole => crankHole;
        public float CrankRadius => BlockAxleArm.GetCrankRadius(crankHole);
        public float CrankPhase => BlockAxleArm.GetCrankPhase(crankHole, GetLinkageFacing(0));

        public BEBehaviorMPAxleArm(BlockEntity blockEntity) : base(blockEntity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            linkageSystem ??= api.ModLoader.GetModSystem<ModSystemLinkage>()
                ?? throw new InvalidOperationException("The linkage mod system is not loaded.");
            maxLinkagePinSize = properties["maxLinkagePinSize"].AsInt(1);
            if (maxLinkagePinSize <= 0) throw new InvalidOperationException($"Invalid maximum linkage pin size for {Block.Code}.");
            base.Initialize(api, properties);

            facing = BlockFacing.FromCode(Block.Variant["side"]);
            linkageSystem.RefreshOwner(this);
            linkageSystem.ReconnectEndpoint(this);
        }

        protected override CompositeShape GetShape()
        {
            ItemStack? stack = Api == null ? LinkageStack : linkageSystem.GetLinkageStack(this);
            int pinSize = (stack?.Item as ItemLinkage)?.PinSize ?? 1;
            return new AxleArmShape(Block.Shape, crankHole, pinSize);
        }

        internal void RefreshShape()
        {
            if (Api != null) updateShape(Api.World);
        }

        public bool IsValidLinkagePoint(int pointIndex)
        {
            return pointIndex == 0;
        }

        // The arm carries one crank peg, so anywhere on it selects the same point.
        public int GetLinkagePointAt(BlockSelection blockSel)
        {
            return 0;
        }

        public bool CanAttachLinkage(ItemLinkage linkage, int point)
        {
            return linkage.PinSize <= maxLinkagePinSize;
        }

        public BlockFacing GetLinkageFacing(int point)
        {
            return facing ??= BlockFacing.FromCode(Block.Variant["side"]);
        }

        public void SetLinkagePin(Vec3d pin, float angle, int point)
        {
            BlockFacing facing = GetLinkageFacing(point);
            float radius = GetLinkageCrankRadius(point);
            angle += GetLinkageCrankPhase(point);
            SetLinkageCrankCenter(pin, point);
            pin.Y -= Math.Cos(angle) * radius;
            double radialSide = Math.Sin(angle) * radius;

            // Axle arms rotate around the negative X or Z axis in the mechanical renderer.
            if (facing.Axis == EnumAxis.X) pin.Z += radialSide;
            else pin.X -= radialSide;
        }

        public void SetLinkagePinRestingPose(Vec3d pin, int point)
        {
            SetLinkagePin(pin, 0, point);
        }

        public void SetLinkageCrankCenter(Vec3d center, int point)
        {
            BlockFacing facing = GetLinkageFacing(point);
            center.SetWithDimension(LinkagePosition).Add(0.5).Sub(facing.Normali.X * BlockAxleArm.CrankAxialOffset, 0, facing.Normali.Z * BlockAxleArm.CrankAxialOffset);
        }

        public bool TrySetLinkagePinAtAngle(Vec3d pin, LinkageEndpointSelection drive, float driveAngle, float rodLength, int point)
        {
            LinkageKinematics? kinematics = linkageSystem.CreateKinematics(drive, new(this, point), rodLength);
            if (kinematics?.IsValid != true) return false;

            SetLinkagePin(pin, (float)kinematics.GetDrivenAngle(driveAngle), point);
            return true;
        }

        // Crank-to-crank reach is checked by the linkage system using both peg positions.
        public bool CanFollowLinkage(LinkageEndpointSelection other, int point)
        {
            return true;
        }

        public bool TryGetLinkageTarget(BlockPos target, out int ownerPoint, out int targetPoint)
        {
            return linkage.TryGetTarget(LinkagePosition, target, out ownerPoint, out targetPoint);
        }

        public void SetLinkage(BlockPos target, ItemStack stack, int ownerPoint, int targetPoint, float rodLength)
        {
            linkage.Set(LinkagePosition, target, stack, ownerPoint, targetPoint, rodLength);
            linkageSystem.RefreshOwner(this);
            Blockentity.MarkDirty(true);
        }

        public void SetLinkageRodLength(float rodLength)
        {
            linkage.SetRodLength(rodLength);
            Blockentity.MarkDirty(true);
        }

        // A connected rod fixes the peg position.
        public bool TrySetCrankHole(int hole)
        {
            if ((uint)hole >= BlockAxleArm.CrankHoleCount
                || linkageSystem.IsLinked(this))
            {
                return false;
            }
            if (crankHole == hole) return true;

            crankHole = hole;
            updateShape(Api.World);
            Blockentity.MarkDirty(true);
            return true;
        }

        public ItemStack? TakeLinkage()
        {
            ItemStack? stack = linkage.Take();
            linkageSystem.RefreshOwner(this);
            Blockentity.MarkDirty(true);
            return stack;
        }

        public override float GetResistance()
        {
            ILinkageConsumer? endpoint = linkageSystem.GetConnectedEndpoint(this) as ILinkageConsumer;
            return endpoint?.GetLinkageResistance(CrankRadius, linkageSystem.GetLinkagePoint(endpoint)) ?? base.GetResistance();
        }

        public virtual float? GetLinkageResistance(float crankRadius, int pointIndex) => null;

        public virtual float GetLinkageCrankRadius(int pointIndex) => CrankRadius;

        public virtual float GetLinkageCrankPhase(int pointIndex) => CrankPhase;

        public override bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, MechanicalNetwork network, MechPowerPath path, out Vec3i? missingChunkPos)
        {
            // A neighbour can discover this arm before Initialize runs.
            linkageSystem ??= api.ModLoader.GetModSystem<ModSystemLinkage>()
                ?? throw new InvalidOperationException("The linkage mod system is not loaded.");
            bool alreadyJoined = Network?.networkId == network.networkId;
            if (!base.JoinAndSpreadNetworkToNeighbours(api, network, path, out missingChunkPos)) return false;
            if (alreadyJoined) return true;

            return linkageSystem.SpreadLinkageNetwork(api, this, network, out missingChunkPos);
        }

        protected override bool tryConnectToNetwork(BlockFacing toFacing, out Vec3i? missingChunkPos)
        {
            MechanicalNetwork? previousNetwork = Network;
            if (!base.tryConnectToNetwork(toFacing, out missingChunkPos)) return false;
            if (Network == null || Network == previousNetwork) return true;

            return linkageSystem.SpreadLinkageNetwork(Api, this, Network, out missingChunkPos);
        }

        public override MechanicalNetwork? CreateJoinAndDiscoverNetwork(BlockFacing powerOutFacing)
        {
            // The other endpoint could load before this behavior is initialized.
            if (manager == null) return null;

            MechanicalNetwork? network = base.CreateJoinAndDiscoverNetwork(powerOutFacing);
            if (network == null) return null;
            if (!linkageSystem.SpreadLinkageNetwork(Api, this, network, out Vec3i? missingChunkPos))
            {
                network.AwaitChunkThenDiscover(missingChunkPos);
                manager.testFullyLoaded(network);
            }
            return network;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            crankHole = GameMath.Clamp(tree.GetInt(crankHoleKey), 0, BlockAxleArm.CrankHoleCount - 1);
            linkage.FromTreeAttributes(tree, worldAccessForResolve);
            if (Api == null) return;

            linkageSystem.RefreshOwner(this);
            linkageSystem.ReconnectEndpoint(this);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt(crankHoleKey, crankHole);
            linkage.ToTreeAttributes(tree);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            linkage.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, resolveImports);
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            linkage.OnStoreCollectibleMappings(Api.World, blockIdMapping, itemIdMapping);
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            LinkageEndpointState.OnTransformed(tree, degreeRotation, flipAxis);
        }

        public override void OnBlockBroken(IPlayer? byPlayer = null)
        {
            linkageSystem.DetachForBreak(this, byPlayer);
            base.OnBlockBroken(byPlayer);
        }

        public override void OnBlockRemoved()
        {
            linkageSystem.DetachForBreak(this, null);
            linkageSystem.UnregisterEndpoint(LinkagePosition);
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            linkageSystem.UnregisterEndpoint(LinkagePosition);
            base.OnBlockUnloaded();
        }

        public virtual bool JoinLinkageNetwork(ICoreAPI api, MechanicalNetwork network, LinkageEndpointSelection drive, int point, out Vec3i? missingChunkPos)
        {
            Api = api;
            linkageSystem ??= api.ModLoader.GetModSystem<ModSystemLinkage>()
                ?? throw new InvalidOperationException("The linkage mod system is not loaded.");
            missingChunkPos = null;
            ILinkageProducer producer = (ILinkageProducer)drive.Endpoint;
            LinkageKinematics? kinematics = linkageSystem.GetKinematics(producer, this);
            if (kinematics?.IsValid != true)
            {
                producer.LinkageGearingConflict = true;
                return true;
            }
            MechPowerPath shaftPath = getLinkagePath(producer, kinematics, point);
            if (Network?.networkId == network.networkId)
            {
                if (HasGearingConflict(shaftPath)
                    || !LinkageAngleTransform.Matches(AngleTransform, shaftPath.AngleTransform))
                {
                    GearingConflict = true;
                }
                network.ConnectLoad(producer as BEBehaviorMPBase, this);
                return true;
            }

            JoinNetwork(network, shaftPath);
            network.ConnectLoad(producer as BEBehaviorMPBase, this);

            BlockPos shaftPos = Position.AddCopy(shaftPath.OutFacing);
            return spreadTo(api, network, shaftPos, shaftPath, out missingChunkPos);
        }

        MechPowerPath getLinkagePath(ILinkageProducer drive, LinkageKinematics kinematics, int point)
        {
            BlockFacing linkageFacing = GetLinkageFacing(point);
            bool inverted = drive.GetPropagationDirection() == linkageFacing.Opposite;
            MechPowerPath shaftPath = new(linkageFacing, drive.GearedRatio, Position, inverted);
            if (kinematics.IsIdentity)
            {
                shaftPath.AngleTransform = drive.LinkageAngleTransform;
            }
            else
            {
                shaftPath.AngleTransform = new(
                    drive.LinkageAngleTransform,
                    kinematics,
                    drive.GearedRatio,
                    drive.IsRotationReversed(),
                    shaftPath.gearingRatio,
                    isRotationReversed(shaftPath.NetworkDir())
                );
            }
            return shaftPath;
        }

        static bool isRotationReversed(BlockFacing direction)
        {
            return direction == BlockFacing.DOWN || direction == BlockFacing.EAST || direction == BlockFacing.SOUTH;
        }
    }
}
