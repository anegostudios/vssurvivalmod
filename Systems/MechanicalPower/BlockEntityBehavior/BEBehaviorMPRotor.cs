using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPRotor : BEBehaviorMPBase
    {
        const float wrongDirectionResistanceFactor = 400f;
        const float maxWrongDirectionResistance = 0.8f;
        const float overspeedResistanceFactor = 80f;
        const float maxOverspeedResistance = 0.2f;

        protected double capableSpeed;
        protected double lastMsAngle;
        protected BlockFacing ownFacing;


        protected virtual AssetLocation Sound { get; }
        protected virtual float GetSoundVolume() => 0f;

        /// <summary>
        /// Affects how quickly this rotor slows down if driving power reduced
        /// </summary>
        protected virtual float Resistance { get; }
        /// <summary>
        /// Affects how quickly this rotor speeds up if driving power increased
        /// </summary>
        protected virtual double AccelerationFactor { get; }
        /// <summary>
        /// The ideal current speed of this network, with this amount of driving power, range 0-1
        /// </summary>
        protected virtual float TargetSpeed { get; }
        /// <summary>
        /// The torque generated can be reduced or increased (e.g. by fewer or more sails, for windmill rotor)
        /// </summary>
        protected virtual float TorqueFactor { get; }
        /// <summary>
        /// The sign of the torque this rotor applies relative to the mechanical network.
        /// Network discovery can reach a rotor from either end, so its local turn direction may require positive or negative network torque.
        /// </summary>
        protected virtual float TorqueDirection => propagationDir == OutFacingForNetworkDiscovery ? 1f : -1f;

        public override float AngleRad
        {
            get
            {
                if (Sound != null && network?.Speed > 0 && Api.World.ElapsedMilliseconds - lastMsAngle > 500 / network.Speed && Api.Side == EnumAppSide.Client)
                {
                    Api.World.PlaySoundAt(Sound, Position, 0, null, false, 18, GetSoundVolume());
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
            OutFacingForNetworkDiscovery = ownFacing.Opposite;
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

            if (Api.Side == EnumAppSide.Client)
            {
                updateShape(api.World);
            }
        }

        public override float GetResistance()
        {
            return Resistance;
        }

        public override float GetTorque(long tick, float speed, out float resistance)
        {
            float targetSpeed = TargetSpeed;
            capableSpeed += (targetSpeed - capableSpeed) * AccelerationFactor;

            // Note: capableSpeed will tend towards the TargetSpeed, within a few ticks it should normally be close
            // The TargetSpeed is dictated by the network as a whole, summing up both torque producers and resistance of every block in the network, noting that both torque and resistance may be different at different speeds.
            // The calculation is complex and to some extent found by empirical testing of what looks good and plausible in-game, not science.

            float absSpeed = Math.Abs(speed);
            var excessSpeed = absSpeed - capableSpeed;    // TargetSpeed and therefore capableSpeed are never negative

            bool wrongDirection = TorqueDirection * speed < 0f;

            // A "correct" anti-clockwise rotation for this rotor might be either positive or negative for the network as a whole.
            // If this rotor is contributing to turning the network, then `TorqueDirection` and `speed` will be either both positive, or both negative.
            // It's also possible, but rare, for the network as a whole to be "forcing" this rotor to turn in the wrong direction, if there are other more powerful torque producers on the network turning in the reverse sense -
            // in that case wrongDirection will be true

            resistance = (float)(wrongDirection ? Resistance * TorqueFactor * Math.Min(maxWrongDirectionResistance, absSpeed * wrongDirectionResistanceFactor) : excessSpeed > 0 ? Resistance * Math.Min(maxOverspeedResistance, excessSpeed * excessSpeed * overspeedResistanceFactor) : 0f);

            // If being forced to turn in the wrong direction, our resistance is very high
            // If turning in the correct direction but with excess speed, increasing resistance proportional to the excess
            // Otherwise, zero resistance

            var power = wrongDirection ? capableSpeed : capableSpeed - absSpeed;

            // Our torque increases proportionate to how much the network is not turning at the speed we are capable of providing: over time, the network should end up turning at capableSpeed, which in turn should be TargetSpeed
            // Torque modified by TorqueFactor, and we provide it to the network in the direction the network is expecting from us (TorqueDirection, which depends on the network's own discovery direction)
            return (float)Math.Max(0f, power) * TorqueFactor * TorqueDirection;
        }

        public override float GetTorqueResponse(float speed, float nextSpeed)
        {
            float factor = TorqueFactor;
            float minSpeed = Math.Min(Math.Abs(speed), Math.Abs(nextSpeed));
            float maxSpeed = Math.Max(Math.Abs(speed), Math.Abs(nextSpeed));
            bool wrongDirection = TorqueDirection * (speed == 0 ? nextSpeed : speed) < 0f;
            if (wrongDirection)
            {
                if (minSpeed >= maxWrongDirectionResistance / wrongDirectionResistanceFactor) return 0f;

                // So only the part below the braking cap changes resistance as the shaft slows down.
                float resistanceResponse = wrongDirectionResistanceFactor;
                float speedChange = Math.Abs(speed) - Math.Abs(nextSpeed);
                if (speedChange > 0) resistanceResponse = Math.Min(resistanceResponse, (maxWrongDirectionResistance - minSpeed * wrongDirectionResistanceFactor) / speedChange);
                return Resistance * factor * resistanceResponse;
            }

            // And as the shaft speeds up, the rotor first loses driving torque, then starts adding drag.
            float response = minSpeed < capableSpeed ? factor : 0f;
            double maxExcess = Math.Sqrt(maxOverspeedResistance / overspeedResistanceFactor);
            if (maxSpeed > capableSpeed && minSpeed < capableSpeed + maxExcess)
            {
                float resistanceResponse = (float)(2 * Resistance * overspeedResistanceFactor * Math.Min(maxSpeed - capableSpeed, maxExcess));
                // So we should use the larger response because only one applies at a time.
                response = Math.Max(response, resistanceResponse);
            }
            return response;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing)
        {
            // Don't run this behavior for power producers. Its done in initialize instead
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath fromExitTurnDir)
        {
            return Array.Empty<MechPowerPath>();
        }
    }
}
