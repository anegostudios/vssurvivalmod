using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
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

        int noiseSizeClimate;
        int noiseSizeForest;
        int noiseSizeShrubs;
        int noiseSizeGeoProv;
        int noiseSizeLandform;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            api.Event.InitWorldGenerator(initWorldGen, "standard");
            api.Event.InitWorldGenerator(initWorldGen, "superflat");

            api.Event.MapRegionGeneration(OnMapRegionGen, "standard");
            api.Event.MapRegionGeneration(OnMapRegionGen, "superflat");
        }

        public void initWorldGen()
        {
            long seed = api.WorldManager.Seed;
            noiseSizeClimate = api.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            noiseSizeForest = api.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            noiseSizeShrubs = api.WorldManager.RegionSize / TerraGenConfig.shrubMapScale;
            noiseSizeGeoProv = api.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            noiseSizeLandform = api.WorldManager.RegionSize / TerraGenConfig.landformMapScale;

            ITreeAttribute tree = api.WorldManager.SaveGame.WorldConfiguration;
            string climate = tree.GetString("worldClimate");
            NoiseClimate noiseClimate;
            switch (climate)
            {
                case "realistic":
                    noiseClimate = new NoiseClimateRealistic(seed, (double)api.WorldManager.MapSizeZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale);
                    break;

                default:
                    noiseClimate = new NoiseClimatePatchy(seed);
                    break;
            }


            climateGen = GetClimateMapGen(seed + 1, noiseClimate);
            forestGen = GetForestMapGen(seed + 2, TerraGenConfig.forestMapScale);
            bushGen = GetForestMapGen(seed + 109, TerraGenConfig.shrubMapScale);
            flowerGen = GetForestMapGen(seed + 223, TerraGenConfig.forestMapScale);


            geologicprovinceGen = GetGeologicProvinceMapGen(seed + 3, api);
            landformsGen = GetLandformMapGen(seed + 4, noiseClimate, api);
        }

        

        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ)
        {
            int pad = TerraGenConfig.geoProvMapPadding;
            mapRegion.GeologicProvinceMap.Data = geologicprovinceGen.GenLayer(
                regionX * noiseSizeGeoProv - pad,
                regionZ * noiseSizeGeoProv - pad,
                noiseSizeGeoProv + 2 * pad,
                noiseSizeGeoProv + 2 * pad
            );
            mapRegion.GeologicProvinceMap.Size = noiseSizeGeoProv + 2 * pad;
            mapRegion.GeologicProvinceMap.TopLeftPadding = mapRegion.GeologicProvinceMap.BottomRightPadding = pad;

            pad = 2;
            mapRegion.ClimateMap.Data = climateGen.GenLayer(
                regionX * noiseSizeClimate - pad, 
                regionZ * noiseSizeClimate - pad, 
                noiseSizeClimate + 2 * pad, 
                noiseSizeClimate + 2 * pad
            );
            mapRegion.ClimateMap.Size = noiseSizeClimate + 2 * pad;
            mapRegion.ClimateMap.TopLeftPadding = mapRegion.ClimateMap.BottomRightPadding = pad;


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
            

            mapRegion.DirtyForSaving = true;
        }

        public static MapLayerBase GetLightningArcMap(long seed)
        {
            MapLayerBase wind = new MapLayerLines(seed + 1);
            wind.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, 50, 50, "Wind 1 - Lines");

            wind = new MapLayerBlur(0, wind, 3);
            wind.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, 50, 50, "Wind 2 - Blur");

            wind = new MapLayerPerlinWobble(seed + 2, wind, 4, 0.8f, 128, 40);
            wind.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, 50, 50, "Wind 3 - Perlin Wobble");

            return wind;
        }

        public static MapLayerBase GetDebugWindMap(long seed)
        {
            MapLayerBase wind = new MapLayerDebugWind(seed + 1);
            wind.DebugDrawBitmap(0, 0, 0, "Wind 1 - Wind");

            return wind;
        }

        public static MapLayerBase GetClimateMapGen(long seed, NoiseClimate climateNoise)
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

        public static MapLayerBase GetDepositVerticalDistort(long seed)
        {
            double[] thresholds = new double[] { 0.1, 0.1, 0.1, 0.1 };
            MapLayerPerlin layer = new MapLayerPerlin(seed + 1, 4, 0.8f, 25 * TerraGenConfig.depositVerticalDistortScale, 40, thresholds);
            
            layer.DebugDrawBitmap(0, 0, 0, "Vertical Distort");

            return layer;
        }



        public static MapLayerBase GetForestMapGen(long seed, int scale)
        {
            MapLayerBase forest = new MapLayerWobbledForest(seed + 1, 3, 0.9f, scale, 600, -100);
            //forest.DebugDrawBitmap(1, 0, 0, "Forest 1 - PerlinWobbleClimate"); - Requires climate  map


            return forest;
        }


        /*public static MapLayerBase GetDepositDistortionMapGen(long seed)
        {
            MapLayerBase dist = new MapLayerPerlin(seed + 12312, 5, 0.9f, 80, 255);
            

            return dist;
        }*/


        public static MapLayerBase GetGeologicProvinceMapGen(long seed, ICoreServerAPI api)
        {
            MapLayerBase provinces = new MapLayerGeoProvince(seed + 5, api);
            provinces.DebugDrawBitmap(DebugDrawMode.ProvinceRGB, 0, 0, "Geologic Province 1 - WobbleProvinces");

            return provinces;
        }


        public static MapLayerBase GetLandformMapGen(long seed, NoiseClimate climateNoise, ICoreServerAPI api)
        {
            MapLayerBase landforms = new MapLayerLandforms(seed + 12, climateNoise, api);
            landforms.DebugDrawBitmap(DebugDrawMode.LandformRGB, 0, 0, "Landforms 1 - Wobble Landforms");

            return landforms;
        }

    }
    
}
