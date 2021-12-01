using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

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

        TextureAtlasPosition contentTextPos;
        CompositeTexture contentTexture;

        public ContainerTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (contentTextPos == null)
                {
                    int textureSubId;

                    textureSubId = ObjectCacheUtil.GetOrCreate<int>(capi, "contenttexture-" + contentTexture.ToString() + "-" + contentTexture.Alpha, () =>
                    {
                        TextureAtlasPosition texPos;
                        int id = 0;

                        BitmapRef bmp = capi.Assets.TryGet(contentTexture.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);
                        if (bmp != null)
                        {
                            if (contentTexture.Alpha != 255)
                            {
                                bmp.MulAlpha(contentTexture.Alpha);
                            }
                            capi.BlockTextureAtlas.InsertTexture(bmp, out id, out texPos);
                            bmp.Dispose();
                        }

                        return id;
                    });

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }



}
