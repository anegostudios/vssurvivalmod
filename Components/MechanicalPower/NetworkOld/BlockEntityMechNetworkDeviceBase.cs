using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public abstract class BlockEntityMechNetworkDeviceBase : BlockEntity, IMechanicalPowerDeviceOld
    {
        public bool markedForRemoval = false;

        long networkId;  // The network we're connected at [orientation] 
        public BlockFacing orientation;

        int propagationId;

        // Wether the device turns clockwise as seen from clockwiseFromFacing (standing 3 blocks away from this facing and looking towards the block 
        public bool clockwise;
        public BlockFacing directionFromFacing;

        float lastAngle = 0f;


        public BlockFacing getOrientation()
        {
            return orientation;
        }

        public float getAngle()
        {
            MechanicalNetworkOld network = getNetwork(null);
            if (network == null)
            {
                return lastAngle;
            }

            if (directionFromFacing != orientation)
            {
                return (lastAngle = 360 - network.getAngle());
            }

            return (lastAngle = network.getAngle());
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            this.networkId = tree.GetLong("networkId");

            clockwise = tree.GetInt("clockwise") > 0;

            int num = tree.GetInt("orientation");
            orientation = num == -1 ? null : BlockFacing.ALLFACES[num];

            num = tree.GetInt("clockwiseFromFacing");
            directionFromFacing = num == -1 ? null : BlockFacing.ALLFACES[num];
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetLong("networkId", networkId);
            tree.SetInt("orientation", orientation == null ? -1 : orientation.Index);
            tree.SetInt("clockwiseFromFacing", directionFromFacing == null ? -1 : directionFromFacing.Index);
            tree.SetInt("clockwise", clockwise ? 1 : 0);
        }


        public void setDirectionFromFacing(BlockFacing facing)
        {
            this.directionFromFacing = facing;
        }

        public BlockFacing getDirectionFromFacing()
        {
            return directionFromFacing;
        }

        
        public MechanicalNetworkOld getNetwork(BlockFacing facing)
        {
            if (api.World == null || MechNetworkManagerRegistry.ManagersByWorld[api.World] == null) return null;

            return MechNetworkManagerRegistry.ManagersByWorld[api.World].getNetworkById(networkId);
        }

        
        public BlockFacing getFacing(MechanicalNetworkOld network)
        {
            return orientation;
        }



        public MechanicalNetworkOld[] getNetworks()
        {
            MechanicalNetworkOld network = MechNetworkManagerRegistry.ManagersByWorld[api.World].getNetworkById(networkId);
            if (network == null)
            {
                return new MechanicalNetworkOld[0];
            }
            return new MechanicalNetworkOld[] { network };
        }

        public float getNetworkSpeed(BlockFacing facing)
        {
            MechanicalNetworkOld network = getNetwork(facing);
            if (network == null) return 0;
            return network.getSpeed();
        }


        public void trySetNetwork(long networkId, BlockFacing localFacing)
        {
            if (hasConnectorAt(localFacing))
            {
                this.networkId = networkId;

                IMechanicalPowerDeviceOld device = getNeighbourDevice(localFacing, true);
                if (device != null)
                {
                    if (getNetwork(localFacing).getDirection() != 0)
                    {
                        clockwise = device.isClockWiseDirection(localFacing.GetOpposite());
                        setDirectionFromFacing(localFacing.GetOpposite());
                    }
                }
                else
                {
                    throw new Exception("Eh, a network coming from " + localFacing + ", but there is no device, instead " + api.World.BlockAccessor.GetBlock(pos.AddCopy(localFacing)) + "?!");
                }

                getNetwork(localFacing).register(this);

                api.World.BlockAccessor.MarkBlockEntityDirty(pos);
            }
            else
            {
                directionFromFacing = null;
                this.networkId = 0;
            }
        }


        public void onDevicePlaced(IWorldAccessor world, BlockPos pos, BlockFacing facing, BlockFacing ontoside)
        {
            orientation = facing;
            handleMechanicalRelayPlacement();
        }

        public void onDeviceRemoved(IWorldAccessor world, BlockPos pos)
        {
            markedForRemoval = true;
            handleMechanicRelayRemoval();
        }



        public void propagateNetworkToNeighbours(int propagationId, int networkId, BlockFacing remoteFacing)
        {
            // Already propagated
            if (this.propagationId == propagationId) return;
            this.propagationId = propagationId;

            trySetNetwork(networkId, remoteFacing.GetOpposite());
            getNetwork(remoteFacing.GetOpposite()).register(this);


            sendNetworkToNeighbours(propagationId, networkId, remoteFacing);
        }


        public void propagateDirectionToNeightbours(int propagationId, BlockFacing remoteFacing, bool clockwise)
        {
            // Already propagated
            if (this.propagationId == propagationId) return;
            this.propagationId = propagationId;

            this.clockwise = clockwise;
            setDirectionFromFacing(remoteFacing);

            api.World.BlockAccessor.MarkBlockEntityDirty(pos);

            Dictionary<BlockFacing, IMechanicalPowerDeviceOld> connectibleNeighbours = getNeighbourDevices(true);

            foreach (BlockFacing localFacing in connectibleNeighbours.Keys)
            {
                if (remoteFacing.GetOpposite() != localFacing)
                {
                    bool remoteClockwise = isClockWiseDirection(localFacing);
                    connectibleNeighbours[localFacing].propagateDirectionToNeightbours(propagationId, localFacing, remoteClockwise);
                }
            }
        }

        public void sendNetworkToNeighbours(int propagationId, int networkId, BlockFacing remoteFacing)
        {
            Dictionary<BlockFacing, IMechanicalPowerDeviceOld> connectibleNeighbours = getNeighbourDevices(true);

            foreach (BlockFacing localFacing in connectibleNeighbours.Keys)
            {
                if (remoteFacing.GetOpposite() != localFacing)
                {
                    connectibleNeighbours[localFacing].propagateNetworkToNeighbours(propagationId, networkId, localFacing);
                }
            }
        }



        public void handleMechanicRelayRemoval()
        {
            if (networkId == 0) return;
            foreach (MechanicalNetworkOld network in getNetworks())
            {
                network.unregister(this);
            }
        }

        public void handleMechanicalRelayPlacement()
        {
            Dictionary<BlockFacing, MechanicalNetworkOld> networks = new Dictionary<BlockFacing, MechanicalNetworkOld>();
            List<BlockFacing> nullnetworks = new List<BlockFacing>();

            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                IMechanicalPowerDeviceOld neib = getNeighbourDevice(facing, true);

                if (neib == null) continue;

                MechanicalNetworkOld network = neib.getNetwork(facing.GetOpposite());

                if (network != null && !networks.ContainsValue(network))
                {
                    networks[facing] = network;
                }

                if (network == null)
                {
                    nullnetworks.Add(facing);
                }
            }

            //System.out.println(worldObj.isRemote + " found " + networks.size() + " networks ");

            if (networks.Count == 1)
            {
                BlockFacing facing = networks.Keys.ToArray()[0];

                trySetNetwork(networks[facing].networkId, facing);

                foreach (BlockFacing nullnetworkfacing in nullnetworks)
                {
                    getNeighbourDevice(nullnetworkfacing, true).propagateNetworkToNeighbours(
                        MechNetworkManagerRegistry.ManagersByWorld[api.World].getUniquePropagationId(),
                        networkId,
                        nullnetworkfacing
                    );
                }
            }

            if (networks.Count > 1 && api.World is IClientWorldAccessor)
            {
                float maxSpeedDifference = 0;
                MechanicalNetworkOld dominantNetwork = null;

                foreach (MechanicalNetworkOld network in networks.Values)
                {
                    if (dominantNetwork == null)
                    {
                        dominantNetwork = network;
                        continue;
                    }

                    maxSpeedDifference = Math.Max(maxSpeedDifference, Math.Abs(network.getSpeed() - dominantNetwork.getSpeed()));

                    if (Math.Abs(network.getSpeed()) > Math.Abs(dominantNetwork.getSpeed()))
                    {
                        dominantNetwork = network;
                    }
                }

                // Here we could disallow connecting of networks if
                // maxSpeedDifference is larger than 1
                // e.g. immediately break the placed block again because it cannot handle
                // the large torque difference. But implementation will be somewhat complicated
                foreach (MechanicalNetworkOld network in networks.Values)
                {
                    if (network != dominantNetwork)
                    {
                        network.isDead = true;
                        MechNetworkManagerRegistry.ManagersByWorld[api.World].discardNetwork(network);
                    }
                }
                dominantNetwork.rebuildNetwork();

                api.World.BlockAccessor.MarkBlockEntityDirty(pos);
            }

        }


        // connected = true   => get connected devices
        // connected = false  => get connectible devices (= devices that could potentially connect to our own device)
        public Dictionary<BlockFacing, IMechanicalPowerDeviceOld> getNeighbourDevices(bool connected)
        {
            Dictionary<BlockFacing, IMechanicalPowerDeviceOld> connectibleNeighbours = new Dictionary<BlockFacing, IMechanicalPowerDeviceOld>();
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                IMechanicalPowerDeviceOld neib = getNeighbourDevice(facing, connected);
                if (neib == null) continue;
                connectibleNeighbours[facing] = neib;
            }
            return connectibleNeighbours;
        }


        public IMechanicalPowerDeviceOld getConnectibleNeighbourDevice(BlockFacing facing)
        {
            return getNeighbourDevice(facing, false);
        }

        public IMechanicalPowerDeviceOld getNeighbourDevice(BlockFacing facing, bool connected)
        {
            return getNeighbourDevice(api.World, pos, facing, connected);
        }


        public static IMechanicalPowerDeviceOld getNeighbourDevice(IWorldAccessor world, BlockPos pos, BlockFacing facing, bool connected)
        {
            if (facing == null) return null;

            BlockEntity te = world.BlockAccessor.GetBlockEntity(pos.Offset(facing));

            if (te is IMechanicalPowerDeviceOld) {
                IMechanicalPowerDeviceOld mechdevice = (IMechanicalPowerDeviceOld)te;
                if (!mechdevice.exists()) return null;

                if (!connected && mechdevice.hasConnectorAt(facing.GetOpposite()))
                {
                    return mechdevice;
                }
                if (connected && mechdevice.isConnectedAt(facing.GetOpposite()))
                {
                    return mechdevice;
                }
            }

            return null;
        }


        public static bool hasConnectibleDevice(IWorldAccessor world, BlockPos pos)
        {
            foreach (BlockFacing facing in BlockFacing.ALLFACES)
            {
                if (getNeighbourDevice(world, pos, facing, false) != null) return true;
            }
            return false;
        }

        // Default behavior: Device is connected to all neighbor devices
        public bool isConnectedAt(BlockFacing facing)
        {
            IMechanicalPowerDeviceOld device = getNeighbourDevice(facing, false);

            return device != null && (device is IMechanicalPowerDeviceOld);
        }



        public bool isClockWiseDirection(BlockFacing facing)
        {
            return clockwise;
        }

        public bool exists()
        {
            return api.World.BlockAccessor.GetBlockEntity(pos) == this && !markedForRemoval;
        }


        public void setClockWiseDirection(int networkId, bool clockwise)
        {
            this.clockwise = clockwise;
        }

        public void clearNetwork()
        {
            this.networkId = 0;
            api.World.BlockAccessor.MarkBlockEntityDirty(pos);
        }

        public void onNeighborBlockChange()
        {
        }

        public abstract bool hasConnectorAt(BlockFacing localFacing);
        public abstract void trySetNetwork(int networkId, BlockFacing localFacing);
        public abstract void propagateNetworkToNeighbours(int propagationId, long networkId, BlockFacing remoteFacing);
        public abstract void setClockWiseDirection(long networkId, bool clockwise);
        public abstract BlockPos getPosition();
    }
}
