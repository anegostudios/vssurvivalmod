using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPRotor : BEBehaviorMPBase
    {
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

            float dir = propagationDir == OutFacingForNetworkDiscovery ? 1f : -1f;

            // Note: propagationDir is a fundamental concept in our mechanical power network, it means which orientation was/will be used for this block for network discovery; if this is not the first block in the network,
            // the network discovery may come "into" our block not "out of" our block even though we are a rotor. Also for some blocks - e.g. windmill rotor - the network could be connected on either end of the axle
            // Depending on the direction of propagation and connection, the network's "positive" rotation might be either clockwise OR anti-clockwise from the point of view of this individual rotor block
            // Therefore a "correct" anti-clockwise rotation for this rotor might be either a positive speed, or a negative speed, from the point of view of the connected network

            float absSpeed = Math.Abs(speed);
            var excessSpeed = absSpeed - capableSpeed;    // TargetSpeed and therefore capableSpeed are never negative

            bool wrongDirection = dir * speed < 0f;

            // As noted above, a "correct" anti-clockwise rotation for this rotor might be either positive or negative for the network as a whole.
            // If this rotor is contributing to turning the network, then `dir` and `speed` will be either both positive, or both negative.
            // It's also possible, but rare, for the network as a whole to be "forcing" this rotor to turn in the wrong direction, if there are other more powerful torque producers on the network turning in the reverse sense -
            // in that case wrongDirection will be true

            resistance = (float)(wrongDirection ? Resistance * TorqueFactor * Math.Min(0.8f, absSpeed * 400f) : excessSpeed > 0 ? Resistance * Math.Min(0.2f, excessSpeed * excessSpeed * 80f) : 0f);

            // If being forced to turn in the wrong direction, our resistance is very high
            // If turning in the correct direction but with excess speed, increasing resistance proportional to the excess
            // Otherwise, zero resistance

            var power = wrongDirection ? capableSpeed : capableSpeed - absSpeed;

            // Our torque increases proportionate to how much the network is not turning at the speed we are capable of providing: over time, the network should end up turning at capableSpeed, which in turn should be TargetSpeed
            // Torque modified by TorqueFactor, and we provide it to the network in the direction the network is expecting from us (dir, which depends on the network's own discovery direction)
            return (float)Math.Max(0f, power) * TorqueFactor * dir;
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
