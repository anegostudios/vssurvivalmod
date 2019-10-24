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
        public BlockFacing Facing;
        public EnumTurnDirection TurnDir;

        public MechPowerPath()
        {
        }
        public MechPowerPath(BlockFacing facing, EnumTurnDirection turnDir)
        {
            this.Facing = facing;
            this.TurnDir = turnDir;
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

        public virtual int[] AxisMapping { get; protected set; }
        public virtual int[] AxisSign { get; protected set; }

        public long NetworkId { get; set; }

        public MechanicalNetwork Network => network;
        public virtual BlockFacing OutFacingForNetworkDiscovery { get; protected set; } = null;

        public virtual Block Block { get; set; }

        public BlockFacing orientation;
        protected EnumTurnDirection turnDir;
        protected BlockFacing turnDirFromFacing = BlockFacing.NORTH;
        protected float lastKnownAngle = 0;

        public virtual float Angle
        {
            get
            {
                if (network == null) return lastKnownAngle;

                if (network.TurnDirection != turnDir)
                {
                    return (lastKnownAngle = 360 - network.Angle);
                }

                return (lastKnownAngle = network.Angle);
            }
        }

        
        public BEBehaviorMPBase(BlockEntity blockentity) : base(blockentity)
        {
            Block = Blockentity.Block;
        }


        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            Shape = Block.Shape;

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

            AxisMapping = new int[3] { 0, 1, 2 };
            AxisSign = new int[3] { 1, 1, 1 };
            SetOrientations();

            if (api.Side == EnumAppSide.Server && OutFacingForNetworkDiscovery != null)
            {
                CreateJoinAndDiscoverNetwork(OutFacingForNetworkDiscovery);
            }
        }



        public virtual void SetOrientations()
        {

        }

        public virtual void WasPlaced(BlockFacing connectedOnFacing, IMechanicalPowerBlock connectedToBlock)
        {
            MechanicalNetwork network;

            if (connectedOnFacing != null)
            {
                network = connectedToBlock.GetNetwork(Api.World, Position.AddCopy(connectedOnFacing));
                if (network != null)
                {
                    IMechanicalPowerNode node = Api.World.BlockAccessor.GetBlockEntity(Position.AddCopy(connectedOnFacing)).GetBehavior<BEBehaviorMPBase>() as IMechanicalPowerNode;

                    SetBaseTurnDirection(node.GetTurnDirection(connectedOnFacing.GetOpposite()), connectedOnFacing.GetOpposite());
                    JoinNetwork(network);

                    MechPowerPath[] paths = GetMechPowerPaths(connectedOnFacing, turnDir);
                    for (int i = 0; i < paths.Length; i++)
                    {
                        BlockPos exitPos = Position.AddCopy(paths[i].Facing);
                        Vec3i missingChunkPos;
                        bool chunkLoaded = spreadTo(Api, manager.GetNextPropagationId(), network, exitPos, paths[i].TurnDir, paths[i].Facing, out missingChunkPos);

                        // TODO
                        /*if (!chunkLoaded)
                        {
                            LeaveNetwork();
                            return false;
                        }*/
                    }

                    //return true;


                }
            }
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

            turnDirFromFacing = BlockFacing.ALLFACES[tree.GetInt("turnDirFromFacing")];
            turnDir = (EnumTurnDirection)tree.GetInt("turnDir");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("networkid", NetworkId);
            tree.SetInt("turnDirFromFacing", turnDirFromFacing.Index);
            tree.SetInt("turnDir", (int)turnDir);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            if (Api.World.EntityDebugMode)
            {
                sb.AppendLine(string.Format(
                    "networkid: {0}, turnDir: {1}, networkTurnDir: {2}, network speed: {3:G4}, avail torque: {4:G4}, network torque sum: {5:G4}, network resist sum: {6:G4}", 
                    NetworkId, turnDir, network?.TurnDirection, network?.Speed, network?.TotalAvailableTorque, network?.NetworkTorque, network?.NetworkResistance
                ));
            }

            //return base.GetBlockInfo(forPlayer);
        }


        public virtual EnumTurnDirection GetBaseTurnDirection()
        {
            return turnDir;
        }

        public virtual void SetBaseTurnDirection(EnumTurnDirection turnDir, BlockFacing fromFacing)
        {
            this.turnDir = turnDir;
        }

        public virtual EnumTurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return turnDir;
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

                Vec3i missingChunkPos;
                bool chunksLoaded = spreadTo(Api, manager.GetNextPropagationId(), newNetwork, Position.AddCopy(powerOutFacing), GetTurnDirection(powerOutFacing), powerOutFacing, out missingChunkPos);

                if (!chunksLoaded)
                {
                    network.AwaitChunkThenDiscover(missingChunkPos);
                    manager.testFullyLoaded(network); // To trigger that allFullyLoaded gets false
                    return network;
                }
            }
            else
            {
                JoinNetwork(neibNetwork);
            }

            return network;
        }

        long currentPropagationId = 0;
        public virtual bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, long propagationId, MechanicalNetwork network, EnumTurnDirection turnDir, BlockFacing fromFacing, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;

            if (propagationId == currentPropagationId) return true; // Already got the message

            currentPropagationId = propagationId;

            JoinNetwork(network);
            (Block as IMechanicalPowerBlock).DidConnectAt(api.World, Position, fromFacing);

            MechPowerPath[] paths = GetMechPowerPaths(fromFacing, turnDir);
            for (int i = 0; i < paths.Length; i++)
            {
                BlockPos exitPos = Position.AddCopy(paths[i].Facing);
                bool chunkLoaded = spreadTo(api, propagationId, network, exitPos, paths[i].TurnDir, paths[i].Facing, out missingChunkPos);

                if (!chunkLoaded)
                {
                    LeaveNetwork();
                    return false;
                }
            }

            return true;
        }

        protected virtual bool spreadTo(ICoreAPI api, long propagationId, MechanicalNetwork network, BlockPos exitPos, EnumTurnDirection turnDir, BlockFacing facing, out Vec3i missingChunkPos)
        {
            missingChunkPos = null;
            BEBehaviorMPBase beMechBase = api.World.BlockAccessor.GetBlockEntity(exitPos)?.GetBehavior<BEBehaviorMPBase>();
            IMechanicalPowerBlock mechBlock = beMechBase?.Block as IMechanicalPowerBlock;

            if (beMechBase == null && api.World.BlockAccessor.GetChunkAtBlockPos(exitPos) == null)
            {
                missingChunkPos = new Vec3i(exitPos.X / api.World.BlockAccessor.ChunkSize, exitPos.Y / api.World.BlockAccessor.ChunkSize, exitPos.Z / api.World.BlockAccessor.ChunkSize);
                return false;
            }

            if (beMechBase != null && mechBlock.HasConnectorAt(api.World, exitPos, facing.GetOpposite()))
            {
                beMechBase.Api = api;
                beMechBase.SetBaseTurnDirection(turnDir, facing.GetOpposite());
                if (!beMechBase.JoinAndSpreadNetworkToNeighbours(api, propagationId, network, turnDir, facing.GetOpposite(), out missingChunkPos))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Must return the path mechanical power takes, coming from given direction and turn direction
        /// </summary>
        /// <param name="fromFacing"></param>
        /// <param name="turnDir"></param>
        /// <returns></returns>
        protected abstract MechPowerPath[] GetMechPowerPaths(BlockFacing fromFacing, EnumTurnDirection turnDir);


        #endregion
    }


}
