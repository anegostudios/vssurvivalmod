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
        CompositeTexture grassTex;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client)
            {
                if (Textures == null || !Textures.TryGetValue("specialSecondTexture", out grassTex))
                {
                    grassTex = Textures?.First().Value;
                }
            }
        }

        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            string grasscover = LastCodePart();
            if (grasscover == "none") return base.GetColorWithoutTint(capi, pos);

            int? textureSubId = grassTex?.Baked.TextureSubId;
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

            int? textureSubId = grassTex?.Baked.TextureSubId;
            if (textureSubId == null)
            {
                return ColorUtil.WhiteArgb;
            }

            int grassColor = capi.BlockTextureAtlas.GetAverageColor((int)textureSubId);

            if (ClimateColorMapResolved != null)
            {
                grassColor = capi.World.ApplyColorMapOnRgba(ClimateColorMapResolved, SeasonColorMapResolved, grassColor, pos.X, pos.Y, pos.Z, false);
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
