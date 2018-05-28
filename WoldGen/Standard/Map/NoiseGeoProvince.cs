using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    class NoiseGeoProvince : NoiseBase
    {
        // (Be aware, static vars never get unloaded even when singleplayer server has been shut down)
        public static GeologicProvinces provinces;

        private int weightSum;


        public NoiseGeoProvince(long seed, ICoreServerAPI api) : base(seed)
        {
            IAsset asset = api.Assets.Get("worldgen/terrain/standard/geologicprovinces.json");
            provinces = asset.ToObject<GeologicProvinces>();


            for (int i = 0; i < provinces.Variants.Length; i++)
            {
                provinces.Variants[i].Index = i;
                provinces.Variants[i].ColorInt = int.Parse(provinces.Variants[i].Hexcolor.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);

                weightSum += provinces.Variants[i].Weight;
            }
            
        }

        public int GetProvinceIndexAt(int xpos, int zpos)
        {
            InitPositionSeed(xpos, zpos);

            int rand = NextInt(weightSum);

            int i = 0;
            for (; i < provinces.Variants.Length; i++)
            {
                rand -= provinces.Variants[i].Weight;
                if (rand <= 0) return provinces.Variants[i].Index;
            }

            return provinces.Variants[i].Index;
        }
      


    }
}
