using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    [ProtoContract]
    public class MechanicalNetwork
    {
        List<IMechanicalPowerNode> nodes = new List<IMechanicalPowerNode>();

        internal MechanicalPowerMod mechanicalPowerMod;

        [ProtoMember(1)]
        public long networkId;
        [ProtoMember(2)]
        protected float totalAvailableTorque;
        [ProtoMember(3)]
        protected float totalResistance;
        [ProtoMember(4)]
        protected float speed;
        [ProtoMember(7)]
        protected float serverSideAngle;
        [ProtoMember(8)]
        protected float angle = 0; // In radiant

        static Random rand = new Random();

        float clientSpeed;

        /// <summary>
        /// In radiant
        /// </summary>
        public float Angle
        {
            get { return angle; }
        }

        public float Speed
        {
            get { return speed; }
        }

        public float TotalAvailableTorque
        {
            get { return totalAvailableTorque; }
        }

        public EnumTurnDirection TurnDirection
        {
            get
            {
                return totalAvailableTorque >= 0 ? EnumTurnDirection.Clockwise : EnumTurnDirection.Counterclockwise;
            }
        }


        public MechanicalNetwork()
        {
            
        }

        public MechanicalNetwork(MechanicalPowerMod mechanicalPowerMod, long networkId)
        {
            this.mechanicalPowerMod = mechanicalPowerMod;
            this.networkId = networkId;
        }


        public void Join(IMechanicalPowerNode node)
        {
            nodes.Add(node);
        }

        public void Leave(IMechanicalPowerNode node)
        {
            nodes.Remove(node);
        }


        // Tick Events are called by the Network Managers
        public void ClientTick(long tickNumber)
        {
            if (speed < 0.001) return;

            clientSpeed += GameMath.Clamp(speed - clientSpeed, -0.01f, 0.01f);

            UpdateAngle(clientSpeed);

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
                updateNetwork();
            }

            if (tickNumber % 40 == 0)
            {
                mechanicalPowerMod.broadcastNetwork(new MechNetworkPacket()
                {
                    angle = angle,
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
        public void updateNetwork()
        {
            /* 2. Determine total available torque and total resistance of the network */

            float nowTorque = 0;
            totalResistance = 0;
            
            foreach (IMechanicalPowerNode powerNode in nodes)
            {
                nowTorque += powerNode.GetTorque();
                totalResistance += powerNode.GetResistance();
            }
            

            /* 3. Unconsumed torque changes the network speed */

            // Positive free torque => increase speed until maxSpeed
            // Negative free torque => decrease speed until -maxSpeed
            // No free torque => lower speed until 0

            float unusedTorque = Math.Abs(nowTorque) - totalResistance;


            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiJtYXgoMSx4XjAuMjUpIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiMCIsIjEwMCIsIjAiLCI1Il19XQ--
            double drag = Math.Max(1, Math.Pow(nodes.Count, 0.25));
            float step = 0.5f / (float)drag;

            // Definition: There is no negative speed, but there can be negative torque
            if (unusedTorque > 0)
            {
                speed += Math.Min(0.05f, step * unusedTorque);
                totalAvailableTorque = Math.Min(nowTorque, totalAvailableTorque + step);

            } else
            {
                speed = Math.Max(0, speed + step * unusedTorque);
                totalAvailableTorque = Math.Max(0, totalAvailableTorque - step);
            }

            
        }


        public void UpdateFromPacket(MechNetworkPacket packet, bool isNew)
        {
            totalAvailableTorque = packet.totalAvailableTorque;
            totalResistance = packet.totalResistance;
            speed = packet.speed;
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
            angle = tree.GetFloat("angle");
        }

        public void WriteToTreeAttribute(ITreeAttribute tree)
        {
            tree.SetLong("networkId", networkId);
            tree.SetFloat("totalAvailableTorque", totalAvailableTorque);
            tree.SetFloat("totalResistance", totalResistance);
            tree.SetFloat("speed", speed);
            tree.SetFloat("angle", angle);
        }

    }


    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class MechNetworkPacket
    {
        public long networkId;
        public float totalAvailableTorque;
        public float totalResistance;
        public float speed;
        public int direction;
        public float angle;
    }
}
