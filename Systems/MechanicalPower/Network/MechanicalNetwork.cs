using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    [ProtoContract]
    public partial class MechanicalNetwork
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
        protected double serverSideAngle;
        [ProtoMember(8)]
        protected double angle = 0; // In radians
        [ProtoMember(9)]
        public Dictionary<Vec3i, int> inChunks = new Dictionary<Vec3i, int>();
        [ProtoMember(10)]
        float networkTorque;
        [ProtoMember(11)]
        public EnumRotDirection TurnDir { get; set; } = EnumRotDirection.Clockwise;

        [ProtoMember(12)]
        public bool HasPowerProducer { get; private set; }



        public float clientSpeed;
        const int chunksize = GlobalConstants.ChunkSize;
        const float clientAngleSyncRate = 10f;
        const float clientAngleTolerance = 0.00001f;
        const float torqueUpdateInterval = 0.1f;
        const float maxSpeedChange = 0.05f;
        public bool fullyLoaded;
        private bool firstTick = true;
        bool hasReceivedPacket;
        float elapsedTime;
        float coastingResistance;
        float coastingAirResistance;
        float coastingInverseInertia;
        bool coasting;

        /// <summary>
        /// Set to false when a block with more than one connection in the network has been broken
        /// </summary>
        public bool Valid
        {
            get; set;
        } = true;

        /// <summary>
        /// In radians. Use TotalAngleRad when the full accumulated precision is needed.
        /// </summary>
        public float AngleRad
        {
            get { return (float)angle; }
            set { angle = value; }
        }

        /// <summary>
        /// Shaft angle in radians, including complete turns needed by slower gears and linked shafts.
        /// </summary>
        public double TotalAngleRad
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

        public MechanicalNetwork() { }

        public MechanicalNetwork(MechanicalPowerMod mechanicalPowerMod, long networkId)
        {
            this.networkId = networkId;
            Init(mechanicalPowerMod);
        }

        public void Init(MechanicalPowerMod mechanicalPowerMod)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
        }

        public void Join(IMechanicalPowerNode node)
        {
            BlockPos pos = node.GetPosition();
            nodes.TryGetValue(pos, out IMechanicalPowerNode previous);
            nodes[pos] = node;
            if (mechanicalPowerMod.Api.Side == EnumAppSide.Server)
            {
                if ((node as IMechanicalPowerDevice)?.OutFacingForNetworkDiscovery != null) HasPowerProducer = true;
                else if ((previous as IMechanicalPowerDevice)?.OutFacingForNetworkDiscovery != null)
                {
                    HasPowerProducer = nodes.Values.Any(member => (member as IMechanicalPowerDevice)?.OutFacingForNetworkDiscovery != null);
                }
            }
            if (previous != null) return;

            Vec3i chunkpos = new Vec3i(pos.X / chunksize, pos.InternalY / chunksize, pos.Z / chunksize);
            inChunks.TryGetValue(chunkpos, out int q);
            inChunks[chunkpos] = q + 1;
        }

        public void DidUnload(IMechanicalPowerDevice node)
        {
            if (mechanicalPowerMod.Api.Side != EnumAppSide.Server)
            {
                fullyLoaded = false;
                return;
            }

            node.LeaveNetwork();
            if (!Valid) return;

            BlockPos pos = node.GetPosition();
            // Keep waiting for this chunk after releasing its unloaded devices.
            AwaitChunkThenDiscover(new Vec3i(pos.X / chunksize, pos.InternalY / chunksize, pos.Z / chunksize));
            mechanicalPowerMod.testFullyLoaded(this);
        }

        public void Leave(IMechanicalPowerNode node)
        {
            BlockPos pos = node.GetPosition();
            if (!nodes.Remove(pos, out IMechanicalPowerNode removed)) return;
            removeLoadPart(removed);
            if (mechanicalPowerMod.Api.Side == EnumAppSide.Server && (removed as IMechanicalPowerDevice)?.OutFacingForNetworkDiscovery != null)
            {
                HasPowerProducer = nodes.Values.Any(member => (member as IMechanicalPowerDevice)?.OutFacingForNetworkDiscovery != null);
            }

            Vec3i chunkpos = new Vec3i(pos.X / chunksize, pos.InternalY / chunksize, pos.Z / chunksize);
            inChunks.TryGetValue(chunkpos, out int q);
            if (q <= 1)
            {
                inChunks.Remove(chunkpos);
            } else
            {
                inChunks[chunkpos] = q - 1;
            }

            if (nodes.Count == 0 && Valid && mechanicalPowerMod.Api.Side == EnumAppSide.Server)
            {
                Valid = false;
                mechanicalPowerMod.DeleteNetwork(this);
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
            if (speed == 0)
            {
                clientSpeed = 0;
            }
            else
            {
                // 50fps is baseline speed for client and server (1000/50 = 20ms)
                float f = dt * 50f;
                clientSpeed += GameMath.Clamp(speed - clientSpeed, f * -0.01f, f * 0.01f);

                float direction = TurnDir == EnumRotDirection.Clockwise ^ DirectionHasReversed ? 1f : -1f;
                angle += f * direction * clientSpeed / 10f;
                serverSideAngle += f * direction * speed / 10f;
            }

            if (angle == serverSideAngle) return;

            // Retain complete turns so slower gears and linked shafts keep the server's phase.
            double diff = serverSideAngle - angle;
            angle += diff * (1f - (float)Math.Exp(-clientAngleSyncRate * dt));
            if (Math.Abs(serverSideAngle - angle) < clientAngleTolerance) angle = serverSideAngle;
        }


        public void ServerTick(float dt, long tickNumber)
        {
            bool wasTurning = speed != 0;
            bool updateSpeed = tickNumber % 5 == 0;
            float tickSpeed = speed;
            elapsedTime += dt;
            if (firstTick || updateSpeed)
            {
                firstTick = false;
                updateNetwork(tickNumber, elapsedTime, false);
                elapsedTime = 0;
            }

            UpdateAngle(coasting ? coast(dt) * 50f : tickSpeed * dt * 50f);

            if (tickNumber % 40 == 0 || updateSpeed && wasTurning && coasting)
            {
                broadcastData();
            }
        }

        public void broadcastData()
        {
            mechanicalPowerMod.broadcastNetwork(new MechNetworkPacket()
            {
                angle = (float)angle,
                totalAngle = angle,
                networkId = networkId,
                speed = speed,
                direction = (speed >= 0f ? 1 : -1),
                totalAvailableTorque = totalAvailableTorque,
                networkResistance = networkResistance,
                networkTorque = networkTorque,
                hasPowerProducer = HasPowerProducer,
                partLoads = getPartLoads()
            });
        }

        public void UpdateAngle(float speed)
        {
            angle += speed / 10f;
            serverSideAngle += speed / 10f;
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
            updateNetwork(tick, torqueUpdateInterval);
        }

        void updateNetwork(long tick, float dt, bool updateCoastingSpeed = true)
        {
            if (dt <= 0) return;
            // Ensure no speed discontinuities if whole network direction suddenly reverses (e.g. because a block was added or removed)
            if (DirectionHasReversed)
            {
                speed = -speed;
                DirectionHasReversed = false;
            }


            // Determine total available torque and total resistance of the network

            float totalTorque = 0f;
            float totalResistance = 0f;
            float airResistance = 0f;
            float totalInertia = 0f;
            float speedTmp = speed;
            beginLoadSamples();

            foreach (IMechanicalPowerNode powerNode in nodes.Values)
            {
                float r = getEffectiveGearedRatio(powerNode);
                var t = powerNode.GetTorque(tick, speedTmp * r, out float resistance);
                float inertia = powerNode.GetRotationalInertia();
                sampleLoad(powerNode, t, resistance, inertia);
                
                totalTorque += r * t;
                totalResistance += Math.Abs(r) * resistance;
                airResistance += Math.Abs(r) * r * r / 1000f;
                totalInertia += inertia * r * r;
            }
            networkTorque = totalTorque;
            networkResistance = totalResistance + speed * speed * airResistance;
            updateLoads(speedTmp, dt);

            // 3. Unconsumed torque changes the network speed
            // Definition: Negative speed (produced by negative torque) signifies anti-clockwise rotation.  There can be negative torque depending on all the torque providers on the network and their senses.
            // Positive free torque => increase speed until maxSpeed
            // Negative free torque => decrease speed until -maxSpeed
            // No free torque => lower speed until 0

            float unusedTorque = Math.Abs(totalTorque) - networkResistance;
            float torqueSign = totalTorque >= 0f ? 1f : -1f;

            // Both torque and stored rotational energy must refer to the same network shaft.
            float step = totalInertia > 0 ? 1f / totalInertia : 0;
            float timeFactor = dt / torqueUpdateInterval;
            coastingResistance = totalResistance;
            coastingAirResistance = airResistance;
            coastingInverseInertia = step;

            bool hasTorque = totalTorque != 0;
            bool wrongTurnSense = speed * torqueSign < 0f;
            float proposedChange = step * (wrongTurnSense ? -networkResistance : unusedTorque) * timeFactor;
            if (hasTorque) proposedChange = GameMath.Clamp(proposedChange, -maxSpeedChange * timeFactor, maxSpeedChange * timeFactor);
            float proposedSpeed = Math.Max(0, Math.Abs(speed) + proposedChange) * (speed == 0 ? torqueSign : Math.Sign(speed));

            float torqueResponse = 0;
            foreach (IMechanicalPowerNode powerNode in nodes.Values)
            {
                float r = getEffectiveGearedRatio(powerNode);
                torqueResponse += r * r * powerNode.GetTorqueResponse(speedTmp * r, proposedSpeed * r);
            }

            // Turns out zero torque doesn't always mean free coasting.
            // A rotor can start driving again as the shaft slows, and its drag can change too.
            coasting = !hasTorque && torqueResponse == 0;
            if (coasting)
            {
                totalAvailableTorque = 0;
                if (updateCoastingSpeed) coast(dt);
                return;
            }

            torqueResponse += 2 * airResistance * Math.Max(Math.Abs(speed), Math.Abs(proposedSpeed));
            // Reduce the speed change when the rotor would otherwise jump past its steady speed.
            float speedStep = totalInertia > 0 ? 1f / (totalInertia + timeFactor * torqueResponse) : 0;
            if (unusedTorque > 0f && !wrongTurnSense)
            {
                speed += Math.Min(maxSpeedChange, speedStep * unusedTorque) * timeFactor * torqueSign;
            }
            else
            {
                float change = Math.Max(-maxSpeedChange, speedStep * (wrongTurnSense ? -networkResistance : unusedTorque)) * timeFactor;
                if (change < -Math.Abs(speed)) change = -Math.Abs(speed);
                if (!hasTorque || change < -0.000001f || Math.Abs(speed) > 0.000001f)   // the change to this line in 1.14rc5 should stop the oscillations (speed crashes to zero)
                {
                    float speedSign = speed < 0f ? -1f : 1f;
                    speed = Math.Max(hasTorque ? 0.000001f : 0f, Math.Abs(speed) + change) * speedSign;  // before 1.14rc5 the 4f * drag multiplier (which was supposed to slow a network down quickly when torque is removed) caused oscillations in larger networks - too much slow-down when only slightly over speed for the current torque
                }
                else if (Math.Abs(unusedTorque) > 0f)
                {
                    speed = torqueSign / 1000000f;   // a small speed in the correct direction, to start things off
                }
            }

            if (!hasTorque) totalAvailableTorque = 0;
            else if (unusedTorque > Math.Abs(totalAvailableTorque))
            {
                if (totalTorque > 0)
                {
                    totalAvailableTorque = Math.Min(totalTorque, totalAvailableTorque + step * timeFactor);
                }
                else
                {
                    totalAvailableTorque = Math.Max(Math.Min(totalTorque, -0.00000001f), totalAvailableTorque - step * timeFactor);
                }
            }
            else totalAvailableTorque *= (float)Math.Pow(0.9f, timeFactor);  // allows for adjustments to happen if torque reduces or changes sign

            TurnDir = speed >= 0 ? EnumRotDirection.Clockwise : EnumRotDirection.Counterclockwise;
        }

        float getEffectiveGearedRatio(IMechanicalPowerNode node)
        {
            return node is BEBehaviorMPBase behavior
                ? behavior.EffectiveGearedRatio
                : node.GearedRatio * ((node as ILinkageProducer)?.LinkageAngleTransform?.GetSpeedFactor(angle) ?? 1);
        }

        float coast(float dt)
        {
            if (speed == 0) return 0;
            if (coastingInverseInertia == 0) return speed * dt;

            // Integrate speed and travel together, ending the travel when the shaft stops.
            double absSpeed = Math.Abs(speed);
            double nextSpeed = absSpeed;
            double resistance = coastingResistance;
            double airResistance = coastingAirResistance;
            double deceleration = coastingInverseInertia / (double)torqueUpdateInterval;
            double elapsed = deceleration * dt;
            double travel;
            if (resistance > 0 && airResistance > 0)
            {
                double ratio = Math.Sqrt(resistance / airResistance);
                double phase = Math.Atan(absSpeed / ratio) - Math.Sqrt(resistance * airResistance) * elapsed;
                nextSpeed = phase > 0 ? ratio * Math.Tan(phase) : 0;
                travel = Math.Log((resistance + airResistance * absSpeed * absSpeed)
                    / (resistance + airResistance * nextSpeed * nextSpeed)) / (2 * airResistance * deceleration);
            }
            else if (resistance > 0)
            {
                nextSpeed = Math.Max(0, absSpeed - resistance * elapsed);
                double turningTime = Math.Min(dt, absSpeed / (resistance * deceleration));
                travel = (absSpeed + nextSpeed) * turningTime / 2;
            }
            else if (airResistance > 0)
            {
                double decay = 1 + airResistance * absSpeed * elapsed;
                nextSpeed = absSpeed / decay;
                travel = Math.Log(decay) / (airResistance * deceleration);
            }
            else travel = absSpeed * dt;

            int direction = Math.Sign(speed);
            speed = (float)nextSpeed * direction;
            if (speed != 0) TurnDir = direction > 0 ? EnumRotDirection.Clockwise : EnumRotDirection.Counterclockwise;
            return (float)travel * direction;
        }

        public void UpdateFromPacket(MechNetworkPacket packet, bool isNew)
        {
            totalAvailableTorque = packet.totalAvailableTorque;
            networkResistance = packet.networkResistance;
            networkTorque = packet.networkTorque;
            HasPowerProducer = packet.hasPowerProducer;
            setClientLoads(packet.partLoads);

            speed = Math.Abs(packet.speed); // ClientTick() expects speed to be positive always
            if (isNew || !hasReceivedPacket)
            {
                angle = packet.totalAngle ?? packet.angle;
                clientSpeed = speed;
            }

            serverSideAngle = packet.totalAngle ?? packet.angle;
            hasReceivedPacket = true;

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
            angle = tree.GetDecimal("angle");
            
            TurnDir = (EnumRotDirection)tree.GetInt("rot");
        }

        public void WriteToTreeAttribute(ITreeAttribute tree)
        {
            tree.SetLong("networkId", networkId);
            tree.SetFloat("totalAvailableTorque", totalAvailableTorque);
            tree.SetFloat("totalResistance", networkResistance);
            tree.SetFloat("speed", speed);
            tree.SetDouble("angle", angle);

            tree.SetInt("rot", (int)TurnDir);
        }

        public void SendBlocksUpdateToClient(IServerPlayer player)
        {
            broadcastData();
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


    [ProtoContract]
    public class MechNetworkPacket
    {
        [ProtoMember(4)]
        public long networkId;
        [ProtoMember(8)]
        public float totalAvailableTorque;
        [ProtoMember(5)]
        public float networkResistance;
        [ProtoMember(6)]
        public float networkTorque;
        [ProtoMember(7)]
        public float speed;
        [ProtoMember(2)]
        public int direction;
        [ProtoMember(1)]
        public float angle;
        [ProtoMember(3)]
        public bool hasPowerProducer;
        // Older senders only provide angle.
        [ProtoMember(9)]
        public double? totalAngle;
        [ProtoMember(10)]
        public MechanicalPartLoad[] partLoads;
    }

    [ProtoContract]
    public class MechanicalPartLoad
    {
        [ProtoMember(1)]
        public BlockPos Position;
        [ProtoMember(2)]
        public float Load;
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
