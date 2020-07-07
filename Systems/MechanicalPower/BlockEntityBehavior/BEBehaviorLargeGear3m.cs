using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPLargeGear3m : BEBehaviorMPBase
    {
        public float ratio = 5.5f;

        public BEBehaviorMPLargeGear3m(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            this.AxisSign = new int[3] { 0, 1, 0 };

            if (api.Side == EnumAppSide.Client)
            {
                Blockentity.RegisterGameTickListener(onEverySecond, 1000);
            }
        }

        private void onEverySecond(float dt)
        {
            float speed = network == null ? 0 : network.Speed;

            if (Api.World.Rand.NextDouble() < speed / 4f)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/woodcreak"), Position.X + 0.5, Position.Y + 0.5, Position.Z + 0.5, null, 0.85f + speed);
            }
        }

        protected override MechPowerPath[] GetMechPowerExits(TurnDirection fromExitTurnDir)
        {
            //Like an axle, power can pass through this from up to down or vice-versa
            return new MechPowerPath[] { new MechPowerPath(fromExitTurnDir.Facing, fromExitTurnDir.Rot) };
        }


        public override TurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return GetInTurnDirection();
        }

        public override float GetResistance()
        {
            float r = 0.004f;   //Large gear's own resistance
            BELargeGear3m belg = Blockentity as BELargeGear3m;
            if (belg != null && network != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    BlockPos smallgear = belg.gear[i];
                    if (smallgear != null)
                    {
                        BlockEntity be = this.Api.World?.BlockAccessor.GetBlockEntity(smallgear);
                        BEBehaviorMPAngledGears beg = be?.GetBehavior<BEBehaviorMPAngledGears>();
                        if (beg != null)
                        {
                            if (beg.Network == null)
                            {
                                beg.CreateNetworkFromHere();
                            }
                            if (beg.Network != null)
                            {
                                int dir = this.inTurnDir.Facing == BlockFacing.UP ? 1 : -1;
                                float drivenResistance = beg.Network.Drive(dir * network.NetworkTorque / ratio, network.Speed * ratio, 1f, dir);
                                if (drivenResistance > 0f) r += drivenResistance * ratio;   //Add scaled up resistance of connected network, if not driven by its own network torque
                                r += 0.002f;   //Small gear's own resistance
                            }
                        }
                    }
                }
            }
            return r;
        }

        public override float GetTorque()
        {
            return 0;
        }

        internal bool OnInteract(IPlayer byPlayer)
        {
            return true;
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return base.OnTesselation(mesher, tesselator);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode) sb.AppendLine(string.Format(Lang.Get("Rotation: {0} - {1} - {2}", inTurnDir.Rot, inTurnDir.Facing, network?.TurnDir.Facing)));
        }

        internal float GetSmallgearAngleRad()
        {
            return AngleRad * ratio;
        }
    }
}
