using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent.Mechanics
{
    public enum EnumLinkageAction
    {
        Begin,
        Complete,
        Cancel,
        Remove
    }

    [ProtoContract]
    public class LinkageUpdatePacket
    {
        [ProtoMember(1)]
        public EnumLinkageAction Action;

        [ProtoMember(2)]
        public BlockPos? Position;

        [ProtoMember(3)]
        public int Point;
    }

    public class LinkagePlacerWorkspace
    {
        public bool NowBuilding;
        public BlockPos? StartPosition;
        public int StartPoint;
    }

    public partial class ModSystemLinkage : ModSystem
    {
        public const int MaxLength = 4;
        public const int MaxLengthSquared = MaxLength * MaxLength;
        const int maxPlacementDistanceSquared = 10 * 10;
        const string channelName = "linkageplacer";

        static readonly Vec2i[] candidateOffsets = createCandidateOffsets();

        readonly Dictionary<string, LinkagePlacerWorkspace> workspaceByPlayer = [];
        readonly Dictionary<BlockPos, LinkageConnection> connectionByOwner = [];
        readonly Dictionary<BlockPos, BlockPos> ownerByEndpoint = [];
        readonly Vec3d scratchStartPin = new();
        readonly Vec3d scratchEndPin = new();

        ICoreAPI api = null!;
        MechanicalPowerMod mechanicalPower = null!;
        ICoreClientAPI? capi;
        ICoreServerAPI? sapi;
        IClientNetworkChannel? clientChannel;
        LinkageRenderer? renderer;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            mechanicalPower = api.ModLoader.GetModSystem<MechanicalPowerMod>()
                ?? throw new InvalidOperationException("The mechanical power mod system is not loaded.");
            api.Network.RegisterChannel(channelName).RegisterMessageType<LinkageUpdatePacket>();
            collisionTickListenerId = api.Event.RegisterGameTickListener(updateColliders, collisionUpdateInterval);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            clientChannel = api.Network.GetChannel(channelName)
                ?? throw new InvalidOperationException("The linkage network channel is not registered.");
            api.Input.InWorldAction += onInWorldAction;
            renderer = new(api, this);
            api.Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "mechanicallinkages");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            api.Network.GetChannel(channelName).SetMessageHandler<LinkageUpdatePacket>(onServerPacket);
            api.Event.DidPlaceBlock += onBlockPlaced;
            api.Event.PlayerDisconnect += onPlayerDisconnect;
        }

        public bool IsPlacing(EntityAgent byEntity)
        {
            return getWorkspace(byEntity).NowBuilding;
        }

        public bool IsLinkageEndpoint(BlockSelection blockSel)
        {
            return TryGetEndpointSelection(blockSel, out _);
        }

        public bool IsPointLinked(BlockSelection blockSel)
        {
            return TryGetEndpointSelection(blockSel, out LinkageEndpointSelection selected)
                && isPointLinked(selected);
        }

        void onInWorldAction(EnumEntityAction action, bool on, ref EnumHandling handling)
        {
            if (!on || action != EnumEntityAction.InWorldLeftMouseDown || capi == null) return;

            var player = capi.World.Player;
            BlockSelection? selection = player.CurrentBlockSelection;
            if (selection == null) return;

            if (player.InventoryManager.ActiveTool == EnumTool.Wrench)
            {
                if (!TryGetEndpointSelection(selection, out LinkageEndpointSelection selected)
                    || !isPointLinked(selected))
                {
                    return;
                }

                TryCancelPlace(player.Entity);
                handling = EnumHandling.PreventDefault;
                clientChannel?.SendPacket(new LinkageUpdatePacket {
                    Action = EnumLinkageAction.Remove,
                    Position = selected.Position.Copy(),
                    Point = selected.Point
                });
                return;
            }

            if (!IsLinkageSelection(selection)) return;
            if (IsPlacing(player.Entity)
                && player.InventoryManager.ActiveHotbarSlot.Itemstack?.Item is ItemLinkage)
            {
                return;
            }

            handling = EnumHandling.PreventDefault;
        }

        public void OnInteract(EntityAgent byEntity, BlockSelection blockSel, ItemLinkage linkage)
        {
            if (api.Side == EnumAppSide.Server || blockSel == null || capi == null) return;

            LinkagePlacerWorkspace workspace = getWorkspace(byEntity);
            LinkageEndpointSelection? start = null;
            if (workspace.NowBuilding)
            {
                ILinkageEndpoint? startEndpoint = workspace.StartPosition == null ? null : GetEndpoint(workspace.StartPosition);
                if (startEndpoint?.IsValidLinkagePoint(workspace.StartPoint) != true)
                {
                    cancel(workspace);
                    showError("linkage-start-missing");
                    return;
                }
                start = new(startEndpoint, workspace.StartPoint);
            }

            if (!TryGetEndpointSelection(blockSel, out var selected))
            {
                showError("linkage-not-endpoint");
                return;
            }

            if (!start.HasValue)
            {
                if (!linkage.CanAttachTo(selected.Endpoint, selected.Point))
                {
                    showError("linkage-does-not-fit");
                    return;
                }
                if (IsLinked(selected.Endpoint))
                {
                    showError("linkage-already-connected");
                    return;
                }

                workspace.StartPosition = selected.Position.Copy();
                workspace.StartPoint = selected.Point;
                workspace.NowBuilding = true;
                clientChannel?.SendPacket(new LinkageUpdatePacket {
                    Action = EnumLinkageAction.Begin,
                    Position = blockSel.Position.Copy(),
                    Point = selected.Point
                });
                return;
            }

            if (!TryValidateSpan(start.Value, selected, linkage, out string failureCode))
            {
                showError(failureCode);
                return;
            }

            cancel(workspace);
            clientChannel?.SendPacket(new LinkageUpdatePacket {
                Action = EnumLinkageAction.Complete,
                Position = blockSel.Position.Copy(),
                Point = selected.Point
            });
        }

        public bool TryCancelPlace(EntityAgent byEntity, bool onlyWhenTooFar = false)
        {
            LinkagePlacerWorkspace workspace = getWorkspace(byEntity);
            if (!workspace.NowBuilding) return false;
            if (onlyWhenTooFar && workspace.StartPosition != null && isWithinPlacementDistance(byEntity, workspace.StartPosition))
            {
                return false;
            }

            cancel(workspace);
            clientChannel?.SendPacket(new LinkageUpdatePacket { Action = EnumLinkageAction.Cancel });
            return true;
        }

        public ILinkageEndpoint? GetEndpoint(BlockPos pos)
        {
            return api.World.BlockAccessor.GetBlock(pos).GetInterface<ILinkageEndpoint>(api.World, pos);
        }

        public bool TryGetEndpointSelection(BlockSelection blockSel, out LinkageEndpointSelection selection)
        {
            selection = default;
            ILinkageEndpoint? endpoint = GetEndpoint(blockSel.Position);
            if (endpoint == null) return false;

            int point = tryGetLinkageSelectionPoint(blockSel, out int linkagePoint)
                ? linkagePoint
                : endpoint.GetLinkagePointAt(blockSel);
            if (!endpoint.IsValidLinkagePoint(point)) return false;

            selection = new(endpoint, point);
            return true;
        }

        bool tryGetEndpointSelection(BlockPos pos, int point, out LinkageEndpointSelection selection)
        {
            selection = default;
            ILinkageEndpoint? endpoint = GetEndpoint(pos);
            if (endpoint == null || !endpoint.IsValidLinkagePoint(point)) return false;

            selection = new(endpoint, point);
            return true;
        }

        public ILinkageEndpoint? GetConnectedEndpoint(ILinkageEndpoint endpoint)
        {
            if (!tryGetConnection(endpoint, out LinkageConnection? connection)) return null;

            bool isOwner = endpoint.LinkagePosition.Equals(connection.OwnerPosition);
            ILinkageEndpoint? other = GetEndpoint(isOwner ? connection.TargetPosition : connection.OwnerPosition);
            int endpointPoint = isOwner ? connection.OwnerPoint : connection.TargetPoint;
            int otherPoint = isOwner ? connection.TargetPoint : connection.OwnerPoint;
            if (other == null
                || !endpoint.IsValidLinkagePoint(endpointPoint)
                || !other.IsValidLinkagePoint(otherPoint)
                || endpoint.GetLinkageFacing(endpointPoint).Axis != other.GetLinkageFacing(otherPoint).Axis)
            {
                return null;
            }

            ILinkageProducer? owner = (isOwner ? endpoint : other) as ILinkageProducer;
            if (owner?.LinkageStack?.Item is not ItemLinkage linkage
                || !linkage.CanAttachTo(endpoint, endpointPoint)
                || !linkage.CanAttachTo(other, otherPoint))
            {
                return null;
            }

            return other;
        }

        public ItemStack? GetLinkageStack(ILinkageEndpoint endpoint)
        {
            if (!tryGetConnection(endpoint, out LinkageConnection? connection)) return null;

            return (GetEndpoint(connection.OwnerPosition) as ILinkageProducer)?.LinkageStack;
        }

        public bool TryGetConnectedPosition(ILinkageEndpoint endpoint, [NotNullWhen(true)] out BlockPos? otherPosition)
        {
            if (!tryGetConnectedPosition(endpoint, out otherPosition)) return false;

            otherPosition = otherPosition.Copy();
            return true;
        }

        bool tryGetConnectedPosition(ILinkageEndpoint endpoint, [NotNullWhen(true)] out BlockPos? otherPosition)
        {
            if (!tryGetConnection(endpoint, out LinkageConnection? connection))
            {
                otherPosition = null;
                return false;
            }

            otherPosition = endpoint.LinkagePosition.Equals(connection.OwnerPosition)
                ? connection.TargetPosition
                : connection.OwnerPosition;
            return true;
        }

        bool tryGetConnection(ILinkageEndpoint endpoint, [NotNullWhen(true)] out LinkageConnection? connection)
        {
            if (!ownerByEndpoint.TryGetValue(endpoint.LinkagePosition, out BlockPos? ownerPos))
            {
                connection = null;
                return false;
            }

            if (!connectionByOwner.TryGetValue(ownerPos, out connection)) return false;

            int point = endpoint.LinkagePosition.Equals(connection.OwnerPosition)
                ? connection.OwnerPoint
                : connection.TargetPoint;
            if (endpoint.IsValidLinkagePoint(point)) return true;

            connection = null;
            return false;
        }

        public int GetLinkagePoint(ILinkageEndpoint endpoint)
        {
            if (!tryGetConnection(endpoint, out LinkageConnection? connection))
            {
                findAndIndexOwner(endpoint);
                if (!tryGetConnection(endpoint, out connection)) return (endpoint as ILinkageProducer)?.DefaultLinkagePoint ?? 0;
            }

            return endpoint.LinkagePosition.Equals(connection.OwnerPosition)
                ? connection.OwnerPoint
                : connection.TargetPoint;
        }

        public bool IsLinked(ILinkageEndpoint endpoint)
        {
            return isEndpointLinked(endpoint, true);
        }

        bool isPointLinked(LinkageEndpointSelection selected)
        {
            if (!tryGetConnection(selected.Endpoint, out LinkageConnection? connection)) return false;

            int point = selected.Position.Equals(connection.OwnerPosition)
                ? connection.OwnerPoint
                : connection.TargetPoint;
            return selected.Point == point;
        }

        // A preview runs every frame, so it reads the index as it stands instead of searching for an owner to add to it.
        bool isEndpointLinked(ILinkageEndpoint endpoint, bool findOwner)
        {
            if ((endpoint as ILinkageProducer)?.LinkageStack != null || tryGetConnectedPosition(endpoint, out _)) return true;
            if (!findOwner) return false;

            findAndIndexOwner(endpoint);
            return tryGetConnectedPosition(endpoint, out _);
        }

        public void RefreshOwner(ILinkageProducer owner)
        {
            BlockPos ownerPos = owner.LinkagePosition;
            BlockPos? previousTarget = connectionByOwner.TryGetValue(ownerPos, out LinkageConnection? previousConnection)
                ? previousConnection.TargetPosition
                : null;
            unregisterOwner(ownerPos);

            BlockPos targetPos = ownerPos.Copy();
            if (owner.LinkageStack == null
                || !owner.TryGetLinkageTarget(targetPos, out int ownerPoint, out int targetPoint)
                || !owner.IsValidLinkagePoint(ownerPoint))
            {
                if (owner is BEBehaviorMPAxleArm unlinkedArm) unlinkedArm.RefreshShape();
                refreshArmShape(previousTarget);
                return;
            }

            LinkageConnection connection = new(ownerPos, ownerPoint, targetPos, targetPoint);
            connectionByOwner[connection.OwnerPosition] = connection;
            ownerByEndpoint[connection.OwnerPosition] = connection.OwnerPosition;
            ownerByEndpoint[connection.TargetPosition] = connection.OwnerPosition;
            tryRefreshSpan(connection, out _);
            if (owner is BEBehaviorMPAxleArm ownerArm) ownerArm.RefreshShape();
            refreshArmShape(connection.TargetPosition);
            if (previousTarget != null && !previousTarget.Equals(connection.TargetPosition)) refreshArmShape(previousTarget);
        }

        void refreshArmShape(BlockPos? pos)
        {
            if (pos != null && GetEndpoint(pos) is BEBehaviorMPAxleArm arm) arm.RefreshShape();
        }

        public void UnregisterEndpoint(BlockPos endpointPos)
        {
            if (unregisterOwner(endpointPos)) return;

            if (ownerByEndpoint.TryGetValue(endpointPos, out BlockPos? ownerPos)
                && !ownerPos.Equals(endpointPos))
            {
                unindexSpan(ownerPos);
                ownerByEndpoint.Remove(endpointPos);
            }
        }

        public void ReconnectEndpoint(ILinkageEndpoint endpoint)
        {
            ILinkageProducer? owner = getOwner(endpoint);

            // Reconnect saved spans without applying current placement clearance rules to an existing build.
            if (owner?.LinkageStack != null
                && tryGetConnection(owner, out LinkageConnection? connection)
                && !spanCellsByOwner.ContainsKey(connection.OwnerPosition))
            {
                tryRefreshSpan(connection, out _);
            }
            if (api.Side != EnumAppSide.Server) return;

            ILinkageEndpoint? other = GetConnectedEndpoint(endpoint);
            if (endpoint is IMechanicalPowerDevice device
                && other is IMechanicalPowerDevice otherDevice
                && device.Network != otherDevice.Network)
            {
                rebuildNetworks(getNetworks(endpoint, other));
            }

            ILinkageProducer? producer = endpoint as ILinkageProducer ?? other as ILinkageProducer;
            if (producer == null) return;

            ILinkageProducer? seed = GetNetworkSeed(producer);
            if (seed == null || seed.Network != null) return;

            seed.CreateJoinAndDiscoverNetwork(seed.GetLinkageFacing(GetLinkagePoint(seed)))?.broadcastData();
        }

        public bool SpreadLinkageNetwork(ICoreAPI api, ILinkageProducer drive, MechanicalNetwork network, out Vec3i? missingChunkPos)
        {
            missingChunkPos = null;
            if (!TryGetConnectedPosition(drive, out BlockPos? linkedPos)) return true;

            if (GetEndpoint(linkedPos) is ILinkageConsumer consumer)
            {
                return consumer.JoinLinkageNetwork(api, network, new(drive, GetLinkagePoint(drive)), GetLinkagePoint(consumer), out missingChunkPos);
            }
            if (api.World.BlockAccessor.GetChunkAtBlockPos(linkedPos) != null) return true;

            missingChunkPos = new(linkedPos.X / GlobalConstants.ChunkSize, linkedPos.InternalY / GlobalConstants.ChunkSize, linkedPos.Z / GlobalConstants.ChunkSize);
            return false;
        }

        bool unregisterOwner(BlockPos ownerPos)
        {
            unindexSpan(ownerPos);
            if (!connectionByOwner.Remove(ownerPos, out LinkageConnection? connection)) return false;

            if (ownerByEndpoint.TryGetValue(ownerPos, out BlockPos? indexedOwner) && indexedOwner.Equals(ownerPos)) ownerByEndpoint.Remove(ownerPos);
            if (ownerByEndpoint.TryGetValue(connection.TargetPosition, out indexedOwner) && indexedOwner.Equals(ownerPos)) ownerByEndpoint.Remove(connection.TargetPosition);
            return true;
        }

        public ItemStack? Detach(ILinkageEndpoint endpoint)
        {
            return detach(endpoint, true);
        }

        bool tryRemoveWithWrench(IPlayer byPlayer, LinkageEndpointSelection selected)
        {
            if (byPlayer.InventoryManager.ActiveTool != EnumTool.Wrench) return false;
            if (!isPointLinked(selected)) return false;
            if (!canModifyLinkage(byPlayer, selected.Endpoint)) return false;

            ItemStack? stack = Detach(selected.Endpoint);
            if (stack == null) return false;

            returnLinkage(stack, byPlayer, selected.Position);
            return true;
        }

        public void DetachForBreak(ILinkageEndpoint endpoint, IPlayer? byPlayer)
        {
            ItemStack? stack = detach(endpoint, endpoint is not BEBehaviorMPBase);
            if (stack == null || api.Side != EnumAppSide.Server || byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative) return;

            api.World.SpawnItemEntity(stack, endpoint.LinkagePosition.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        ItemStack? detach(ILinkageEndpoint endpoint, bool rebuildNetwork)
        {
            ILinkageProducer? owner = getOwner(endpoint);
            if (owner == null) return null;
            if (!rebuildNetwork) return owner.TakeLinkage();

            List<MechanicalNetwork> networks = getNetworks(owner, GetConnectedEndpoint(owner));
            ItemStack? stack = owner.TakeLinkage();
            rebuildNetworks(networks);
            return stack;
        }

        public bool TryValidateSpan(LinkageEndpointSelection start, LinkageEndpointSelection end, ItemLinkage linkage, out string failureCode)
        {
            if (!tryValidateSpan(start, end, linkage, true, out failureCode)) return false;
            if (!isPlacementSpanClear(start, end, linkage))
            {
                failureCode = "linkage-obstructed";
                return false;
            }

            return true;
        }

        // The swept obstruction scan is too much for a per frame preview, so the preview only checks the span rules.
        public bool TryValidatePreviewSpan(LinkageEndpointSelection start, LinkageEndpointSelection end, ItemLinkage linkage)
        {
            return tryValidateSpan(start, end, linkage, false, out _);
        }

        bool tryValidateSpan(LinkageEndpointSelection start, LinkageEndpointSelection end, ItemLinkage linkage, bool findOwners, out string failureCode)
        {
            ILinkageEndpoint startEndpoint = start.Endpoint;
            ILinkageEndpoint endEndpoint = end.Endpoint;
            if (!startEndpoint.IsValidLinkagePoint(start.Point) || !endEndpoint.IsValidLinkagePoint(end.Point))
            {
                failureCode = "linkage-not-endpoint";
                return false;
            }
            if (!linkage.CanAttachTo(startEndpoint, start.Point) || !linkage.CanAttachTo(endEndpoint, end.Point))
            {
                failureCode = "linkage-does-not-fit";
                return false;
            }

            BlockPos startPos = start.Position;
            BlockPos endPos = end.Position;
            if (startPos.Equals(endPos))
            {
                failureCode = "linkage-same-endpoint";
                return false;
            }
            EnumAxis axis = start.Facing.Axis;
            if (axis != end.Facing.Axis)
            {
                failureCode = "linkage-not-parallel";
                return false;
            }
            LinkageEndpointSelection driveSelection = start;
            LinkageEndpointSelection drivenSelection = end;
            LinkageGeometry.OrderEndpoints(ref driveSelection, ref drivenSelection);
            if (driveSelection.Endpoint is not ILinkageProducer drive || drivenSelection.Endpoint is not ILinkageConsumer consumer)
            {
                failureCode = "linkage-incompatible-endpoints";
                return false;
            }
            if (isEndpointLinked(startEndpoint, findOwners) || isEndpointLinked(endEndpoint, findOwners))
            {
                failureCode = "linkage-already-connected";
                return false;
            }

            if (!isValidLinkageSpan(startPos, endPos, axis))
            {
                failureCode = "linkage-not-parallel";
                return false;
            }

            if (!isWithinLinkageRange(startPos, endPos))
            {
                failureCode = "linkage-too-long";
                return false;
            }

            if (!consumer.CanFollowLinkage(driveSelection, drivenSelection.Point))
            {
                failureCode = "linkage-cannot-follow";
                return false;
            }
            if (findOwners && drivenSelection.Endpoint is ILinkageProducer driven)
            {
                MechanicalNetwork? driveNetwork = drive.Network;
                MechanicalNetwork? drivenNetwork = driven.Network;
                if (driveNetwork?.Valid != true || drivenNetwork?.Valid != true
                    || api.Side == EnumAppSide.Server && (!driveNetwork.fullyLoaded || !drivenNetwork.fullyLoaded))
                {
                    failureCode = "linkage-cannot-follow";
                    return false;
                }

                LinkageKinematics? kinematics = CreateKinematics(driveSelection, drivenSelection, GetRodLength(driveSelection, drivenSelection));
                // The larger crank must be free to follow the smaller one.
                if (kinematics?.IsValid != true
                    || !kinematics.IsLinear && (drivenNetwork.HasPowerProducer || driven.LinkageAngleTransform != null))
                {
                    failureCode = "linkage-cannot-follow";
                    return false;
                }
                if (driveNetwork == drivenNetwork
                    && (!kinematics.IsIdentity
                        || drive.GearedRatio != driven.GearedRatio
                        || drive.IsRotationReversed() != driven.IsRotationReversed()
                        || !LinkageAngleTransform.Matches(drive.LinkageAngleTransform, driven.LinkageAngleTransform)))
                {
                    failureCode = "linkage-cannot-follow";
                    return false;
                }
            }

            failureCode = "";
            return true;
        }

        // Returns the concrete collection so the renderer can walk it every frame without allocating an enumerator.
        public Dictionary<BlockPos, LinkageConnection>.ValueCollection GetLoadedLinks()
        {
            return connectionByOwner.Values;
        }

        public float GetRodLength(LinkageConnection connection)
        {
            if (connection.RodLength > 0) return connection.RodLength;

            ILinkageProducer? owner = GetEndpoint(connection.OwnerPosition) as ILinkageProducer;
            ILinkageEndpoint? target = GetEndpoint(connection.TargetPosition);
            if (owner == null || target == null
                || !owner.IsValidLinkagePoint(connection.OwnerPoint)
                || !target.IsValidLinkagePoint(connection.TargetPoint))
            {
                return connection.RodLength;
            }

            connection.RodLength = owner.LinkageRodLength;
            if (connection.RodLength > 0) return connection.RodLength;

            connection.RodLength = GetRodLength(new(owner, connection.OwnerPoint), new(target, connection.TargetPoint));
            owner.SetLinkageRodLength(connection.RodLength);
            return connection.RodLength;
        }

        public float GetRodLength(LinkageEndpointSelection start, LinkageEndpointSelection end)
        {
            if (start.Endpoint is ILinkageProducer startCrank && end.Endpoint is ILinkageProducer endCrank)
            {
                EnumAxis axis = start.Facing.Axis;
                if (axis == end.Facing.Axis && axis != EnumAxis.Y)
                {
                    startCrank.SetLinkageCrankCenter(scratchStartPin, start.Point);
                    endCrank.SetLinkageCrankCenter(scratchEndPin, end.Point);
                    return (float)scratchStartPin.DistanceTo(scratchEndPin);
                }
            }

            start.Endpoint.SetLinkagePinRestingPose(scratchStartPin, start.Point);
            end.Endpoint.SetLinkagePinRestingPose(scratchEndPin, end.Point);
            return (float)scratchStartPin.DistanceTo(scratchEndPin);
        }

        public float GetRodLength(ILinkageEndpoint endpoint)
        {
            return tryGetConnection(endpoint, out LinkageConnection? connection) ? GetRodLength(connection) : 0;
        }

        public ILinkageProducer? GetNetworkSeed(ILinkageProducer producer)
        {
            ILinkageEndpoint? other = GetConnectedEndpoint(producer);
            // Wait for both endpoints before choosing which crank can drive the rod.
            if (other == null) return IsLinked(producer) ? null : producer;
            LinkageEndpointSelection drive = new(producer, GetLinkagePoint(producer));
            LinkageEndpointSelection driven = new(other, GetLinkagePoint(other));
            LinkageGeometry.OrderEndpoints(ref drive, ref driven);
            return drive.Endpoint as ILinkageProducer;
        }

        public LinkageKinematics? GetKinematics(ILinkageProducer drive, ILinkageProducer driven)
        {
            if (!tryGetConnection(drive, out LinkageConnection? connection)) return null;

            if (connection.OwnerPosition.Equals(drive.LinkagePosition))
            {
                if (!connection.TargetPosition.Equals(driven.LinkagePosition)) return null;
                return connection.ForwardKinematics ??= CreateKinematics(new(drive, connection.OwnerPoint), new(driven, connection.TargetPoint), GetRodLength(connection));
            }
            if (!connection.TargetPosition.Equals(drive.LinkagePosition) || !connection.OwnerPosition.Equals(driven.LinkagePosition)) return null;

            return connection.ReverseKinematics ??= CreateKinematics(new(drive, connection.TargetPoint), new(driven, connection.OwnerPoint), GetRodLength(connection));
        }

        public LinkageKinematics? CreateKinematics(LinkageEndpointSelection driveSelection, LinkageEndpointSelection drivenSelection, float rodLength)
        {
            if (driveSelection.Endpoint is not ILinkageProducer drive || drivenSelection.Endpoint is not ILinkageProducer driven) return null;
            EnumAxis axis = driveSelection.Facing.Axis;
            if (axis != drivenSelection.Facing.Axis || axis == EnumAxis.Y || rodLength <= 0) return null;

            drive.SetLinkageCrankCenter(scratchStartPin, driveSelection.Point);
            driven.SetLinkageCrankCenter(scratchEndPin, drivenSelection.Point);
            double axialDistance = axis == EnumAxis.X
                ? scratchEndPin.X - scratchStartPin.X
                : scratchEndPin.Z - scratchStartPin.Z;
            double planarLengthSquared = rodLength * rodLength - axialDistance * axialDistance;
            if (planarLengthSquared <= 0) return null;

            double centerHorizontal = axis == EnumAxis.X
                ? scratchEndPin.Z - scratchStartPin.Z
                : scratchEndPin.X - scratchStartPin.X;
            double centerVertical = scratchEndPin.Y - scratchStartPin.Y;
            int radialSign = axis == EnumAxis.X ? 1 : -1;
            return new(
                centerHorizontal,
                centerVertical,
                Math.Sqrt(planarLengthSquared),
                drive.GetLinkageCrankRadius(driveSelection.Point),
                driven.GetLinkageCrankRadius(drivenSelection.Point),
                drive.GetLinkageCrankPhase(driveSelection.Point),
                driven.GetLinkageCrankPhase(drivenSelection.Point),
                radialSign
            );
        }

        public LinkagePlacerWorkspace GetClientWorkspace()
        {
            return capi == null ? new LinkagePlacerWorkspace() : getWorkspace(capi.World.Player.PlayerUID);
        }

        public override void Dispose()
        {
            if (collisionTickListenerId != 0)
            {
                api.Event.UnregisterGameTickListener(collisionTickListenerId);
                collisionTickListenerId = 0;
            }
            if (capi != null)
            {
                capi.Input.InWorldAction -= onInWorldAction;
                if (renderer != null) capi.Event.UnregisterRenderer(renderer, EnumRenderStage.Opaque);
            }
            if (sapi != null)
            {
                sapi.Event.DidPlaceBlock -= onBlockPlaced;
                sapi.Event.PlayerDisconnect -= onPlayerDisconnect;
            }
            renderer?.Dispose();
            renderer = null;
            clearCollisionState();
            workspaceByPlayer.Clear();
            connectionByOwner.Clear();
            ownerByEndpoint.Clear();
            base.Dispose();
        }

        void onPlayerDisconnect(IServerPlayer player)
        {
            workspaceByPlayer.Remove(player.PlayerUID);
        }

        void onServerPacket(IServerPlayer fromPlayer, LinkageUpdatePacket packet)
        {
            LinkagePlacerWorkspace workspace = getWorkspace(fromPlayer.PlayerUID);
            if (packet.Action == EnumLinkageAction.Cancel)
            {
                cancel(workspace);
                return;
            }

            if (packet.Action == EnumLinkageAction.Remove)
            {
                cancel(workspace);
                if (packet.Position == null
                    || !isWithinPlacementDistance(fromPlayer.Entity, packet.Position)
                    || !tryGetEndpointSelection(packet.Position, packet.Point, out LinkageEndpointSelection removalSelection)
                    || !tryRemoveWithWrench(fromPlayer, removalSelection))
                {
                    return;
                }

                ItemSlot wrenchSlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
                if (fromPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    wrenchSlot.Itemstack?.Collectible.DamageItem(api.World, fromPlayer.Entity, wrenchSlot);
                }
                return;
            }

            ItemSlot slot = fromPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack? heldStack = slot.Itemstack;
            if (heldStack?.Item is not ItemLinkage linkage || packet.Position == null) return;

            if (!isWithinPlacementDistance(fromPlayer.Entity, packet.Position))
            {
                cancel(workspace);
                return;
            }
            if (!tryGetEndpointSelection(packet.Position, packet.Point, out LinkageEndpointSelection selected)) return;

            ILinkageEndpoint endpoint = selected.Endpoint;
            if (!api.World.Claims.TryAccess(fromPlayer, endpoint.LinkagePosition, EnumBlockAccessFlags.BuildOrBreak)) return;

            if (packet.Action == EnumLinkageAction.Begin)
            {
                if (!linkage.CanAttachTo(endpoint, selected.Point) || IsLinked(endpoint)) return;

                workspace.StartPosition = endpoint.LinkagePosition.Copy();
                workspace.StartPoint = selected.Point;
                workspace.NowBuilding = true;
                return;
            }

            if (packet.Action != EnumLinkageAction.Complete) return;

            if (!workspace.NowBuilding || workspace.StartPosition == null) return;
            if (!isWithinPlacementDistance(fromPlayer.Entity, workspace.StartPosition))
            {
                cancel(workspace);
                return;
            }

            BlockPos startPos = workspace.StartPosition;
            int startPoint = workspace.StartPoint;
            ILinkageEndpoint? startEndpoint = GetEndpoint(startPos);
            LinkageEndpointSelection start = startEndpoint == null
                ? default
                : new(startEndpoint, startPoint);
            cancel(workspace);
            if (startEndpoint == null
                || !api.World.Claims.TryAccess(fromPlayer, startEndpoint.LinkagePosition, EnumBlockAccessFlags.BuildOrBreak)
                || !TryValidateSpan(start, selected, linkage, out _))
            {
                return;
            }

            LinkageEndpointSelection ownerSelection = start;
            LinkageEndpointSelection targetSelection = selected;
            LinkageGeometry.OrderEndpoints(ref ownerSelection, ref targetSelection);

            List<MechanicalNetwork> networks = getNetworks(start.Endpoint, selected.Endpoint);
            float rodLength = GetRodLength(ownerSelection, targetSelection);
            ((ILinkageProducer)ownerSelection.Endpoint).SetLinkage(targetSelection.Position, heldStack, ownerSelection.Point, targetSelection.Point, rodLength);

            if (fromPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            rebuildNetworks(networks);
            ReconnectEndpoint(ownerSelection.Endpoint);
        }

        bool canModifyLinkage(IPlayer byPlayer, ILinkageEndpoint endpoint)
        {
            if (!api.World.Claims.TryAccess(byPlayer, endpoint.LinkagePosition, EnumBlockAccessFlags.BuildOrBreak)) return false;

            return !tryGetConnectedPosition(endpoint, out BlockPos? otherPos)
                || api.World.Claims.TryAccess(byPlayer, otherPos, EnumBlockAccessFlags.BuildOrBreak);
        }

        void returnLinkage(ItemStack? stack, IPlayer? byPlayer, BlockPos dropPos)
        {
            if (stack == null || byPlayer?.WorldData.CurrentGameMode == EnumGameMode.Creative) return;
            if (byPlayer?.Entity.TryGiveItemStack(stack) == true) return;

            api.World.SpawnItemEntity(stack, dropPos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        ILinkageProducer? getOwner(ILinkageEndpoint endpoint)
        {
            if (endpoint is ILinkageProducer producer && producer.LinkageStack != null) return producer;
            if (!ownerByEndpoint.TryGetValue(endpoint.LinkagePosition, out BlockPos? ownerPos))
            {
                findAndIndexOwner(endpoint);
                if (!ownerByEndpoint.TryGetValue(endpoint.LinkagePosition, out ownerPos)) return null;
            }
            return GetEndpoint(ownerPos) as ILinkageProducer;
        }

        void findAndIndexOwner(ILinkageEndpoint endpoint)
        {
            if (endpoint is ILinkageProducer producer && producer.LinkageStack != null)
            {
                RefreshOwner(producer);
                return;
            }

            BlockPos candidatePos = endpoint.LinkagePosition.Copy();
            foreach (Vec2i offset in candidateOffsets)
            {
                // The saved connection supplies the point, so search both possible planes.
                for (int plane = 0; plane < (offset.X == 0 ? 1 : 2); plane++)
                {
                    candidatePos.Set(endpoint.LinkagePosition);
                    ref int horizontal = ref (plane == 0 ? ref candidatePos.X : ref candidatePos.Z);
                    horizontal += offset.X;
                    candidatePos.Y += offset.Y;
                    ILinkageProducer? candidate = GetEndpoint(candidatePos) as ILinkageProducer;
                    if (candidate?.LinkageStack == null) continue;

                    BlockPos targetPos = candidate.LinkagePosition.Copy();
                    if (!candidate.TryGetLinkageTarget(targetPos, out int ownerPoint, out int targetPoint)
                        || !targetPos.Equals(endpoint.LinkagePosition)
                        || !candidate.IsValidLinkagePoint(ownerPoint)
                        || !endpoint.IsValidLinkagePoint(targetPoint))
                    {
                        continue;
                    }

                    EnumAxis axis = endpoint.GetLinkageFacing(targetPoint).Axis;
                    if (candidate.GetLinkageFacing(ownerPoint).Axis != axis
                        || !isValidLinkageSpan(candidate.LinkagePosition, targetPos, axis))
                    {
                        continue;
                    }

                    RefreshOwner(candidate);
                    return;
                }
            }
        }

        static Vec2i[] createCandidateOffsets()
        {
            List<Vec2i> offsets = [];

            for (int lengthSquared = 1; lengthSquared <= MaxLengthSquared; lengthSquared++)
            {
                for (int horizontal = -MaxLength; horizontal <= MaxLength; horizontal++)
                {
                    for (int vertical = -MaxLength; vertical <= MaxLength; vertical++)
                    {
                        if (horizontal * horizontal + vertical * vertical == lengthSquared) offsets.Add(new Vec2i(horizontal, vertical));
                    }
                }
            }

            return offsets.ToArray();
        }

        List<MechanicalNetwork> getNetworks(ILinkageEndpoint first, ILinkageEndpoint? second)
        {
            var firstNetwork = (first as IMechanicalPowerDevice)?.Network;
            var secondNetwork = (second as IMechanicalPowerDevice)?.Network;
            if (firstNetwork != null)
            {
                if (secondNetwork != null && secondNetwork != firstNetwork) return [firstNetwork, secondNetwork];
                return [firstNetwork];
            }

            return secondNetwork != null ? [secondNetwork] : [];
        }

        void rebuildNetworks(List<MechanicalNetwork> networks)
        {
            if (networks.Count == 0) return;

            mechanicalPower.RebuildNetworks(networks);
        }

        LinkagePlacerWorkspace getWorkspace(EntityAgent byEntity)
        {
            return getWorkspace((byEntity as EntityPlayer)?.PlayerUID ?? "");
        }

        LinkagePlacerWorkspace getWorkspace(string playerUid)
        {
            if (!workspaceByPlayer.TryGetValue(playerUid, out var workspace))
            {
                workspaceByPlayer[playerUid] = workspace = new LinkagePlacerWorkspace();
            }
            return workspace;
        }

        static void cancel(LinkagePlacerWorkspace workspace)
        {
            workspace.NowBuilding = false;
            workspace.StartPosition = null;
            workspace.StartPoint = 0;
        }

        static bool isWithinPlacementDistance(EntityAgent byEntity, BlockPos startPos)
        {
            if (byEntity.Pos.Dimension != startPos.dimension) return false;

            return byEntity.Pos.SquareDistanceTo(startPos.X + 0.5, startPos.InternalY + 0.5, startPos.Z + 0.5)
                <= maxPlacementDistanceSquared;
        }

        static bool isValidLinkageSpan(BlockPos startPos, BlockPos endPos, EnumAxis axis)
        {
            return startPos.dimension == endPos.dimension
                && (axis == EnumAxis.X && startPos.X == endPos.X
                    || axis == EnumAxis.Z && startPos.Z == endPos.Z);
        }

        // Only valid for a span isValidLinkageSpan() has already held to one dimension.
        static bool isWithinLinkageRange(BlockPos startPos, BlockPos endPos)
        {
            long dx = (long)endPos.X - startPos.X;
            long dy = (long)endPos.Y - startPos.Y;
            long dz = (long)endPos.Z - startPos.Z;
            if (Math.Abs(dx) > MaxLength || Math.Abs(dy) > MaxLength || Math.Abs(dz) > MaxLength) return false;

            return dx * dx + dy * dy + dz * dz <= MaxLengthSquared;
        }

        void showError(string failureCode)
        {
            capi?.TriggerIngameError(this, failureCode, Lang.Get(failureCode));
        }
    }
}
