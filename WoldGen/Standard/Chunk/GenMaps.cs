using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class GenMaps : ModSystem
    {
        ICoreServerAPI api;

        MapLayerBase climateGen;
        MapLayerBase flowerGen;
        MapLayerBase bushGen;
        MapLayerBase forestGen;
        MapLayerBase geologicprovinceGen;
        MapLayerBase landformsGen;
        //MapLayerBase depositDistortionGen;

        int noiseSizeClimate;
        int noiseSizeForest;
        int noiseSizeShrubs;
        int noiseSizeGeoProv;
        int noiseSizeLandform;
        //int noiseSizeDepositDistortion;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            api.Event.MapRegionGeneration(OnMapRegionGen, EnumPlayStyleFlag.All);
            api.Event.SaveGameLoaded(GameWorldLoaded);

            // Call our loaded method manually if the server is already running (happens when mods are reloaded at runtime)
            if (api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                GameWorldLoaded();
            }
        }

        public void GameWorldLoaded()
        {
            long seed = api.WorldManager.Seed;
            noiseSizeClimate = api.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            noiseSizeForest = api.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            noiseSizeShrubs = api.WorldManager.RegionSize / TerraGenConfig.shrubMapScale;
            noiseSizeGeoProv = api.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            noiseSizeLandform = api.WorldManager.RegionSize / TerraGenConfig.landformMapScale;
            //noiseSizeDepositDistortion = api.WorldManager.RegionSize / TerraGenConfig.depositDistortionscale;

            NoiseClimate noiseClimate = new NoiseClimate(seed);
            
            climateGen = GetClimateMap(seed + 1, noiseClimate);
            forestGen = GetForestMap(seed + 2, TerraGenConfig.forestMapScale);
            bushGen = GetForestMap(seed + 109, TerraGenConfig.shrubMapScale);
            flowerGen = GetForestMap(seed + 223, TerraGenConfig.forestMapScale);
            //depositDistortionGen = GetDepositDistortionMap(seed + 123123);

            geologicprovinceGen = GetGeologicProvinceMap(seed + 3, api);
            landformsGen = GetLandformMap(seed + 4, noiseClimate, api);
        }

        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ)
        {

            /*mapRegion.DepositDistortionMap.Size = noiseSizeDepositDistortion + 1;
            mapRegion.DepositDistortionMap.BottomRightPadding = 1;
            mapRegion.DepositDistortionMap.Data = depositDistortionGen.GenLayer(regionX * noiseSizeDepositDistortion, regionZ * noiseSizeDepositDistortion, noiseSizeDepositDistortion + 1, noiseSizeDepositDistortion + 1);*/


            int pad = TerraGenConfig.geoProvMapPadding;
            mapRegion.GeologicProvinceMap.Data = geologicprovinceGen.GenLayer(
                regionX * noiseSizeGeoProv - pad,
                regionZ * noiseSizeGeoProv - pad,
                noiseSizeGeoProv + 2 * pad,
                noiseSizeGeoProv + 2 * pad
            );
            mapRegion.GeologicProvinceMap.Size = noiseSizeGeoProv + 2 * pad;
            mapRegion.GeologicProvinceMap.TopLeftPadding = mapRegion.GeologicProvinceMap.BottomRightPadding = pad;

            mapRegion.ClimateMap.Size = noiseSizeClimate + 1;
            mapRegion.ClimateMap.BottomRightPadding = 1;
            mapRegion.ClimateMap.Data = climateGen.GenLayer(regionX * noiseSizeClimate, regionZ * noiseSizeClimate, noiseSizeClimate+1, noiseSizeClimate+1);


            mapRegion.ForestMap.Size = noiseSizeForest + 1;
            mapRegion.ForestMap.BottomRightPadding = 1;
            forestGen.SetInputMap(mapRegion.ClimateMap, mapRegion.ForestMap);
            mapRegion.ForestMap.Data = forestGen.GenLayer(regionX * noiseSizeForest, regionZ * noiseSizeForest, noiseSizeForest+1, noiseSizeForest+1);



            mapRegion.ShrubMap.Size = noiseSizeShrubs + 1;
            mapRegion.ShrubMap.BottomRightPadding = 1;
            bushGen.SetInputMap(mapRegion.ClimateMap, mapRegion.ShrubMap);
            mapRegion.ShrubMap.Data = bushGen.GenLayer(regionX * noiseSizeShrubs, regionZ * noiseSizeShrubs, noiseSizeShrubs + 1, noiseSizeShrubs + 1);


            mapRegion.FlowerMap.Size = noiseSizeForest + 1;
            mapRegion.FlowerMap.BottomRightPadding = 1;
            flowerGen.SetInputMap(mapRegion.ClimateMap, mapRegion.FlowerMap);
            mapRegion.FlowerMap.Data = flowerGen.GenLayer(regionX * noiseSizeForest, regionZ * noiseSizeForest, noiseSizeForest + 1, noiseSizeForest + 1);



            pad = TerraGenConfig.landformMapPadding;
            mapRegion.LandformMap.Data = landformsGen.GenLayer(regionX * noiseSizeLandform - pad, regionZ * noiseSizeLandform - pad, noiseSizeLandform + 2*pad, noiseSizeLandform + 2*pad);
            mapRegion.LandformMap.Size = noiseSizeLandform + 2 * pad;
            mapRegion.LandformMap.TopLeftPadding = mapRegion.LandformMap.BottomRightPadding = pad;

            //Console.WriteLine("map region {0} {1} generated", regionX, regionZ);

            mapRegion.DirtyForSaving = true;
        }

        public static MapLayerBase GetLightningArcMap(long seed)
        {
            MapLayerBase wind = new MapLayerLines(seed + 1);
            wind.DebugDrawBitmap(1, 50, 50, "Wind 1 - Lines");

            wind = new MapLayerBlur(0, wind, 3);
            wind.DebugDrawBitmap(1, 50, 50, "Wind 2 - Blur");

            wind = new MapLayerPerlinWobble(seed + 2, wind, 4, 0.8f, 128, 40);
            wind.DebugDrawBitmap(1, 50, 50, "Wind 3 - Perlin Wobble");

            return wind;
        }

        public static MapLayerBase GetDebugWindMap(long seed)
        {
            MapLayerBase wind = new MapLayerDebugWind(seed + 1);
            wind.DebugDrawBitmap(0, 0, 0, "Wind 1 - Wind");

            return wind;
        }

        public static MapLayerBase GetClimateMap(long seed, NoiseClimate climateNoise)
        {
            MapLayerBase climate = new MapLayerClimate(seed + 1, climateNoise);
            climate.DebugDrawBitmap(0, 0, 0, "Climate 1 - Noise");

            climate = new MapLayerPerlinWobble(seed + 2, climate, 6, 0.7f, TerraGenConfig.climateMapWobbleScale, TerraGenConfig.climateMapWobbleScale * 0.15f);
            climate.DebugDrawBitmap(0, 0, 0, "Climate 2 - Perlin Wobble");

            return climate;
        }

        public static MapLayerBase GetOreMap(long seed, NoiseOre oreNoise)
        {
            MapLayerBase ore = new MapLayerOre(seed + 1, oreNoise);
            ore.DebugDrawBitmap(0, 0, 0, "Ore 1 - Noise");

            ore = new MapLayerPerlinWobble(seed + 2, ore, 6, 0.7f, TerraGenConfig.oreMapWobbleScale, TerraGenConfig.oreMapWobbleScale * 0.15f);
            ore.DebugDrawBitmap(0, 0, 0, "Ore 1 - Perlin Wobble");

            return ore;
        }

        public static MapLayerBase GetForestMap(long seed, int scale)
        {
            MapLayerBase forest = new MapLayerWobbledForest(seed + 1, 3, 0.9f, scale, 600, -100);
            //forest.DebugDrawBitmap(1, 0, 0, "Forest 1 - PerlinWobbleClimate"); - Requires climate  map

            return forest;
        }

        /*public static MapLayerBase GetDepositDistortionMap(long seed)
        {
            MapLayerBase dist = new MapLayerPerlin(seed + 12312, 3, 0.9f, 40, 12);
            return dist;
        }*/


        public static MapLayerBase GetGeologicProvinceMap(long seed, ICoreServerAPI api)
        {
            MapLayerBase provinces = new MapLayerGeoProvince(seed + 5, api);
            provinces.DebugDrawBitmap(3, 0, 0, "Geologic Province 1 - WobbleProvinces");

            return provinces;
        }


        public static MapLayerBase GetLandformMap(long seed, NoiseClimate climateNoise, ICoreServerAPI api)
        {
            MapLayerBase landforms = new MapLayerLandforms(seed + 12, climateNoise, api);
            landforms.DebugDrawBitmap(2, 0, 0, "Landforms 1 - Wobble Landforms");

            return landforms;
        }

    }
    
}
