using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
        public EnumRotDirection TurnDir { get; set; } = EnumRotDirection.Clockwise;



        public float clientSpeed;
        int chunksize;
        public bool fullyLoaded;
        private bool firstTick = true;

        /// <summary>
        /// Set to false when a block with more than one connection in the network has been broken
        /// </summary>
        public bool Valid
        {
            get; set;
        } = true;

        /// <summary>
        /// In radians
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

        public bool DirectionHasReversed { get; set; } = false;

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
            BlockPos pos = node.GetPosition();
            nodes[pos] = node;

            Vec3i chunkpos = new Vec3i(pos.X / chunksize, pos.Y / chunksize, pos.Z / chunksize);
            int q;
            inChunks.TryGetValue(chunkpos, out q);
            inChunks[chunkpos] = q + 1;
        }

        public void DidUnload(IMechanicalPowerDevice node)
        {
            fullyLoaded = false;
        }

        public void Leave(IMechanicalPowerNode node)
        {
            BlockPos pos = node.GetPosition();
            nodes.Remove(pos);

            Vec3i chunkpos = new Vec3i(pos.X / chunksize, pos.Y / chunksize, pos.Z / chunksize);
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
            if (firstTick)
            {
                firstTick = false;
                mechanicalPowerMod.SendNetworkBlocksUpdateRequestToServer(this.networkId);
            }
            if (speed < 0.001f) return;

            // 50fps is baseline speed for client and server (1000/50 = 20ms)
            //float weirdOffset = 5f; // Server seems to complete a work item quicker than on the client, does it update the angle more quickly or something? o.O
            float f = dt * (50f);// + weirdOffset);

            clientSpeed += GameMath.Clamp(speed - clientSpeed, f * -0.01f, f * 0.01f);

            UpdateAngle(f * (TurnDir == EnumRotDirection.Clockwise ^ DirectionHasReversed ? clientSpeed : -clientSpeed));

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
                updateNetwork(tickNumber);
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
                direction = (speed >= 0f ? 1 : -1),
                totalAvailableTorque = totalAvailableTorque,
                networkResistance = networkResistance,
                networkTorque = networkTorque
            });
        }

        public void UpdateAngle(float speed)
        {
            angle += speed / 10f;
            //angle = angle % GameMath.TWOPI;

            serverSideAngle += speed / 10f;
            //serverSideAngle = serverSideAngle % GameMath.TWOPI;
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

        // Should run every 5 ticks or so
        public void updateNetwork(long tick)
        {
            //ensure no speed discontinuities if whole network direction suddenly reverses (e.g. because a block was added or removed)
            if (DirectionHasReversed)
            {
                speed = -speed;
                DirectionHasReversed = false;
                //TurnDir = TurnDir == EnumRotDirection.Clockwise ? EnumRotDirection.Counterclockwise : EnumRotDirection.Clockwise;
            }
            /* 2. Determine total available torque and total resistance of the network */

            float totalTorque = 0f;
            float totalResistance = 0f;
            float speedTmp = speed;

            float resistance;
            foreach (IMechanicalPowerNode powerNode in nodes.Values)
            {
                float r = powerNode.GearedRatio;
                totalTorque += r * powerNode.GetTorque(tick, speedTmp * r, out resistance);
                totalResistance += r * resistance;
                totalResistance += speed * speed * r * r / 1000f;  //this creates an air resistance effect - very fast turning networks will quickly slow if torque is removed
            }
            networkTorque = totalTorque;
            networkResistance = totalResistance;

            /* 3. Unconsumed torque changes the network speed */
            // Definition: Negative speed (produced by negative torque) signifies anti-clockwise rotation.  There can be negative torque depending on all the torque providers on the network and their senses.

            // Positive free torque => increase speed until maxSpeed
            // Negative free torque => decrease speed until -maxSpeed
            // No free torque => lower speed until 0

            float unusedTorque = Math.Abs(totalTorque) - networkResistance;
            float torqueSign = totalTorque >= 0f ? 1f : -1f;

            float drag = Math.Max(1f, (float) Math.Pow(nodes.Count, 0.25));
            float step = 1f / (float)drag;

            bool wrongTurnSense = speed * torqueSign < 0f;
            if (unusedTorque > 0f && !wrongTurnSense)
            {
                speed += Math.Min(0.05f, step * unusedTorque) * torqueSign;
            }
            else
            {
                float change = unusedTorque;
                if (wrongTurnSense) change = -networkResistance;
                if (change < -Math.Abs(speed)) change = -Math.Abs(speed);  //this creates a momentum effect: the network will not stop suddenly, even if resistance suddenly goes sky high.  Momentum increases for more pieces in the network (1/drag).
                if (change < -0.000001f)
                {
                    float speedSign = speed < 0f ? -1f : 1f;
                    speed = Math.Max(0.000001f, Math.Abs(speed) + 4f * drag * change) * speedSign;  //the 4f * drag multiplier slows a network down fairly quickly when torque is removed
                }
                else if (Math.Abs(unusedTorque) > 0f) speed = torqueSign / 1000000f;   //a small speed in the correct direction, to start things off
            }

            if (unusedTorque > Math.Abs(totalAvailableTorque))
            {
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

            TurnDir = speed >= 0 ? EnumRotDirection.Clockwise : EnumRotDirection.Counterclockwise;
        }

        public void UpdateFromPacket(MechNetworkPacket packet, bool isNew)
        {
            totalAvailableTorque = packet.totalAvailableTorque;
            networkResistance = packet.networkResistance;
            networkTorque = packet.networkTorque;

            speed = Math.Abs(packet.speed);  //ClientTick() expects speed to be positive always
            if (isNew)
            {
                angle = packet.angle;
                clientSpeed = speed;
            }

            serverSideAngle = packet.angle;

            TurnDir = packet.direction >= 0 ? EnumRotDirection.Clockwise : EnumRotDirection.Counterclockwise;
            DirectionHasReversed = false;
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
            
            TurnDir = (EnumRotDirection)tree.GetInt("rot");
        }

        public void WriteToTreeAttribute(ITreeAttribute tree)
        {
            tree.SetLong("networkId", networkId);
            tree.SetFloat("totalAvailableTorque", totalAvailableTorque);
            tree.SetFloat("totalResistance", networkResistance);
            tree.SetFloat("speed", speed);
            tree.SetFloat("angle", angle);

            tree.SetInt("rot", (int)TurnDir);
        }

        public void SendBlocksUpdateToClient(IServerPlayer player)
        {
            foreach (IMechanicalPowerNode powerNode in nodes.Values)
            {
                if (powerNode is BEBehaviorMPBase bemp)
                {
                    bemp.Blockentity.MarkDirty();
                    //TODO: for efficiency in multiplayer it would be better to send the MarkDirty update only to the client who sent the MechClientRequestPacket message...
                }
            }
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

    /// <summary>
    /// Used by clients to request that the server refresh (MarkDirty()) all blockEntities comprised in the serverside network, to ensure server-client networkID sync on all blockEntities
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MechClientRequestPacket
    {
        public long networkId;
    }
}
