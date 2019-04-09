using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockEntityAngledGears : BEMPBase
    {

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            string orientations = Block.LastCodePart();

            switch (orientations)
            {
                case "w":
                case "e":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 1, 2 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                case "u":
                case "d":
                    AxisMapping = new int[6] { 1, 2, 0, 0, 2, 1 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                case "es":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 1, 2 };
                    AxisSign = new int[6] { -1, 1, 1, 1, 1, 1 };
                    break;

                case "nw":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 1, 2 };
                    AxisSign = new int[6] { -1, 1, 1, 1, 1, 1 };
                    break;

                case "sd":
                    AxisMapping = new int[6] { 0, 1, 2, 1, 2, 0 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                case "ed":
                    AxisMapping = new int[6] { 0, 2, 1, 2, 1, 0 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                case "wd":
                    AxisMapping = new int[6] { 2, 1, 0, 0, 2, 1 };
                    AxisSign = new int[6] { 1, 1, 1, -1, -1, -1 };
                    break;

                case "nd":
                    AxisMapping = new int[6] { 0, 1, 2, 0, 2, 1 };
                    AxisSign = new int[6] { 1, 1, 1, -1, -1, -1 };
                    break;

                case "nu":
                    AxisMapping = new int[6] { 0, 2, 1, 0, 1, 2 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;

                case "eu":
                    AxisMapping = new int[6] { 0, 2, 1, 2, 1, 0 };
                    AxisSign = new int[6] { 1, 1, 1, -1, -1, -1 };
                    break;

                case "su":
                    AxisMapping = new int[6] { 1, 2, 0, 0, 1, 2 };
                    AxisSign = new int[6] { 1, 1, 1, -1, -1, -1 };
                    break;

                case "wu":
                    AxisMapping = new int[6] { 1, 2, 0, 2, 1, 0 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;


                default:
                    AxisMapping = new int[6] { 0, 1, 2, 2, 1, 0 };
                    AxisSign = new int[6] { 1, 1, 1, 1, 1, 1 };
                    break;
            }

            
        }

        public override void OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer)
        {
            if (world.Side == EnumAppSide.Client) MarkDirty(true);
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {


            return base.OnTesselation(mesher, tesselator);
        }
    }
}
