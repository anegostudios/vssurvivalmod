using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Vintagestory.GameContent.Mechanics
{
    public class MechPowerPath {
        public BlockFacing OutFacing;
        public bool invert;
        public float gearingRatio;
        private readonly BlockPos fromPos;

        public MechPowerPath()
        {
        }
        
        /// <summary>
        /// Specify fromPos where it matters (e.g. when SetPropagationDir using a direction taken from a neighbour
        /// </summary>
        public MechPowerPath(BlockFacing facing, float gearingRatioHere, BlockPos fromPos = null, bool inverted = false)
        {
            this.OutFacing = facing;
            this.invert = inverted;
            this.gearingRatio = gearingRatioHere;
            this.fromPos = fromPos;
        }

        public BlockFacing NetworkDir()
        {
            return this.invert ? OutFacing.Opposite : OutFacing;
        }

        public bool IsInvertedTowards(BlockPos testPos)
        {
            return fromPos == null ? invert : fromPos.AddCopy(this.NetworkDir()) != testPos;
        }
    }

    public abstract class BEBehaviorMPBase : BlockEntityBehavior, IMechanicalPowerDevice
    {
#if DEBUG
        /// <summary>
        /// Set to true to debug Mechanical Power network discovery etc..  The #if DEBUG is just in case: ensures this debugging log spam can never make it into a release build by accident.
        /// </summary>
        private static readonly bool DEBUG = false;
#else
        private static readonly bool DEBUG = false;
#endif

        protected MechanicalPowerMod manager;
        protected MechanicalNetwork network;

        public Vec4f lightRbs = new Vec4f();

        public virtual BlockPos Position { get { return Blockentity.Pos; } }
        public BlockPos GetPosition() { return Position; }
        public virtual Vec4f LightRgba { get { return lightRbs; } }

        /// <summary>
        /// Return null to not a mechanical power instanced renderer for this block. When you change the shape, the renderer is also updated to reflect that change
        /// </summary>
        private CompositeShape shape = null;
        public virtual CompositeShape Shape
        {
            get { return shape; }
            set
            {
                CompositeShape prev = Shape;
                if (prev != null && manager != null && prev != value)
                {
                    manager.RemoveDeviceForRender(this);
                    this.shape = value;
                    manager.AddDeviceForRender(this);
                }
                else
                {
                    this.shape = value;
                }
            }
        }

        public virtual int[] AxisSign { get; protected set; }

        public long NetworkId { get; set; }

        public MechanicalNetwork Network => network;
        public virtual BlockFacing OutFacingForNetworkDiscovery { get; protected set; } = null;

        public virtual Block Block => Blockentity.Block;

        protected BlockFacing propagationDir = BlockFacing.NORTH;
        private float gearedRatio = 1.0f;
        public float GearedRatio { get { return gearedRatio; } set { gearedRatio = value;} }
        public virtual float GetGearedRatio(BlockFacing face) { return gearedRatio; }

        protected float lastKnownAngleRad = 0;
        public bool disconnected = false;

        public virtual float AngleRad
        {
            get
            {
                if (network == null) return lastKnownAngleRad;

                if (isRotationReversed())
                {
                    return (lastKnownAngleRad = GameMath.TWOPI - (network.AngleRad * this.gearedRatio) % GameMath.TWOPI);
                }

                return (lastKnownAngleRad = (network.AngleRad * this.gearedRatio) % GameMath.TWOPI);
            }
        }

        public virtual bool isRotationReversed()
        {
            if (propagationDir == null) return false;

            return propagationDir == BlockFacing.DOWN || propagationDir == BlockFacing.EAST || propagationDir == BlockFacing.SOUTH;
        }

        public virtual bool isInvertedNetworkFor(BlockPos pos)
        {
            if (propagationDir == null || pos == null) return false;
            BlockPos testPos = this.Position.AddCopy(propagationDir);
            return !testPos.Equals(pos);
        }

        public BEBehaviorMPBase(BlockEntity blockentity) : base(blockentity)
        {
        }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            Shape = GetShape();

            manager = Api.ModLoader.GetModSystem<MechanicalPowerMod>();

            if (Api.World.Side == EnumAppSide.Client)
            {
                lightRbs = Api.World.BlockAccessor.GetLightRGBs(Blockentity.Pos);
                if (NetworkId > 0)
                {
                    network = manager.GetOrCreateNetwork(NetworkId);
                    JoinNetwork(network);
                }
            }

            manager.AddDeviceForRender(this);

            AxisSign = new int[3] { 0, 0, 1 };
            SetOrientations();

            if (api.Side == EnumAppSide.Server && OutFacingForNetworkDiscovery != null)
            {
                CreateJoinAndDiscoverNetwork(OutFacingForNetworkDiscovery);
            }
        }

        protected virtual CompositeShape GetShape()
        {
            return Block.Shape;
        }

        public virtual void SetOrientations()
        {

        }

        public virtual void WasPlaced(BlockFacing connectedOnFacing)
        {
            //Skip this if already called CreateJoinAndDiscoverNetwork in Initialize()
            if ((Api.Side == EnumAppSide.Client || OutFacingForNetworkDiscovery == null) && connectedOnFacing != null)
            {
                if (!tryConnect(connectedOnFacing))
                {
                    if (DEBUG) Api.Logger.Notification("Was placed fail connect 2nd: " + connectedOnFacing + " at " + Position);
                }
                else if (DEBUG) Api.Logger.Notification("Was placed connected 1st: " + connectedOnFacing + " at " + Position);
            }
        }

        public bool tryConnect(BlockFacing toFacing)
        {
            MechanicalNetwork newNetwork;

            BlockPos pos = Position.AddCopy(toFacing);
            IMechanicalPowerBlock connectedToBlock = Api.World.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
            if (DEBUG) Api.Logger.Notification("tryConnect at " + this.Position + " towards " + toFacing + " " + pos);

            if (connectedToBlock == null || !connectedToBlock.HasMechPowerConnectorAt(Api.World, pos, toFacing.Opposite)) return false;
            newNetwork = connectedToBlock.GetNetwork(Api.World, pos);
            if (newNetwork != null)
            {
                IMechanicalPowerDevice node = Api.World.BlockAccessor.GetBlockEntity(pos).GetBehavior<BEBehaviorMPBase>();

                connectedToBlock.DidConnectAt(Api.World, pos, toFacing.Opposite);  //do this first to set the new Angled Gear block correctly prior to getting propagation direction
                MechPowerPath curPath = new MechPowerPath(toFacing, node.GetGearedRatio(toFacing), pos, !node.IsPropagationDirection(Position, toFacing));
                SetPropagationDirection(curPath);
                MechPowerPath[] paths = GetMechPowerExits(curPath);
                JoinNetwork(newNetwork);

                for (int i = 0; i < paths.Length; i++)
                {
                    if (DEBUG) Api.Logger.Notification("== spreading path " + (paths[i].invert ? "-" : "") + paths[i].OutFacing + "  " + paths[i].gearingRatio);
                    BlockPos exitPos = Position.AddCopy(paths[i].OutFacing);

                    bool chunkLoaded = spreadTo(Api, newNetwork, exitPos, paths[i], out _);
                    if (!chunkLoaded)
                    {
                        LeaveNetwork();
                        return true;
                    }
                }

                return true;
            }
            else if (this.network != null)
            {
                BEBehaviorMPBase node = Api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>();
                if (node != null) return node.tryConnect(toFacing.Opposite);
            }

            return false;
        }

        public virtual void JoinNetwork(MechanicalNetwork network)
        {
            if (this.network != null && this.network != network)
            {
                LeaveNetwork();
            }

            if (this.network == null)
            {
                this.network = network;
                network?.Join(this);
            }

            if (network == null) NetworkId = 0;
            else
            {
                NetworkId = network.networkId;
            }

            Blockentity.MarkDirty();
        }

        public virtual void LeaveNetwork()
        {
            if (DEBUG) Api.Logger.Notification("Leaving network " + NetworkId + " at " + this.Position);
            network?.Leave(this);
            network = null;
            NetworkId = 0;
            Blockentity.MarkDirty();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            this.disconnected = true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (network != null)
            {
                manager.OnNodeRemoved(this);
            }

            LeaveNetwork();
            manager.RemoveDeviceForRender(this);
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            network?.DidUnload(this);
            manager?.RemoveDeviceForRender(this);
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            lightRbs = Api.World.BlockAccessor.GetLightRGBs(Blockentity.Pos);

            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            long nowNetworkId = tree.GetLong("networkid");
            if (worldAccessForResolve.Side == EnumAppSide.Client)
            {
                propagationDir = BlockFacing.ALLFACES[tree.GetInt("turnDirFromFacing")];
                gearedRatio = tree.GetFloat("g");

                if (NetworkId != nowNetworkId)   //don't ever change network settings from tree on server side - networkId is not data to be saved  (otherwise would mess up networks on chunk loading, if BE tree loaded after a BE has already had network assigned on the server by propagation from a neighbour)
                {
                    NetworkId = 0;
                    if (worldAccessForResolve.Side == EnumAppSide.Client)
                    {
                        NetworkId = nowNetworkId;
                        if (NetworkId == 0)
                        {
                            LeaveNetwork();
                            network = null;
                        }
                        else if (manager != null)
                        {
                            network = manager.GetOrCreateNetwork(NetworkId);
                            JoinNetwork(network);
                            Blockentity.MarkDirty();
                        }
                    }
                }
            }

            SetOrientations();
            updateShape(worldAccessForResolve);
        }

        protected virtual void updateShape(IWorldAccessor worldForResolve)
        {
            Shape = Block?.Shape;
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("networkid", NetworkId);
            tree.SetInt("turnDirFromFacing", propagationDir.Index);
            tree.SetFloat("g", gearedRatio);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (DEBUG || Api.World.EntityDebugMode)
            {
                sb.AppendLine(string.Format("networkid: {0}  turnDir: {1}  {2}  {3:G3}", NetworkId, propagationDir, network?.TurnDir.ToString(), gearedRatio));
                sb.AppendLine(string.Format("speed: {0:G4}  avail torque: {1:G4}  torque sum: {2:G4}  resist sum: {3:G4}", network?.Speed * this.GearedRatio, network?.TotalAvailableTorque / this.GearedRatio, network?.NetworkTorque / this.GearedRatio, network?.NetworkResistance / this.GearedRatio));
            }
        }

        public virtual BlockFacing GetPropagationDirection()
        {
            return propagationDir;
        }

        public virtual BlockFacing GetPropagationDirectionInput()
        {
            return propagationDir;
        }

        public virtual bool IsPropagationDirection(BlockPos fromPos, BlockFacing test)
        {
            return propagationDir == test;
        }

        public virtual void SetPropagationDirection(MechPowerPath path)
        {
            BlockFacing turnDir = path.NetworkDir();
            if (this.propagationDir == turnDir.Opposite && this.network != null)
            {
                if (!network.DirectionHasReversed) network.TurnDir = network.TurnDir == EnumRotDirection.Clockwise ? EnumRotDirection.Counterclockwise : EnumRotDirection.Clockwise;
                network.DirectionHasReversed = true;
            }
            this.propagationDir = turnDir;
            this.GearedRatio = path.gearingRatio;
            if (DEBUG) Api.Logger.Notification("setting dir " + this.propagationDir + " " + this.Position);
        }

        public virtual float GetTorque(long tick, float speed, out float resistance)
        {
            resistance = GetResistance();
            return 0f;
        }

        public abstract float GetResistance();

        public virtual void DestroyJoin(BlockPos pos)
        {
        }


#region Network Discovery


        public virtual MechanicalNetwork CreateJoinAndDiscoverNetwork(BlockFacing powerOutFacing)
        {
            BlockPos neibPos = Position.AddCopy(powerOutFacing);
            IMechanicalPowerBlock neibMechBlock = null;
            neibMechBlock = Api.World.BlockAccessor.GetBlock(neibPos) as IMechanicalPowerBlock;

            MechanicalNetwork neibNetwork = neibMechBlock == null ? null : neibMechBlock.GetNetwork(Api.World, neibPos);

            if (neibNetwork == null || !neibNetwork.Valid)
            {
                MechanicalNetwork newNetwork = this.network;
                if (newNetwork == null)
                {
                    newNetwork = manager.CreateNetwork(this);
                    JoinNetwork(newNetwork);
                    if (DEBUG) Api.Logger.Notification("===setting inturn at " + Position + " " + powerOutFacing);
                    SetPropagationDirection(new MechPowerPath(powerOutFacing, 1));
                }

                Vec3i missingChunkPos;
                bool chunksLoaded = spreadTo(Api, newNetwork, neibPos, new MechPowerPath(GetPropagationDirection(), this.gearedRatio), out missingChunkPos);
                if (network == null)
                {
                    if (DEBUG) Api.Logger.Notification("Incomplete chunkloading, possible issues with mechanical network around block " + neibPos);
                    return null;
                }
 
                if (!chunksLoaded)
                {
                    network.AwaitChunkThenDiscover(missingChunkPos);
                    manager.testFullyLoaded(network); // To trigger that allFullyLoaded gets false
                    return network;
                }
                else
                {
                    IMechanicalPowerDevice node = Api.World.BlockAccessor.GetBlockEntity(neibPos)?.GetBehavior<BEBehaviorMPBase>() as IMechanicalPowerDevice;
                    if (node != null)
                    {
                        BlockFacing facing = node.IsPropagationDirection(Position, powerOutFacing) ? powerOutFacing : powerOutFacing.Opposite;
                        SetPropagationDirection(new MechPowerPath(facing, node.GetGearedRatio(facing.Opposite), neibPos));
                    }
                }
            }
            else
            {
                BEBehaviorMPBase neib = Api.World.BlockAccessor.GetBlockEntity(neibPos).GetBehavior<BEBehaviorMPBase>();
                if (OutFacingForNetworkDiscovery != null)
                {
                    if (tryConnect(OutFacingForNetworkDiscovery))
                    {
                        this.gearedRatio = neib.GetGearedRatio(OutFacingForNetworkDiscovery);  //no need to set propagationDir, it's already been set by tryConnect
                    }
                }
                else
                {
                    JoinNetwork(neibNetwork);
                    SetPropagationDirection(new MechPowerPath(neib.propagationDir, neib.GetGearedRatio(neib.propagationDir), neibPos));
                }
            }

            return network;
        }

        /// <summary>
        /// Network propagation has a power path direction (for the network discovery process) and normally power transmission will be in the same sense, but it may be inverted (e.g. if a block is inserted in a way which joins two existing networks)
        /// </summary>
        public virtual bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, MechanicalNetwork network, MechPowerPath exitTurnDir, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;
            if (this.network?.networkId == network?.networkId) return true; // Already got the message

            if (DEBUG) api.Logger.Notification("Spread to " + this.Position + " with direction " + exitTurnDir.OutFacing + (exitTurnDir.invert ? "-" : "") + " Network:" + network.networkId);
            SetPropagationDirection(exitTurnDir);

            JoinNetwork(network);
            (Block as IMechanicalPowerBlock).DidConnectAt(api.World, Position, exitTurnDir.OutFacing.Opposite);

            MechPowerPath[] paths = GetMechPowerExits(exitTurnDir);
            for (int i = 0; i < paths.Length; i++)
            {
                //if (paths[i].OutFacing == exitTurnDir.OutFacing.GetOpposite()) continue;   //currently commented out to force testing of path in both directions, though usually (maybe always) the OutFacing.getOpposite() sense will return quickly - anyhow, it seems to work with this commented out
                if (DEBUG) api.Logger.Notification("-- spreading path " + (paths[i].invert ? "-" : "") + paths[i].OutFacing + "  " + paths[i].gearingRatio);
                BlockPos exitPos = Position.AddCopy(paths[i].OutFacing);
                bool chunkLoaded = spreadTo(api, network, exitPos, paths[i], out missingChunkPos);

                if (!chunkLoaded)
                {
                    //LeaveNetwork();    //we don't want to set the network to null here, as then there would be nowhere to store the missingChunkPos
                    return false;
                }
            }

            return true;
        }

        protected virtual bool spreadTo(ICoreAPI api, MechanicalNetwork network, BlockPos exitPos, MechPowerPath propagatePath, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;
            BEBehaviorMPBase beMechBase = api.World.BlockAccessor.GetBlockEntity(exitPos)?.GetBehavior<BEBehaviorMPBase>();
            IMechanicalPowerBlock mechBlock = beMechBase?.Block as IMechanicalPowerBlock;
            if (DEBUG) api.Logger.Notification("attempting spread to " + exitPos + (beMechBase == null ? " -" : ""));

            if (beMechBase == null && api.World.BlockAccessor.GetChunkAtBlockPos(exitPos) == null)
            {
                if (OutsideMap(api.World.BlockAccessor, exitPos)) return true;  //Network discovery should not fail if there cannot be a block in this position
                missingChunkPos = new Vec3i(exitPos.X / api.World.BlockAccessor.ChunkSize, exitPos.Y / api.World.BlockAccessor.ChunkSize, exitPos.Z / api.World.BlockAccessor.ChunkSize);
                return false;
            }

            if (beMechBase != null && mechBlock.HasMechPowerConnectorAt(api.World, exitPos, propagatePath.OutFacing.Opposite))
            {
                beMechBase.Api = api;
                if (!beMechBase.JoinAndSpreadNetworkToNeighbours(api, network, propagatePath, out missingChunkPos))
                {
                    return false;
                }
            }
            else if (DEBUG) api.Logger.Notification("no connector at " + exitPos + " " + propagatePath.OutFacing.Opposite);

            return true;
        }

        private bool OutsideMap(IBlockAccessor blockAccessor, BlockPos exitPos)
        {
            if (exitPos.X < 0 || exitPos.X >= blockAccessor.MapSizeX) return true;
            if (exitPos.Y < 0 || exitPos.Y >= blockAccessor.MapSizeY) return true;
            if (exitPos.Z < 0 || exitPos.Z >= blockAccessor.MapSizeZ) return true;
            return false;
        }

        /// <summary>
        /// Must return the path mechanical power takes, assuming block is aligned so that the given entry direction is a valid path, and preserve inverted state
        /// </summary>
        /// <param name="entryDir"></param>
        /// <returns></returns>
        protected virtual MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            // Most blocks - like axles - have two power exits in opposite directions
            return new MechPowerPath[] { entryDir, new MechPowerPath(entryDir.OutFacing.Opposite, entryDir.gearingRatio, Position, !entryDir.invert) };
        }


#endregion
    }


}
