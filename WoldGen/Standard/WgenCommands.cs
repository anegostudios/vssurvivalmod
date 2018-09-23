using System;
using System.Collections.Generic;
using System.Drawing;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    static class RandomExtensions
    {
        public static void Shuffle<T>(this Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }
    }

    public class WgenCommands : ModSystem
    {
        ICoreServerAPI api;
        TreeGeneratorsUtil treeGenerators;

        int regionSize;


        int groupId = GlobalConstants.ServerInfoChatGroup;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI m)
        {
            this.api = m;
            treeGenerators = new TreeGeneratorsUtil(m);

            m.RegisterCommand("wgen", "World generator tools", "[testmap|genmap|testnoise|chunk|region|pos|tree]", CmdWgen, Privilege.controlserver);

            m.Event.SaveGameLoaded += OnGameWorldLoaded;
            if (api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                OnGameWorldLoaded();
            }

          //  api.WorldManager.AutoGenerateChunks = false;
        }


        private void OnGameWorldLoaded()
        {
            regionSize = api.WorldManager.RegionSize;
        }



        private void CmdWgen(IServerPlayer player, int groupId, CmdArgs arguments)
        {
            this.groupId = groupId;

            if (arguments.Length < 1)
            {
                player.SendMessage(groupId, "/wgen [testmap|testnoise|chunk|region|pos|tree]", EnumChatType.CommandError);
                return;
            }

            switch (arguments[0])
            {
                case "autogen":
                    arguments.PopWord();
                    api.WorldManager.AutoGenerateChunks = (bool)arguments.PopBool(false);
                    player.SendMessage(groupId, "Autogen now " + (api.WorldManager.AutoGenerateChunks ? "on" : "off"), EnumChatType.CommandError);
                    break;

                case "gt":
                    TerraGenConfig.GenerateVegetation = arguments.Length > 1 && (arguments[1] == "1" || arguments[1] == "on");
                    player.SendMessage(groupId, "Generate trees now " + (TerraGenConfig.GenerateVegetation ? "on" : "off"), EnumChatType.CommandError);
                    break;

                case "regen":
                    TestWgen(player, arguments);
                    break;

                case "del":
                    DelChunks(player, arguments);
                    break;

                case "tree":
                    TestTree(player, arguments);
                    break;

                case "treelineup":
                    TreeLineup(player, arguments);
                    break;

                case "testmap":
                    TestMap(player, arguments);
                    break;

                case "genmap":
                    GenMap(player, arguments);
                    break;

                case "chunk":
                    ReadChunk(player, arguments);
                    break;

                case "region":
                    arguments.PopWord();
                    ReadRegion(player, arguments);
                    break;

                case "pos":
                    ReadPos(player, arguments);
                    break;


                case "testnoise":
                    TestNoise(player, arguments);
                    break;


                default:
                    player.SendMessage(groupId, "/wgen [testmap|testnoise|chunk|region|pos|tree]", EnumChatType.CommandError);
                    break;
            }

        }

        private void DelChunks(IServerPlayer player, CmdArgs arguments)
        {
            Regen(player, arguments, true);
        }

        private void TestWgen(IServerPlayer player, CmdArgs arguments)
        {
            NoiseLandforms.ReloadLandforms(api);

            api.ModLoader.GetModSystem<GenVegetation>().GameWorldLoaded();
            api.ModLoader.GetModSystem<GenMaps>().GameWorldLoaded();
            api.ModLoader.GetModSystem<GenLakes>().GameWorldLoaded();
            api.ModLoader.GetModSystem<GenTerra>().GameWorldLoaded();
            api.ModLoader.GetModSystem<GenBlockLayers>().GameWorldLoaded();

            Regen(player, arguments, false);
        }

        void Regen(IServerPlayer player, CmdArgs arguments, bool onlydelete)
        {
            int chunkMidX = api.WorldManager.MapSizeX / api.WorldManager.ChunkSize / 2;
            int chunkMidZ = api.WorldManager.MapSizeZ / api.WorldManager.ChunkSize / 2;

            List<Vec2i> coords = new List<Vec2i>();

            int rad = 2;
            if (arguments.Length > 1)
            {
                int.TryParse(arguments[1], out rad);
            }

            for (int x = -rad; x <= rad; x++)
            {
                for (int z = -rad; z <= rad; z++)
                {
                    coords.Add(new Vec2i(chunkMidX + x, chunkMidZ + z));
                }
            }

            

            foreach (Vec2i coord in coords)
            {
                api.WorldManager.DeleteChunkColumn(coord.X, coord.Y);
                if(!onlydelete) api.WorldManager.ForceLoadChunkColumn(coord.X, coord.Y, false);
            }

            int diam = 2 * rad + 1;
            if (onlydelete)
            {
                player.SendMessage(groupId, "Deleted " + diam + "x" + diam + " columns", EnumChatType.CommandSuccess);
            } else
            {
                player.SendMessage(groupId, "Reloaded landforms and regenerating " + diam + "x" + diam + " columns", EnumChatType.CommandSuccess);
            }
            
        }





        private void TestNoise(IServerPlayer player, CmdArgs arguments)
        {
            bool use3d = false;
            int octaves = 1;

            if (arguments.Length > 1)
            {
                if (!int.TryParse(arguments[1], out octaves)) octaves = 1;
            }
            

            Random rnd = new Random();
            long seed = rnd.Next();

            NormalizedSimplexNoise noise = NormalizedSimplexNoise.FromDefaultOctaves(octaves, 5, 0.7, seed);
            int size = 800;
            Bitmap bmp = new Bitmap(size, size);

            int underflows = 0;
            int overflows = 0;
            float min = 1;
            float max = 0;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    double value = use3d ? noise.Noise((double)x / size, 0, (double)y / size) : noise.Noise((double)x / size, (double)y / size);
                    if (value < 0)
                    {
                        underflows++;
                        value = 0;
                    }
                    if (value > 1)
                    {
                        overflows++;
                        value = 1;
                    }

                    min = Math.Min((float)value, min);
                    max = Math.Max((float)value, max);

                    int light = (int)(value * 255);
                    bmp.SetPixel(x, y, Color.FromArgb(255, light, light, light));
                }
            }

            bmp.Save("noise.png");
            player.SendMessage(groupId, (use3d ? "3D" : "2D") + " Noise (" + octaves + " Octaves) saved to noise.png. Overflows: " + overflows + ", Underflows: " + underflows, EnumChatType.CommandSuccess);
            player.SendMessage(groupId, "Noise min = " + min.ToString("0.##") +", max= " + max.ToString("0.##"), EnumChatType.CommandSuccess);
        }

        void TestTree(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 2)
            {
                player.SendMessage(groupId, "/wgen tree {treeWorldPropertyCode} [0.1 - 3] [aheadoffset]", EnumChatType.CommandError);
                return;
            }

            float size = 1f;
            int aheadoffset = 0;

            if (arguments.Length > 2)
            {
                float.TryParse(arguments[2], out size);
            }

            if (arguments.Length > 3)
            {
                int.TryParse(arguments[3], out aheadoffset);
            }

            BlockPos pos = player.Entity.Pos.HorizontalAheadCopy(aheadoffset).AsBlockPos;

            IBlockAccessor blockAccessor = api.WorldManager.GetBlockAccessorBulkUpdate(true, true);
            
            while (blockAccessor.GetBlockId(pos) == 0 && pos.Y > 1)
            {
                pos.Down();
            }

            treeGenerators.ReloadTreeGenerators();
            treeGenerators.RunGenerator(new AssetLocation(arguments[1]), blockAccessor, pos, size);

            blockAccessor.Commit();

            player.SendMessage(groupId, arguments[1] + " size " + size + " generated.", EnumChatType.CommandError);
        }


        


        void TreeLineup(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 2)
            {
                player.SendMessage(groupId, "/wgen treelineup {treeWorldPropertyCode} [0.1 - 3]", EnumChatType.CommandError);
                return;
            }

            EntityPos pos = player.Entity.Pos;
            BlockPos center = pos.HorizontalAheadCopy(25).AsBlockPos;
            IBlockAccessor blockAccessor = api.WorldManager.GetBlockAccessorBulkUpdate(true, true, true);

            int size = 12;
            for (int dx = -2*size; dx < 2*size; dx++)
            {
                for (int dz = -size; dz < size; dz++)
                {
                    for (int dy = 0; dy < 2 * size; dy++)
                    {
                        blockAccessor.SetBlock(0, center.AddCopy(dx, dy, dz));
                    }
                }
            }


            treeGenerators.ReloadTreeGenerators();
            treeGenerators.RunGenerator(new AssetLocation(arguments[1]), blockAccessor, center.AddCopy(0, -1, 0));
            treeGenerators.RunGenerator(new AssetLocation(arguments[1]), blockAccessor, center.AddCopy(-9, -1, 0));
            treeGenerators.RunGenerator(new AssetLocation(arguments[1]), blockAccessor, center.AddCopy(9, -1, 0));

            blockAccessor.Commit();
        }



        void TestMap(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 2)
            {
                player.SendMessage(groupId, "/wgen testmap [climate|forest|wind|gprov|landform|ore]", EnumChatType.CommandError);
                return;
            }

            Random rnd = new Random();
            long seed = rnd.Next();

            switch (arguments[1])
            {
                case "climate":
                    {
                        NoiseBase.Debug = true;
                        NoiseClimate noiseClimate = new NoiseClimate(seed);
                        MapLayerBase climate = GenMaps.GetClimateMap(seed, noiseClimate);
                        player.SendMessage(groupId, "Climate map generated", EnumChatType.CommandSuccess);
                    }
                    break;

                case "forest":
                    {
                        NoiseBase.Debug = false;
                        NoiseClimate noiseClimate = new NoiseClimate(seed);
                        MapLayerBase climate = GenMaps.GetClimateMap(seed, noiseClimate);
                        MapLayerBase forest = GenMaps.GetForestMap(seed + 1, TerraGenConfig.forestMapScale);

                        IntMap climateMap = new IntMap() { Data = climate.GenLayer(0, 0, 512, 512), Size = 512 };

                        forest.SetInputMap(climateMap, new IntMap() { Size = 512 });

                        NoiseBase.Debug = true;
                        forest.DebugDrawBitmap(1, 0, 0, "Forest 1 - Forest");
                        player.SendMessage(groupId, "Forest map generated", EnumChatType.CommandSuccess);
                    }
                    break;


                case "ore":
                    {
                        NoiseBase.Debug = false;
                        NoiseOre noiseOre = new NoiseOre(seed);
                        MapLayerBase climate = GenMaps.GetOreMap(seed, noiseOre);
                        NoiseBase.Debug = true;
                        climate.DebugDrawBitmap(0, 0, 0, 1024, "Ore 1 - Ore");
                        player.SendMessage(groupId, "ore map generated", EnumChatType.CommandSuccess);
                    }
                    break;

                case "wind":
                    NoiseBase.Debug = true;
                    NoiseBase wind = GenMaps.GetDebugWindMap(seed);
                    player.SendMessage(groupId, "Wind map generated", EnumChatType.CommandSuccess);
                    break;

                case "gprov":
                    NoiseBase.Debug = true;
                    MapLayerBase provinces = GenMaps.GetGeologicProvinceMap(seed, api);

                    player.SendMessage(groupId, "Province map generated", EnumChatType.CommandSuccess);
                    break;

                case "landform":
                    {
                        NoiseBase.Debug = true;
                        NoiseClimate noiseClimate = new NoiseClimate(seed);
                        MapLayerBase landforms = GenMaps.GetLandformMap(seed + 1, noiseClimate, api);

                        player.SendMessage(groupId, "Landforms map generated", EnumChatType.CommandSuccess);
                    }
                    break;


                default:
                    player.SendMessage(groupId, "/wgen testmap [climate|forest|wind|gprov]", EnumChatType.CommandError);
                    break;
            }

            NoiseBase.Debug = false;

        }



        void GenMap(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 2)
            {
                player.SendMessage(groupId, "/wgen genmap [climate|forest|wind|gprov|landform|ore]", EnumChatType.CommandError);
                return;
            }

            int seed = api.World.Seed;
            BlockPos pos = player.Entity.ServerPos.XYZ.AsBlockPos;

            int noiseSizeClimate = api.WorldManager.RegionSize / TerraGenConfig.climateMapScale;
            int noiseSizeForest = api.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            int noiseSizeShrubs = api.WorldManager.RegionSize / TerraGenConfig.shrubMapScale;
            int noiseSizeGeoProv = api.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            int noiseSizeLandform = api.WorldManager.RegionSize / TerraGenConfig.landformMapScale;

            NoiseClimate noiseClimate = new NoiseClimate(seed);

            MapLayerBase climateGen = GenMaps.GetClimateMap(seed + 1, noiseClimate);
            MapLayerBase forestGen = GenMaps.GetForestMap(seed + 2, TerraGenConfig.forestMapScale);
            MapLayerBase bushGen = GenMaps.GetForestMap(seed + 109, TerraGenConfig.shrubMapScale);
            MapLayerBase flowerGen = GenMaps.GetForestMap(seed + 223, TerraGenConfig.forestMapScale);
            MapLayerBase geologicprovinceGen = GenMaps.GetGeologicProvinceMap(seed + 3, api);
            MapLayerBase landformsGen = GenMaps.GetLandformMap(seed + 4, noiseClimate, api);

            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;

            

            NoiseBase.Debug = true;

            switch (arguments[1])
            {
                case "climate":
                    {
                        int startX = regionX * noiseSizeClimate - 256;
                        int startZ = regionZ * noiseSizeClimate - 256;


                        climateGen.DebugDrawBitmap(0, startX, startZ, "climatemap");
                        player.SendMessage(groupId, "Climate map generated", EnumChatType.CommandSuccess);
                    }
                    break;

                case "forest":
                    {
                        //forestGen.SetInputMap(climateGen, new IntMap() { Size = 512 });

                       // forestGen.DebugDrawBitmap(1, 0, 0, "Forest 1 - Forest");
                        player.SendMessage(groupId, "Forest map gen not added yet", EnumChatType.CommandSuccess);
                    }
                    break;


                case "ore":
                    {
                        /*NoiseOre noiseOre = new NoiseOre(seed);
                        MapLayerBase climate = GenMaps.GetOreMap(seed, noiseOre);
                        
                        climate.DebugDrawBitmap(0, 0, 0, 1024, "Ore 1 - Ore");
                        player.SendMessage(groupId, "ore map generated", EnumChatType.CommandSuccess);*/
                    }
                    break;
                    
                case "gprov":
                    {
                        int startX = regionX * noiseSizeGeoProv - 256;
                        int startZ = regionZ * noiseSizeGeoProv - 256;

                        geologicprovinceGen.DebugDrawBitmap(3, startX, startZ, "gprovmap");
                        player.SendMessage(groupId, "Province map generated", EnumChatType.CommandSuccess);
                        break;
                    }

                case "landform":
                    {
                        int startX = regionX * noiseSizeLandform - 256;
                        int startZ = regionZ * noiseSizeLandform - 256;

                        landformsGen.DebugDrawBitmap(2, startX, startZ, "landformmap");
                        player.SendMessage(groupId, "Landforms map generated", EnumChatType.CommandSuccess);
                    }
                    break;


                default:
                    player.SendMessage(groupId, "/wgen testmap [climate|forest|wind|gprov]", EnumChatType.CommandError);
                    break;
            }


            NoiseBase.Debug = false;
        }





        void ReadChunk(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 2)
            {
                player.SendMessage(groupId, "Nothing implemented here", EnumChatType.CommandError);
                return;
            }
        }



        private void ReadRegion(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 1)
            {
                player.SendMessage(groupId, "/wgen region [climate|ore|forest|wind|gprov|gprovi|landform|landformi]", EnumChatType.CommandError);
                return;
            }

            int chunkSize = api.WorldManager.ChunkSize; 
            BlockPos pos = player.Entity.Pos.AsBlockPos;
            IServerChunk serverchunk = api.WorldManager.GetChunk(pos);
            if (serverchunk == null)
            {
                player.SendMessage(groupId, "Can't check here, beyond chunk boundaries!", EnumChatType.CommandError);
                return;
            }

            IMapRegion mapRegion = serverchunk.MapChunk.MapRegion;

                      
            int regionX = pos.X / regionSize;
            int regionZ = pos.Z / regionSize;

            string arg = arguments.PopWord();

            string subarg = arguments.PopWord();
            bool dolerp = subarg == "nolerp";

            NoiseBase.Debug = true;

            switch (arg)
            {
                case "climate":
                    DrawMapRegion(0, player, mapRegion.ClimateMap, "climate", dolerp, regionX, regionZ, TerraGenConfig.climateMapScale);
                    break;

                case "ore":
                    string type = dolerp ? arguments.PopWord("limonite") : subarg;
                    if (type == null) type = "limonite";

                    if (!mapRegion.OreMaps.ContainsKey(type))
                    {
                        player.SendMessage(groupId, "Mapregion does not contain an ore map for ore " + type, EnumChatType.CommandError);
                        return;
                    }

                    DrawMapRegion(0, player, mapRegion.OreMaps[type], "ore-" + type, dolerp, regionX, regionZ, TerraGenConfig.oreMapScale);
                    break;

                case "forest":
                    DrawMapRegion(1, player, mapRegion.ForestMap, "forest", dolerp, regionX, regionZ, TerraGenConfig.forestMapScale);
                    break;


                case "wind":
                    {

                    }
                    break;


                case "gprov":
                    DrawMapRegion(3, player, mapRegion.GeologicProvinceMap, "province", dolerp, regionX, regionZ, TerraGenConfig.geoProvMapScale);
                    break;


                case "gprovi":
                    {
                        int[] gprov = mapRegion.GeologicProvinceMap.Data;
                        int noiseSizeGeoProv = mapRegion.GeologicProvinceMap.InnerSize;

                        int outSize = (noiseSizeGeoProv + TerraGenConfig.geoProvMapPadding - 1) * TerraGenConfig.geoProvMapScale;

                        GeologicProvinceVariant[] provincesByIndex = NoiseGeoProvince.provinces.Variants;

                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(gprov, noiseSizeGeoProv + 2 * TerraGenConfig.geoProvMapPadding, 2);

                        int[] outColors = new int[outSize * outSize];
                        for (int x = 0; x < outSize; x++)
                        {
                            for (int z = 0; z < outSize; z++)
                            {
                                WeightedIndex[] indices = map[(float)x / TerraGenConfig.geoProvMapScale, (float)z / TerraGenConfig.geoProvMapScale];
                                for (int i = 0; i < indices.Length; i++)
                                {
                                    indices[i].index = provincesByIndex[indices[i].index].ColorInt;
                                }
                                int[] colors;
                                float[] weights;
                                map.Split(indices, out colors, out weights);
                                outColors[z * outSize + x] = ColorUtil.ColorAverage(colors, weights);
                            }
                        }

                        NoiseBase.DebugDrawBitmap(3, outColors, outSize, outSize, "geoprovince-lerped-" + regionX + "-" + regionZ);

                        player.SendMessage(groupId, "done", EnumChatType.CommandSuccess);

                        break;
                    }
                     
                    
                       

                case "landform":
                    DrawMapRegion(2, player, mapRegion.LandformMap, "landform", dolerp, regionX, regionZ, TerraGenConfig.landformMapScale);
                    break;



                case "landformi":
                    {
                        int[] data = mapRegion.LandformMap.Data;
                        int noiseSizeLandform = mapRegion.LandformMap.InnerSize;

                        int outSize = (noiseSizeLandform + TerraGenConfig.landformMapPadding - 1) * TerraGenConfig.landformMapScale;

                        LandformVariant[] landformsByIndex = NoiseLandforms.landforms.LandFormsByIndex;

                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(data, mapRegion.LandformMap.Size, 1);

                        int[] outColors = new int[outSize * outSize];
                        for (int x = 0; x < outSize; x++)
                        {
                            for (int z = 0; z < outSize; z++)
                            {
                                WeightedIndex[] indices = map[(float)x / TerraGenConfig.landformMapScale, (float)z / TerraGenConfig.landformMapScale];
                                for (int i = 0; i < indices.Length; i++)
                                {
                                    indices[i].index = landformsByIndex[indices[i].index].ColorInt;
                                }
                                int[] colors;
                                float[] weights;
                                map.Split(indices, out colors, out weights);
                                outColors[z * outSize + x] = ColorUtil.ColorAverage(colors, weights);
                            }
                        }

                        NoiseBase.DebugDrawBitmap(2, outColors, outSize, outSize, "landform-lerped-" + regionX + "-" + regionZ);

                        player.SendMessage(groupId, "Landform map done", EnumChatType.CommandSuccess);

                        break;
                    }


                default:
                    player.SendMessage(groupId, "/wgen region [climate|ore|forest|wind|gprov|landform]", EnumChatType.CommandError);
                    break;
            }

            NoiseBase.Debug = false;
        }


        void DrawMapRegion(int mode, IServerPlayer player, IntMap map, string prefix, bool lerp, int regionX, int regionZ, int scale)
        {

            if (lerp)
            {
                int[] lerped = GameMath.BiLerpColorMap(map, scale);
                NoiseBase.DebugDrawBitmap(mode, lerped, map.InnerSize * scale, prefix + "-" + regionX + "-" + regionZ + "-l");
                player.SendMessage(groupId, "Lerped " + prefix + " map generated.", EnumChatType.CommandSuccess);
            }
            else
            {
                NoiseBase.DebugDrawBitmap(mode, map.Data, map.Size, prefix + "-" + regionX + "-" + regionZ);
                player.SendMessage(groupId, "Original " + prefix + " map generated.", EnumChatType.CommandSuccess);
            }
        }



        void ReadPos(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 2)
            {
                player.SendMessage(groupId, "/wgen pos [gprov|landform|climate|height]", EnumChatType.CommandError);
                return;
            }


            int chunkSize = api.WorldManager.ChunkSize;
            BlockPos pos = player.Entity.Pos.AsBlockPos;
            IServerChunk serverchunk = api.WorldManager.GetChunk(pos);
            if (serverchunk == null)
            {
                player.SendMessage(groupId, "Can check here, beyond chunk boundaries!", EnumChatType.CommandError);
                return;
            }

            IMapRegion mapRegion = serverchunk.MapChunk.MapRegion;

            
            int regionChunkSize = api.WorldManager.RegionSize / chunkSize;


            int lx = pos.X % chunkSize;
            int lz = pos.Z % chunkSize;
            int chunkX = pos.X / chunkSize;
            int chunkZ = pos.Z / chunkSize;
            int regionX = pos.X / regionSize;
            int regionZ = pos.Z / regionSize;

            switch (arguments[1])
            {
                case "height":
                    {
                        string str = string.Format("Rain y={0}, Worldgen terrain y={1}", serverchunk.MapChunk.RainHeightMap[lz * chunkSize + lx], serverchunk.MapChunk.WorldGenTerrainHeightMap[lz * chunkSize + lx]);
                        player.SendMessage(groupId, str, EnumChatType.CommandSuccess);
                    }
                    break;

                case "gprov":
                    {
                        int noiseSizeGeoProv = mapRegion.GeologicProvinceMap.InnerSize;

                        float posXInRegion = ((float)pos.X / regionSize - pos.X / regionSize) * noiseSizeGeoProv;
                        float posZInRegion = ((float)pos.Z / regionSize - pos.Z / regionSize) * noiseSizeGeoProv;


                        GeologicProvinceVariant[] provincesByIndex = NoiseGeoProvince.provinces.Variants;

                        IntMap intmap = mapRegion.GeologicProvinceMap;

                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(intmap.Data, mapRegion.GeologicProvinceMap.Size, TerraGenConfig.geoProvSmoothingRadius);

                        WeightedIndex[] indices = map[intmap.TopLeftPadding + posXInRegion, intmap.TopLeftPadding + posZInRegion];

                        string text = "";
                        foreach (WeightedIndex windex in indices)
                        {
                            if (text.Length > 0)
                                text += ", ";

                            text += (100 * windex.weight).ToString("#.#") + "% " + provincesByIndex[windex.index].Code;
                        }

                        player.SendMessage(groupId, text, EnumChatType.CommandSuccess);

                        break;
                    }


                case "landform":
                    {
                        int noiseSizeLandform = mapRegion.LandformMap.InnerSize;

                        float posXInRegion = ((float)pos.X / regionSize - pos.X / regionSize) * noiseSizeLandform;
                        float posZInRegion = ((float)pos.Z / regionSize - pos.Z / regionSize) * noiseSizeLandform;


                        LandformVariant[] landforms = NoiseLandforms.landforms.LandFormsByIndex;

                        IntMap intmap = mapRegion.LandformMap;

                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(intmap.Data, mapRegion.LandformMap.Size, TerraGenConfig.landFormSmothingRadius);

                        WeightedIndex[] indices = map[intmap.TopLeftPadding + posXInRegion, intmap.TopLeftPadding + posZInRegion];

                        string text = "";
                        foreach (WeightedIndex windex in indices)
                        {
                            if (text.Length > 0)
                                text += ", ";

                            text += (100 * windex.weight).ToString("#.#") + "% " + landforms[windex.index].Code;
                        }

                        player.SendMessage(groupId, text, EnumChatType.CommandSuccess);

                        break;
                    }

                case "climate":
                    {
                        ClimateCondition climate = api.World.BlockAccessor.GetClimateAt(pos);

                        string text = string.Format("Temperature: {0}°, Rainfall: {1}%, Fertility: {2}%, Forest: {3}%, Shrub: {4}%, Sealevel dist: {5}%", climate.Temperature.ToString("0.#"), (int)(climate.Rainfall * 100f), (int)(climate.Fertility * 100f), (int)(climate.ForestDensity * 100f), (int)(climate.ShrubDensity * 100f), (int)(100f * pos.Y / 255f));

                        player.SendMessage(groupId, text, EnumChatType.CommandSuccess);


                        break;
                    }

            }
        }
    }
}
