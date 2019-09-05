using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockEntityAxle : BEMPBase
    {
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            string orientations = Block.Variant["rotation"];
            switch (orientations)
            {
                case "ns":
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "we":
                    AxisMapping = new int[] { 2, 1, 0 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "ud":
                    AxisMapping = new int[] { 1, 2, 0 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;
            }
        }

        public override EnumTurnDirection GetTurnDirection(BlockFacing forFacing)
        {
            return EnumTurnDirection.Clockwise;
        }

        public override float GetResistance()
        {
            return 0.0005f;
        }

        public override float GetTorque()
        {
            return 0;
        }
    }
}
