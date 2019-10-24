using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPConsumer : BEBehaviorMPBase
    {
        float resistance = 0.1f;

        public API.Common.Action OnConnected;
        public API.Common.Action OnDisconnected;


        public BEBehaviorMPConsumer(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            Shape = properties["mechPartShape"].AsObject<CompositeShape>(null);
            Shape?.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            resistance = properties["resistance"].AsFloat(0.1f);
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


        public override EnumTurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return GetBaseTurnDirection();
        }

        public override float GetResistance()
        {
            return resistance;
        }

        public override float GetTorque()
        {
            return 0;
        }

        protected override MechPowerPath[] GetMechPowerPaths(BlockFacing fromFacing, EnumTurnDirection turnDir)
        {
            // This but' a dead end, baby!
            return new MechPowerPath[0];
        }
    }
}
