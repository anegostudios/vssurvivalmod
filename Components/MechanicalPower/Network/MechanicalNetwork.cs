using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    [ProtoContract]
    public class MechanicalNetwork
    {
        internal MechanicalPowerMod mechanicalPowerMod;

        [ProtoMember(1)]
        public long networkId;
        [ProtoMember(2)]
        protected float totalAvailableTorque;
        [ProtoMember(3)]
        protected float totalResistance;
        [ProtoMember(4)]
        protected float speed;
        [ProtoMember(5)]
        protected int direction;
        protected BlockFacing directionFromFacing; // From which facing is the direction?

        [ProtoMember(6)]
        private int directionFromFacingIndex
        {
            get { return directionFromFacing == null ? 0 : directionFromFacing.Index; }
            set { directionFromFacing = BlockFacing.ALLFACES[value]; }
        }

        [ProtoMember(7)]
        protected float serverSideAngle;
        [ProtoMember(8)]
        protected float angle = 0; // In radiant

        static Random rand = new Random();

        /// <summary>
        /// In radiant
        /// </summary>
        public float Angle
        {
            get { return angle; }
        }

        public MechanicalNetwork()
        {
            speed = 0.1f + (float)rand.NextDouble() / 3;
        }

        public MechanicalNetwork(MechanicalPowerMod mechanicalPowerMod, long networkId)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
            this.networkId = networkId;

            speed = 0.1f + (float)rand.NextDouble() / 3;
        }



        // Tick Events are called by the Network Managers
        public void ClientTick(long tickNumber)
        {
            if (speed < 0.001) return;

            UpdateAngle(speed);

            // Since the server may be running at different tick speeds,
            // we slowly sync angle updates from server to reduce 
            // rotation jerkiness on the client

            // Each tick, add 5% of server<->client angle difference

            float diff = GameMath.AngleRadDistance(angle, serverSideAngle);
            angle += GameMath.Clamp(diff, -0.002f * Math.Abs(diff), 0.002f * Math.Abs(diff));
        }


        public void ServerTick(long tickNumber)
        {
            UpdateAngle(speed);

            if (tickNumber % 5 == 0)
            {
                UpdateNetwork();
            }

            if (tickNumber % 40 == 0)
            {
                mechanicalPowerMod.broadcastNetwork(new MechNetworkPacket()
                {
                    angle = angle,
                    direction = direction,
                    /*firstNodeX = firstPowerNode.X,
                    firstNodeY = firstPowerNode.Y,
                    firstNodeZ = firstPowerNode.Z,*/
                    networkId = networkId,
                    speed = speed,
                    totalAvailableTorque = totalAvailableTorque,
                    totalResistance = totalResistance
                });
            }
        }


        public void UpdateAngle(float speed)
        {
            angle -= speed / 10f;
            angle = angle % GameMath.TWOPI;

            serverSideAngle -= speed / 10f;
            serverSideAngle = serverSideAngle % GameMath.TWOPI;
        }



        // Should run every 5 ticks or so
        public void UpdateNetwork()
        {
            
        }


        public void UpdateFromPacket(MechNetworkPacket packet, bool isNew)
        {
            totalAvailableTorque = packet.totalAvailableTorque;
            totalResistance = packet.totalResistance;
            speed = packet.speed;
            direction = packet.direction;
            if (isNew)
            {
                angle = packet.angle;
            }

            serverSideAngle = packet.angle;
        }

        public void ReadFromTreeAttribute(ITreeAttribute tree)
        {
            networkId = tree.GetLong("networkId");
            totalAvailableTorque = tree.GetFloat("totalAvailableTorque");
            totalResistance = tree.GetFloat("totalResistance");
            speed = tree.GetFloat("speed");
            direction = tree.GetInt("direction");
            angle = tree.GetFloat("angle");
        }

        public void WriteToTreeAttribute(ITreeAttribute tree)
        {
            tree.SetLong("networkId", networkId);
            tree.SetFloat("totalAvailableTorque", totalAvailableTorque);
            tree.SetFloat("totalResistance", totalResistance);
            tree.SetFloat("speed", speed);
            tree.SetInt("direction", direction);
            tree.SetFloat("angle", angle);
        }

    }
}
