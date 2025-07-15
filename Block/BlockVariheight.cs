using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockVariheight : Block
    {
        int height;

        public override void OnLoaded(ICoreAPI api)
        {
            int.TryParse(Code.EndVariant(), out height);

            base.OnLoaded(api);
        }

        public override bool ShouldMergeFace(int tileSide, Block nBlock, int intraChunkIndex3d)
        {
            if (tileSide == TileSideEnum.Up) return false;
            if (nBlock.SideOpaque[TileSideEnum.GetOpposite(tileSide)]) return true;
            if (tileSide == TileSideEnum.Down) return false;
            if (nBlock is BlockVariheight bvh) return bvh.height >= this.height;
            return false;
        }
    }
}
