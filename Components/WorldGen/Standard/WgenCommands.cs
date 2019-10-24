using System;
using System.Collections.Generic;
using System.Drawing;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
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



        private void CmdWgen(IServerPlayer player, int groupId, CmdArgs args)
        {
            this.groupId = groupId;

            if (args.Length < 1)
            {
                player.SendMessage(groupId, "/wgen [testmap|testnoise|chunk|region|pos|tree]", EnumChatType.CommandError);
                return;
            }

            string cmd = args.PopWord();

            switch (cmd)
            {
                case "autogen":
                    api.WorldManager.AutoGenerateChunks = (bool)args.PopBool(false);
                    player.SendMessage(groupId, "Autogen now " + (api.WorldManager.AutoGenerateChunks ? "on" : "off"), EnumChatType.CommandError);
                    break;

                case "gt":
                    TerraGenConfig.GenerateVegetation = (bool)args.PopBool(true);
                    player.SendMessage(groupId, "Generate trees now " + (TerraGenConfig.GenerateVegetation ? "on" : "off"), EnumChatType.CommandError);
                    break;

                case "regen":
                    RegenChunksAroundPlayer(player, args, false);
                    break;

                case "regenr":
                    RegenChunksAroundPlayer(player, args, true);
                    break;

                case "regenc":
                    RegenChunks(player, args);
                    break;
                    
                case "regenrc":
                    RegenChunks(player, args, false, true);
                    break;

                case "delrock":
                    DelRock(player, args, true);
                    break;

                case "delrockc":
                    DelRock(player, args);
                    break;

                case "del":
                    DelChunks(player, args);
                    break;

                case "tree":
                    TestTree(player, args);
                    break;

                case "treelineup":
                    TreeLineup(player, args);
                    break;

                case "testmap":
                    TestMap(player, args);
                    break;

                case "genmap":
                    GenMap(player, args);
                    break;

                case "chunk":
                    ReadChunk(player, args);
                    break;

                case "region":
                    args.PopWord();
                    ReadRegion(player, args);
                    break;

                case "pos":
                    ReadPos(player, args);
                    break;


                case "testnoise":
                    TestNoise(player, args);
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


        private void DelRock(IServerPlayer player, CmdArgs arguments, bool aroundPlayer = false)
        {
            player.SendMessage(groupId, "Deleting rock, this may take a while...", EnumChatType.CommandError);

            int chunkMidX = api.WorldManager.MapSizeX / api.WorldManager.ChunkSize / 2;
            int chunkMidZ = api.WorldManager.MapSizeZ / api.WorldManager.ChunkSize / 2;

            if (aroundPlayer)
            {
                chunkMidX = (int)player.Entity.Pos.X / api.WorldManager.ChunkSize;
                chunkMidZ = (int)player.Entity.Pos.Z / api.WorldManager.ChunkSize;
            }

            List<Vec2i> coords = new List<Vec2i>();

            int rad = (int)arguments.PopInt(2);

            for (int x = -rad; x <= rad; x++)
            {
                for (int z = -rad; z <= rad; z++)
                {
                    coords.Add(new Vec2i(chunkMidX + x, chunkMidZ + z));
                }
            }

            int chunksize = api.WorldManager.ChunkSize;

            List<Block> blocks = api.World.Blocks;

            foreach (Vec2i coord in coords)
            {
                for (int cy = 0; cy < api.WorldManager.MapSizeY / api.World.BlockAccessor.ChunkSize; cy++)
                {
                    IServerChunk chunk = api.WorldManager.GetChunk(coord.X, cy, coord.Y);
                    if (chunk == null) continue;

                    chunk.Unpack();
                    for (int i = 0; i < chunk.Blocks.Length; i++)
                    {
                        Block block = blocks[chunk.Blocks[i]];
                        if (block.BlockMaterial == EnumBlockMaterial.Stone || block.BlockMaterial == EnumBlockMaterial.Liquid || block.BlockMaterial == EnumBlockMaterial.Soil)
                        {
                            chunk.Blocks[i] = 0;
                        }
                    }

                    chunk.MarkModified();
                }

                api.WorldManager.FullRelight(new BlockPos(coord.X * chunksize, 0 * chunksize, coord.Y * chunksize), new BlockPos(coord.X * chunksize, api.WorldManager.MapSizeY, coord.Y * chunksize));
            }


            player.CurrentChunkSentRadius = 0;
        }




        private void RegenChunksAroundPlayer(IServerPlayer player, CmdArgs arguments, bool randomSeed)
        {
            RegenChunks(player, arguments, true, randomSeed);
        }

        private void RegenChunks(IServerPlayer player, CmdArgs arguments, bool aroundPlayer = false, bool randomSeed = false)
        {
            int seedDiff = randomSeed ? api.World.Rand.Next(100000) : 0;
            if (randomSeed) player.SendMessage(GlobalConstants.CurrentChatGroup, "Using random seed diff " + seedDiff, EnumChatType.Notification);

            player.SendMessage(GlobalConstants.CurrentChatGroup, "Waiting for chunk thread to pause...", EnumChatType.Notification);

            if (api.Server.PauseThread("chunkdbthread"))
            {
                NoiseLandforms.ReloadLandforms(api);

                api.ModLoader.GetModSystem<GenTerra>().initWorldGen();
                api.ModLoader.GetModSystem<GenMaps>().initWorldGen();
                api.ModLoader.GetModSystem<GenRockStrataNew>().initWorldGen(seedDiff);

                if (ModStdWorldGen.DoDecorationPass)
                {
                    api.ModLoader.GetModSystem<GenVegetation>().initWorldGen();
                    api.ModLoader.GetModSystem<GenPonds>().initWorldGen();
                    api.ModLoader.GetModSystem<GenBlockLayers>().InitWorldGen();
                    api.ModLoader.GetModSystem<GenCaves>().initWorldGen();
                    api.ModLoader.GetModSystem<GenDeposits>().initWorldGen();
                }

                Regen(player, arguments, false, aroundPlayer);
            } else
            {
                player.SendMessage(GlobalConstants.CurrentChatGroup, "Unable to regenerate chunks. Was not able to pause the chunk gen thread", EnumChatType.Notification);
            }

            api.Server.ResumeThread("chunkdbthread");
            player.CurrentChunkSentRadius = 0;
        }

        void Regen(IServerPlayer player, CmdArgs arguments, bool onlydelete, bool aroundPlayer = false)
        {
            int chunkMidX = api.WorldManager.MapSizeX / api.WorldManager.ChunkSize / 2;
            int chunkMidZ = api.WorldManager.MapSizeZ / api.WorldManager.ChunkSize / 2;

            if (aroundPlayer)
            {
                chunkMidX = (int)player.Entity.Pos.X / api.WorldManager.ChunkSize;
                chunkMidZ = (int)player.Entity.Pos.Z / api.WorldManager.ChunkSize;
            }

            List<Vec2i> coords = new List<Vec2i>();

            int rad = (int)arguments.PopInt(2);

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
            }

            if (!onlydelete)
            {
                // so that resends arrive after all deletes

                foreach (Vec2i coord in coords)
                {
                    int cx = coord.X;
                    int cz = coord.Y;

                    api.WorldManager.LoadChunkColumnFast(coord.X, coord.Y, new ChunkLoadOptions()
                    {
                        OnLoaded = () => {
                            for (int cy = 0; cy < api.WorldManager.MapSizeY / api.WorldManager.ChunkSize; cy++)
                            {
                                api.WorldManager.BroadcastChunk(cx, cy, cz, true);
                            }
                        }
                    });
                }
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
            int octaves = (int)arguments.PopInt(1);

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
            if (arguments.Length < 1)
            {
                player.SendMessage(groupId, "/wgen tree {treeWorldPropertyCode} [0.1 - 3] [aheadoffset]", EnumChatType.CommandError);
                return;
            }

            AssetLocation loc = new AssetLocation(arguments.PopWord());
            float size = (float)arguments.PopFloat(1);
            int aheadoffset = (int)arguments.PopInt(0);

            BlockPos pos = player.Entity.Pos.HorizontalAheadCopy(aheadoffset).AsBlockPos;

            IBlockAccessor blockAccessor = api.WorldManager.GetBlockAccessorBulkUpdate(true, true);
            
            while (blockAccessor.GetBlockId(pos) == 0 && pos.Y > 1)
            {
                pos.Down();
            }

            treeGenerators.ReloadTreeGenerators();
            treeGenerators.RunGenerator(loc, blockAccessor, pos, size);

            blockAccessor.Commit();

            player.SendMessage(groupId, loc + " size " + size + " generated.", EnumChatType.CommandError);
        }


        


        void TreeLineup(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 1)
            {
                player.SendMessage(groupId, "/wgen treelineup {treeWorldPropertyCode} [0.1 - 3]", EnumChatType.CommandError);
                return;
            }

            EntityPos pos = player.Entity.Pos;
            BlockPos center = pos.HorizontalAheadCopy(25).AsBlockPos;
            IBlockAccessor blockAccessor = api.WorldManager.GetBlockAccessorBulkUpdate(true, true, true);
            AssetLocation loc = new AssetLocation(arguments.PopWord());

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
            treeGenerators.RunGenerator(loc, blockAccessor, center.AddCopy(0, -1, 0));
            treeGenerators.RunGenerator(loc, blockAccessor, center.AddCopy(-9, -1, 0));
            treeGenerators.RunGenerator(loc, blockAccessor, center.AddCopy(9, -1, 0));

            blockAccessor.Commit();
        }



        void TestMap(IServerPlayer player, CmdArgs arguments)
        {
            if (arguments.Length < 1)
            {
                player.SendMessage(groupId, "/wgen testmap [climate|forest|wind|gprov|landform|ore]", EnumChatType.CommandError);
                return;
            }

            Random rnd = new Random();
            long seed = 1239123912;// rnd.Next();

            string subcmd = arguments.PopWord();

            switch (subcmd)
            {
                case "climate":
                    {
                        NoiseBase.Debug = true;
                        NoiseClimatePatchy noiseClimate = new NoiseClimatePatchy(seed);
                        MapLayerBase climate = GenMaps.GetClimateMapGen(seed, noiseClimate);
                        player.SendMessage(groupId, "Patchy climate map generated", EnumChatType.CommandSuccess);
                    }
                    break;

                case "climater":
                    {
                        NoiseBase.Debug = false;
                        NoiseClimateRealistic noiseClimate = new NoiseClimateRealistic(seed, api.World.BlockAccessor.MapSizeZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale);
                        MapLayerBase climate = GenMaps.GetClimateMapGen(seed, noiseClimate);

                        NoiseBase.DebugDrawBitmap(DebugDrawMode.RGB, climate.GenLayer(0, 0, 128, 2048), 128, 2048, "realisticlimate");

                        player.SendMessage(groupId, "Realistic climate map generated", EnumChatType.CommandSuccess);
                    }
                    break;


                case "forest":
                    {
                        NoiseBase.Debug = false;
                        NoiseClimatePatchy noiseClimate = new NoiseClimatePatchy(seed);
                        MapLayerBase climate = GenMaps.GetClimateMapGen(seed, noiseClimate);
                        MapLayerBase forest = GenMaps.GetForestMapGen(seed + 1, TerraGenConfig.forestMapScale);

                        IntMap climateMap = new IntMap() { Data = climate.GenLayer(0, 0, 512, 512), Size = 512 };

                        forest.SetInputMap(climateMap, new IntMap() { Size = 512 });

                        NoiseBase.Debug = true;
                        forest.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, 0, 0, "Forest 1 - Forest");
                        player.SendMessage(groupId, "Forest map generated", EnumChatType.CommandSuccess);
                    }
                    break;


                case "ore":
                    {
                        NoiseBase.Debug = true;
                        NoiseOre noiseOre = new NoiseOre(seed);

                        float scaleMul = (float)arguments.PopFloat(1f);
                        float contrast = (float)arguments.PopFloat(1f);
                        float sub = (float)arguments.PopFloat(0f);

                        MapLayerBase oremap = GenMaps.GetOreMap(seed, noiseOre, scaleMul, contrast, sub);
                        NoiseBase.Debug = false;
                        //climate.DebugDrawBitmap(DebugDrawMode.RGB, 0, 0, 1024, "Ore 1 - Ore");
                        player.SendMessage(groupId, "ore map generated", EnumChatType.CommandSuccess);
                    }
                    break;

                case "oretopdistort":
                    NoiseBase.Debug = true;
                    NoiseBase topdistort = GenMaps.GetDepositVerticalDistort(seed);
                    player.SendMessage(groupId, "Ore top distort map generated", EnumChatType.CommandSuccess);
                    break;

                case "wind":
                    NoiseBase.Debug = true;
                    NoiseBase wind = GenMaps.GetDebugWindMap(seed);
                    player.SendMessage(groupId, "Wind map generated", EnumChatType.CommandSuccess);
                    break;

                case "gprov":
                    NoiseBase.Debug = true;
                    MapLayerBase provinces = GenMaps.GetGeologicProvinceMapGen(seed, api);

                    player.SendMessage(groupId, "Province map generated", EnumChatType.CommandSuccess);
                    break;


                case "landform":
                    {
                        NoiseBase.Debug = true;
                        NoiseClimatePatchy noiseClimate = new NoiseClimatePatchy(seed);
                        MapLayerBase landforms = GenMaps.GetLandformMapGen(seed + 1, noiseClimate, api);

                        player.SendMessage(groupId, "Landforms map generated", EnumChatType.CommandSuccess);
                    }
                    break;

                case "rockstrata":
                    {
                        NoiseBase.Debug = true;
                        GenRockStrataNew mod = api.ModLoader.GetModSystem<GenRockStrataNew>();
                        for (int i = 0; i < mod.strataNoises.Length; i++)
                        {
                            mod.strataNoises[i].DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, 0, 0, "Rockstrata-" + mod.strata.Variants[i].BlockCode);
                        }

                        player.SendMessage(groupId, "Rockstrata maps generated", EnumChatType.CommandSuccess);
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
            if (arguments.Length < 1)
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

            NoiseClimatePatchy noiseClimate = new NoiseClimatePatchy(seed);

            MapLayerBase climateGen = GenMaps.GetClimateMapGen(seed + 1, noiseClimate);
            MapLayerBase forestGen = GenMaps.GetForestMapGen(seed + 2, TerraGenConfig.forestMapScale);
            MapLayerBase bushGen = GenMaps.GetForestMapGen(seed + 109, TerraGenConfig.shrubMapScale);
            MapLayerBase flowerGen = GenMaps.GetForestMapGen(seed + 223, TerraGenConfig.forestMapScale);
            MapLayerBase geologicprovinceGen = GenMaps.GetGeologicProvinceMapGen(seed + 3, api);
            MapLayerBase landformsGen = GenMaps.GetLandformMapGen(seed + 4, noiseClimate, api);
            
            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;
            int chunkSize = api.WorldManager.ChunkSize;
            

            NoiseBase.Debug = true;
            string subcmd = arguments.PopWord();

            switch (subcmd)
            {
                case "climate":
                    {
                        int startX = regionX * noiseSizeClimate - 256;
                        int startZ = regionZ * noiseSizeClimate - 256;
                        climateGen.DebugDrawBitmap(DebugDrawMode.RGB, startX, startZ, "climatemap");
                        player.SendMessage(groupId, "Climate map generated", EnumChatType.CommandSuccess);
                    }
                    break;

                case "forest":
                    {
                        int startX = regionX * noiseSizeForest - 256;
                        int startZ = regionZ * noiseSizeForest - 256;
                        forestGen.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, startX, startZ, "forestmap");
                        player.SendMessage(groupId, "Forest map generated", EnumChatType.CommandSuccess);
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

                        geologicprovinceGen.DebugDrawBitmap(DebugDrawMode.ProvinceRGB, startX, startZ, "gprovmap");
                        player.SendMessage(groupId, "Province map generated", EnumChatType.CommandSuccess);
                        break;
                    }

                case "landform":
                    {
                        int startX = regionX * noiseSizeLandform - 256;
                        int startZ = regionZ * noiseSizeLandform - 256;

                        landformsGen.DebugDrawBitmap(DebugDrawMode.LandformRGB, startX, startZ, "landformmap");
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
            if (arguments.Length < 1)
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

                    DrawMapRegion(DebugDrawMode.RGB, player, mapRegion.OreMaps[type], "ore-" + type, dolerp, regionX, regionZ, TerraGenConfig.oreMapScale);
                    break;

                case "forest":
                    DrawMapRegion(DebugDrawMode.FirstByteGrayscale, player, mapRegion.ForestMap, "forest", dolerp, regionX, regionZ, TerraGenConfig.forestMapScale);
                    break;


                case "oretopdistort":
                    DrawMapRegion(DebugDrawMode.FirstByteGrayscale, player, mapRegion.OreMapVerticalDistortTop, "oretopdistort", dolerp, regionX, regionZ, TerraGenConfig.depositVerticalDistortScale);
                    break;

                //case "depdist":
                //DrawMapRegion(0, player, mapRegion.DepositDistortionMap, "depositdistortion", dolerp, regionX, regionZ, TerraGenConfig.depositDistortionScale);
                //break;

                case "rockstrata":

                    for (int i = 0; i < mapRegion.RockStrata.Length; i++)
                    {
                        DrawMapRegion(DebugDrawMode.FirstByteGrayscale, player, mapRegion.RockStrata[i], "rockstrata" + i, dolerp, regionX, regionZ, TerraGenConfig.rockStrataScale);
                    }
                    break;

                case "wind":
                    {

                    }
                    break;


                case "gprov":
                    DrawMapRegion(DebugDrawMode.ProvinceRGB, player, mapRegion.GeologicProvinceMap, "province", dolerp, regionX, regionZ, TerraGenConfig.geoProvMapScale);
                    break;


                case "gprovi":
                    {
                        int[] gprov = mapRegion.GeologicProvinceMap.Data;
                        int noiseSizeGeoProv = mapRegion.GeologicProvinceMap.InnerSize;

                        int outSize = (noiseSizeGeoProv + TerraGenConfig.geoProvMapPadding - 1) * TerraGenConfig.geoProvMapScale;

                        GeologicProvinceVariant[] provincesByIndex = NoiseGeoProvince.provinces.Variants;

                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(gprov, noiseSizeGeoProv + 2 * TerraGenConfig.geoProvMapPadding, 2, mapRegion.GeologicProvinceMap.TopLeftPadding, mapRegion.GeologicProvinceMap.BottomRightPadding);

                        int[] outColors = new int[outSize * outSize];
                        for (int x = 0; x < outSize; x++)
                        {
                            for (int z = 0; z < outSize; z++)
                            {
                                WeightedIndex[] indices = map[(float)x / TerraGenConfig.geoProvMapScale, (float)z / TerraGenConfig.geoProvMapScale];
                                for (int i = 0; i < indices.Length; i++)
                                {
                                    indices[i].Index = provincesByIndex[indices[i].Index].ColorInt;
                                }
                                int[] colors;
                                float[] weights;
                                map.Split(indices, out colors, out weights);
                                outColors[z * outSize + x] = ColorUtil.ColorAverage(colors, weights);
                            }
                        }

                        NoiseBase.DebugDrawBitmap(DebugDrawMode.ProvinceRGB, outColors, outSize, outSize, "geoprovince-lerped-" + regionX + "-" + regionZ);

                        player.SendMessage(groupId, "done", EnumChatType.CommandSuccess);

                        break;
                    }
                     
                    
                       

                case "landform":
                    DrawMapRegion(DebugDrawMode.LandformRGB, player, mapRegion.LandformMap, "landform", dolerp, regionX, regionZ, TerraGenConfig.landformMapScale);
                    break;



                case "landformi":
                    {
                        int[] data = mapRegion.LandformMap.Data;
                        int noiseSizeLandform = mapRegion.LandformMap.InnerSize;

                        int outSize = (noiseSizeLandform + TerraGenConfig.landformMapPadding - 1) * TerraGenConfig.landformMapScale;

                        LandformVariant[] landformsByIndex = NoiseLandforms.landforms.LandFormsByIndex;

                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(data, mapRegion.LandformMap.Size, 1, mapRegion.LandformMap.TopLeftPadding, mapRegion.LandformMap.BottomRightPadding);

                        int[] outColors = new int[outSize * outSize];
                        for (int x = 0; x < outSize; x++)
                        {
                            for (int z = 0; z < outSize; z++)
                            {
                                WeightedIndex[] indices = map[(float)x / TerraGenConfig.landformMapScale, (float)z / TerraGenConfig.landformMapScale];
                                for (int i = 0; i < indices.Length; i++)
                                {
                                    indices[i].Index = landformsByIndex[indices[i].Index].ColorInt;
                                }
                                int[] colors;
                                float[] weights;
                                map.Split(indices, out colors, out weights);
                                outColors[z * outSize + x] = ColorUtil.ColorAverage(colors, weights);
                            }
                        }

                        NoiseBase.DebugDrawBitmap(DebugDrawMode.LandformRGB, outColors, outSize, outSize, "landform-lerped-" + regionX + "-" + regionZ);

                        player.SendMessage(groupId, "Landform map done", EnumChatType.CommandSuccess);

                        break;
                    }


                default:
                    player.SendMessage(groupId, "/wgen region [climate|ore|forest|wind|gprov|landform]", EnumChatType.CommandError);
                    break;
            }

            NoiseBase.Debug = false;
        }


        void DrawMapRegion(DebugDrawMode mode, IServerPlayer player, IntMap map, string prefix, bool lerp, int regionX, int regionZ, int scale)
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
            if (arguments.Length < 1)
            {
                player.SendMessage(groupId, "/wgen pos [gprov|landform|climate|height]", EnumChatType.CommandError);
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
            IMapChunk mapchunk = serverchunk.MapChunk;
            
            int regionChunkSize = api.WorldManager.RegionSize / chunkSize;


            int lx = pos.X % chunkSize;
            int lz = pos.Z % chunkSize;
            int chunkX = pos.X / chunkSize;
            int chunkZ = pos.Z / chunkSize;
            int regionX = pos.X / regionSize;
            int regionZ = pos.Z / regionSize;

            string subcmd = arguments.PopWord();

            switch (subcmd)
            {
                case "coords":
                    player.SendMessage(groupId, string.Format("Chunk X/Z: {0}/{1}, Region X/Z: {2},{3}", chunkX, chunkZ, regionX, regionZ), EnumChatType.CommandSuccess);
                    break;

                case "structures":
                    bool found = false;
                    api.World.BlockAccessor.WalkStructures(pos, (struc) =>
                    {
                        found = true;
                        player.SendMessage(groupId, "Structure with code " + struc.Code + " at this position", EnumChatType.CommandSuccess);
                    });

                    if (!found)
                    {
                        player.SendMessage(groupId, "No structures at this position", EnumChatType.CommandSuccess);
                    }

                    return;


                case "height":
                    {
                        string str = string.Format("Rain y={0}, Worldgen terrain y={1}", serverchunk.MapChunk.RainHeightMap[lz * chunkSize + lx], serverchunk.MapChunk.WorldGenTerrainHeightMap[lz * chunkSize + lx]);
                        player.SendMessage(groupId, str, EnumChatType.CommandSuccess);
                    }
                    break;


                case "cavedistort":
                    Bitmap bmp = new Bitmap(chunkSize, chunkSize);

                    for (int x = 0; x < chunkSize; x++)
                    {
                        for (int z = 0; z < chunkSize; z++)
                        {
                            byte color = mapchunk.CaveHeightDistort[z * chunkSize + x];
                            bmp.SetPixel(x, z, Color.FromArgb((color >> 16) & 0xff, (color >> 8) & 0xff, color & 0xff));
                        }
                    }

                    bmp.Save("cavedistort"+chunkX+"-"+chunkZ+".png");
                    player.SendMessage(groupId, "saved bitmap cavedistort" + chunkX + "-" + chunkZ + ".png", EnumChatType.CommandSuccess);
                    break;


                case "gprov":
                    {
                        int noiseSizeGeoProv = mapRegion.GeologicProvinceMap.InnerSize;

                        float posXInRegion = ((float)pos.X / regionSize - regionX) * noiseSizeGeoProv;
                        float posZInRegion = ((float)pos.Z / regionSize - regionZ) * noiseSizeGeoProv;
                        GeologicProvinceVariant[] provincesByIndex = NoiseGeoProvince.provinces.Variants;
                        IntMap intmap = mapRegion.GeologicProvinceMap;
                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(intmap.Data, mapRegion.GeologicProvinceMap.Size, TerraGenConfig.geoProvSmoothingRadius, mapRegion.GeologicProvinceMap.TopLeftPadding, mapRegion.GeologicProvinceMap.BottomRightPadding);
                        WeightedIndex[] indices = map[posXInRegion, posZInRegion];

                        string text = "";
                        foreach (WeightedIndex windex in indices)
                        {
                            if (text.Length > 0)
                                text += ", ";

                            text += (100 * windex.Weight).ToString("#.#") + "% " + provincesByIndex[windex.Index].Code;
                        }

                        player.SendMessage(groupId, text, EnumChatType.CommandSuccess);

                        break;
                    }


                case "rockstrata":
                    {
                        GenRockStrataNew rockstratagen = api.ModLoader.GetModSystem<GenRockStrataNew>();

                        int noiseSizeGeoProv = mapRegion.GeologicProvinceMap.InnerSize;
                        float posXInRegion = ((float)pos.X / regionSize - pos.X / regionSize) * noiseSizeGeoProv;
                        float posZInRegion = ((float)pos.Z / regionSize - pos.Z / regionSize) * noiseSizeGeoProv;
                        GeologicProvinceVariant[] provincesByIndex = NoiseGeoProvince.provinces.Variants;
                        IntMap intmap = mapRegion.GeologicProvinceMap;
                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(intmap.Data, mapRegion.GeologicProvinceMap.Size, TerraGenConfig.geoProvSmoothingRadius, mapRegion.GeologicProvinceMap.TopLeftPadding, mapRegion.GeologicProvinceMap.BottomRightPadding);
                        WeightedIndex[] indices = map[posXInRegion, posZInRegion];

                        float[] rockGroupMaxThickness = new float[4];

                        rockGroupMaxThickness[0] = rockGroupMaxThickness[1] = rockGroupMaxThickness[2] = rockGroupMaxThickness[3] = 0;

                        int rdx = chunkX % regionChunkSize;
                        int rdz = chunkZ % regionChunkSize;
                        IntMap rockMap;
                        float step = 0;
                        float distx = (float)rockstratagen.distort2dx.Noise(pos.X, pos.Z);
                        float distz = (float)rockstratagen.distort2dz.Noise(pos.X, pos.Z);



                        for (int i = 0; i < indices.Length; i++)
                        {
                            float w = indices[i].Weight;

                            GeologicProvinceVariant var = NoiseGeoProvince.provinces.Variants[indices[i].Index];

                            rockGroupMaxThickness[0] += var.RockStrataIndexed[0].MaxThickness * w;
                            rockGroupMaxThickness[1] += var.RockStrataIndexed[1].MaxThickness * w;
                            rockGroupMaxThickness[2] += var.RockStrataIndexed[2].MaxThickness * w;
                            rockGroupMaxThickness[3] += var.RockStrataIndexed[3].MaxThickness * w;
                        }

                        System.Text.StringBuilder sb = new System.Text.StringBuilder();

                        sb.AppendLine("Sedimentary max thickness: " + rockGroupMaxThickness[(int)EnumRockGroup.Sedimentary]);
                        sb.AppendLine("Metamorphic max thickness: " + rockGroupMaxThickness[(int)EnumRockGroup.Metamorphic]);
                        sb.AppendLine("Igneous max thickness: " + rockGroupMaxThickness[(int)EnumRockGroup.Igneous]);
                        sb.AppendLine("Volcanic max thickness: " + rockGroupMaxThickness[(int)EnumRockGroup.Volcanic]);
                        sb.AppendLine("========");

                        for (int id = 0; id < rockstratagen.strata.Variants.Length; id++)
                        {
                            rockMap = mapchunk.MapRegion.RockStrata[id];
                            step = (float)rockMap.InnerSize / regionChunkSize;

                            float dist = 1 + GameMath.Clamp((distx + distz) / 30, 0.9f, 1.1f);
                            sb.AppendLine(rockstratagen.strata.Variants[id].BlockCode.ToShortString() + " max thickness: " + rockMap.GetIntLerpedCorrectly(rdx * step + step * (float)(lx + distx) / chunkSize, rdz * step + step * (float)(lz + distz) / chunkSize));
                        }

                        sb.AppendLine("======");

                        int surfaceY = api.World.BlockAccessor.GetTerrainMapheightAt(pos);
                        int ylower = 1;
                        int yupper = surfaceY;
                        int rockStrataId = -1;
                        float strataThickness = 0;
                        RockStratum stratum = null;
                        

                        OrderedDictionary<int, int> stratathicknesses = new OrderedDictionary<int, int>();

                        while (ylower <= yupper)
                        {
                            if (--strataThickness <= 0)
                            {
                                rockStrataId++;
                                if (rockStrataId >= rockstratagen.strata.Variants.Length)
                                {
                                    break;
                                }
                                stratum = rockstratagen.strata.Variants[rockStrataId];
                                rockMap = mapchunk.MapRegion.RockStrata[rockStrataId];
                                step = (float)rockMap.InnerSize / regionChunkSize;

                                int grp = (int)stratum.RockGroup;
                                
                                float dist = 1 + GameMath.Clamp((distx + distz) / 30, 0.9f, 1.1f);
                                strataThickness = Math.Min(rockGroupMaxThickness[grp] * dist, rockMap.GetIntLerpedCorrectly(rdx * step + step * (float)(lx + distx) / chunkSize, rdz * step + step * (float)(lz + distz) / chunkSize));

                                strataThickness -= (stratum.RockGroup == EnumRockGroup.Sedimentary) ? Math.Max(0, yupper - TerraGenConfig.seaLevel) * 0.5f : 0;

                                if (strataThickness < 2)
                                {
                                    strataThickness = -1;
                                    continue;
                                }
                            }

                            if (!stratathicknesses.ContainsKey(stratum.BlockId)) stratathicknesses[stratum.BlockId] = 0;
                            stratathicknesses[stratum.BlockId]++;

                            if (stratum.GenDir == EnumStratumGenDir.BottomUp)
                            {
                                ylower++;

                            }
                            else
                            {
                                yupper--;
                            }
                        }

                        foreach (var val in stratathicknesses)
                        {
                            sb.AppendLine(api.World.Blocks[val.Key].Code.ToShortString() + " : " + val.Value + " blocks"); 
                        }


                        player.SendMessage(groupId, sb.ToString(), EnumChatType.CommandSuccess);

                        break;
                    }


                case "landform":
                    {
                        int noiseSizeLandform = mapRegion.LandformMap.InnerSize;

                        float posXInRegion = ((float)pos.X / regionSize - pos.X / regionSize) * noiseSizeLandform;
                        float posZInRegion = ((float)pos.Z / regionSize - pos.Z / regionSize) * noiseSizeLandform;


                        LandformVariant[] landforms = NoiseLandforms.landforms.LandFormsByIndex;

                        IntMap intmap = mapRegion.LandformMap;

                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(intmap.Data, mapRegion.LandformMap.Size, TerraGenConfig.landFormSmoothingRadius, intmap.TopLeftPadding, intmap.BottomRightPadding);

                        WeightedIndex[] indices = map[posXInRegion, posZInRegion];

                        string text = "";
                        foreach (WeightedIndex windex in indices)
                        {
                            if (text.Length > 0)
                                text += ", ";

                            text += (100 * windex.Weight).ToString("#.#") + "% " + landforms[windex.Index].Code;
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
