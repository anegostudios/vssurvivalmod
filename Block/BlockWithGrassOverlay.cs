using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.GameContent
{
    public class BlockWithGrassOverlay : Block
    {
        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            string grasscover = LastCodePart();
            if (grasscover == "none") return base.GetColorWithoutTint(capi, pos);

            CompositeTexture tex;
            if (Textures == null || !Textures.TryGetValue("specialSecondTexture", out tex))
            {
                tex = Textures?.First().Value;
            }

            int? textureSubId = tex?.Baked.TextureSubId;
            if (textureSubId == null)
            {
                return ColorUtil.WhiteArgb;
            }

            int grassColor = capi.BlockTextureAtlas.GetRandomColor((int)textureSubId);
            
            if (grasscover == "normal")
            {
                return grassColor;

            }
            else
            {
                int soilColor = capi.BlockTextureAtlas.GetRandomColor((int)Textures["up"].Baked.TextureSubId);
                return ColorUtil.ColorOverlay(soilColor, grassColor, grasscover == "verysparse" ? 0.5f : 0.75f);
            }
        }



        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            string grasscover = LastCodePart();
            if (grasscover == "none") return base.GetColorWithoutTint(capi, pos);

            CompositeTexture tex;
            if (Textures == null || !Textures.TryGetValue("specialSecondTexture", out tex))
            {
                tex = Textures?.First().Value;
            }

            int? textureSubId = tex?.Baked.TextureSubId;
            if (textureSubId == null)
            {
                return ColorUtil.WhiteArgb;
            }

            int grassColor = capi.BlockTextureAtlas.GetAverageColor((int)textureSubId);

            if (TintIndex > 0)
            {
                grassColor = capi.ApplyColorTintOnRgba(TintIndex, grassColor, pos.X, pos.Y, pos.Z, false);
            }

            if (grasscover == "normal")
            {
                return grassColor;
                
            } else
            {
                int soilColor = capi.BlockTextureAtlas.GetAverageColor((int)Textures["up"].Baked.TextureSubId);

                return ColorUtil.ColorOverlay(soilColor, grassColor, grasscover == "verysparse" ? 0.5f : 0.75f);
            }
            
        }
    }
}
