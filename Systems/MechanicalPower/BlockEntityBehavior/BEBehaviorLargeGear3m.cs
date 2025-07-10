using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

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
                turnDir = path.IsInvertedTowards(Position) ? BlockFacing.UP : BlockFacing.DOWN;  //network coming in (clockwise) from any of the 4 sides should then propagate (clockwise) in the down direction
                this.GearedRatio = path.gearingRatio / ratio;
            }
            else
            {
                this.GearedRatio = path.gearingRatio;
            }
            if (this.propagationDir == turnDir.Opposite && this.network != null)
            {
                if (!network.DirectionHasReversed) network.TurnDir = network.TurnDir == EnumRotDirection.Clockwise ? EnumRotDirection.Counterclockwise : EnumRotDirection.Clockwise;
                network.DirectionHasReversed = true;
            }
            this.propagationDir = turnDir;
        }

        public override bool IsPropagationDirection(BlockPos fromPos, BlockFacing test)
        {
            if (propagationDir == test) return true;

            if (test.IsHorizontal)
            {
                // Directions coming in from the sides correspond to this having a propagation direction of DOWN
                if (fromPos.AddCopy(test) == this.Position) return this.propagationDir == BlockFacing.DOWN;
                if (fromPos.AddCopy(test.Opposite) == this.Position) return this.propagationDir == BlockFacing.UP;
            }

            return false;
        }

        public override float GetGearedRatio(BlockFacing face)
        {   
            return face.IsHorizontal ? this.GearedRatio * ratio : this.GearedRatio;
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
                paths[++index] = new MechPowerPath(pathDir.OutFacing.Opposite, pathDir.gearingRatio, null, !pathDir.invert);
                bool sideInvert = face == BlockFacing.DOWN ^ pathDir.invert;
                for (int i = 0; i < 4; i++)
                {
                    BlockFacing horizFace = BlockFacing.HORIZONTALS[i];
                    if (beg.HasGearAt(Api, Position.AddCopy(horizFace))) paths[++index] = new MechPowerPath(horizFace, pathDir.gearingRatio * ratio, null, sideInvert);  //invert all horizontal output paths
                }
                return paths;
            }

            MechPowerPath[] pathss = new MechPowerPath[2 + beg.CountGears(Api)];
            bool invert = pathDir.IsInvertedTowards(Position);
            pathss[0] = new MechPowerPath(BlockFacing.DOWN, pathDir.gearingRatio / ratio, null, invert);
            pathss[1] = new MechPowerPath(BlockFacing.UP, pathDir.gearingRatio / ratio, null, !invert);
            index = 1;
            bool sidesInvert = face == BlockFacing.DOWN ^ !invert;  //invert power in the opposite sense if power fed in from one of the sides instead of through up/down axle
            for (int i = 0; i < 4; i++)
            {
                BlockFacing horizFace = BlockFacing.HORIZONTALS[i];
                if (beg.HasGearAt(Api, Position.AddCopy(horizFace))) pathss[++index] = new MechPowerPath(horizFace, pathDir.gearingRatio, null, sidesInvert);  //horizontals match the gearing ratio of the input horizontal
            }
            return pathss;
        }

        public bool AngledGearNotAlreadyAdded(BlockPos position)
        {
            return ((BELargeGear3m)Blockentity).AngledGearNotAlreadyAdded(position);
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
