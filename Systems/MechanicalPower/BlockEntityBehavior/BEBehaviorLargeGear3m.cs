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

        EnumAxis gearAxis => BlockLargeGear3m.GetGearAxis(Block);

        BlockFacing[] axleFacings => BlockLargeGear3m.GetAxleFacings(Block);

        BlockFacing[] ringFacings => BlockLargeGear3m.GetRingFacings(Block);

        // The disc sits opposite the axle stub where the rim meets its ring gears.
        bool rimMirrored
        {
            get
            {
                BlockFacing stub = Block == null ? BlockFacing.UP : BlockLargeGear3m.GetStubFacing(Block);
                return stub == BlockFacing.DOWN || stub == BlockFacing.SOUTH || stub == BlockFacing.EAST;
            }
        }

        public bool IsRimInverted()
        {
            return (GetPropagationDirection() == axleFacings[1]) ^ rimMirrored;
        }

        public BEBehaviorMPLargeGear3m(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            this.AxisSign = gearAxis switch
            {
                EnumAxis.Z => [0, 0, -1],
                EnumAxis.X => [-1, 0, 0],
                _ => [0, 1, 0]
            };
        }

        public override bool IsInvertedNetworkFor(BlockPos pos)
        {
            return propagationDir == axleFacings[1]; // [1] = DOWN / SOUTH / EAST
        }

        public override void SetPropagationDirection(MechPowerPath path)
        {
            BlockFacing turnDir = getPropagationDirection(path);
            var axles = axleFacings;
            float gearedRatio = path.gearingRatio;
            if (path.NetworkDir().IsAdjacent(axles[0]))
            {
                gearedRatio /= ratio;
            }

            base.SetPropagationDirection(new MechPowerPath(turnDir, gearedRatio, null, false, path.AngleTransform));
        }

        BlockFacing getPropagationDirection(MechPowerPath path)
        {
            BlockFacing turnDir = path.NetworkDir();
            var axles = axleFacings;
            if (!turnDir.IsAdjacent(axles[0])) return turnDir;
            return path.IsInvertedTowards(Position) ^ rimMirrored ? axles[0] : axles[1];
        }

        protected override bool isPropagationDirectionConsistent(MechPowerPath path)
        {
            return propagationDir == getPropagationDirection(path);
        }

        public override bool IsPropagationDirection(BlockPos fromPos, BlockFacing test)
        {
            if (propagationDir == test) return true;

            var axles = axleFacings;
            if (test.IsAdjacent(axles[0])) // Ring gear face.
            {
                BlockFacing facing = Position.FacingFrom(fromPos);
                if (facing == test) return propagationDir == (rimMirrored ? axles[0] : axles[1]);
                if (facing == test.Opposite) return propagationDir == (rimMirrored ? axles[1] : axles[0]);
            }

            return false;
        }

        public override float GetGearedRatio(BlockFacing face)
        {
            return face.IsAdjacent(axleFacings[0]) ? this.GearedRatio * ratio : this.GearedRatio;
        }


        public override MechPowerPath[] GetMechPowerExits(MechPowerPath pathDir)
        {
            BlockFacing face = pathDir.OutFacing;
            BELargeGear3m beg = Blockentity as BELargeGear3m;
            var axles = axleFacings;
            var ring = ringFacings;
            BlockPos ringPos = new BlockPos(Position.dimension);
            int index = 0;

            if (face.Axis == axles[0].Axis) // Axle entry or exit.
            {
                MechPowerPath[] paths = new MechPowerPath[2 + beg.CountGears(Api)];
                paths[index] = pathDir;
                paths[++index] = pathDir.PropagatedClone(pathDir.OutFacing.Opposite, !pathDir.invert);
                bool ringInvert = face == axles[1] ^ pathDir.invert ^ rimMirrored;
                foreach (BlockFacing ringFace in ring)
                {
                    if (beg.HasGearAt(Api, ringPos.Set(Position).Add(ringFace)))
                    {
                        // Invert all ring output paths.
                        paths[++index] = new MechPowerPath(ringFace, pathDir.gearingRatio * ratio, null, ringInvert, pathDir.AngleTransform);
                    }
                }
                return paths;
            }

            MechPowerPath[] pathss = new MechPowerPath[2 + beg.CountGears(Api)];
            bool invert = pathDir.IsInvertedTowards(Position);
            pathss[0] = new MechPowerPath(axles[1], pathDir.gearingRatio / ratio, null, invert ^ rimMirrored, pathDir.AngleTransform);
            pathss[1] = new MechPowerPath(axles[0], pathDir.gearingRatio / ratio, null, !invert ^ rimMirrored, pathDir.AngleTransform);
            index = 1;
            // Ring paths invert in the opposite sense when power enters from the ring instead of the axle.
            foreach (BlockFacing ringFace in ring)
            {
                if (beg.HasGearAt(Api, ringPos.Set(Position).Add(ringFace)))
                {
                    pathss[++index] = pathDir.PropagatedClone(ringFace, !invert);  // Ring outputs keep the input ratio.
                }
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
            float angle = rimMirrored ? GameMath.TWOPI - AngleRad : AngleRad;
            return angle * ratio;
        }
    }
}
