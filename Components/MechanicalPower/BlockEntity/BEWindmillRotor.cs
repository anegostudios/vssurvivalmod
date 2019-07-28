using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockEntityWindmillRotor : BEMPBase
    {
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            string orientation = Block.Variant["side"];
            switch (orientation)
            {
                case "north":
                    AxisMapping = new int[] { 0, 1, 2 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "east":
                    AxisMapping = new int[] { 2, 1, 0 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "south":
                    AxisMapping = new int[] { 0, 1, 2 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "west":
                    AxisMapping = new int[] { 2, 1, 0 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;
            }
        }


        public override float GetResistance()
        {
            return 0;
        }

        public override float GetTorque()
        {
            return Math.Max(0, 1 - network.Speed);
        }

        public override EnumTurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return EnumTurnDirection.Clockwise;
        }
        
    }
}
