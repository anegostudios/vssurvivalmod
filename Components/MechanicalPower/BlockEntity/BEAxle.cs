using Vintagestory.API.Common;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockEntityAxle : BEMPBase
    {
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            string orientations = Block.LastCodePart();
            switch (orientations)
            {
                case "we":
                    AxisMapping = new int[] { 2, 1, 0 };
                    AxisSign = new int[] { -1, -1, -1 };
                    break;

                case "ud":
                    AxisMapping = new int[] { 1, 2, 0 };
                    break;
            }
        }
    }
}
