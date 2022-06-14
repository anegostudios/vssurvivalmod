using Vintagestory.API.Client;

namespace Vintagestory.GameContent
{

    public class BlockEntityClutter : BlockEntityShapeFromAttributes
    {


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return base.OnTesselation(mesher, tessThreadTesselator);
        }

    }
}
