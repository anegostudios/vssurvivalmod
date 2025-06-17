using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Datastructures;

#nullable disable

namespace Vintagestory.ServerMods
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class LatitudeData
    {
        public double ZOffset = 0;
        public bool isRealisticClimate = false;
        public int polarEquatorDistance;
    }

    public class ForceLandform
    {
        public string LandformCode;
        public int Radius;
        public BlockPos CenterPos;

        internal int landFormIndex;
    }

    public class ForceClimate
    {
        public int Radius;
        public BlockPos CenterPos;
        public int Climate;
    }

    public class GenMaps : ModSystem
    {
        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        public MapLayerBase upheavelGen;
        public MapLayerBase oceanGen;
        public MapLayerBase climateGen;
        public MapLayerBase flowerGen;
        public MapLayerBase bushGen;
        public MapLayerBase forestGen;
        public MapLayerBase beachGen;
        public MapLayerBase geologicprovinceGen;
        public MapLayerBase landformsGen;

        public int noiseSizeUpheavel;
        public int noiseSizeOcean;
        public int noiseSizeClimate;
        public int noiseSizeForest;
        public int noiseSizeBeach;
        public int noiseSizeShrubs;
        public int noiseSizeGeoProv;
        public int noiseSizeLandform;

        LatitudeData latdata = new LatitudeData();
        List<ForceLandform> forceLandforms = new List<ForceLandform>();
        List<ForceClimate> forceClimate = new List<ForceClimate>();

        NormalizedSimplexNoise noisegenX;
        NormalizedSimplexNoise noisegenZ;

        public static float upheavelCommonness;



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

        public void ForceClimateAt(ForceClimate climate)
        {
            forceClimate.Add(climate);
        }

        /// <summary>
        /// Forces a specific landform in a specified area. Area position are block positions. e.g. new Rectanglei(410000, 410000, 300, 300)
        /// </summary>
        /// <param name="landform"></param>
        public void ForceLandformAt(ForceLandform landform)
        {
            forceLandforms.Add(landform);
            ForceLandAt(landform);

            var list = NoiseLandforms.landforms.LandFormsByIndex;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Code.Path == landform.LandformCode)
                {
                    landform.landFormIndex = i;
                    return;
                }
            }

            throw new ArgumentException("No landform with code " + landform.LandformCode + " found.");
        }

        public void ForceLandAt(ForceLandform fl)
        {
            // this is related to the requiresSpawnOffset in MapLayerOceans
            if (GameVersion.IsLowerVersionThan(sapi.WorldManager.SaveGame.CreatedGameVersion, "1.20.0-pre.14"))
            {
                int regSize = sapi.WorldManager.RegionSize;

                var flRadius = fl.Radius;
                int minx = ((fl.CenterPos.X - flRadius) * noiseSizeOcean) / regSize;
                int minz = ((fl.CenterPos.Z - flRadius) * noiseSizeOcean) / regSize;
                int maxx = ((fl.CenterPos.X + flRadius) * noiseSizeOcean) / regSize;
                int maxz = ((fl.CenterPos.Z + flRadius) * noiseSizeOcean) / regSize;

                for (int x = minx; x <= maxx; x++)
                {
                    for (int z = minz; z < maxz; z++)
                    {
                        requireLandAt.Add(new XZ(x, z));
                    }
                }
            }
            else
            {
                // add extra chunk size so when blurred we still have enough land for the story locations
                var radius = fl.Radius + sapi.WorldManager.ChunkSize;
                ForceRandomLandArea(fl.CenterPos.X, fl.CenterPos.Z, radius);
            }
        }

        private void ForceRandomLandArea(int positionX, int positionZ, int radius)
        {
            var regionSize = sapi.WorldManager.RegionSize;
            var minx = (positionX - radius) * noiseSizeOcean / regionSize;
            var minz = (positionZ - radius) * noiseSizeOcean / regionSize;
            var maxx = (positionX + radius) * noiseSizeOcean / regionSize;
            var maxz = (positionZ + radius) * noiseSizeOcean / regionSize;

            // randomly grow the square into a more natural looking shape if all surroundings are ocean
            var lcgRandom = new LCGRandom(sapi.World.Seed);
            lcgRandom.InitPositionSeed(positionX, positionZ);
            var naturalShape = new NaturalShape(lcgRandom);
            var sizeX = maxx - minx;
            var sizeZ = maxz - minz;
            naturalShape.InitSquare(sizeX, sizeZ);
            naturalShape.Grow(sizeX * sizeZ);

            foreach (var pos in naturalShape.GetPositions())
            {
                requireLandAt.Add(new XZ(minx + pos.X, minz + pos.Y));
            }

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

        public List<XZ> requireLandAt = new List<XZ>();

        public void initWorldGen()
        {
            requireLandAt.Clear();
            forceLandforms.Clear();
            long seed = sapi.WorldManager.Seed;
            noiseSizeOcean = sapi.WorldManager.RegionSize / TerraGenConfig.oceanMapScale;
            noiseSizeUpheavel = sapi.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
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
            upheavelCommonness = worldConfig.GetString("upheavelCommonness", "0.3").ToFloat(0.3f);
            float landcover = worldConfig.GetString("landcover", "1").ToFloat(1f);
            float oceanscale = worldConfig.GetString("oceanscale", "1").ToFloat(1f);
            float landformScale = worldConfig.GetString("landformScale", "1.0").ToFloat(1.0f);

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
                    (noiseClimate as NoiseClimateRealistic).GeologicActivityStrength = worldConfig.GetString("geologicActivity").ToFloat(0.05f);

                    latdata.isRealisticClimate = true;
                    latdata.ZOffset = (noiseClimate as NoiseClimateRealistic).ZOffset;
                    break;

                default:
                    noiseClimate = new NoiseClimatePatchy(seed);
                    break;
            }

            noiseClimate.rainMul = rainModifier;
            noiseClimate.tempMul = tempModifier;

            // this is related to the requiresSpawnOffset in MapLayerOceans
            var requiresSpawnOffset = GameVersion.IsLowerVersionThan(sapi.WorldManager.SaveGame.CreatedGameVersion, "1.20.0-pre.14");
            if (requiresSpawnOffset)
            {
                int centerRegX = sapi.WorldManager.MapSizeX / sapi.WorldManager.RegionSize / 2;
                int centerRegZ = sapi.WorldManager.MapSizeZ / sapi.WorldManager.RegionSize / 2;
                requireLandAt.Add(new XZ(centerRegX * noiseSizeOcean, centerRegZ * noiseSizeOcean));
            }
            else
            {
                var chunkSize = sapi.WorldManager.ChunkSize;
                var radius = 4 * chunkSize;
                var spawnPosX = (sapi.WorldManager.MapSizeX + chunkSize) / 2;
                var spawnPosZ = (sapi.WorldManager.MapSizeZ + chunkSize) / 2;
                ForceRandomLandArea(spawnPosX, spawnPosZ, radius);
            }

            climateGen = GetClimateMapGen(seed + 1, noiseClimate);
            upheavelGen = GetGeoUpheavelMapGen(seed + 873, TerraGenConfig.geoUpheavelMapScale);
            oceanGen = GetOceanMapGen(seed + 1873, landcover, TerraGenConfig.oceanMapScale, oceanscale, requireLandAt, requiresSpawnOffset);
            forestGen = GetForestMapGen(seed + 2, TerraGenConfig.forestMapScale);
            bushGen = GetForestMapGen(seed + 109, TerraGenConfig.shrubMapScale);
            flowerGen = GetForestMapGen(seed + 223, TerraGenConfig.forestMapScale);
            beachGen = GetBeachMapGen(seed + 2273, TerraGenConfig.beachMapScale);
            geologicprovinceGen = GetGeologicProvinceMapGen(seed + 3, sapi);
            landformsGen = GetLandformMapGen(seed + 4, noiseClimate, sapi, landformScale);

            sapi.World.Calendar.OnGetLatitude = getLatitude;

            int woctaves = 2;
            float wscale = 2f * TerraGenConfig.landformMapScale;
            float wpersistence = 0.9f;
            noisegenX = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 2);
            noisegenZ = NormalizedSimplexNoise.FromDefaultOctaves(woctaves, 1 / wscale, wpersistence, seed + 1231296);
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
            // https://pfortuny.net/fooplot.com/#W3sidHlwZSI6MCwiZXEiOiIyLzk3LjY1NjI1Kig5Ny42NTYyNS1hYnMoYWJzKHgrOTcuNjUyNS8yKSUoMio5Ny42NTYyNSktOTcuNjU2MjUpKS0xIiwiY29sb3IiOiIjMDAwMDAwIn0seyJ0eXBlIjoxMDAwLCJ3aW5kb3ciOlsiLTc5Ni4xNTM4NDYxNTM4NDYxIiwiNzAzLjg0NjE1Mzg0NjE1MzkiLCItMS4yMDEyNSIsIjEuMjk4NzUiXX1d
            double latitude = (A / P) * (P - Math.Abs(Math.Abs(z / 2 - P) % (2 * P) - P)) - 1;

            return latitude;
        }



        private void OnMapRegionGen(IMapRegion mapRegion, int regionX, int regionZ, ITreeAttribute chunkGenParams = null)
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

            int upPad = 3;
            mapRegion.UpheavelMap.Size = noiseSizeUpheavel + 2 * upPad;
            mapRegion.UpheavelMap.TopLeftPadding = upPad;
            mapRegion.UpheavelMap.BottomRightPadding = upPad;
            mapRegion.UpheavelMap.Data = upheavelGen.GenLayer(
                regionX * noiseSizeUpheavel - upPad, regionZ * noiseSizeUpheavel - upPad,
                noiseSizeUpheavel + 2* upPad, noiseSizeUpheavel + 2* upPad
            );

            int opad = 5;
            mapRegion.OceanMap.Size = noiseSizeOcean + 2*opad;
            mapRegion.OceanMap.TopLeftPadding = opad;
            mapRegion.OceanMap.BottomRightPadding = opad;
            mapRegion.OceanMap.Data = oceanGen.GenLayer(
                regionX * noiseSizeOcean - opad,
                regionZ * noiseSizeOcean - opad,
                noiseSizeOcean + 2*opad, noiseSizeOcean + 2 * opad
            );

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

            if (chunkGenParams?.HasAttribute("forceLandform")==true)
            {
                var index = chunkGenParams.GetInt("forceLandform");
                for (int i = 0; i < mapRegion.LandformMap.Data.Length; i++)
                {
                    mapRegion.LandformMap.Data[i] = index;
                }
            }

            int regionsize = sapi.WorldManager.RegionSize;
            foreach (var fl in forceLandforms)
            {
                forceLandform(mapRegion, regionX, regionZ, pad, regionsize, fl);
                forceNoUpheavel(mapRegion, regionX, regionZ, upPad, regionsize, fl);
            }

            foreach (var climate in forceClimate)
            {
                ForceClimate(mapRegion, regionX, regionZ, pad, regionsize, climate);
            }

            mapRegion.DirtyForSaving = true;
        }

        private void forceNoUpheavel(IMapRegion mapRegion, int regionX, int regionZ, int pad, int regionsize, ForceLandform fl)
        {
            var map = mapRegion.UpheavelMap;
            int uhmapsize = map.InnerSize;

            float wobbleIntensityBlocks = 80;

            float padRel_wobblepaduh = (float)pad / noiseSizeUpheavel + wobbleIntensityBlocks / regionsize;

            float minlf = -padRel_wobblepaduh;
            float maxlf = (1 + padRel_wobblepaduh);
            // +100 for the transition between forced landform and no upheavel
            // else in cases where a veryflat should be and a glacier is it does not have enough room to transition between
            var rad = fl.Radius + 100;

            float startX = (float)(fl.CenterPos.X - rad) / regionsize - regionX;
            float endX = (float)(fl.CenterPos.X + rad) / regionsize - regionX;
            float startZ = (float)(fl.CenterPos.Z - rad) / regionsize - regionZ;
            float endZ = (float)(fl.CenterPos.Z + rad) / regionsize - regionZ;


            // Only proceed if this mapregion is within the rectangle to be forced
            if (endX >= minlf && startX <= maxlf && endZ >= minlf && startZ <= maxlf)
            {
                double radiussq = Math.Pow((double)rad / regionsize * uhmapsize, 2);

                double centerRegionX = (double)fl.CenterPos.X / regionsize;
                double centerRegionZ = (double)fl.CenterPos.Z / regionsize;

                // Distance to center position from current region
                // in region coordinate system
                double regionOffsetToCenterX = centerRegionX - regionX;
                double regionOffsetToCenterZ = centerRegionZ - regionZ;

                // Now we upscale this to amount of pixels in the upheavel map
                regionOffsetToCenterX *= uhmapsize;
                regionOffsetToCenterZ *= uhmapsize;

                startX = GameMath.Clamp(startX, minlf, maxlf) * uhmapsize - pad;
                endX = GameMath.Clamp(endX, minlf, maxlf) * uhmapsize + pad;
                startZ = GameMath.Clamp(startZ, minlf, maxlf) * uhmapsize - pad;
                endZ = GameMath.Clamp(endZ, minlf, maxlf) * uhmapsize + pad;

                for (int x = (int)startX; x < endX; x++)
                {
                    for (int z = (int)startZ; z < endZ; z++)
                    {
                        double rsq = Math.Pow(x - regionOffsetToCenterX, 2) + Math.Pow(z - regionOffsetToCenterZ, 2);

                        if (rsq >= radiussq) continue;

                        double attn = Math.Pow(1 - rsq / radiussq, 3) * 512;

                        int finalX = x + pad;
                        int finalZ = z + pad;

                        if (finalX >= 0 && finalX < map.Size && finalZ >= 0 && finalZ < map.Size)
                        {
                            map.SetInt(finalX, finalZ, (int)Math.Max(0, map.GetInt(finalX, finalZ) - attn));
                        }
                    }
                }
            }
        }

        private void ForceClimate(IMapRegion mapRegion, int regionX, int regionZ, int pad, int regionsize, ForceClimate fl)
        {
            var map = mapRegion.ClimateMap;
            var innerSize = map.InnerSize;

            float wobbleIntensityBlocks = 80;

            var padRel_wobblepaduh = (float)pad / noiseSizeClimate + wobbleIntensityBlocks / regionsize;

            var minlf = -padRel_wobblepaduh;
            var maxlf = (1 + padRel_wobblepaduh);
            var transitionDist = 300f;
            var rad = fl.Radius + transitionDist;

            var startX = (fl.CenterPos.X - rad) / regionsize - regionX;
            var endX = (fl.CenterPos.X + rad) / regionsize - regionX;
            var startZ = (fl.CenterPos.Z - rad) / regionsize - regionZ;
            var endZ = (fl.CenterPos.Z + rad) / regionsize - regionZ;


            // Only proceed ifs this mapregion is within the rectangle to be forced
            if (endX >= minlf && startX <= maxlf && endZ >= minlf && startZ <= maxlf)
            {
                var radiussq = Math.Pow((double)rad / regionsize * innerSize, 2);
                var transsq = Math.Pow((double)transitionDist / regionsize * innerSize, 2);
                var startTransitionFade = Math.Sqrt(radiussq) - Math.Sqrt(transsq);

                var centerRegionX = (double)fl.CenterPos.X / regionsize;
                var centerRegionZ = (double)fl.CenterPos.Z / regionsize;

                // Distance to center position from current region
                // in region coordinate system
                var regionOffsetToCenterX = centerRegionX - regionX;
                var regionOffsetToCenterZ = centerRegionZ - regionZ;

                // Now we upscale this to amount of pixels in the upheavel map
                regionOffsetToCenterX *= innerSize;
                regionOffsetToCenterZ *= innerSize;

                startX = GameMath.Clamp(startX, minlf, maxlf) * innerSize - pad;
                endX = GameMath.Clamp(endX, minlf, maxlf) * innerSize + pad;
                startZ = GameMath.Clamp(startZ, minlf, maxlf) * innerSize - pad;
                endZ = GameMath.Clamp(endZ, minlf, maxlf) * innerSize + pad;

                var forceRain = (fl.Climate >> 8) & 0xff;
                var forceTemperature = (fl.Climate >> 16) & 0xff;

                for (var x = (int)startX; x < endX; x++)
                {
                    for (var z = (int)startZ; z < endZ; z++)
                    {
                        var rsq = Math.Pow(x - regionOffsetToCenterX, 2) + Math.Pow(z - regionOffsetToCenterZ, 2);
                        if (rsq >= radiussq) continue;

                        var finalX = x + pad;
                        var finalZ = z + pad;

                        if (finalX >= 0 && finalX < map.Size && finalZ >= 0 && finalZ < map.Size)
                        {
                            var climate = map.GetInt(finalX, finalZ);
                            var geologicActivity = climate & 0xff;
                            var rain = (climate >> 8) & 0xff;
                            var temperature = (climate >> 16) & 0xff;

                            var mapDist = Math.Sqrt(rsq);
                            var distanceFadeStart = Math.Max(0, mapDist - startTransitionFade);
                            var lerpAmount = Math.Min(1, distanceFadeStart / startTransitionFade);

                            var newTemperature = (int)GameMath.Lerp(forceTemperature, temperature, lerpAmount);
                            var newRain = (int)GameMath.Lerp(forceRain, rain, lerpAmount);
                            var newClimate = (newTemperature << 16) + (newRain << 8) + (geologicActivity);
                            map.SetInt(finalX, finalZ, newClimate);
                        }
                    }
                }
            }
        }

        private void forceLandform(IMapRegion mapRegion, int regionX, int regionZ, int pad, int regionsize, ForceLandform fl)
        {
            int lfmapsize = mapRegion.LandformMap.InnerSize;

            float wobbleIntensityBlocks = 80;
            float wobbleIntensityPixelslf = wobbleIntensityBlocks / regionsize * lfmapsize;

            float padRel_wobblepadlf = (float)pad / noiseSizeLandform + wobbleIntensityBlocks / regionsize;

            float minlf = -padRel_wobblepadlf;
            float maxlf = (1 + padRel_wobblepadlf);

            var flRadius = fl.Radius;
            float startX = (float)(fl.CenterPos.X - flRadius) / regionsize - regionX;
            float endX = (float)(fl.CenterPos.X + flRadius) / regionsize - regionX;
            float startZ = (float)(fl.CenterPos.Z - flRadius) / regionsize - regionZ;
            float endZ = (float)(fl.CenterPos.Z + flRadius) / regionsize - regionZ;

            // Only proceed if this mapregion is within the rectangle to be forced
            if (endX >= minlf && startX <= maxlf && endZ >= minlf && startZ <= maxlf)
            {
                // Normalise the start/end positions within this mapregion
                startX = GameMath.Clamp(startX, minlf, maxlf) * lfmapsize - pad;
                endX = GameMath.Clamp(endX, minlf, maxlf) * lfmapsize + pad;
                startZ = GameMath.Clamp(startZ, minlf, maxlf) * lfmapsize - pad;
                endZ = GameMath.Clamp(endZ, minlf, maxlf) * lfmapsize + pad;

                double radiussq = Math.Pow((double)flRadius / regionsize * lfmapsize, 2);

                double centerRegionX = (double)fl.CenterPos.X / regionsize;
                double centerRegionZ = (double)fl.CenterPos.Z / regionsize;

                // Distance to center position from current region
                // in region coordinate system
                double regionOffsetToCenterX = centerRegionX - regionX;
                double regionOffsetToCenterZ = centerRegionZ - regionZ;

                // Now we upscale this to amount of pixels in the upheavel map
                regionOffsetToCenterX *= lfmapsize;
                regionOffsetToCenterZ *= lfmapsize;

                for (int x = (int)startX; x < endX; x++)
                {
                    for (int z = (int)startZ; z < endZ; z++)
                    {
                        double rsq = Math.Pow(x - regionOffsetToCenterX, 2) + Math.Pow(z - regionOffsetToCenterZ, 2);

                        if (rsq >= radiussq) continue;

                        double nx = x + regionX * lfmapsize;
                        double nz = z + regionZ * lfmapsize;

                        int offsetX = (int)(wobbleIntensityPixelslf * noisegenX.Noise(nx, nz));
                        int offsetZ = (int)(wobbleIntensityPixelslf * noisegenZ.Noise(nx, nz));

                        int finalX = x + offsetX + pad;
                        int finalZ = z + offsetZ + pad;

                        if (finalX >= 0 && finalX < mapRegion.LandformMap.Size && finalZ >= 0 && finalZ < mapRegion.LandformMap.Size)
                        {
                            mapRegion.LandformMap.SetInt(finalX, finalZ, fl.landFormIndex);
                        }
                    }
                }
            }
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
            return forest;
        }

        public static MapLayerBase GetGeoUpheavelMapGen(long seed, int scale)
        {
            var map = new MapLayerPerlinUpheavel(seed, upheavelCommonness, scale, 600, -300);
            var blurred = new MapLayerBlur(0, map, 3);
            return blurred;
        }

        public static MapLayerBase GetOceanMapGen(long seed, float landcover, int oceanMapScale, float oceanScaleMul, List<XZ> requireLandAt, bool requiresSpawnOffset)
        {
            var map = new MapLayerOceans(seed, oceanMapScale * oceanScaleMul, landcover, requireLandAt, requiresSpawnOffset);
            var blurred = new MapLayerBlur(0, map, 5);
            return blurred;
        }

        public static MapLayerBase GetBeachMapGen(long seed, int scale)
        {
            MapLayerPerlin layer = new MapLayerPerlin(seed + 1, 6, 0.9f, scale/3, 255, new double[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f });
            MapLayerBase beach = new MapLayerPerlinWobble(seed + 986876, layer, 4, 0.9f, scale / 2);

            return beach;
        }

        public static MapLayerBase GetGeologicProvinceMapGen(long seed, ICoreServerAPI api)
        {
            MapLayerBase provinces = new MapLayerGeoProvince(seed + 5, api);
            provinces.DebugDrawBitmap(DebugDrawMode.ProvinceRGB, 0, 0, "Geologic Province 1 - WobbleProvinces");

            return provinces;
        }

        public static MapLayerBase GetLandformMapGen(long seed, NoiseClimate climateNoise, ICoreServerAPI api, float landformScale)
        {
            MapLayerBase landforms = new MapLayerLandforms(seed + 12, climateNoise, api, landformScale);
            landforms.DebugDrawBitmap(DebugDrawMode.LandformRGB, 0, 0, "Landforms 1 - Wobble Landforms");

            return landforms;
        }
    }
}
