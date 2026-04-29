using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPConsumer : BEBehaviorMPBase
    {
        public float Resistance = 0.1f;

        public Action OnConnected;
        public Action OnDisconnected;

        public override float AngleRad
        {
            get { return base.AngleRad; }
        }

        public float TrueSpeed { get { return Math.Abs(Network?.Speed * GearedRatio ?? 0f); }}

        public BEBehaviorMPConsumer(BlockEntity blockentity) : base(blockentity) { }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            Shape = properties["mechPartShape"].AsObject<CompositeShape>(Block.Shape);
            Shape?.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Resistance = properties["resistance"].AsFloat(0.1f);

            var orientations = Block.Variant["side"];
            switch (orientations)
            {
                case "north":
                    AxisSign = new int[] { 0, 0, -1 };
                    break;
                case "east":
                    AxisSign = new int[] { -1, 0, 0 };
                    break;
                case "west":
                    AxisSign = new int[] { -1, 0, 0 };
                    break;
                case "south":
                    AxisSign = new int[] { 0, 0, -1 };
                    break;
            }

        }


        public override void JoinNetwork(MechanicalNetwork network)
        {
            base.JoinNetwork(network);
            OnConnected?.Invoke();
        }

        public override void LeaveNetwork()
        {
            base.LeaveNetwork();
            OnDisconnected?.Invoke();
        }

        public override float GetResistance()
        {
            return Resistance;
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath fromExitTurnDir)
        {
            return Array.Empty<MechPowerPath>();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (Block == null) return false;
            return true;
        }

    }
}
