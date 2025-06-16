using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockTopTextureSource : ITexPositionSource
    {
        ICoreClientAPI capi;
        Block block;

        public BlockTopTextureSource(ICoreClientAPI capi, Block block)
        {
            this.capi = capi;
            this.block = block;
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                TextureAtlasPosition pos = capi.BlockTextureAtlas.GetPosition(block, "up");
                return pos;
            }
        }
    }

    public class ContainerTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private ICoreClientAPI capi;
        CompositeTexture contentTexture;

        public ContainerTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;

            contentTexture.Bake(capi.Assets);
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                capi.BlockTextureAtlas.GetOrInsertTexture(contentTexture.Baked.BakedName, out _, out var contentTextPos);
                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }



}
