using System;
using System.Diagnostics;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
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
        ICoreClientAPI capi;


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
                if (Sound != null && network?.Speed > 0 && Api.World.ElapsedMilliseconds - lastMsAngle > 500 / network.Speed && Api.Side == EnumAppSide.Client)
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
            OutFacingForNetworkDiscovery = ownFacing.Opposite;
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            capi = api as ICoreClientAPI;

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
            float csFloat = (float)capableSpeed;

            float dir = this.propagationDir == OutFacingForNetworkDiscovery ? 1f : -1f;
            float absSpeed = Math.Abs(speed);
            float excessSpeed = absSpeed - csFloat;
            bool wrongDirection = dir * speed < 0f;

            resistance = wrongDirection ? Resistance * TorqueFactor * Math.Min(0.8f, absSpeed * 400f) : excessSpeed > 0 ? Resistance * Math.Min(0.2f, excessSpeed * excessSpeed * 80f) : 0f;
            float power = wrongDirection ? csFloat : csFloat - absSpeed;
            return Math.Max(0f, power) * TorqueFactor * dir;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing)
        {
            // Don't run this behavior for power producers. Its done in initialize instead
        }

        protected override MechPowerPath[] GetMechPowerExits(MechPowerPath fromExitTurnDir)
        {
            return new MechPowerPath[0];
        }
    }
}