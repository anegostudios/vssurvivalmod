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
            if (Facing.IsHorizontal)
            {
                if (entryDir.OutFacing.Opposite == Facing)
                {
                    return [
                        entryDir,
                        entryDir.PropagatedClone(entryDir.OutFacing.GetCW(), !entryDir.invert, propagationDir.Opposite),
                        entryDir.PropagatedClone(entryDir.OutFacing.GetCCW(), !entryDir.invert, propagationDir.Opposite),
                        entryDir.PropagatedClone(BlockFacing.UP, !entryDir.invert, propagationDir.Opposite),
                        entryDir.PropagatedClone(BlockFacing.DOWN, !entryDir.invert, propagationDir.Opposite)
                    ];
                }
                else
                {
                    return [
                        entryDir.PropagatedClone(Facing, entryDir.invert, propagationDir),
                        entryDir.PropagatedClone(Facing.GetCW(), !entryDir.invert, propagationDir.Opposite),
                        entryDir.PropagatedClone(Facing.GetCCW(), !entryDir.invert, propagationDir.Opposite),
                        entryDir.PropagatedClone(BlockFacing.UP, !entryDir.invert, propagationDir.Opposite),
                        entryDir.PropagatedClone(BlockFacing.DOWN, !entryDir.invert, propagationDir.Opposite)
                    ];
                }
            } else
            {
                return [
                    entryDir,
                    /*new MechPowerPath(BlockFacing.NORTH, entryDir.gearingRatio, null, !entryDir.invert),
                    new MechPowerPath(BlockFacing.EAST, entryDir.gearingRatio, null, !entryDir.invert),
                    new MechPowerPath(BlockFacing.SOUTH, entryDir.gearingRatio, null, !entryDir.invert),
                    new MechPowerPath(BlockFacing.WEST, entryDir.gearingRatio, null, !entryDir.invert)*/
                ];
            }            
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
