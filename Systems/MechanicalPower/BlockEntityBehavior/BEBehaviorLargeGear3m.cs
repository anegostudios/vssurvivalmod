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

        public override bool isInvertedNetworkFor(BlockPos pos)
        {
            return propagationDir == BlockFacing.DOWN;
        }

        private void onEverySecond(float dt)
        {
            float speed = network == null ? 0 : network.Speed;

            if (Api.World.Rand.NextDouble() < speed / 4f)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/woodcreak"), Position.X + 0.5, Position.Y + 0.5, Position.Z + 0.5, null, 0.85f + speed);
            }
        }

        public override void SetPropagationDirection(MechPowerPath path)
        {
            BlockFacing turnDir = path.NetworkDir();
            if (turnDir != BlockFacing.UP && turnDir != BlockFacing.DOWN)
            {
                turnDir = (Api.World.BlockAccessor.GetBlock(Position.DownCopy()) is IMechanicalPowerBlock) ^ path.invert ? BlockFacing.DOWN : BlockFacing.UP;
                this.GearedRatio = path.gearingRatio / ratio;
            }
            else
            {
                this.GearedRatio = path.gearingRatio;
            }
            if (this.propagationDir == turnDir.GetOpposite() && this.network != null)
            {
                if (!network.DirectionHasReversed) network.TurnDir = network.TurnDir == EnumRotDirection.Clockwise ? EnumRotDirection.Counterclockwise : EnumRotDirection.Clockwise;
                network.DirectionHasReversed = true;
            }
            this.propagationDir = turnDir;
        }

        protected override MechPowerPath[] GetMechPowerExits(MechPowerPath pathDir)
        {
            BlockFacing face = pathDir.OutFacing;
            BELargeGear3m beg = Blockentity as BELargeGear3m;
            int index = 0;
            if (face == BlockFacing.UP || face == BlockFacing.DOWN)
            {
                MechPowerPath[] paths = new MechPowerPath[2 + beg.CountGears(Api)];
                paths[index] = pathDir;
                paths[++index] = new MechPowerPath(pathDir.OutFacing.GetOpposite(), pathDir.gearingRatio, !pathDir.invert);
                for (int i = 0; i < 4; i++)
                {
                    face = BlockFacing.HORIZONTALS[i];
                    if (beg.HasGearAt(Api, Position.AddCopy(face))) paths[++index] = new MechPowerPath(face, pathDir.gearingRatio * ratio, face == BlockFacing.UP ? true : false);
                }
                return paths;
            }

            MechPowerPath[] pathss = new MechPowerPath[2 + beg.CountGears(Api)];
            pathss[0] = new MechPowerPath(BlockFacing.DOWN, pathDir.gearingRatio / ratio, pathDir.invert);
            pathss[1] = new MechPowerPath(BlockFacing.UP, pathDir.gearingRatio / ratio, !pathDir.invert);
            index = 1;
            for (int i = 0; i < 4; i++)
            {
                BlockFacing side = BlockFacing.HORIZONTALS[i];
                if (beg.HasGearAt(Api, Position.AddCopy(side))) pathss[++index] = new MechPowerPath(side, pathDir.gearingRatio, face.GetOpposite() == side ^ !pathDir.invert);  //horizontals match the gearing ratio of the input horizontal; invert unless its the input side
            }
            return pathss;
        }

        public override float GetResistance()
        {
            return 0.004f;   //Large gear's own resistance
        }

        internal bool OnInteract(IPlayer byPlayer)
        {
            return true;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);
            if (Api.World.EntityDebugMode)
            {
                //sb.AppendLine(string.Format(Lang.Get("Rotation: {0} - {1} - {2} - {3}", this.isRotationReversed(), propagationDir, network?.TurnDir, this.GearedRatio)));
            }
        }

        internal float GetSmallgearAngleRad()
        {
            return AngleRad * ratio;
        }
    }
}
