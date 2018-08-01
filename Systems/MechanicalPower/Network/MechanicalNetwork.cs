using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class MechanicalNetwork
    {
        MechNetworkManager myManager;

        // List of all connected devices  
        List<IMechanicalPowerNetworkNode> powerNodes = new List<IMechanicalPowerNetworkNode>();
        List<IMechanicalPowerNetworkRelay> powerRelays = new List<IMechanicalPowerNetworkRelay>();

        internal float totalAvailableTorque;
        internal float totalResistance;
        internal float speed;

        public long networkId;

        internal int direction;
        internal BlockFacing directionFromFacing; // From which facing is the direction?

        internal BlockPos firstPowerNode;

        internal float angle = 0;

        public bool isDead;

        public float serverSideAngle;


        public void updateAngle(float speed)
        {
            angle -= speed / 10f;
            angle = angle % 360f;

            serverSideAngle -= speed / 10f;
            serverSideAngle = serverSideAngle % 360f;
        }

        public float getAngle()
        {
            return angle;
        }



        public MechanicalNetwork(MechNetworkManager myManager, long networkId)
        {
            this.networkId = networkId;
            this.myManager = myManager;

            speed = 0;
            totalAvailableTorque = 0;
            totalResistance = 0;
        }

        public float getSpeed()
        {
            return speed;
        }

        public float getAvailableTorque()
        {
            return totalAvailableTorque;
        }

        public float getTotalResistance()
        {
            return totalResistance;
        }

        public bool isClockWise(BlockFacing facing)
        {
            if (facing == directionFromFacing)
            {
                return direction > 0;
            }
            return direction < 0;
        }

        public float getRemainingResistance()
        {
            return Math.Max(0, totalResistance - totalAvailableTorque);
        }
        public float getUnusedTorque()
        {
            return Math.Max(0, totalAvailableTorque - totalResistance);
        }

        public void register(IMechanicalPowerDevice device)
        {
            if (device is IMechanicalPowerNetworkNode && !powerNodes.Contains(device)) {
                powerNodes.Add((IMechanicalPowerNetworkNode)device);
            }
            if (device is IMechanicalPowerNetworkRelay && !powerRelays.Contains(device)) {
                powerRelays.Add((IMechanicalPowerNetworkRelay)device);
            }
        }

        public void unregister(IMechanicalPowerDevice device)
        {
            if (device is IMechanicalPowerNetworkNode) {
                powerNodes.Remove((IMechanicalPowerNetworkNode)device);
            }
            if (device is IMechanicalPowerNetworkRelay) {
                powerRelays.Remove((IMechanicalPowerNetworkRelay)device);
            }

            rebuildNetwork();
        }



        // Tick Events are called by the Network Managers
        public void ClientTick(long tickNumber) {
            if (speed < 0.001) return;

            updateAngle(speed);

            // Since the server may be running at different tick speeds,
            // we slowly sync angle updates from server to reduce 
            // rotation jerkiness on the client

            // Each tick, add 5% of server<->client angle difference
            float diff = 0.01f * (serverSideAngle - angle);
            if (diff > 0.005f)
            {
                angle -= diff;
            }
        }


        public void ServerTick(long tickNumber)
        {
            updateAngle(speed);

            if (tickNumber % 5 == 0)
            {
                updateNetwork();
            }

            if (tickNumber % 40 == 0)
            {
                myManager.broadcastNetwork(new MechNetworkPacket()
                {
                    angle = angle,
                    direction = direction,
                    firstNodeX = firstPowerNode.X,
                    firstNodeY = firstPowerNode.Y,
                    firstNodeZ = firstPowerNode.Z,
                    networkId = networkId,
                    speed = speed,
                    totalAvailableTorque = totalAvailableTorque,
                    totalResistance = totalResistance
                });
            }
        }


        // Should run every 5 ticks or so
        public void updateNetwork()
        {

            /* 1. Verify network */

            IMechanicalPowerNetworkNode[] powernodesArray = powerNodes.ToArray();
            foreach (IMechanicalPowerNetworkNode node in powernodesArray)
            {
                if (!node.exists())
                {

                    unregister(node);
                }
            }
            if (powerNodes.Count == 0)
            {
                isDead = true;
                return;
            }



            /* 2. Determine total available torque and total resistance of the network */

            totalAvailableTorque = 0;
            totalResistance = 0;

            IMechanicalPowerNetworkNode dominantNode = null;


            foreach (IMechanicalPowerNetworkNode powerNode in powerNodes)
            {
                totalAvailableTorque += powerNode.getTorque(this);
                totalResistance += powerNode.getResistance(this);

                if (dominantNode == null || powerNode.getTorque(this) > dominantNode.getTorque(this))
                {
                    dominantNode = powerNode;
                }
            }
            directionFromFacing = dominantNode.getDirectionFromFacing();



            /* 3. Unconsumed torque changes the network speed */

            // Positive free torque => increase speed until maxSpeed
            // Negative free torque => decrease speed until -maxSpeed
            // No free torque => lower speed until 0

            float unusedTorque = Math.Abs(totalAvailableTorque) - totalResistance;
            int speedChange = (totalAvailableTorque > 0) ? 1 : -1;
            if (unusedTorque <= 0)
            {
                speedChange = 0;
            }

            /*if (networkId == 6) {
                System.out.println("unusedTorque: " + unusedTorque + " / speedChange: " + speedChange);
            }*/

            // TODO: This step value should be determined by the total system drag
            float step = 0.75f;

            switch (speedChange)
            {
                case 1:
                    speed = speed + step;
                    break;

                case -1:
                    speed = speed - step;
                    break;

                case 0:
                    if (speed > 0)
                    {
                        speed = Math.Max(0, speed - step);
                    }
                    if (speed < 0)
                    {
                        speed = Math.Max(0, speed + step);
                    }
                    break;
            }



            /* 4. Set direction, also did the direction change? Propagate it through the network */

            int olddirection = direction;
            direction = (int)Math.Sign(speed);

            if (olddirection != direction)
            {
                //System.out.println("=====propagate direction to neighbours");
                foreach (IMechanicalPowerNetworkNode powerNode in powerNodes)
                {
                    // FIXME: This assumes there is only 1 power producer per network
                    if (powerNode.getTorque(this) > 0)
                    {
                        powerNode.propagateDirectionToNeightbours(
                            myManager.getUniquePropagationId(),
                            powerNode.getOutputSideForNetworkPropagation(),
                            direction > 0
                        );
                        break;
                    }
                }
            }

        }




        public void rediscoverNetwork(IWorldAccessor world)
        {
            //	System.out.println("rediscovering networks");
            BlockEntity te = world.BlockAccessor.GetBlockEntity(firstPowerNode);
            if (te is IMechanicalPowerNetworkNode) {
                //	System.out.println("go");
                IMechanicalPowerNetworkNode node = (IMechanicalPowerNetworkNode)te;
                node.propagateNetworkToNeighbours(myManager.getUniquePropagationId(), networkId, node.getOutputSideForNetworkPropagation());
            }

            //System.out.println("rediscovery complete, found " + powerNodes.size() + " power nodes");
        }


        public void rebuildNetwork()
        {
            //System.out.println("rebuilding network");

            foreach (IMechanicalPowerDevice device in powerRelays)
            {
                if (device != null)
                {
                    device.clearNetwork();
                }
            }

            if (powerNodes.Count == 0)
            {
                //System.out.println("no more power nodes in the network :(");
                return;
            }
            IMechanicalPowerNetworkNode firstNode = powerNodes[0];
            Dictionary<IMechanicalPowerNetworkNode, BlockFacing> otherNodes = new Dictionary<IMechanicalPowerNetworkNode, BlockFacing>();


            foreach (IMechanicalPowerNetworkNode powernode in powerNodes)
            {
                if (powernode != null && firstNode != powernode)
                {
                    otherNodes[powernode] = powernode.getFacing(this);
                    powernode.clearNetwork();
                }
            }

            if (firstNode == null)
            {
                //System.out.println("node is null");
                return;
            }

            powerNodes.Clear();
            powerRelays.Clear();

            firstNode.propagateNetworkToNeighbours(
                myManager.getUniquePropagationId(),
                networkId,
                firstNode.getOutputSideForNetworkPropagation()
            );


            // Whatever power nodes are now not in the current network now need to have the current network forked for them
            foreach (IMechanicalPowerNetworkNode otherNode in otherNodes.Keys)
            {
                bool found = false;
                foreach (MechanicalNetwork network in otherNode.getNetworks())
                {
                    if (network.networkId == networkId)
                    {
                        found = true;
                    }
                }
                if (!found)
                {
                    //System.out.println("forked network");
                    forkMechanicalNetwork(otherNode, otherNodes[otherNode]);
                }
            }

            //System.out.println("total networks in game: " + myManager.networksById.size());
        }


        public void forkMechanicalNetwork(IMechanicalPowerNetworkNode powernode, BlockFacing facing)
        {
            powernode.createMechanicalNetwork(this, facing);

        }

        public bool isClockWise()
        {
            return direction > 0;
        }

        public int getDirection()
        {
            return direction;
        }




        public void updateFromPacket(MechNetworkPacket packet, bool isNew)
        {
            totalAvailableTorque = packet.totalAvailableTorque;
            totalResistance = packet.totalResistance;
            speed = packet.speed;
            direction = packet.direction;
            if (isNew)
            {
                serverSideAngle = packet.angle;
            } else
            {
                angle = packet.angle;
            }

            firstPowerNode = new BlockPos(packet.firstNodeX, packet.firstNodeY, packet.firstNodeZ);
        }

        public void readFromTreeAttribute(ITreeAttribute tree)
        {
            networkId = tree.GetLong("networkId");
            totalAvailableTorque = tree.GetFloat("totalAvailableTorque");
            totalResistance = tree.GetFloat("totalResistance");
            speed = tree.GetFloat("speed");
            direction = tree.GetInt("direction");
            
            angle = tree.GetFloat("angle");

            firstPowerNode = new BlockPos(
                tree.GetInt("firstNodeX"),
                tree.GetInt("firstNodeY"),
                tree.GetInt("firstNodeZ")
            );
        }

        public void writeToTreeAttribute(ITreeAttribute tree)
        {
            tree.SetLong("networkId", networkId);
            tree.SetFloat("totalAvailableTorque", totalAvailableTorque);
            tree.SetFloat("totalResistance", totalResistance);
            tree.SetFloat("speed", speed);
            tree.SetInt("direction", direction);
            tree.SetFloat("angle", angle);

            tree.SetInt("firstNodeX", firstPowerNode.X);
            tree.SetInt("firstNodeY", firstPowerNode.Y);
            tree.SetInt("firstNodeZ", firstPowerNode.Z);
        }


    }
}
