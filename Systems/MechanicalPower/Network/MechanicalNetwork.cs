using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    [ProtoContract]
    public class MechanicalNetwork
    {
        public Dictionary<BlockPos, IMechanicalPowerNode> nodes = new Dictionary<BlockPos, IMechanicalPowerNode>();

        internal MechanicalPowerMod mechanicalPowerMod;

        [ProtoMember(1)]
        public long networkId;
        [ProtoMember(2)]
        protected float totalAvailableTorque;
        [ProtoMember(3)]
        protected float networkResistance;
        [ProtoMember(4)]
        protected float speed;
        [ProtoMember(7)]
        protected float serverSideAngle;
        [ProtoMember(8)]
        protected float angle = 0; // In radians
        [ProtoMember(9)]
        public Dictionary<Vec3i, int> inChunks = new Dictionary<Vec3i, int>();
        [ProtoMember(10)]
        float networkTorque;
        [ProtoMember(11)]
        public TurnDirection TurnDir { get; set; } = new TurnDirection();



        float clientSpeed;
        int chunksize;
        public bool fullyLoaded;
        private float drivenTorque = 0f;

        /// <summary>
        /// Set to false when a block with more than one connection in the network has been broken
        /// </summary>
        public bool Valid
        {
            get; set;
        } = true;

        /// <summary>
        /// In radiant
        /// </summary>
        public float AngleRad
        {
            get { return angle; }
            set { angle = value; }
        }

        public float Speed
        {
            get { return speed; }
            set { speed = value; }
        }

        public float TotalAvailableTorque
        {
            get { return totalAvailableTorque; }
            set { totalAvailableTorque = value; }
        }

        public MechanicalNetwork()
        {
            
        }

        public MechanicalNetwork(MechanicalPowerMod mechanicalPowerMod, long networkId)
        {
            this.networkId = networkId;
            Init(mechanicalPowerMod);
        }

        public void Init(MechanicalPowerMod mechanicalPowerMod)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;

            chunksize = mechanicalPowerMod.Api.World.BlockAccessor.ChunkSize;
        }

        public void Join(IMechanicalPowerNode node)
        {
            nodes[node.Position] = node;

            Vec3i chunkpos = new Vec3i(node.Position.X / chunksize, node.Position.Y / chunksize, node.Position.Z / chunksize);
            int q;
            inChunks.TryGetValue(chunkpos, out q);
            inChunks[chunkpos] = q + 1;
        }

        public void DidUnload(IMechanicalPowerNode node)
        {
            fullyLoaded = false;
        }

        public void Leave(IMechanicalPowerNode node)
        {
            nodes.Remove(node.Position);

            Vec3i chunkpos = new Vec3i(node.Position.X / chunksize, node.Position.Y / chunksize, node.Position.Z / chunksize);
            int q;
            inChunks.TryGetValue(chunkpos, out q);
            if (q <= 1)
            {
                inChunks.Remove(chunkpos);
            } else
            {
                inChunks[chunkpos] = q - 1;
            }
        }

        internal void AwaitChunkThenDiscover(Vec3i missingChunkPos)
        {
            inChunks[missingChunkPos] = 1;
            fullyLoaded = false;
        }


        // Tick Events are called by the Network Managers
        public void ClientTick(float dt)
        {
            if (speed < 0.001) return;

            // 50fps is baseline speed for client and server (1000/50 = 20ms)
            //float weirdOffset = 5f; // Server seems to complete a work item quicker than on the client, does it update the angle more quickly or something? o.O
            float f = dt * (50f);// + weirdOffset);

            clientSpeed += GameMath.Clamp(speed - clientSpeed, f * -0.01f, f * 0.01f); 

            UpdateAngle(f * clientSpeed);

            // Since the server may be running at different tick speeds,
            // we slowly sync angle updates from server to reduce 
            // rotation jerkiness on the client

            // Each tick, add 5% of server<->client angle difference

            float diff = f * GameMath.AngleRadDistance(angle, serverSideAngle);
            angle += GameMath.Clamp(diff, -0.002f * Math.Abs(diff), 0.002f * Math.Abs(diff));
        }


        public void ServerTick(float dt, long tickNumber)
        {
            UpdateAngle(speed * dt * 50f);

            if (tickNumber % 5 == 0)
            {
                updateNetwork();
            }

            if (tickNumber % 40 == 0)
            {
                broadcastData();
            }
        }

        public void broadcastData()
        {
            mechanicalPowerMod.broadcastNetwork(new MechNetworkPacket()
            {
                angle = angle,
                networkId = networkId,
                speed = speed,
                totalAvailableTorque = totalAvailableTorque,
                networkResistance = networkResistance,
                networkTorque = networkTorque
            });
        }

        public void UpdateAngle(float speed)
        {
            angle += speed / 10f;
            angle = angle % GameMath.TWOPI;

            serverSideAngle += speed / 10f;
            serverSideAngle = serverSideAngle % GameMath.TWOPI;
        }

        public float NetworkTorque {
            get { return networkTorque; }
            set { networkTorque = value; }
        }

        public float NetworkResistance
        {
            get { return networkResistance; }
            set { networkResistance = value; }
        }

        /// <summary>
        /// Allow a network to be driven by another network, instead of by a rotor
        /// </summary>
        public float Drive(float torqueIn, float targetSpeed, float accelerationFactor, int dir)
        {
            if ((targetSpeed >= speed || Math.Abs(networkTorque) < Math.Abs(torqueIn)) && Math.Abs(drivenTorque) < Math.Abs(torqueIn))
            {
                speed += (targetSpeed - speed) * accelerationFactor;
                drivenTorque = torqueIn;
                TurnDir.Rot = dir > 0 ? EnumRotDirection.Clockwise : EnumRotDirection.Counterclockwise;
            }
            return networkResistance;// - Math.Abs(networkTorque);
            //System.Diagnostics.Debug.WriteLine("Drive " + dir + " " + TurnDir.Rot + " " + drivenTorque + " " + totalAvailableTorque);
        }

        // Should run every 5 ticks or so
        public void updateNetwork()
        {
            /* 2. Determine total available torque and total resistance of the network */

            networkTorque = 0f;
            networkResistance = 0;
            
            foreach (IMechanicalPowerNode powerNode in nodes.Values)
            {
                networkTorque += powerNode.GetTorque();
                networkResistance += powerNode.GetResistance();
            }

            float totalTorque = networkTorque + drivenTorque;
            drivenTorque *= 0.85f;  //this falls off quite fast if no longer driven


            /* 3. Unconsumed torque changes the network speed */

            // Positive free torque => increase speed until maxSpeed
            // Negative free torque => decrease speed until -maxSpeed
            // No free torque => lower speed until 0

            float unusedTorque = Math.Abs(totalTorque) - networkResistance;


            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJtYXgoMSx4XjAuMjUpIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjEwMCIsIjAiLCI1Il19XQ--
            double drag = Math.Max(1, Math.Pow(nodes.Count, 0.25));
            float step = 0.5f / (float)drag;

            // Definition: There is no negative speed, but there can be negative torque
            if (unusedTorque > 0)
            {
                speed += Math.Min(0.05f, step * unusedTorque);
            } else
            {
                speed = Math.Max(0, speed + step * unusedTorque);
            }

            if (unusedTorque > Math.Abs(totalAvailableTorque))
            {
                //System.Diagnostics.Debug.WriteLine("update " + TurnDir.Rot + " " + drivenTorque + " " + totalAvailableTorque + " " + totalTorque);
                if (totalTorque > 0)
                {
                    totalAvailableTorque = Math.Min(totalTorque, totalAvailableTorque + step);
                }
                else
                {
                    totalAvailableTorque = Math.Max(Math.Min(totalTorque, -0.00000001f), totalAvailableTorque - step);
                }
            }
            else totalAvailableTorque *= 0.9f;  //allows for adjustments to happen if torque reduces or changes sign

            TurnDir.Rot = totalAvailableTorque >= 0 ? EnumRotDirection.Clockwise : EnumRotDirection.Counterclockwise;
        }


        public void UpdateFromPacket(MechNetworkPacket packet, bool isNew)
        {
            totalAvailableTorque = packet.totalAvailableTorque;
            networkResistance = packet.networkResistance;
            networkTorque = packet.networkTorque;

            speed = packet.speed;
            if (isNew)
            {
                angle = packet.angle;
                clientSpeed = speed;
            }

            serverSideAngle = packet.angle;

            TurnDir.Rot = totalAvailableTorque >= 0 ? EnumRotDirection.Clockwise : EnumRotDirection.Counterclockwise;
        }

        
        public bool testFullyLoaded(ICoreAPI api)
        {
            foreach (var chunkpos in inChunks.Keys)
            {
                if (api.World.BlockAccessor.GetChunk(chunkpos.X, chunkpos.Y, chunkpos.Z) == null)
                {
                    return false;
                }
            }

            return true;
        }

        public void ReadFromTreeAttribute(ITreeAttribute tree)
        {
            networkId = tree.GetLong("networkId");
            totalAvailableTorque = tree.GetFloat("totalAvailableTorque");
            networkResistance = tree.GetFloat("totalResistance");
            speed = tree.GetFloat("speed");
            angle = tree.GetFloat("angle");
            
            TurnDir.Facing = BlockFacing.ALLFACES[tree.GetInt("turnDirectionFacing")];
            TurnDir.Rot = (EnumRotDirection)tree.GetInt("rot");
        }

        public void WriteToTreeAttribute(ITreeAttribute tree)
        {
            tree.SetLong("networkId", networkId);
            tree.SetFloat("totalAvailableTorque", totalAvailableTorque);
            tree.SetFloat("totalResistance", networkResistance);
            tree.SetFloat("speed", speed);
            tree.SetFloat("angle", angle);

            tree.SetInt("turnDirectionFacing", TurnDir.Facing.Index);
            tree.SetInt("rot", (int)TurnDir.Rot);
        }

        
    }


    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MechNetworkPacket
    {
        public long networkId;
        public float totalAvailableTorque;
        public float networkResistance;
        public float networkTorque;
        public float speed;
        public int direction;
        public float angle;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class NetworkRemovedPacket
    {
        public long networkId;
    }

}
