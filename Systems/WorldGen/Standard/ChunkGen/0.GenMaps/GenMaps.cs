using ProtoBuf;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class LatitudeData
    {
        public double ZOffset = 0;
        public bool isRealisticClimate = false;
        public int polarEquatorDistance;
    }

    public class GenMaps : ModSystem
    {
        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        public MapLayerBase climateGen;
        public MapLayerBase flowerGen;
        public MapLayerBase bushGen;
        public MapLayerBase forestGen;
        public MapLayerBase beachGen;
        public MapLayerBase geologicprovinceGen;
        public MapLayerBase landformsGen;

        public int noiseSizeClimate;
        public int noiseSizeForest;
        public int noiseSizeBeach;
        public int noiseSizeShrubs;
        public int noiseSizeGeoProv;
        public int noiseSizeLandform;

        LatitudeData latdata = new LatitudeData();

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("latitudedata")
               .RegisterMessageType(typeof(LatitudeData))
            ;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network.GetChannel("latitudedata").SetMessageHandler<LatitudeData>(onLatitudeDataReceived);
            api.Event.LevelFinalize += Event_LevelFinalize;

            capi = api;
            
        }

        private void Event_LevelFinalize()
        {
            capi.World.Calendar.OnGetLatitude = getLatitude;
        }

        private void onLatitudeDataReceived(LatitudeData latdata)
        {
            this.latdata = latdata;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            api.Event.InitWorldGenerator(initWorldGen, "standard");
            api.Event.InitWorldGenerator(initWorldGen, "superflat");

            api.Event.MapRegionGeneration(OnMapRegionGen, "standard");
            api.Event.MapRegionGeneration(OnMapRegionGen, "superflat");

            api.Event.PlayerJoin += (plr) =>
            {
                api.Network.GetChannel("latitudedata").SendPacket(latdata, plr);
            };
        }


        public void initWorldGen()
        {
            long seed = sapi.WorldManager.Seed;
            noiseSizeClimate = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            noiseSizeForest = sapi.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            noiseSizeShrubs = sapi.WorldManager.RegionSize / TerraGenConfig.shrubMapScale;
            noiseSizeGeoProv = sapi.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            noiseSizeLandform = sapi.WorldManager.RegionSize / TerraGenConfig.landformMapScale;
            noiseSizeBeach = sapi.WorldManager.RegionSize / TerraGenConfig.beachMapScale;

            ITreeAttribute worldConfig = sapi.WorldManager.SaveGame.WorldConfiguration;
            string climate = worldConfig.GetString("worldClimate", "realistic");
            NoiseClimate noiseClimate;

            float tempModifier = worldConfig.GetString("globalTemperature", "1").ToFloat(1);
            float rainModifier = worldConfig.GetString("globalPrecipitation", "1").ToFloat(1);
            latdata.polarEquatorDistance = worldConfig.GetString("polarEquatorDistance", "50000").ToInt(50000);

            switch (climate)
            {
                case "realistic":
                    int spawnMinTemp = 6;
                    int spawnMaxTemp = 14;

                    string startingClimate = worldConfig.GetString("startingClimate");
                    switch (startingClimate)
                    {
                        case "hot":
                            spawnMinTemp = 28;
                            spawnMaxTemp = 32;
                            break;
                        case "warm":
                            spawnMinTemp = 19;
                            spawnMaxTemp = 23;
                            break;
                        case "cool":
                            spawnMinTemp = -5;
                            spawnMaxTemp = 1;
                            break;
                        case "icy":
                            spawnMinTemp = -15;
                            spawnMaxTemp = -10;
                            break;
                    }

                    noiseClimate = new NoiseClimateRealistic(seed, (double)sapi.WorldManager.MapSizeZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale, latdata.polarEquatorDistance, spawnMinTemp, spawnMaxTemp);
                    latdata.isRealisticClimate = true;
                    latdata.ZOffset = (noiseClimate as NoiseClimateRealistic).ZOffset;
                    break;

                default:
                    noiseClimate = new NoiseClimatePatchy(seed);
                    break;
            }

            noiseClimate.rainMul = rainModifier;
            noiseClimate.tempMul = tempModifier;


            climateGen = GetClimateMapGen(seed + 1, noiseClimate);
            forestGen = GetForestMapGen(seed + 2, TerraGenConfig.forestMapScale);
            bushGen = GetForestMapGen(seed + 109, TerraGenConfig.shrubMapScale);
            flowerGen = GetForestMapGen(seed + 223, TerraGenConfig.forestMapScale);
            beachGen = GetBeachMapGen(seed + 2273, TerraGenConfig.beachMapScale);
            geologicprovinceGen = GetGeologicProvinceMapGen(seed + 3, sapi);
            landformsGen = GetLandformMapGen(seed + 4, noiseClimate, sapi);

            sapi.World.Calendar.OnGetLatitude = getLatitude;
        }


        private double getLatitude(double posZ)
        {
            if (!latdata.isRealisticClimate)
            {
                return 0.5;
            }

            double halfRange = (double)latdata.polarEquatorDistance / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale;

            double A = 2;
            double P = halfRange;
            double z = posZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale + latdata.ZOffset;

            // Shifted and normalized sawtooth so we have -1 for south pole, 1 for north pole and 0 for equator
            // Due to the shift on the Y-Axis we also had to half the frequency
            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiIyLzk3LjY1NjI1Kig5Ny42NTYyNS1hYnMoYWJzKHgrOTcuNjUyNS8yKSUoMio5Ny42NTYyNSktOTcuNjU2MjUpKS0xIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiLTc5Ni4xNTM4NDYxNTM4NDYxIiwiNzAzLjg0NjE1Mzg0NjE1MzkiLCItMS4yMDEyNSIsIjEuMjk4NzUiXX1d
            double latitude = (A / P) * (P - Math.Abs(Math.Abs(z / 2 - P) % (2 * P) - P)) - 1;

            return latitude;
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


            mapRegion.BeachMap.Size = noiseSizeBeach + 1;
            mapRegion.BeachMap.BottomRightPadding = 1;
            mapRegion.BeachMap.Data = beachGen.GenLayer(regionX * noiseSizeBeach, regionZ * noiseSizeBeach, noiseSizeBeach + 1, noiseSizeBeach + 1);

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

        public static MapLayerBase GetOreMap(long seed, NoiseOre oreNoise, float scaleMul, float contrast, float sub)
        {
            MapLayerBase ore = new MapLayerOre(seed + 1, oreNoise, scaleMul, contrast, sub);
            ore.DebugDrawBitmap(DebugDrawMode.RGB, 0, 0, 512, "Ore 1 - Noise");

            ore = new MapLayerPerlinWobble(seed + 2, ore, 5, 0.85f, TerraGenConfig.oreMapWobbleScale, TerraGenConfig.oreMapWobbleScale * 0.15f);
            ore.DebugDrawBitmap(DebugDrawMode.RGB, 0, 0, 512, "Ore 1 - Perlin Wobble");

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

        public static MapLayerBase GetBeachMapGen(long seed, int scale)
        {
            MapLayerPerlin layer = new MapLayerPerlin(seed + 1, 6, 0.9f, scale/3, 255, new double[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f });

            MapLayerBase beach = new MapLayerPerlinWobble(seed + 986876, layer, 4, 0.9f, scale / 2);
            //forest.DebugDrawBitmap(1, 0, 0, "Forest 1 - PerlinWobbleClimate"); - Requires climate  map

            return beach;
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
