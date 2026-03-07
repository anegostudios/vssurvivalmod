using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public class BEBehaviorMPSpurGear : BEBehaviorMPBase
    {
        public BlockFacing Facing => BlockFacing.FromFirstLetter(Block.Variant["orientation"]);

        float angleOffset;
        public override float AngleRad => base.AngleRad + angleOffset;

        public BEBehaviorMPSpurGear(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            // Makes it correct in most cases. Whoever reads this - feel free to make it perfect
            angleOffset = 11.25f * GameMath.DEG2RAD * (Pos.X % 32 + Pos.Y % 32 + Pos.Z % 32);

            AxisSign = new int[3] { 0, 0, 0 };
            switch (Facing.Index)
            {
                case 0: // N
                    AxisSign[2] = -1;
                    break;
                case 1: // E
                    AxisSign[0] = -1;
                    break;
                case 2: // S
                    AxisSign[2] = -1;
                    break;
                case 3: // W
                    AxisSign[0] = -1;
                    break;
                case 4: // U
                    AxisSign[1] = 1;
                    break;
                case 5: // D
                    AxisSign[1] = 1;
                    break;
            }
        }

        public override MechPowerPath[] GetMechPowerExits(MechPowerPath entryDir)
        {
            BlockFacing left, right, above, below;

            // Get the directions of potential neighbour connectable spur gears from this block's Facing
            if (Facing.IsHorizontal)
            {
                left = entryDir.OutFacing.Opposite == Facing ? entryDir.OutFacing.GetCW() : Facing.GetCW();
                right = entryDir.OutFacing.Opposite == Facing ? entryDir.OutFacing.GetCCW() : Facing.GetCCW();
                above = BlockFacing.UP;
                below = BlockFacing.DOWN;
            }
            else
            {
                left = BlockFacing.WEST;
                right = BlockFacing.EAST;
                above = BlockFacing.NORTH;
                below = BlockFacing.SOUTH;
            }

            // Test whether we have any valid connectable spur gear neighbours (must be same orientation as this one, to connect) - if so, these are potential output paths
            BlockPos tmpPos = Pos.Copy();
            bool doLeft = Api.World.BlockAccessor.GetBlock(tmpPos.Add(left)) == Block;
            bool doRight = Api.World.BlockAccessor.GetBlock(tmpPos.Set(Pos).Add(right)) == Block;
            bool doAbove = Api.World.BlockAccessor.GetBlock(tmpPos.Set(Pos).Add(above)) == Block;
            bool doBelow = Api.World.BlockAccessor.GetBlock(tmpPos.Set(Pos).Add(below)) == Block;

            // Convenient way to quickly test all 4 conditions in a single == test below
            SmallBoolArray bools = new SmallBoolArray();
            bools[0] = doLeft;
            bools[1] = doRight;
            bools[2] = doAbove;
            bools[3] = doBelow;

            // The axial path - this is the axis of this spur gear
            MechPowerPath axial = entryDir.OutFacing.Opposite == Facing ? entryDir : entryDir.PropagatedClone(Facing, entryDir.invert, propagationDir);

            // Outputs for zero spur gear neighbours
            if (bools == 0) return [axial];

            // Outputs for exactly one spur gear neighbour
            MechPowerPath side = null;
            if (bools == 1) side = entryDir.PropagatedClone(left, !entryDir.invert, propagationDir.Opposite);
            if (bools == 2) side = entryDir.PropagatedClone(right, !entryDir.invert, propagationDir.Opposite);
            if (bools == 4) side = entryDir.PropagatedClone(above, !entryDir.invert, propagationDir.Opposite);
            if (bools == 8) side = entryDir.PropagatedClone(below, !entryDir.invert, propagationDir.Opposite);
            if (side != null)
            {
                return [axial, side];
            }

            // Outputs for more than one spur gear neighbour (uncommon, here we temporarily create a List<>)
            List<MechPowerPath> paths = [axial];
            if (doLeft) paths.Add(entryDir.PropagatedClone(left, !entryDir.invert, propagationDir.Opposite));
            if (doRight) paths.Add(entryDir.PropagatedClone(right, !entryDir.invert, propagationDir.Opposite));
            if (doAbove) paths.Add(entryDir.PropagatedClone(above, !entryDir.invert, propagationDir.Opposite));
            if (doBelow) paths.Add(entryDir.PropagatedClone(below, !entryDir.invert, propagationDir.Opposite));

            return paths.ToArray();
        }


        public override BlockFacing GetPropagatingTurnDir(BlockFacing toFacing)
        {
            return propagationDir.Opposite;
        }


        public override float GetResistance()
        {
            return 0.0005f;
        }
    }
}
