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
        public EnumRotDirection OutRot;

        public MechPowerPath()
        {
        }
        public MechPowerPath(BlockFacing facing, EnumRotDirection rot)
        {
            this.OutFacing = facing;
            this.OutRot = rot;
        }
    }

    public class TurnDirection
    {
        public BlockFacing Facing = BlockFacing.NORTH;
        public EnumRotDirection Rot;

        public TurnDirection() { }
        public TurnDirection(BlockFacing facing, EnumRotDirection rot)
        {
            this.Facing = facing;
            this.Rot = rot;
        }

        public override string ToString()
        {
            return "("+Rot+" when looking "+Facing+")";
        }
    }

    public abstract class BEBehaviorMPBase : BlockEntityBehavior, IMechanicalPowerNode
    {
        protected MechanicalPowerMod manager;
        protected MechanicalNetwork network;

        public Vec4f lightRbs = new Vec4f();

        public virtual BlockPos Position { get { return Blockentity.Pos; } }
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
                if (prev != null && manager != null)
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

        public virtual Block Block { get; set; }

        public BlockFacing orientation;
        protected TurnDirection inTurnDir = new TurnDirection();

        protected float lastKnownAngleRad = 0;
        public bool disconnected = false;

        public virtual float AngleRad
        {
            get
            {
                if (network == null) return lastKnownAngleRad;

                bool invert = inTurnDir.Facing != network.TurnDir.Facing;
                if (network.TurnDir.Rot == EnumRotDirection.Counterclockwise)
                {
                    invert = !invert;
                }

                if (inTurnDir.Facing == BlockFacing.UP || inTurnDir.Facing == BlockFacing.WEST) invert = !invert;

                if (invert)
                {
                    return (lastKnownAngleRad = GameMath.TWOPI - network.AngleRad);
                }

                return (lastKnownAngleRad = network.AngleRad);
            }
        }

        
        public BEBehaviorMPBase(BlockEntity blockentity) : base(blockentity)
        {
            Block = Blockentity.Block;
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
            if (connectedOnFacing != null)
            {
                if (!tryConnect(connectedOnFacing))
                {
                    //## What is this code doing?  Why test opposite connection, only makes sense for axle
                    MechPowerPath[] paths = GetMechPowerExits(new TurnDirection() { Facing = connectedOnFacing.GetOpposite() });
                    if (paths.Length > 0) {
                        //Api.World.Logger.Notification("Was placed try connect 2nd: " + paths[0].OutFacing + " at " + Position);
                        tryConnect(paths[0].OutFacing);
                    }
                    //else Api.World.Logger.Notification("Was placed fail connect 2nd: " + connectedOnFacing.GetOpposite() + " at " + Position);
                }
                //else Api.World.Logger.Notification("Was placed connected 1st: " + connectedOnFacing + " at " + Position);
            }
        }

        protected bool tryConnect(BlockFacing toFacing)
        {
            MechanicalNetwork network;

            BlockPos pos = Position.AddCopy(toFacing);
            IMechanicalPowerBlock connectedToBlock = Api.World.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;

            if (connectedToBlock == null || !connectedToBlock.HasMechPowerConnectorAt(Api.World, pos, toFacing.GetOpposite())) return false;
            network = connectedToBlock.GetNetwork(Api.World, pos);
            if (network != null)
            {
                IMechanicalPowerNode node = Api.World.BlockAccessor.GetBlockEntity(pos).GetBehavior<BEBehaviorMPBase>() as IMechanicalPowerNode;

                //Don't override turn direction of an existing network; if two existing networks, the one with higher torque should win
                TurnDirection newTurnDir = inTurnDir;
                if (this.inTurnDir == null) newTurnDir = this.GetTurnDirection(toFacing);
                if (network.TurnDir != null && (this.network == null || Math.Abs(network.NetworkTorque) > Math.Abs(this.network.NetworkTorque)))
                {
                    TurnDirection toCopy = node.GetTurnDirection(toFacing.GetOpposite());
                    newTurnDir = new TurnDirection(toCopy.Facing, toCopy.Rot);
                }
                SetInTurnDirection(newTurnDir);
                JoinNetwork(network);
                connectedToBlock.DidConnectAt(Api.World, pos, toFacing.GetOpposite());

                MechPowerPath[] paths = GetMechPowerExits(inTurnDir);
                for (int i = 0; i < paths.Length; i++)
                {
                    BlockPos exitPos = Position.AddCopy(paths[i].OutFacing);

                    Vec3i missingChunkPos;
                    bool chunkLoaded = spreadTo(Api, manager.GetNextPropagationId(), network, exitPos, new TurnDirection(paths[i].OutFacing, paths[i].OutRot), out missingChunkPos);
                    if (!chunkLoaded)
                    {
                        LeaveNetwork();
                        return true;
                    }
                }

                return true;
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
            network?.Leave(this);
            network = null;
            NetworkId = 0;
            Blockentity.MarkDirty();
        }

        public override void OnBlockBroken()
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
            manager.RemoveDeviceForRender(this);
        }


        public virtual void DidConnectTo(BlockPos pos, BlockFacing facing)
        {
            BEBehaviorMPBase nbe = Api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>();
            if (nbe != null)
            {
                NetworkId = nbe.NetworkId;
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            lightRbs = Api.World.BlockAccessor.GetLightRGBs(Blockentity.Pos);

            return true;
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            long nowNetworkId = tree.GetLong("networkid");
            inTurnDir.Facing = BlockFacing.ALLFACES[tree.GetInt("turnDirFromFacing")];
            inTurnDir.Rot = (EnumRotDirection)tree.GetInt("turnDir");

            if (NetworkId != nowNetworkId)
            {
                NetworkId = 0;
                if (worldAccessForResolve.Side == EnumAppSide.Client)
                {
                    NetworkId = tree.GetLong("networkid");
                    if (NetworkId == 0)
                    {
                        LeaveNetwork();
                        network = null;
                    }

                    if (NetworkId > 0 && manager != null)
                    {
                        network = manager.GetOrCreateNetwork(NetworkId);
                        JoinNetwork(network);
                        Blockentity.MarkDirty();
                    }
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("networkid", NetworkId);
            tree.SetInt("turnDirFromFacing", inTurnDir.Facing.Index);
            tree.SetInt("turnDir", (int)inTurnDir.Rot);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (Api.World.EntityDebugMode)
            {
                sb.AppendLine(string.Format(
                    "networkid: {0}, turnDir: {1}, network speed: {2:G4}, avail torque: {3:G4}, network torque sum: {4:G4}, network resist sum: {5:G4}", 
                    NetworkId, inTurnDir, network?.Speed, network?.TotalAvailableTorque, network?.NetworkTorque, network?.NetworkResistance
                ));
            }

            //return base.GetBlockInfo(forPlayer);
        }


        public virtual TurnDirection GetInTurnDirection()
        {
            return inTurnDir;
        }

        public virtual void SetInTurnDirection(TurnDirection turnDir)
        {
            this.inTurnDir = turnDir;
        }

        public virtual TurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return inTurnDir;
        }

        public abstract float GetTorque();
        public abstract float GetResistance();


        #region Network Discovery


        public virtual MechanicalNetwork CreateJoinAndDiscoverNetwork(BlockFacing powerOutFacing)
        {
            IMechanicalPowerBlock neibMechBlock = Api.World.BlockAccessor.GetBlock(Position.AddCopy(powerOutFacing)) as IMechanicalPowerBlock;

            MechanicalNetwork neibNetwork = neibMechBlock == null ? null : neibMechBlock.GetNetwork(Api.World, Position.AddCopy(powerOutFacing));

            if (neibNetwork == null || !neibNetwork.Valid)
            {
                MechanicalNetwork newNetwork = this.network;
                if (network == null)
                {
                    newNetwork = manager.CreateNetwork(this);
                    JoinNetwork(newNetwork);
                }

                newNetwork.TurnDir.Facing = powerOutFacing;
                Vec3i missingChunkPos;
                SetInTurnDirection(new TurnDirection(powerOutFacing, EnumRotDirection.Clockwise));
                bool chunksLoaded = spreadTo(Api, manager.GetNextPropagationId(), newNetwork, Position.AddCopy(powerOutFacing), GetTurnDirection(powerOutFacing), out missingChunkPos);

                if (!chunksLoaded)
                {
                    network.AwaitChunkThenDiscover(missingChunkPos);
                    manager.testFullyLoaded(network); // To trigger that allFullyLoaded gets false
                    return network;
                }
            }
            else
            {
                neibNetwork.TurnDir.Facing = powerOutFacing;
                JoinNetwork(neibNetwork);
            }

            return network;
        }

        long currentPropagationId = 0;
        public virtual bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, long propagationId, MechanicalNetwork network, TurnDirection exitTurnDir, out Vec3i missingChunkPos)
        {
            SetInTurnDirection(new TurnDirection(exitTurnDir.Facing, exitTurnDir.Rot));

            missingChunkPos = null;

            if (propagationId == currentPropagationId) return true; // Already got the message

            currentPropagationId = propagationId;

            JoinNetwork(network);
            (Block as IMechanicalPowerBlock).DidConnectAt(api.World, Position, exitTurnDir.Facing);

            MechPowerPath[] paths = GetMechPowerExits(exitTurnDir);
            for (int i = 0; i < paths.Length; i++)
            {
                BlockPos exitPos = Position.AddCopy(paths[i].OutFacing);
                bool chunkLoaded = spreadTo(api, propagationId, network, exitPos, new TurnDirection(paths[i].OutFacing, paths[i].OutRot), out missingChunkPos);

                if (!chunkLoaded)
                {
                    LeaveNetwork();
                    return false;
                }
            }

            return true;
        }

        protected virtual bool spreadTo(ICoreAPI api, long propagationId, MechanicalNetwork network, BlockPos exitPos, TurnDirection exitTurnDir, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;
            BEBehaviorMPBase beMechBase = api.World.BlockAccessor.GetBlockEntity(exitPos)?.GetBehavior<BEBehaviorMPBase>();
            IMechanicalPowerBlock mechBlock = beMechBase?.Block as IMechanicalPowerBlock;

            if (beMechBase == null && api.World.BlockAccessor.GetChunkAtBlockPos(exitPos) == null)
            {
                missingChunkPos = new Vec3i(exitPos.X / api.World.BlockAccessor.ChunkSize, exitPos.Y / api.World.BlockAccessor.ChunkSize, exitPos.Z / api.World.BlockAccessor.ChunkSize);
                return false;
            }

            if (beMechBase != null && mechBlock.HasMechPowerConnectorAt(api.World, exitPos, exitTurnDir.Facing.GetOpposite()))
            {
                beMechBase.Api = api;
                if (!beMechBase.JoinAndSpreadNetworkToNeighbours(api, propagationId, network, exitTurnDir, out missingChunkPos))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Must return the path mechanical power takes, coming from given direction and turn direction
        /// </summary>
        /// <param name="fromExitTurnDir"></param>
        /// <returns></returns>
        protected abstract MechPowerPath[] GetMechPowerExits(TurnDirection fromExitTurnDir);


        #endregion
    }


}
