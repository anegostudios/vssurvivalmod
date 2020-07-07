using System;
using System.Diagnostics;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPRotor : BEBehaviorMPBase
    {
        protected double capableSpeed;
        protected double lastMsAngle;

        protected BlockFacing ownFacing;
        EntityPartitioning partitionUtil;

        protected virtual AssetLocation Sound { get; }
        protected virtual float GetSoundVolume() => 0f;

        /// <summary>
        /// Affects how quickly this rotor slows down if driving power reduced
        /// </summary>
        protected virtual float Resistance { get; }
        /// <summary>
        ///Affects how quickly this rotor speeds up if driving power increased
        /// </summary>
        protected virtual double AccelerationFactor { get; }
        /// <summary>
        ///The ideal current speed of this network, with this amount of driving power, range 0-1
        /// </summary>
        protected virtual float TargetSpeed { get; }
        /// <summary>
        ///The torque generated can be reduced or increased (e.g. by fewer or more sails, for windmill rotor)
        /// </summary>
        protected virtual float TorqueFactor { get; }

        public override float AngleRad
        {
            get
            {
                if (Sound != null && network?.Speed > 0 && Api.World.ElapsedMilliseconds - lastMsAngle > 500 / network.Speed)
                {
                    Api.World.PlaySoundAt(Sound, Position.X + 0.5, Position.Y + 0.5, Position.Z + 0.5, null, false, 18, GetSoundVolume());
                    lastMsAngle = Api.World.ElapsedMilliseconds;
                }

                return base.AngleRad;
            }
        }

        public BEBehaviorMPRotor(BlockEntity blockentity) : base(blockentity)
        {
            Blockentity = blockentity;

            string orientation = blockentity.Block.Variant["side"];
            ownFacing = BlockFacing.FromCode(orientation);
            OutFacingForNetworkDiscovery = ownFacing.GetOpposite();

            inTurnDir.Rot = ownFacing == BlockFacing.WEST || ownFacing == BlockFacing.NORTH ? EnumRotDirection.Counterclockwise : EnumRotDirection.Clockwise;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            switch (ownFacing.Code)
            {
                case "north":
                case "south":
                    AxisSign = new int[] { 0, 0, -1 };
                    break;

                case "east":
                case "west":
                    AxisSign = new int[] { -1, 0, 0 };
                    break;
            }

            if (api.Side == EnumAppSide.Server)
            {
                partitionUtil = Api.ModLoader.GetModSystem<EntityPartitioning>();
            }
            if (Api.Side == EnumAppSide.Client)
            {
                updateShape();
            }
        }

        public override float GetResistance()
        {
            return capableSpeed - network.Speed < 0 ? Resistance : 0;
        }

        public override float GetTorque()
        {
            float targetSpeed = TargetSpeed;
            capableSpeed += (targetSpeed - capableSpeed) * AccelerationFactor;

            int dir = 1;// (2 * (int)GetTurnDirection(ownFacing).Rot - 1);

            return Math.Max(0, (float)capableSpeed - network.Speed) * dir * TorqueFactor;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing)
        {
            // Don't run this behavior for power producers. Its done in initialize instead
        }

        protected override MechPowerPath[] GetMechPowerExits(TurnDirection fromExitTurnDir)
        {
            return new MechPowerPath[0];
        }

        protected virtual void updateShape()
        {
        }
    }
}