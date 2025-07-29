using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

#nullable disable

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

        private int _regionSize;
        private long _seed = 1239123912;
        private int _chunksize;
        private WorldGenStructuresConfig _scfg;
        private int _regionChunkSize;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override double ExecuteOrder()
        {
            // run this after GenStructures so we can load the config from there
            return 0.33;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            treeGenerators = new TreeGeneratorsUtil(api);
            api.Event.SaveGameLoaded += OnGameWorldLoaded;
            if (this.api.Server.CurrentRunPhase == EnumServerRunPhase.RunGame)
            {
                OnGameWorldLoaded();
            }

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(InitWorldGen, "standard");
            }

            CreateCommands();

            this.api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () =>
            {
                var parsers = api.ChatCommands.Parsers;
                var trees = api.World.TreeGenerators.Keys.Select(a => a.Path).ToArray();
                api.ChatCommands.GetOrCreate("wgen")
                    .BeginSubCommand("tree")
                        .WithDescription("Generate a tree in front of the player")
                        .RequiresPlayer()
                        .WithArgs(
                            parsers.WordRange("treeWorldPropertyCode", trees),
                            parsers.OptionalFloat("size", 1f), parsers.OptionalFloat("aheadoffset", 0f))
                        .HandleWith(OnCmdTree)
                    .EndSubCommand()
                    .BeginSubCommand("treelineup")
                        .WithDescription("treelineup")
                        .RequiresPlayer()
                        .WithArgs(parsers.Word("treeWorldPropertyCode", trees))
                        .HandleWith(OnCmdTreelineup)
                    .EndSubCommand()
                    ;
            });
        }


        private void InitWorldGen()
        {
            _chunksize = GlobalConstants.ChunkSize;
            _regionChunkSize = api.WorldManager.RegionSize / _chunksize;

            _scfg = api.ModLoader.GetModSystem<GenStructures>().scfg;

            // only allow commands in survival since it needs the structures and possible applied patches from mods for testing, also replaceblocklayers makes more sense in survival
            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.GetOrCreate("wgen")
                .BeginSubCommand("structures")
                    .BeginSubCommand("spawn")
                        .RequiresPlayer()
                        .WithDescription("Spawn a structure from structure.json like during worldgen. Target position will be the selected block or your position. See /dev list <num> command to get the correct index.")
                        .WithArgs(parsers.Int("structure_index"),parsers.OptionalInt("schematic_index"),parsers.OptionalIntRange("rotation_index", 0,3))
                        .HandleWith(OnStructuresSpawn)
                    .EndSubCommand()
                    .BeginSubCommand("list")
                        .WithDescription("List structures with their indices for the /dev structure spawn command")
                        .WithArgs(parsers.OptionalInt("structure_num"))
                        .HandleWith(OnStructuresList)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("resolve-meta")
                    .WithDescription("Toggle resolve meta blocks mode during Worldgen. Turn it off to spawn structures as they are. For example, in this mode, instead of traders, their meta spawners will spawn")
                    .WithAlias("rm")
                    .WithArgs(parsers.OptionalBool("on/off"))
                    .HandleWith(handleToggleImpresWgen)
                .EndSubCommand()
                ;
        }

        private void OnGameWorldLoaded()
        {
            _regionSize = api.WorldManager.RegionSize;
        }

        private void CreateCommands(){

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.GetOrCreate("wgen")
                .WithDescription("World generator tools")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("decopass")
                    .WithDescription("Toggle DoDecorationPass on/off")
                    .RequiresPlayer()
                    .WithArgs(parsers.OptionalBool("DoDecorationPass"))
                    .HandleWith(OnCmdDecopass)
                .EndSubCommand()
                .BeginSubCommand("autogen")
                    .WithDescription("Toggle AutoGenerateChunks on/off")
                    .RequiresPlayer()
                    .WithArgs(parsers.OptionalBool("AutoGenerateChunks"))
                    .HandleWith(OnCmdAutogen)
                .EndSubCommand()
                .BeginSubCommand("gt")
                    .WithDescription("Toggle GenerateVegetation on/off")
                    .RequiresPlayer()
                    .WithArgs(parsers.OptionalBool("GenerateVegetation"))
                    .HandleWith(OnCmdGt)
                .EndSubCommand()
                .BeginSubCommand("regenk")
                    .WithDescription("Regenerate chunks around the player. Keeps the mapregion and so will not regenerate structures use /wgen regen if you want to also regenerate the structures")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50), parsers.OptionalWord("landform"))
                    .HandleWith(OnCmdRegenk)
                .EndSubCommand()
                .BeginSubCommand("regen")
                    .WithDescription("Regenerate chunks around the player also regenerating the region. Keeps unaffected structures outside of the range and copy them to the new region")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50), parsers.OptionalWord("landform"))
                    .HandleWith(OnCmdRegen)
                .EndSubCommand()
                .BeginSubCommand("regenr")
                    .WithDescription("Regenerate chunks around the player with random seed")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50), parsers.OptionalWord("landform"))
                    .HandleWith(OnCmdRegenr)
                .EndSubCommand()
                .BeginSubCommand("regenc")
                    .WithDescription("Regenerate chunks around world center")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50), parsers.OptionalWord("landform"))
                    .HandleWith(OnCmdRegenc)
                .EndSubCommand()
                .BeginSubCommand("regenrc")
                    .WithDescription("Regenerate chunks around world center with random seed")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50), parsers.OptionalWord("landform"))
                    .HandleWith(OnCmdRegenrc)
                .EndSubCommand()
                .BeginSubCommand("pregen")
                    .WithDescription("Pregenerate chunks around the player or around world center when executed from console.")
                    .WithArgs(parsers.OptionalInt("chunk_range", 2))
                    .HandleWith(OnCmdPregen)
                .EndSubCommand()
                .BeginSubCommand("delrock")
                    .WithDescription("Delete all rocks in specified chunk range around the player. Good for testing ore generation.")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50))
                    .HandleWith(OnCmdDelrock)
                .EndSubCommand()
                .BeginSubCommand("delrockc")
                    .WithDescription("Delete all rocks in specified chunk range around the world center. Good for testing ore generation.")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50))
                    .HandleWith(OnCmdDelrockc)
                .EndSubCommand()
                .BeginSubCommand("del")
                    .WithDescription("Delete chunks around the player")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50), parsers.OptionalWord("landform"))
                    .HandleWith(OnCmdDel)
                .EndSubCommand()
                    .BeginSubCommand("delr")
                    .WithDescription("Delete chunks around the player and the map regions. This will allow that changed terrain can generate for example at story locations.")
                    .RequiresPlayer()
                    .WithArgs(parsers.IntRange("chunk_range",0,50))
                    .HandleWith(OnCmdDelr)
                .EndSubCommand()
                .BeginSubCommand("delrange")
                    .WithDescription("Delete a range of chunks. Start and end positions are in chunk coordinates. See CTRL + F3")
                    .RequiresPlayer()
                    .WithArgs(parsers.Int("x_start"),parsers.Int("z_start"), parsers.Int("x_end"),parsers.Int("z_end"))
                    .HandleWith(OnCmdDelrange)
                .EndSubCommand()
                .BeginSubCommand("treemap")
                    .WithDescription("treemap")
                    .HandleWith(OnCmdTreemap)
                .EndSubCommand()
                .BeginSubCommand("testmap")
                    .WithDescription("Generate a large noise map, to test noise generation")
                    .WithPreCondition(DisallowHosted)
                    .BeginSubCommand("climate")
                        .WithDescription("Print a climate testmap")
                        .HandleWith(OnCmdClimate)
                    .EndSubCommand()
                    .BeginSubCommand("geoact")
                        .WithDescription("Print a geoact testmap")
                        .WithArgs(parsers.OptionalInt("size",512))
                        .HandleWith(OnCmdGeoact)
                    .EndSubCommand()
                    .BeginSubCommand("climater")
                        .WithDescription("Print a geoact testmap")
                        .HandleWith(OnCmdClimater)
                    .EndSubCommand()
                    .BeginSubCommand("forest")
                        .WithDescription("Print a forest testmap")
                        .HandleWith(OnCmdForest)
                    .EndSubCommand()
                    .BeginSubCommand("upheavel")
                        .WithDescription("Print a upheavel testmap")
                        .WithArgs(parsers.OptionalInt("size",512))
                        .HandleWith(OnCmdUpheavel)
                    .EndSubCommand()
                    .BeginSubCommand("ocean")
                        .WithDescription("Print a ocean testmap")
                        .WithArgs(parsers.OptionalInt("size",512))
                        .HandleWith(OnCmdOcean)
                    .EndSubCommand()
                    .BeginSubCommand("ore")
                        .WithDescription("Print a ore testmap")
                        .WithArgs(parsers.OptionalFloat("scaleMul",1), parsers.OptionalFloat("contrast",1), parsers.OptionalFloat("sub"))
                        .HandleWith(OnCmdOre)
                    .EndSubCommand()
                    .BeginSubCommand("oretopdistort")
                        .WithDescription("Print a oretopdistort testmap")
                        .HandleWith(OnCmdOretopdistort)
                    .EndSubCommand()
                    .BeginSubCommand("wind")
                        .WithDescription("Print a wind testmap")
                        .HandleWith(OnCmdWind)
                    .EndSubCommand()
                    .BeginSubCommand("gprov")
                        .WithDescription("Print a gprov testmap")
                        .HandleWith(OnCmdGprov)
                    .EndSubCommand()
                    .BeginSubCommand("landform")
                        .WithDescription("Print a landform testmap")
                        .HandleWith(OnCmdLandform)
                    .EndSubCommand()
                    .BeginSubCommand("rockstrata")
                        .WithDescription("Print a rockstrata testmap")
                        .HandleWith(OnCmdRockstrata)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("genmap")
                    .WithDescription("Generate a large noise map around the players current location")
                    .WithPreCondition(DisallowHosted)
                    .BeginSubCommand("climate")
                        .WithDescription("Generate a climate map")
                        .RequiresPlayer()
                        .WithArgs(parsers.OptionalFloat("GeologicActivityStrength", 1))
                        .HandleWith(OnCmdGenmapClimate)
                    .EndSubCommand()
                    .BeginSubCommand("forest")
                        .WithDescription("Generate a forest map")
                        .RequiresPlayer()
                        .HandleWith(OnCmdGenmapForest)
                    .EndSubCommand()
                    .BeginSubCommand("upheavel")
                        .WithDescription("Generate a upheavel map")
                        .RequiresPlayer()
                        .WithArgs(parsers.OptionalInt("size", 512))
                        .HandleWith(OnCmdGenmapUpheavel)
                    .EndSubCommand()
                    .BeginSubCommand("mushroom")
                        .WithDescription("Generate a mushroom map")
                        .RequiresPlayer()
                        .HandleWith(OnCmdGenmapMushroom)
                    .EndSubCommand()
                    .BeginSubCommand("ore")
                        .WithDescription("Generate a ore map")
                        .RequiresPlayer()
                        .HandleWith(OnCmdGenmapOre)
                    .EndSubCommand()
                    .BeginSubCommand("gprov")
                        .WithDescription("Generate a gprov map")
                        .RequiresPlayer()
                        .HandleWith(OnCmdGenmapGprov)
                    .EndSubCommand()
                    .BeginSubCommand("landform")
                        .WithDescription("Generate a landform map")
                        .RequiresPlayer()
                        .HandleWith(OnCmdGenmapLandform)
                    .EndSubCommand()
                    .BeginSubCommand("ocean")
                        .WithDescription("Generate a ocean map")
                        .RequiresPlayer()
                        .HandleWith(OnCmdGenmapOcean)
                    .EndSubCommand()
                .EndSubCommand()
                .BeginSubCommand("stitchclimate")
                    .WithDescription("Print a 3x3 stitched climate map")
                    .RequiresPlayer()
                    .HandleWith(OnCmdStitch)
                .EndSubCommand()
                .BeginSubCommand("region")
                    .WithDescription("Extract already generated noise map data from the current region")
                    .RequiresPlayer()
                    .WithArgs(parsers.WordRange("sub_command", "climate", "ore", "forest", "upheavel", "ocean", "oretopdistort", "patches", "rockstrata", "gprov", "gprovi", "landform", "landformi"), parsers.OptionalBool("dolerp"), parsers.OptionalWord("orename"))
                    .HandleWith(OnCmdRegion)
                .EndSubCommand()
                .BeginSubCommand("regions")
                    .BeginSubCommand("ore")
                        .WithDescription("Print a region ore map")
                        .RequiresPlayer()
                        .WithArgs(parsers.OptionalInt("radius",1),parsers.OptionalWord("orename"))
                        .HandleWith(OnCmdRegionsOre)
                    .EndSubCommand()
                    .BeginSubCommand("upheavel")
                        .WithDescription("Print a region upheavel map")
                        .RequiresPlayer()
                        .WithArgs(parsers.OptionalInt("radius", 1))
                        .HandleWith(OnCmdRegionsUpheavel)
                    .EndSubCommand()
                    .BeginSubCommand("climate")
                        .WithDescription("Print a region climate map")
                        .RequiresPlayer()
                        .WithArgs(parsers.OptionalInt("radius", 1))
                        .HandleWith(OnCmdRegionsClimate)
                        .EndSubCommand()
                    .EndSubCommand()
                .BeginSubCommand("pos")
                    .WithDescription("Print info for the current position")
                    .RequiresPlayer()
                    .WithArgs(parsers.WordRange("sub_command","ymax","coords","latitude","structures","height","cavedistort","gprov","rockstrata","landform","climate"))
                    .HandleWith(OnCmdPos)
                .EndSubCommand()
                .BeginSubCommand("testnoise")
                    .WithDescription("Testnoise command")
                    .RequiresPlayer()
                    .WithArgs(parsers.OptionalInt("octaves",1))
                    .HandleWith(OnCmdTestnoise)
                .EndSubCommand()
                .BeginSubCommand("testvillage")
                    .WithDescription("Testvillage command")
                    .RequiresPlayer()
                    .HandleWith(OnCmdTestVillage)
                .EndSubCommand()
            ;
        }

        private TextCommandResult handleToggleImpresWgen(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success("Meta block replacing and Item resolving for worldgen currently " + (GenStructures.ReplaceMetaBlocks ? "on" : "off"));
            }

            var doReplace = (bool)args[0];
            GenStructures.ReplaceMetaBlocks = doReplace;
            return TextCommandResult.Success("Meta block replacing and Item resolving for worldgen now " + (doReplace ? "on" : "off"));
        }

        private TextCommandResult OnCmdTestVillage(TextCommandCallingArgs args)
        {
            if (api.Server.Config.HostedMode)
            {
                return TextCommandResult.Success(Lang.Get("Can't access this feature, server is in hosted mode"));
            }

            api.Assets.Reload(AssetCategory.worldgen);

            var ms = api.ModLoader.GetModSystem<GenStructures>();
            ms.initWorldGen();

            const int chunksize = GlobalConstants.ChunkSize;
            var pos = args.Caller.Pos;
            int chunkx = (int)pos.X / chunksize;
            int chunkz = (int)pos.Z / chunksize;
            var mr = api.World.BlockAccessor.GetMapRegion((int)pos.X / _regionSize, (int)pos.Z / _regionSize);

            for (int i = 0; i < 50; i++)
            {
                int len = ms.vcfg.VillageTypes.Length;
                WorldGenVillage struc = ms.vcfg.VillageTypes[api.World.Rand.Next(len)];
                bool ok = ms.GenVillage(api.World.BlockAccessor, mr, struc, chunkx, chunkz);
                if (ok)
                {
                    return TextCommandResult.Success($"Generated after {i+1} tries");
                }
            }

            return TextCommandResult.Error("Unable to generate, likely not flat enough here.");
        }

        private TextCommandResult DisallowHosted(TextCommandCallingArgs args)
        {
            if (api.Server.Config.HostedMode)
            {
                return TextCommandResult.Error(Lang.Get("Can't access this feature, server is in hosted mode"));
            }
            return TextCommandResult.Success();
        }

        private TextCommandResult OnCmdRegion(TextCommandCallingArgs args)
        {
            var player = args.Caller.Player as IServerPlayer;
            var pos = player.Entity.Pos.AsBlockPos;
            var serverchunk = api.WorldManager.GetChunk(pos);
            if (serverchunk == null)
            {
                return TextCommandResult.Success("Can't check here, beyond chunk boundaries!");
            }

            var mapRegion = serverchunk.MapChunk.MapRegion;
            var regionX = pos.X / _regionSize;
            var regionZ = pos.Z / _regionSize;

            var subargtype = args[0] as string;
            var dolerp = (bool)args[1];

            NoiseBase.Debug = true;

            switch (subargtype)
            {
                case "climate":
                    DrawMapRegion(0, args.Caller, mapRegion.ClimateMap, "climate", dolerp, regionX, regionZ, TerraGenConfig.climateMapScale);
                    break;

                case "ore":
                    var type = args.Parsers[2].IsMissing ? "limonite" : args[2] as string;

                    if (!mapRegion.OreMaps.ContainsKey(type))
                    {
                        player.SendMessage(args.Caller.FromChatGroupId, "Mapregion does not contain an ore map for ore " + type, EnumChatType.CommandError);
                    }

                    DrawMapRegion(DebugDrawMode.RGB, args.Caller, mapRegion.OreMaps[type], "ore-" + type, dolerp, regionX, regionZ, TerraGenConfig.oreMapScale);
                    break;

                case "forest":
                    DrawMapRegion(DebugDrawMode.FirstByteGrayscale, args.Caller, mapRegion.ForestMap, "forest", dolerp, regionX, regionZ, TerraGenConfig.forestMapScale);
                    break;

                case "upheavel":
                    DrawMapRegion(DebugDrawMode.FirstByteGrayscale, args.Caller, mapRegion.UpheavelMap, "upheavel", dolerp, regionX, regionZ, TerraGenConfig.geoUpheavelMapScale);
                    break;

                case "ocean":
                    DrawMapRegion(DebugDrawMode.FirstByteGrayscale, args.Caller, mapRegion.OceanMap, "ocean", dolerp, regionX, regionZ, TerraGenConfig.oceanMapScale);
                    break;


                case "oretopdistort":
                    DrawMapRegion(DebugDrawMode.FirstByteGrayscale, args.Caller, mapRegion.OreMapVerticalDistortTop, "oretopdistort", dolerp, regionX, regionZ, TerraGenConfig.depositVerticalDistortScale);
                    break;


                case "patches":
                    {
                        foreach (var val in mapRegion.BlockPatchMaps)
                        {
                            DrawMapRegion(DebugDrawMode.FirstByteGrayscale, args.Caller, val.Value, val.Key, dolerp, regionX, regionZ, TerraGenConfig.forestMapScale);
                        }

                        player.SendMessage(args.Caller.FromChatGroupId, "Patch maps generated", EnumChatType.CommandSuccess);
                    }
                    break;

                case "rockstrata":

                    for (var i = 0; i < mapRegion.RockStrata.Length; i++)
                    {
                        DrawMapRegion(DebugDrawMode.FirstByteGrayscale, args.Caller, mapRegion.RockStrata[i], "rockstrata" + i, dolerp, regionX, regionZ, TerraGenConfig.rockStrataScale);
                    }
                    break;

                case "gprov":
                    DrawMapRegion(DebugDrawMode.ProvinceRGB, args.Caller, mapRegion.GeologicProvinceMap, "province", dolerp, regionX, regionZ, TerraGenConfig.geoProvMapScale);
                    break;


                case "gprovi":
                {
                    var gprov = mapRegion.GeologicProvinceMap.Data;
                    var noiseSizeGeoProv = mapRegion.GeologicProvinceMap.InnerSize;

                    var outSize = (noiseSizeGeoProv + TerraGenConfig.geoProvMapPadding - 1) *
                                  TerraGenConfig.geoProvMapScale;

                    var provincesByIndex = NoiseGeoProvince.provinces.Variants;

                    var map = new LerpedWeightedIndex2DMap(gprov,
                        noiseSizeGeoProv + 2 * TerraGenConfig.geoProvMapPadding, 2,
                        mapRegion.GeologicProvinceMap.TopLeftPadding, mapRegion.GeologicProvinceMap.BottomRightPadding);

                    var outColors = new int[outSize * outSize];
                    for (var x = 0; x < outSize; x++)
                    {
                        for (var z = 0; z < outSize; z++)
                        {
                            var indices = map[(float)x / TerraGenConfig.geoProvMapScale,
                                (float)z / TerraGenConfig.geoProvMapScale];
                            for (var i = 0; i < indices.Length; i++)
                            {
                                indices[i].Index = provincesByIndex[indices[i].Index].ColorInt;
                            }

                                map.Split(indices, out int[] colors, out float[] weights);
                                outColors[z * outSize + x] = ColorUtil.ColorAverage(colors, weights);
                        }
                    }

                    NoiseBase.DebugDrawBitmap(DebugDrawMode.ProvinceRGB, outColors, outSize, outSize,
                        "geoprovince-lerped-" + regionX + "-" + regionZ);

                    player.SendMessage(args.Caller.FromChatGroupId, "done", EnumChatType.CommandSuccess);

                    break;
                }

                case "landform":
                    DrawMapRegion(DebugDrawMode.LandformRGB, args.Caller, mapRegion.LandformMap, "landform", dolerp, regionX, regionZ, TerraGenConfig.landformMapScale);
                    break;

                case "landformi":
                    {
                        var data = mapRegion.LandformMap.Data;
                        var noiseSizeLandform = mapRegion.LandformMap.InnerSize;

                        var outSize = (noiseSizeLandform + TerraGenConfig.landformMapPadding - 1) * TerraGenConfig.landformMapScale;

                        var landformsByIndex = NoiseLandforms.landforms.LandFormsByIndex;

                        var map = new LerpedWeightedIndex2DMap(data, mapRegion.LandformMap.Size, 1, mapRegion.LandformMap.TopLeftPadding, mapRegion.LandformMap.BottomRightPadding);

                        var outColors = new int[outSize * outSize];
                        for (var x = 0; x < outSize; x++)
                        {
                            for (var z = 0; z < outSize; z++)
                            {
                                var indices = map[(float)x / TerraGenConfig.landformMapScale, (float)z / TerraGenConfig.landformMapScale];
                                for (var i = 0; i < indices.Length; i++)
                                {
                                    indices[i].Index = landformsByIndex[indices[i].Index].ColorInt;
                                }
                                map.Split(indices, out int[] colors, out float[] weights);
                                outColors[z * outSize + x] = ColorUtil.ColorAverage(colors, weights);
                            }
                        }
                        NoiseBase.DebugDrawBitmap(DebugDrawMode.LandformRGB, outColors, outSize, outSize, "landform-lerped-" + regionX + "-" + regionZ);

                        player.SendMessage(args.Caller.FromChatGroupId, "Landform map done", EnumChatType.CommandSuccess);

                        break;
                    }
            }

            NoiseBase.Debug = false;
            return TextCommandResult.Success();
        }

        private TextCommandResult OnCmdDecopass(TextCommandCallingArgs args)
        {
            TerraGenConfig.DoDecorationPass = (bool)args[0];
            return TextCommandResult.Success("Decopass now " + (TerraGenConfig.DoDecorationPass ? "on" : "off"));
        }

        private TextCommandResult OnCmdAutogen(TextCommandCallingArgs args)
        {
            api.WorldManager.AutoGenerateChunks = (bool)args[0];
            return TextCommandResult.Success("Autogen now " + (api.WorldManager.AutoGenerateChunks ? "on" : "off"));
        }

        private TextCommandResult OnCmdGt(TextCommandCallingArgs args)
        {
            TerraGenConfig.GenerateVegetation = (bool)args[0];
            return TextCommandResult.Success("Generate trees now " + (TerraGenConfig.GenerateVegetation ? "on" : "off"));
        }

        private TextCommandResult OnCmdRegenk(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            var landform = args[1] as string;
            return RegenChunks(args.Caller, range, landform, true);
        }

        private TextCommandResult OnCmdRegen(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            var landform = args[1] as string;
            return RegenChunks(args.Caller, range, landform, true, false, true);
        }

        private TextCommandResult OnCmdRegenr(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            var landform = args[1] as string;
            return RegenChunks(args.Caller, range, landform, true, true, true);
        }

        private TextCommandResult OnCmdRegenc(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            var landform = args[1] as string;
            return RegenChunks(args.Caller, range, landform, false, false, true);
        }

        private TextCommandResult OnCmdRegenrc(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            var landform = args[1] as string;
            return RegenChunks(args.Caller, range, landform, false, true, true);
        }

        private TextCommandResult OnCmdPregen(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            return PregenerateChunksAroundPlayer(args.Caller, range);
        }

        private TextCommandResult OnCmdDelrock(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            DelRock(args.Caller, range, true);
            return TextCommandResult.Success();
        }

        private TextCommandResult OnCmdDelrockc(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            DelRock(args.Caller, range, false);
            return TextCommandResult.Success();
        }

        private TextCommandResult OnCmdDel(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            var landform = args[1] as string;
            return Regen(args.Caller, range, true, landform,true);
        }

        private TextCommandResult OnCmdDelr(TextCommandCallingArgs args)
        {
            var range = (int)args[0];
            return Regen(args.Caller, range, true, null,true, true);
        }

        private TextCommandResult OnCmdDelrange(TextCommandCallingArgs args)
        {
            var xs = (int)args[0];
            var zs = (int)args[1];
            var xe = (int)args[2];
            var ze = (int)args[3];
            return DelChunkRange(new Vec2i(xs,zs), new Vec2i(xe,ze));
        }

        private TextCommandResult OnCmdTree(TextCommandCallingArgs args)
        {
            var asset = args[0] as string;
            var size = (float)args[1];
            var aheadoffset = (float)args[2];
            var player = args.Caller.Player as IServerPlayer;
            return TestTree(player, asset, size, aheadoffset);
        }

        private TextCommandResult OnCmdTreelineup(TextCommandCallingArgs args)
        {
            var asset = args[0] as string;
            var player = args.Caller.Player as IServerPlayer;
            return TreeLineup(player, asset);
        }

        private TextCommandResult OnCmdGenmapClimate(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            BlockPos pos = args.Caller.Entity.ServerPos.XYZ.AsBlockPos;
            int noiseSizeClimate = api.WorldManager.RegionSize / TerraGenConfig.climateMapScale;

            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;
            int startX = regionX * noiseSizeClimate - 256;
            int startZ = regionZ * noiseSizeClimate - 256;
            var genmapsSys = api.ModLoader.GetModSystem<GenMaps>();
            genmapsSys.initWorldGen();
            MapLayerBase climateGen = genmapsSys.climateGen;
            if (!args.Parsers[0].IsMissing)
            {
                float fac = (float)args[0];
                (((climateGen as MapLayerPerlinWobble).parent as MapLayerClimate).noiseMap as NoiseClimateRealistic).GeologicActivityStrength = fac;
                climateGen.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, startX, startZ, "climatemap-" + fac);

                NoiseBase.Debug = false;
                return TextCommandResult.Success("Geo activity map generated");
            }

            climateGen.DebugDrawBitmap(DebugDrawMode.RGB, startX, startZ, "climatemap");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Climate map generated");
        }

        private TextCommandResult OnCmdGenmapForest(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            var genmapsSys = api.ModLoader.GetModSystem<GenMaps>();
            genmapsSys.initWorldGen();
            MapLayerBase forestGen = genmapsSys.forestGen;

            BlockPos pos = args.Caller.Entity.ServerPos.XYZ.AsBlockPos;
            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;

            int noiseSizeForest = api.WorldManager.RegionSize / TerraGenConfig.forestMapScale;

            int startX = regionX * noiseSizeForest - 256;
            int startZ = regionZ * noiseSizeForest - 256;
            forestGen.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, startX, startZ, "forestmap");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Forest map generated");
        }

        private TextCommandResult OnCmdGenmapUpheavel(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            var genmapsSys = api.ModLoader.GetModSystem<GenMaps>();
            genmapsSys.initWorldGen();

            BlockPos pos = args.Caller.Entity.ServerPos.XYZ.AsBlockPos;
            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;
            MapLayerBase upheavelGen = genmapsSys.upheavelGen;

            int noiseSizeUpheavel = api.WorldManager.RegionSize / TerraGenConfig.geoUpheavelMapScale;
            int startX = regionX * noiseSizeUpheavel - 256;
            int startZ = regionZ * noiseSizeUpheavel - 256;
            int size = (int)args[0];
            upheavelGen.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, startX, startZ, size, "upheavelmap");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Upheavel map generated");
        }

        private TextCommandResult OnCmdGenmapMushroom(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            var genmapsSys = api.ModLoader.GetModSystem<GenMaps>();
            genmapsSys.initWorldGen();

            BlockPos pos = args.Caller.Entity.ServerPos.XYZ.AsBlockPos;
            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;

            int noiseSizeForest = api.WorldManager.RegionSize / TerraGenConfig.forestMapScale;
            int startX = regionX * noiseSizeForest - 256;
            int startZ = regionZ * noiseSizeForest - 256;

            var gen = new MapLayerWobbled(api.World.Seed + 112897, 2, 0.9f, TerraGenConfig.forestMapScale, 4000, -3000);
            gen.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, startX, startZ, "mushroom");

            NoiseBase.Debug = false;
            return TextCommandResult.Success("Mushroom maps generated");
        }

        private TextCommandResult OnCmdGenmapOre(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            /*NoiseOre noiseOre = new NoiseOre(seed);
            MapLayerBase climate = GenMaps.GetOreMap(seed, noiseOre);

            climate.DebugDrawBitmap(0, 0, 0, 1024, "Ore 1 - Ore");
            return TextCommandResult.Success("ore map generated");*/
            NoiseBase.Debug = false;
            return TextCommandResult.Success();
        }

        private TextCommandResult OnCmdGenmapGprov(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            int noiseSizeGeoProv = api.WorldManager.RegionSize / TerraGenConfig.geoProvMapScale;
            var genmapsSys = api.ModLoader.GetModSystem<GenMaps>();
            genmapsSys.initWorldGen();
            MapLayerBase geologicprovinceGen = genmapsSys.geologicprovinceGen;

            BlockPos pos = args.Caller.Entity.ServerPos.XYZ.AsBlockPos;
            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;

            int startX = regionX * noiseSizeGeoProv - 256;
            int startZ = regionZ * noiseSizeGeoProv - 256;

            geologicprovinceGen.DebugDrawBitmap(DebugDrawMode.ProvinceRGB, startX, startZ, "gprovmap");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Province map generated");
        }

        private TextCommandResult OnCmdGenmapLandform(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;

            int noiseSizeLandform = api.WorldManager.RegionSize / TerraGenConfig.landformMapScale;
            var genmapsSys = api.ModLoader.GetModSystem<GenMaps>();
            genmapsSys.initWorldGen();
            MapLayerBase landformsGen = genmapsSys.landformsGen;

            BlockPos pos = args.Caller.Entity.ServerPos.XYZ.AsBlockPos;
            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;

            int startX = regionX * noiseSizeLandform - 256;
            int startZ = regionZ * noiseSizeLandform - 256;

            landformsGen.DebugDrawBitmap(DebugDrawMode.LandformRGB, startX, startZ, "landformmap");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Landforms map generated");
        }

        private TextCommandResult OnCmdGenmapOcean(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;

            var genmapsSys = api.ModLoader.GetModSystem<GenMaps>();
            genmapsSys.initWorldGen();
            MapLayerBase oceanGen = genmapsSys.oceanGen;

            BlockPos pos = args.Caller.Entity.ServerPos.XYZ.AsBlockPos;
            int regionX = pos.X / api.WorldManager.RegionSize;
            int regionZ = pos.Z / api.WorldManager.RegionSize;

            int noiseSizeOcean = api.WorldManager.RegionSize / TerraGenConfig.oceanMapScale;
            int startX = regionX * noiseSizeOcean - 256;
            int startZ = regionZ * noiseSizeOcean - 256;

            oceanGen.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, startX, startZ, "oceanmap");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Ocean map generated");
        }

        private TextCommandResult OnCmdStitch(TextCommandCallingArgs args)
        {
            BlockPos pos = args.Caller.Entity.Pos.AsBlockPos;
            IServerChunk serverchunk = api.WorldManager.GetChunk(pos);
            if (serverchunk == null)
            {
                return TextCommandResult.Success("Can't check here, beyond chunk boundaries!");
            }

            IMapRegion mapRegion = serverchunk.MapChunk.MapRegion;

            int regionX = pos.X / _regionSize;
            int regionZ = pos.Z / _regionSize;

            var climateGen = api.ModLoader.GetModSystem<GenMaps>().climateGen;

            NoiseBase.Debug = true;

            int size = mapRegion.ClimateMap.InnerSize;
            int stitchSize = size * 3;

            int[] stitchedMap = new int[stitchSize * stitchSize];

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    var map = OnMapRegionGen(regionX + dx, regionZ + dz, climateGen);
                    for (int px = 0; px < size; px++)
                    {
                        for (int py = 0; py < size; py++)
                        {
                            int col = map.GetUnpaddedInt(px, py);
                            int y = (dz+1) * size + py;
                            int x = (dx+1) * size + px;

                            stitchedMap[y * stitchSize + x] = col;
                        }
                    }
                }
            }

            NoiseBase.DebugDrawBitmap(DebugDrawMode.RGB, stitchedMap, stitchSize, "climated-3x3-stitch");
            NoiseBase.Debug = false;
            return TextCommandResult.Success();
        }

        private TextCommandResult OnCmdRegionsOre(TextCommandCallingArgs args)
        {
            var pos = args.Caller.Entity.Pos.AsBlockPos;
            var serverchunk = api.WorldManager.GetChunk(pos);
            if (serverchunk == null)
            {
                return TextCommandResult.Success("Can't check here, beyond chunk boundaries!");
            }

            var mapRegion = serverchunk.MapChunk.MapRegion;

            var regionX = pos.X / _regionSize;
            var regionZ = pos.Z / _regionSize;

            var radius = (int)args[0];

            NoiseBase.Debug = false;

            var type = args.Parsers[1].IsMissing ? "limonite" : args[1] as string;

            if (!mapRegion.OreMaps.ContainsKey(type))
            {
                return TextCommandResult.Success("Mapregion does not contain an ore map for ore " + type);
            }

            var oreMapSize = mapRegion.OreMaps[type].InnerSize;
            var len = (2*radius+1) * oreMapSize;
            var outPixels = new int[len * len];

            var depsys = api.ModLoader.GetModSystem<GenDeposits>();
            api.ModLoader.GetModSystem<GenDeposits>().initWorldGen();

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    mapRegion = api.World.BlockAccessor.GetMapRegion(regionX + dx, regionZ + dz);
                    if (mapRegion == null) {
                        continue;
                    }

                    mapRegion.OreMaps.Clear();

                    depsys.OnMapRegionGen(mapRegion, regionX + dx, regionZ + dz);

                    if (!mapRegion.OreMaps.ContainsKey(type))
                    {
                        return TextCommandResult.Success("Mapregion does not contain an ore map for ore " + type);
                    }

                    IntDataMap2D map = mapRegion.OreMaps[type];

                    int baseX = (dx + radius) * oreMapSize;
                    int baseZ = (dz + radius) * oreMapSize;

                    for (int px = 0; px < map.InnerSize; px++)
                    {
                        for (int pz = 0; pz < map.InnerSize; pz++)
                        {
                            int pixel = map.GetUnpaddedInt(px, pz);

                            outPixels[(pz + baseZ) * len + px + baseX] = pixel;
                        }
                    }
                }
            }

            NoiseBase.Debug = true;
            NoiseBase.DebugDrawBitmap(DebugDrawMode.RGB, outPixels, len, "ore-"+type+"around-" + regionX + "-" + regionZ);
            NoiseBase.Debug = false;
            return TextCommandResult.Success(type + " ore map generated.");
        }

        private TextCommandResult OnCmdRegionsClimate(TextCommandCallingArgs args)
        {
            var pos = args.Caller.Entity.Pos.AsBlockPos;
            var serverchunk = api.WorldManager.GetChunk(pos);
            if (serverchunk == null)
            {
                return TextCommandResult.Success("Can't check here, beyond chunk boundaries!");
            }

            var mapRegion = serverchunk.MapChunk.MapRegion;

            var regionX = pos.X / _regionSize;
            var regionZ = pos.Z / _regionSize;

            var radius = (int)args[0];

            NoiseBase.Debug = false;

            var oreMapSize = mapRegion.ClimateMap.InnerSize;
            var len = (2 * radius + 1) * oreMapSize;
            var outPixels = new int[len * len];

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    mapRegion = api.World.BlockAccessor.GetMapRegion(regionX + dx, regionZ + dz);
                    if (mapRegion == null)
                    {
                        continue;
                    }

                    IntDataMap2D map = mapRegion.ClimateMap;

                    int baseX = (dx + radius) * oreMapSize;
                    int baseZ = (dz + radius) * oreMapSize;

                    for (int px = 0; px < map.InnerSize; px++)
                    {
                        for (int pz = 0; pz < map.InnerSize; pz++)
                        {
                            int pixel = map.GetUnpaddedInt(px, pz);

                            outPixels[(pz + baseZ) * len + px + baseX] = pixel;
                        }
                    }
                }
            }

            NoiseBase.Debug = true;
            NoiseBase.DebugDrawBitmap(DebugDrawMode.RGB, outPixels, len, "climates-" + regionX + "-" + regionZ + "-" + radius);
            NoiseBase.Debug = false;
            return TextCommandResult.Success("climate map generated.");
        }

        private TextCommandResult OnCmdRegionsUpheavel(TextCommandCallingArgs args)
        {
            var pos = args.Caller.Entity.Pos.AsBlockPos;
            var serverchunk = api.WorldManager.GetChunk(pos);
            if (serverchunk == null)
            {
                return TextCommandResult.Success("Can't check here, beyond chunk boundaries!");
            }

            var mapRegion = serverchunk.MapChunk.MapRegion;

            var regionX = pos.X / _regionSize;
            var regionZ = pos.Z / _regionSize;

            var radius = (int)args[0];

            NoiseBase.Debug = false;

            var oreMapSize = mapRegion.UpheavelMap.InnerSize;
            var len = (2 * radius + 1) * oreMapSize;
            var outPixels = new int[len * len];

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    mapRegion = api.World.BlockAccessor.GetMapRegion(regionX + dx, regionZ + dz);
                    if (mapRegion == null)
                    {
                        continue;
                    }

                    IntDataMap2D map = mapRegion.UpheavelMap;

                    int baseX = (dx + radius) * oreMapSize;
                    int baseZ = (dz + radius) * oreMapSize;

                    for (int px = 0; px < map.InnerSize; px++)
                    {
                        for (int pz = 0; pz < map.InnerSize; pz++)
                        {
                            int pixel = map.GetUnpaddedInt(px, pz);

                            outPixels[(pz + baseZ) * len + px + baseX] = pixel;
                        }
                    }
                }
            }

            NoiseBase.Debug = true;
            NoiseBase.DebugDrawBitmap(DebugDrawMode.RGB, outPixels, len, "upheavels-" + regionX + "-" + regionZ + "-" + radius);
            NoiseBase.Debug = false;
            return TextCommandResult.Success("upheavel map generated.");
        }

        private TextCommandResult OnCmdPos(TextCommandCallingArgs args)
        {

            var chunkSize = GlobalConstants.ChunkSize;
            var player = args.Caller.Player as IServerPlayer;
            BlockPos pos = args.Caller.Entity.Pos.AsBlockPos;
            IServerChunk serverchunk = api.WorldManager.GetChunk(pos);
            if (serverchunk == null)
            {
                return TextCommandResult.Success("Can't check here, beyond chunk boundaries!");
            }

            IMapRegion mapRegion = serverchunk.MapChunk.MapRegion;
            IMapChunk mapchunk = serverchunk.MapChunk;

            int regionChunkSize = api.WorldManager.RegionSize / chunkSize;

            int lx = pos.X % chunkSize;
            int lz = pos.Z % chunkSize;
            int chunkX = pos.X / chunkSize;
            int chunkZ = pos.Z / chunkSize;
            int regionX = pos.X / _regionSize;
            int regionZ = pos.Z / _regionSize;

            string subcmd = args[0] as string;

            switch (subcmd)
            {
                case "ymax":
                    return TextCommandResult.Success(string.Format("YMax: {0}", serverchunk.MapChunk.YMax));
                case "coords":
                    return TextCommandResult.Success(string.Format("Chunk X/Z: {0}/{1}, Region X/Z: {2},{3}", chunkX, chunkZ, regionX, regionZ));

                case "latitude":
                    double? lat = api.World.Calendar.OnGetLatitude(pos.Z);
                    return TextCommandResult.Success(string.Format("Latitude: {0:0.##}°, {1}", lat * 90, lat < 0 ? "Southern Hemisphere" : "Northern Hemisphere"));

                case "structures":
                    bool found = false;
                    api.World.BlockAccessor.WalkStructures(pos, (struc) =>
                    {
                        found = true;
                        player.SendMessage(args.Caller.FromChatGroupId,"Structure with code " + struc.Code + " at this position", EnumChatType.CommandSuccess);
                    });

                    if (!found)
                    {
                        return TextCommandResult.Success("No structures at this position");
                    }
                    break;

                case "height":
                    {
                        string str = string.Format("Rain y={0}, Worldgen terrain y={1}", serverchunk.MapChunk.RainHeightMap[lz * chunkSize + lx], serverchunk.MapChunk.WorldGenTerrainHeightMap[lz * chunkSize + lx]);
                        player.SendMessage(args.Caller.FromChatGroupId, str, EnumChatType.CommandSuccess);
                    }
                    break;


                case "cavedistort":
                    SKBitmap bmp = new SKBitmap(chunkSize, chunkSize);

                    for (int x = 0; x < chunkSize; x++)
                    {
                        for (int z = 0; z < chunkSize; z++)
                        {
                            byte color = mapchunk.CaveHeightDistort[z * chunkSize + x];
                            bmp.SetPixel(x, z, new SKColor((byte)((color >> 16) & 0xFF), (byte)((color >> 8) & 0xFF), (byte)(color & 0xFF)));
                        }
                    }

                    bmp.Save("cavedistort"+chunkX+"-"+chunkZ+".png");
                    player.SendMessage(args.Caller.FromChatGroupId, "saved bitmap cavedistort" + chunkX + "-" + chunkZ + ".png", EnumChatType.CommandSuccess);
                    break;


                case "gprov":
                    {
                        int noiseSizeGeoProv = mapRegion.GeologicProvinceMap.InnerSize;

                        float posXInRegion = ((float)pos.X / _regionSize - regionX) * noiseSizeGeoProv;
                        float posZInRegion = ((float)pos.Z / _regionSize - regionZ) * noiseSizeGeoProv;
                        GeologicProvinceVariant[] provincesByIndex = NoiseGeoProvince.provinces.Variants;
                        IntDataMap2D intmap = mapRegion.GeologicProvinceMap;
                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(intmap.Data, mapRegion.GeologicProvinceMap.Size, TerraGenConfig.geoProvSmoothingRadius, mapRegion.GeologicProvinceMap.TopLeftPadding, mapRegion.GeologicProvinceMap.BottomRightPadding);
                        WeightedIndex[] indices = map[posXInRegion, posZInRegion];

                        string text = "";
                        foreach (WeightedIndex windex in indices)
                        {
                            if (text.Length > 0)
                                text += ", ";

                            text += (100 * windex.Weight).ToString("#.#") + "% " + provincesByIndex[windex.Index].Code;
                        }

                        player.SendMessage(args.Caller.FromChatGroupId, text, EnumChatType.CommandSuccess);

                        break;
                    }


                case "rockstrata":
                    {
                        GenRockStrataNew rockstratagen = api.ModLoader.GetModSystem<GenRockStrataNew>();

                        int noiseSizeGeoProv = mapRegion.GeologicProvinceMap.InnerSize;
                        float posXInRegion = ((float)pos.X / _regionSize - pos.X / _regionSize) * noiseSizeGeoProv;
                        float posZInRegion = ((float)pos.Z / _regionSize - pos.Z / _regionSize) * noiseSizeGeoProv;
                        GeologicProvinceVariant[] provincesByIndex = NoiseGeoProvince.provinces.Variants;
                        IntDataMap2D intmap = mapRegion.GeologicProvinceMap;
                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(intmap.Data, mapRegion.GeologicProvinceMap.Size, TerraGenConfig.geoProvSmoothingRadius, mapRegion.GeologicProvinceMap.TopLeftPadding, mapRegion.GeologicProvinceMap.BottomRightPadding);
                        WeightedIndex[] indices = map[posXInRegion, posZInRegion];

                        float[] rockGroupMaxThickness = new float[4];

                        rockGroupMaxThickness[0] = rockGroupMaxThickness[1] = rockGroupMaxThickness[2] = rockGroupMaxThickness[3] = 0;

                        int rdx = chunkX % regionChunkSize;
                        int rdz = chunkZ % regionChunkSize;
                        IntDataMap2D rockMap;
                        float step = 0;
                        float distx = (float)rockstratagen.distort2dx.Noise(pos.X, pos.Z);
                        float distz = (float)rockstratagen.distort2dz.Noise(pos.X, pos.Z);



                        for (int i = 0; i < indices.Length; i++)
                        {
                            float w = indices[i].Weight;

                            GeologicProvinceVariant var = NoiseGeoProvince.provinces.Variants[indices[i].Index];

                            rockGroupMaxThickness[0] += var.RockStrataIndexed[0].ScaledMaxThickness * w;
                            rockGroupMaxThickness[1] += var.RockStrataIndexed[1].ScaledMaxThickness * w;
                            rockGroupMaxThickness[2] += var.RockStrataIndexed[2].ScaledMaxThickness * w;
                            rockGroupMaxThickness[3] += var.RockStrataIndexed[3].ScaledMaxThickness * w;
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


                        API.Datastructures.OrderedDictionary<int, int> stratathicknesses = new ();

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


                        player.SendMessage(args.Caller.FromChatGroupId, sb.ToString(), EnumChatType.CommandSuccess);

                        break;
                    }


                case "landform":
                    {
                        int noiseSizeLandform = mapRegion.LandformMap.InnerSize;

                        float posXInRegion = ((float)pos.X / _regionSize - pos.X / _regionSize) * noiseSizeLandform;
                        float posZInRegion = ((float)pos.Z / _regionSize - pos.Z / _regionSize) * noiseSizeLandform;


                        LandformVariant[] landforms = NoiseLandforms.landforms.LandFormsByIndex;

                        IntDataMap2D intmap = mapRegion.LandformMap;

                        LerpedWeightedIndex2DMap map = new LerpedWeightedIndex2DMap(intmap.Data, mapRegion.LandformMap.Size, TerraGenConfig.landFormSmoothingRadius, intmap.TopLeftPadding, intmap.BottomRightPadding);

                        WeightedIndex[] indices = map[posXInRegion, posZInRegion];

                        string text = "";
                        foreach (WeightedIndex windex in indices)
                        {
                            if (text.Length > 0)
                                text += ", ";

                            text += (100 * windex.Weight).ToString("#.#") + "% " + landforms[windex.Index].Code.ToShortString();
                        }

                        player.SendMessage(args.Caller.FromChatGroupId, text, EnumChatType.CommandSuccess);

                        break;
                    }

                case "climate":
                    {
                        ClimateCondition climate = api.World.BlockAccessor.GetClimateAt(pos);

                        string text = string.Format(
                            "Temperature: {0}°C, Year avg: {1}°C, Avg. Rainfall: {2}%, Geologic Activity: {3}%, Fertility: {4}%, Forest: {5}%, Shrub: {6}%, Sealevel dist: {7}%, Season: {8}, Hemisphere: {9}",
                            climate.Temperature.ToString("0.#"),
                            climate.WorldGenTemperature.ToString("0.#"),
                            (int)(climate.WorldgenRainfall * 100f),
                            (int)(climate.GeologicActivity * 100),
                            (int)(climate.Fertility * 100f),
                            (int)(climate.ForestDensity * 100f), (int)(climate.ShrubDensity * 100f), (int)(100f * pos.Y / 255f),
                            api.World.Calendar.GetSeason(pos),
                            api.World.Calendar.GetHemisphere(pos)
                        );

                        player.SendMessage(args.Caller.FromChatGroupId, text, EnumChatType.CommandSuccess);


                        break;
                    }


            }
            return TextCommandResult.Success();
        }

        private TextCommandResult OnCmdTestnoise(TextCommandCallingArgs args)
        {
            bool use3d = false;
            int octaves = (int)args[0];

            Random rnd = new Random();
            long seed = rnd.Next();

            NormalizedSimplexNoise noise = NormalizedSimplexNoise.FromDefaultOctaves(octaves, 5, 0.7, seed);
            int size = 800;
            SKBitmap bitmap = new SKBitmap(size, size);

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

                    byte light = (byte)(value * 255);
                    bitmap.SetPixel(x, y, new SKColor(light, light, light,255));
                }
            }

            bitmap.Save("noise.png");
            var msg = (use3d ? "3D" : "2D") + " Noise (" + octaves + " Octaves) saved to noise.png. Overflows: " +
                      overflows + ", Underflows: " + underflows;
            msg += "\nNoise min = " + min.ToString("0.##") + ", max= " + max.ToString("0.##");
            // player.SendMessage(groupId, (use3d ? "3D" : "2D") + " Noise (" + octaves + " Octaves) saved to noise.png. Overflows: " + overflows + ", Underflows: " + underflows, EnumChatType.CommandSuccess);
            // player.SendMessage(groupId, "\nNoise min = " + min.ToString("0.##") +", max= " + max.ToString("0.##"), EnumChatType.CommandSuccess);
            return TextCommandResult.Success(msg);
        }

        private TextCommandResult OnCmdClimate(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            NoiseClimatePatchy noiseClimate = new NoiseClimatePatchy(_seed);
            MapLayerBase climate = GenMaps.GetClimateMapGen(_seed, noiseClimate);
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Patchy climate map generated");
        }

        private TextCommandResult OnCmdGeoact(TextCommandCallingArgs args)
        {

            var worldConfig = api.WorldManager.SaveGame.WorldConfiguration;
            var polarEquatorDistance = worldConfig.GetString("polarEquatorDistance", "50000").ToInt(50000);

            var size = (int)args[0];
            var spawnMinTemp = 6;
            var spawnMaxTemp = 14;
            NoiseBase.Debug = true;
            NoiseClimateRealistic noiseClimate = new NoiseClimateRealistic(_seed, api.World.BlockAccessor.MapSizeZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale, polarEquatorDistance, spawnMinTemp, spawnMaxTemp);
            MapLayerBase climate = GenMaps.GetClimateMapGen(_seed, noiseClimate);

            NoiseBase.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, climate.GenLayer(0, 0, 128, 2048), 128, size, "geoactivity");

            return TextCommandResult.Success("Geologic activity map generated");
        }

        private TextCommandResult OnCmdClimater(TextCommandCallingArgs args)
        {
            var worldConfig = api.WorldManager.SaveGame.WorldConfiguration;
            int polarEquatorDistance = worldConfig.GetString("polarEquatorDistance", "50000").ToInt(50000);

            int spawnMinTemp = 6;
            int spawnMaxTemp = 14;

            string startingClimate = worldConfig.GetString("worldClimate", "realistic");
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

            NoiseBase.Debug = true;
            NoiseClimateRealistic noiseClimate = new NoiseClimateRealistic(_seed, api.World.BlockAccessor.MapSizeZ / TerraGenConfig.climateMapScale / TerraGenConfig.climateMapSubScale, polarEquatorDistance, spawnMinTemp, spawnMaxTemp);
            MapLayerBase climate = GenMaps.GetClimateMapGen(_seed, noiseClimate);

            NoiseBase.DebugDrawBitmap(DebugDrawMode.RGB, climate.GenLayer(0, 0, 128, 2048), 128, 2048, "realisticlimate");
            NoiseBase.Debug = false;

            return TextCommandResult.Success("Realistic climate map generated");
        }

        private TextCommandResult OnCmdForest(TextCommandCallingArgs args)
        {

            NoiseClimatePatchy noiseClimate = new NoiseClimatePatchy(_seed);
            MapLayerBase climate = GenMaps.GetClimateMapGen(_seed, noiseClimate);
            MapLayerBase forest = GenMaps.GetForestMapGen(_seed + 1, TerraGenConfig.forestMapScale);

            IntDataMap2D climateMap = new IntDataMap2D() { Data = climate.GenLayer(0, 0, 512, 512), Size = 512 };

            forest.SetInputMap(climateMap, new IntDataMap2D() { Size = 512 });

            NoiseBase.Debug = true;
            forest.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, 0, 0, "Forest 1 - Forest");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Forest map generated");
        }

        private TextCommandResult OnCmdUpheavel(TextCommandCallingArgs args)
        {
            var size = (int)args[0];
            var map = GenMaps.GetGeoUpheavelMapGen(_seed + 873, TerraGenConfig.geoUpheavelMapScale);
            NoiseBase.Debug = true;
            map.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, 0, 0, size, "Geoupheavel 1");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Geo upheavel map generated");
        }

        private TextCommandResult OnCmdOcean(TextCommandCallingArgs args)
        {

            var worldConfig = api.WorldManager.SaveGame.WorldConfiguration;
            var size = (int)args[0];
            float landcover = worldConfig.GetString("landcover", "1").ToFloat(1f);
            float oceanscale = worldConfig.GetString("oceanscale", "1").ToFloat(1f);

            var chunkSize = GlobalConstants.ChunkSize;

            var modSystem = api.ModLoader.GetModSystem<GenMaps>();
            var list = modSystem.requireLandAt;

            var startX = 0;
            var startZ = 0;
            if(args.Caller.Player != null)
            {
                startX = (int)args.Caller.Player.Entity.Pos.X / chunkSize;
                startZ = (int)args.Caller.Player.Entity.Pos.Z / chunkSize;
            }
            var requiresSpawnOffset = GameVersion.IsLowerVersionThan(api.WorldManager.SaveGame.CreatedGameVersion, "1.20.0-pre.14");
            var map = GenMaps.GetOceanMapGen(_seed + 1873, landcover, TerraGenConfig.oceanMapScale, oceanscale, list, requiresSpawnOffset);
            NoiseBase.Debug = true;
            map.DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, startX, startZ, size, "Ocean 1-"+startX+"-"+startZ);
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Ocean map generated");
        }

        private TextCommandResult OnCmdOre(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            NoiseOre noiseOre = new NoiseOre(_seed);

            var scaleMul = (float)args[0];
            var contrast = (float)args[1];
            var sub = (float)args[2];

            MapLayerBase oremap = GenMaps.GetOreMap(_seed, noiseOre, scaleMul, contrast, sub);
            //climate.DebugDrawBitmap(DebugDrawMode.RGB, 0, 0, 1024, "Ore 1 - Ore");
            NoiseBase.Debug = false;
            return TextCommandResult.Success("ore map generated");
        }

        private TextCommandResult OnCmdOretopdistort(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            NoiseBase topdistort = GenMaps.GetDepositVerticalDistort(_seed);
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Ore top distort map generated");
        }

        private TextCommandResult OnCmdWind(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            NoiseBase wind = GenMaps.GetDebugWindMap(_seed);
            NoiseBase.Debug = false;
            return TextCommandResult.Success("Wind map generated");
        }

        private TextCommandResult OnCmdGprov(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            MapLayerBase provinces = GenMaps.GetGeologicProvinceMapGen(_seed, api);
            NoiseBase.Debug = false;

            return TextCommandResult.Success("Province map generated");
        }

        private TextCommandResult OnCmdLandform(TextCommandCallingArgs args)
        {
            var worldConfig = api.WorldManager.SaveGame.WorldConfiguration;
            NoiseBase.Debug = true;
            NoiseClimatePatchy noiseClimate = new NoiseClimatePatchy(_seed);
            float landformScale = worldConfig.GetString("landformScale", "1").ToFloat(1f);
            MapLayerBase landforms = GenMaps.GetLandformMapGen(_seed + 1, noiseClimate, api, landformScale);
            NoiseBase.Debug = false;

            return TextCommandResult.Success("Landforms map generated");
        }

        private TextCommandResult OnCmdRockstrata(TextCommandCallingArgs args)
        {
            NoiseBase.Debug = true;
            GenRockStrataNew mod = api.ModLoader.GetModSystem<GenRockStrataNew>();
            for (int i = 0; i < mod.strataNoises.Length; i++)
            {
                mod.strataNoises[i].DebugDrawBitmap(DebugDrawMode.FirstByteGrayscale, 0, 0, "Rockstrata-" + mod.strata.Variants[i].BlockCode.ToShortString().Replace(":", "-"));
            }
            NoiseBase.Debug = false;

            return TextCommandResult.Success("Rockstrata maps generated");
        }

        private TextCommandResult OnCmdTreemap(TextCommandCallingArgs args)
        {
            int chs = 3;

            //var asset = api.Assets.TryGet(new AssetLocation("textures/environment/planttint.png"));
            //BitmapExternal bmpt = new BitmapExternal(asset.Data, asset.Data.Length, api.Logger);
            //int[] tintPixels = new int[bmpt.Width * bmpt.Height * chs];
            //bmpt.bmp.SetPixels(tintPixels);


            byte[] pixels = new byte[256 * 512 * chs];
            int w = 256;
            // int tw = 264;
            for (int x = 0; x < 256; x++)
            {
                for (int y = 0; y < 256; y++)
                {
                    pixels[(y * w + x) * chs + 0] = 255;// tintPixels[((y + 4) * tw + x + y) * chs];
                    pixels[(y * w + x) * chs + 1] = 255;// tintPixels[((y + 4) * tw + x + y) * chs + 1];
                    pixels[(y * w + x) * chs + 2] = 255;// tintPixels[((y + 4) * tw + x + y) * chs + 2];
                }
            }


            var treeSupplier = new WgenTreeSupplier(api);
            treeSupplier.LoadTrees();
            TreeVariant[] gens = treeSupplier.treeGenProps.TreeGens;

            Random rnd = new Random(123);

            int[] colors = new int[gens.Length];
            for (int i = 0; i < colors.Length;i++)
            {
                colors[i] = rnd.Next() | (128 << 24);
            }

            /*for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    int color = 0;
                    int rain = y;
                    int unscaledTemp = x;
                    int temp = Climate.GetScaledAdjustedTemperature(unscaledTemp, 0);
                    float heightRel = 0;
                    int fertility = Climate.GetFertility(rain, temp, heightRel);

                    float fertDist, rainDist, tempDist, forestDist, heightDist;

                    for (int i = 0; i < gens.Length; i++)
                    {
                        TreeVariant variant = gens[i];

                        fertDist = Math.Abs(fertility - variant.FertMid) / variant.FertRange;
                        rainDist = Math.Abs(rain - variant.RainMid) / variant.RainRange;
                        tempDist = Math.Abs(temp - variant.TempMid) / variant.TempRange;
                        forestDist = 0;
                        heightDist = 0;


                        double distSq =
                            Math.Max(0, 1.2f * fertDist * fertDist - 1) +
                            Math.Max(0, 1.2f * rainDist * rainDist - 1) +
                            Math.Max(0, 1.2f * tempDist * tempDist - 1) +
                            Math.Max(0, 1.2f * forestDist * forestDist - 1) +
                            Math.Max(0, 1.2f * heightDist * heightDist - 1)
                        ;

                        if (distSq < 1)
                        {
                            int overColor = colors[i];
                            color = ColorUtil.ColorOver(color, overColor);
                        }
                    }

                    int col = ColorUtil.ColorOver(
                        pixels[(y * 512 + x) * 4 + 0] | (pixels[(y * 512 + x) * 4 + 1] << 8) | (pixels[(y * 512 + x) * 4 + 2] << 16) | (pixels[(y * 512 + x) * 4 + 3] << 24),
                        color
                    );

                    pixels[(y * 512 + x) * 4 + 0] = (byte)(col & 0xff);
                    pixels[(y * 512 + x) * 4 + 1] = (byte)(((col >> 8) & 0xff) << 8);
                    pixels[(y * 512 + x) * 4 + 2] = (byte)(((col >> 16) & 0xff) << 16);
                }
            }*/

            ImageSurface surface = (ImageSurface)ImageSurface.CreateForImage(pixels, Format.Rgb24, 256, 512);
            Context ctx = new Context(surface);

            //ctx.MoveTo((int)0, 300);
            //ctx.ShowText("test");

            surface.WriteToPng("treecoveragemap.png");

            ctx.Dispose();
            surface.Dispose();
            return TextCommandResult.Success("treecoveragemap.png created.");
        }

        private TextCommandResult DelChunkRange(Vec2i start,Vec2i end)
        {
            for (int x = start.X; x <= end.X; x++)
            {
                for (int z = start.Y; z <= end.Y; z++)
                {
                    api.WorldManager.DeleteChunkColumn(x, z);
                }
            }

            return TextCommandResult.Success("Ok, chunk deletions enqueued, might take a while to process. Run command without args to see queue size");
        }

        private void DelRock(Caller caller, int rad, bool aroundPlayer = false)
        {
            var player = caller.Player as IServerPlayer;
            player.SendMessage(caller.FromChatGroupId, "Deleting rock, this may take a while...", EnumChatType.CommandError);

            int chunkMidX = api.WorldManager.MapSizeX / GlobalConstants.ChunkSize / 2;
            int chunkMidZ = api.WorldManager.MapSizeZ / GlobalConstants.ChunkSize / 2;

            if (aroundPlayer)
            {
                chunkMidX = (int)player.Entity.Pos.X / GlobalConstants.ChunkSize;
                chunkMidZ = (int)player.Entity.Pos.Z / GlobalConstants.ChunkSize;
            }

            List<Vec2i> coords = new List<Vec2i>();

            for (int x = -rad; x <= rad; x++)
            {
                for (int z = -rad; z <= rad; z++)
                {
                    coords.Add(new Vec2i(chunkMidX + x, chunkMidZ + z));
                }
            }

            int chunksize = GlobalConstants.ChunkSize;

            IList<Block> blocks = api.World.Blocks;

            foreach (Vec2i coord in coords)
            {
                for (int cy = 0; cy < api.WorldManager.MapSizeY / GlobalConstants.ChunkSize; cy++)
                {
                    IServerChunk chunk = api.WorldManager.GetChunk(coord.X, cy, coord.Y);
                    if (chunk == null) continue;

                    chunk.Unpack();
                    for (int i = 0; i < chunk.Data.Length; i++)
                    {
                        Block block = blocks[chunk.Data[i]];
                        if (block.BlockMaterial == EnumBlockMaterial.Stone || block.BlockMaterial == EnumBlockMaterial.Liquid || block.BlockMaterial == EnumBlockMaterial.Soil)
                        {
                            chunk.Data[i] = 0;
                        }
                    }
                    chunk.MarkModified();
                }
                api.WorldManager.FullRelight(new BlockPos(coord.X * chunksize, 0 * chunksize, coord.Y * chunksize), new BlockPos(coord.X * chunksize, api.WorldManager.MapSizeY, coord.Y * chunksize));
            }
            player.CurrentChunkSentRadius = 0;
        }

        private TextCommandResult PregenerateChunksAroundPlayer(Caller caller, int range)
        {
            int chunkMidX;
            int chunkMidZ;
            if (caller.Type == EnumCallerType.Console)
            {
                chunkMidX = api.WorldManager.MapSizeX / GlobalConstants.ChunkSize / 2;
                chunkMidZ = api.WorldManager.MapSizeX / GlobalConstants.ChunkSize / 2;
            }
            else
            {
                var player = caller.Player as IServerPlayer;
                chunkMidX = (int)player.Entity.Pos.X / GlobalConstants.ChunkSize;
                chunkMidZ = (int)player.Entity.Pos.Z / GlobalConstants.ChunkSize;
            }

            List<Vec2i> coords = new List<Vec2i>();


            for (int x = -range; x <= range; x++)
            {
                for (int z = -range; z <= range; z++)
                {
                    coords.Add(new Vec2i(chunkMidX + x, chunkMidZ + z));
                }
            }

            LoadColumnsSlow(caller, coords, 0);
            return TextCommandResult.Success("Type /debug chunk queue to see current generating queue size");
        }

        private void LoadColumnsSlow(Caller caller, List<Vec2i> coords, int startIndex)
        {
            int qadded = 0;
            var player = caller.Player as IServerPlayer;
            if (api.WorldManager.CurrentGeneratingChunkCount < 10)
            {
                int batchSize = 200;

                for (int i = startIndex; i < coords.Count; i++)
                {
                    qadded++;
                    startIndex++;
                    Vec2i coord = coords[i];
                    api.WorldManager.LoadChunkColumn(coord.X, coord.Y);

                    if (qadded > batchSize)
                    {
                        break;
                    }
                }
                if (caller.Type == EnumCallerType.Console)
                {
                    api.Logger.Notification("Ok, added {0} columns, {1} left to add, waiting until these are done.", qadded, coords.Count - startIndex);
                }
                else
                {
                    player.SendMessage(caller.FromChatGroupId, string.Format("Ok, added {0} columns, {1} left to add, waiting until these are done.", qadded, coords.Count - startIndex), EnumChatType.CommandSuccess);
                }
            }

            if (startIndex < coords.Count)
            {
                api.World.RegisterCallback((dt) => LoadColumnsSlow(caller, coords, startIndex), 1000);
            } else
            {
                if (caller.Type == EnumCallerType.Console)
                {
                    api.Logger.Notification("Ok, {0} columns, generated!", coords.Count);
                }
                else
                {
                    player.SendMessage(caller.FromChatGroupId, string.Format("Ok, {0} columns, generated!", coords.Count), EnumChatType.CommandSuccess);
                }
            }
        }

        private TextCommandResult RegenChunks(Caller caller,int range, string landform = null, bool aroundPlayer = false, bool randomSeed = false, bool deleteRegion = false)
        {
            var seedDiff = 0;
            TextCommandResult msg;
            var player = caller.Player as IServerPlayer;
            if (randomSeed)
            {
                seedDiff = api.World.Rand.Next(100000);
                player.SendMessage(GlobalConstants.CurrentChatGroup, "Using random seed diff " + seedDiff,
                    EnumChatType.Notification);
            }

            player.SendMessage(GlobalConstants.CurrentChatGroup, "Waiting for chunk thread to pause...", EnumChatType.Notification);

            if (api.Server.PauseThread("chunkdbthread"))
            {
                api.Assets.Reload(new AssetLocation("worldgen/"));
                var patchLoader = api.ModLoader.GetModSystem<ModJsonPatchLoader>();
                patchLoader.ApplyPatches("worldgen/");

                NoiseLandforms.LoadLandforms(api);

                api.Event.TriggerInitWorldGen();

                msg = Regen(caller, range, false,landform, aroundPlayer, deleteRegion);
            } else
            {
                msg = TextCommandResult.Success("Unable to regenerate chunks. Was not able to pause the chunk gen thread");
            }

            api.Server.ResumeThread("chunkdbthread");
            return msg;
        }

        TextCommandResult Regen(Caller caller, int rad, bool onlydelete, string landforms, bool aroundPlayer = false, bool deleteRegion = false)
        {
            int chunkMidX = api.WorldManager.MapSizeX / GlobalConstants.ChunkSize / 2;
            int chunkMidZ = api.WorldManager.MapSizeZ / GlobalConstants.ChunkSize / 2;
            var player = caller.Player as IServerPlayer;
            if (aroundPlayer)
            {
                chunkMidX = (int)player.Entity.Pos.X / GlobalConstants.ChunkSize;
                chunkMidZ = (int)player.Entity.Pos.Z / GlobalConstants.ChunkSize;
            }

            List<Vec2i> coords = new List<Vec2i>();
            HashSet<Vec2i> regCoords = new HashSet<Vec2i>();

            int regionChunkSize = api.WorldManager.RegionSize / GlobalConstants.ChunkSize;
            for (int x = -rad; x <= rad; x++)
            {
                for (int z = -rad; z <= rad; z++)
                {
                    coords.Add(new Vec2i(chunkMidX + x, chunkMidZ + z));
                    regCoords.Add(new Vec2i((chunkMidX + x) / regionChunkSize, (chunkMidZ + z) / regionChunkSize));
                }
            }

            var modSys = api.ModLoader.GetModSystem<GenStoryStructures>();

            TreeAttribute tree = null;
            if (deleteRegion && !onlydelete)
            {
                Dictionary<long, List<GeneratedStructure>> regionStructures = new();
                var chunkSize = GlobalConstants.ChunkSize;
                foreach (Vec2i coord in coords)
                {
                    var regionIndex = api.WorldManager.MapRegionIndex2D(coord.X / regionChunkSize, coord.Y / regionChunkSize);
                    var mapRegion = api.WorldManager.GetMapRegion(regionIndex);
                    if (mapRegion?.GeneratedStructures.Count > 0)
                    {
                        // only adds the region once
                        regionStructures.TryAdd(regionIndex, mapRegion.GeneratedStructures);

                        // remove the structures from each chunk that will be regenerated
                        var structures = mapRegion.GeneratedStructures.Where(s =>
                            coord.X == s.Location.X1 / chunkSize &&
                            coord.Y == s.Location.Z1 / chunkSize).ToList();

                        foreach (var structure in structures)
                        {
                            var location = modSys.GetStoryStructureAt(structure.Location.X1, structure.Location.Z1);
                            if (location != null && modSys.storyStructureInstances.TryGetValue(location.Code, out var structureInstance))
                            {
                                if (structure.Group != null && structureInstance.SchematicsSpawned?.TryGetValue(structure.Group, out var spawned) == true)
                                {
                                    structureInstance.SchematicsSpawned[structure.Group] = Math.Max(0, spawned - 1);
                                }
                            }
                        }
                        regionStructures[regionIndex].RemoveAll(s => structures.Contains(s));
                    }
                }

                tree = new TreeAttribute();
                tree.SetBytes("GeneratedStructures", SerializerUtil.Serialize(regionStructures));
            }

            foreach (Vec2i coord in coords)
            {
                api.WorldManager.DeleteChunkColumn(coord.X, coord.Y);
            }
            if (deleteRegion)
            {
                foreach (Vec2i coord in regCoords)
                {
                    api.WorldManager.DeleteMapRegion(coord.X, coord.Y);
                }
            }

            if (!onlydelete)
            {
                if (landforms != null)
                {
                    tree ??= new TreeAttribute();

                    var list = NoiseLandforms.landforms.LandFormsByIndex;
                    int index = -1;
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (list[i].Code.Path.Equals(landforms))
                        {
                            index = i;
                            break;
                        }
                    }
                    if (index < 0)
                    {
                        return TextCommandResult.Success("No such landform exists");
                    }

                    tree.SetInt("forceLandform", index);
                }

                // so that resends arrive after all deletes
                int leftToLoad = coords.Count;
                bool sent = false;
                api.WorldManager.SendChunks = false;

                foreach (Vec2i coord in coords)
                {
                    api.WorldManager.LoadChunkColumnPriority(coord.X, coord.Y, new ChunkLoadOptions()
                    {
                        OnLoaded = () => {
                            leftToLoad--;

                            if (leftToLoad <= 0 && !sent)
                            {
                                modSys.FinalizeRegeneration(chunkMidX, chunkMidZ);
                                sent = true;
                                player.SendMessage(caller.FromChatGroupId, "Regen complete", EnumChatType.CommandSuccess);

                                player.CurrentChunkSentRadius = 0;
                                api.WorldManager.SendChunks = true;

                                foreach (Vec2i ccoord in coords)
                                {
                                    for (int cy = 0; cy < api.WorldManager.MapSizeY / GlobalConstants.ChunkSize; cy++)
                                    {
                                        api.WorldManager.BroadcastChunk(ccoord.X, cy, ccoord.Y, true);
                                    }
                                }
                            }
                        },
                        ChunkGenParams = tree
                    });
                }
            }
            else
            {
                if (!deleteRegion)
                {
                    // when only deleting chunks we delete all structures from the mapregion
                    const int chunkSize = GlobalConstants.ChunkSize;
                    foreach (Vec2i coord in coords)
                    {
                        var regionIndex = api.WorldManager.MapRegionIndex2D(coord.X / regionChunkSize, coord.Y / regionChunkSize);
                        var mapRegion = api.WorldManager.GetMapRegion(regionIndex);
                        if (mapRegion?.GeneratedStructures.Count > 0)
                        {
                            // remove the structures from each chunk that will be deleted
                            var generatedStructures = mapRegion.GeneratedStructures;
                            var structures = generatedStructures.Where(s =>
                                coord.X == s.Location.X1 / chunkSize &&
                                coord.Y == s.Location.Z1 / chunkSize).ToList();
                            foreach (var structure in structures)
                            {
                                var location = modSys.GetStoryStructureAt(structure.Location.X1, structure.Location.Z1);
                                if (location != null && modSys.storyStructureInstances.TryGetValue(location.Code, out var structureInstance))
                                {
                                    if (structure.Group != null && structureInstance.SchematicsSpawned?.TryGetValue(structure.Group, out var spawned) == true)
                                    {
                                        structureInstance.SchematicsSpawned[structure.Group] = Math.Max(0, spawned - 1);
                                    }
                                }
                            }
                            generatedStructures.RemoveAll(s => structures.Contains(s));
                        }
                    }
                }
            }

            int diam = 2 * rad + 1;
            if (onlydelete)
            {
                return TextCommandResult.Success("Deleted " + diam + "x" + diam + " columns" + (deleteRegion ? " and regions" : ""));
            }

            return TextCommandResult.Success("Reloaded landforms and regenerating " + diam + "x" + diam + " columns" + (deleteRegion ? " and regions" : ""));
        }

        TextCommandResult TestTree(IServerPlayer player, string asset,float size, float aheadoffset)
        {
            var loc = new AssetLocation(asset);

            var pos = player.Entity.Pos.HorizontalAheadCopy(aheadoffset).AsBlockPos;

            IBlockAccessor blockAccessor = api.World.GetBlockAccessorBulkUpdate(true, true);

            while (blockAccessor.GetBlockId(pos) == 0 && pos.Y > 1)
            {
                pos.Down();
            }

            treeGenerators.ReloadTreeGenerators();

            if (treeGenerators.GetGenerator(loc) == null)
            {
                return TextCommandResult.Success("Cannot generate this tree, no such generator found");
            }

            treeGenerators.RunGenerator(loc, blockAccessor, pos, new TreeGenParams() { size = size, skipForestFloor=true });

            blockAccessor.Commit();

            return TextCommandResult.Success(loc + " size " + size + " generated.");
        }

        TextCommandResult TreeLineup(IServerPlayer player, string asset)
        {
            EntityPos pos = player.Entity.Pos;
            BlockPos center = pos.HorizontalAheadCopy(25).AsBlockPos;
            IBlockAccessor blockAccessor = api.World.GetBlockAccessorBulkUpdate(true, true, true);
            AssetLocation loc = new AssetLocation(asset);

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

            var pa = new TreeGenParams() { size = 1 };
            treeGenerators.ReloadTreeGenerators();

            treeGenerators.RunGenerator(loc, blockAccessor, center.AddCopy(0, -1, 0), pa);
            treeGenerators.RunGenerator(loc, blockAccessor, center.AddCopy(-9, -1, 0), pa);
            treeGenerators.RunGenerator(loc, blockAccessor, center.AddCopy(9, -1, 0), pa);

            blockAccessor.Commit();
            return TextCommandResult.Success();
        }

        private IntDataMap2D OnMapRegionGen(int regionX, int regionZ, MapLayerBase climateGen)
        {
            int pad = 2;
            int noiseSizeClimate = api.WorldManager.RegionSize / TerraGenConfig.climateMapScale;

            var map = new IntDataMap2D();
            map.Data = climateGen.GenLayer(
                regionX * noiseSizeClimate - pad,
                regionZ * noiseSizeClimate - pad,
                noiseSizeClimate + 2 * pad,
                noiseSizeClimate + 2 * pad
            );
            map.Size = noiseSizeClimate + 2 * pad;
            map.TopLeftPadding = map.BottomRightPadding = pad;
            return map;
        }

        void DrawMapRegion(DebugDrawMode mode, Caller caller, IntDataMap2D map, string prefix, bool lerp, int regionX, int regionZ, int scale)
        {
            var player = caller.Player as IServerPlayer;
            if (lerp)
            {
                int[] lerped = GameMath.BiLerpColorMap(map, scale);
                NoiseBase.DebugDrawBitmap(mode, lerped, map.InnerSize * scale, prefix + "-" + regionX + "-" + regionZ + "-l");
                player.SendMessage(caller.FromChatGroupId, "Lerped " + prefix + " map generated.", EnumChatType.CommandSuccess);
            }
            else
            {
                NoiseBase.DebugDrawBitmap(mode, map.Data, map.Size, prefix + "-" + regionX + "-" + regionZ);
                player.SendMessage(caller.FromChatGroupId, "Original " + prefix + " map generated.", EnumChatType.CommandSuccess);
            }
        }

        private TextCommandResult OnStructuresList(TextCommandCallingArgs args)
        {
            var sb = new StringBuilder();
            if (args.Parsers[0].IsMissing)
            {
                for (var i = 0; i < _scfg.Structures.Length; i++)
                {
                    var structure = _scfg.Structures[i];
                    var domain = structure.Schematics.FirstOrDefault()?.Domain;
                    sb.AppendLine($"{i}: Name: {structure.Name} - Code: {domain}:{structure.Code} - Group: {structure.Group}");
                    sb.AppendLine($"     YOff: {structure.OffsetY} - MinGroupDist: {structure.MinGroupDistance}");
                }

                return TextCommandResult.Success(sb.ToString());
            }
            var structureNum = (int)args[0];
            if (structureNum < 0 || structureNum >= _scfg.Structures.Length)
            {
                return TextCommandResult.Success($"structureNum is out of range: 0-{_scfg.Structures.Length - 1}");
            }
            var structures = _scfg.Structures[structureNum];
            for (var i = 0; i < structures.schematicDatas.Length; i++)
            {
                var schematic = structures.schematicDatas[i];

                sb.AppendLine($"{i}: File: {schematic[0].FromFileName}");
            }

            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult OnStructuresSpawn(TextCommandCallingArgs args)
        {
            var structureNum = (int)args[0];
            var schematicNum = (int)args[1];
            var schematicRot = (int)args[2];

            if (structureNum < 0 || structureNum >= _scfg.Structures.Length)
            {
                return TextCommandResult.Success($"structureNum is out of range: 0-{_scfg.Structures.Length - 1}");
            }

            var struc = _scfg.Structures[structureNum];

            if (schematicNum < 0 || schematicNum >= struc.schematicDatas.Length)
            {
                return TextCommandResult.Success($"schematicNum is out of range: 0-{struc.schematicDatas.Length - 1}");
            }

            // take target block if available or own position
            var pos = args.Caller.Player.CurrentBlockSelection?.Position.AddCopy(0,struc.OffsetY ?? 0,0) ?? args.Caller.Pos.AsBlockPos.AddCopy(0,struc.OffsetY ?? 0,0);

            var schematic = struc.schematicDatas[schematicNum][schematicRot];
            schematic.Unpack(api, schematicRot);
            var chunkX = pos.X / _chunksize;
            var chunkZ = pos.Z / _chunksize;
            var chunkY = pos.Y / _chunksize;
            switch (struc.Placement)
            {
                case EnumStructurePlacement.SurfaceRuin:
                case EnumStructurePlacement.Surface:
                {
                    var chunk = api.WorldManager.GetChunk(chunkX, chunkY, chunkZ);
                    var climateMap = chunk.MapChunk.MapRegion.ClimateMap;
                    var rlX = chunkX % _regionChunkSize;
                    var rlZ = chunkZ % _regionChunkSize;

                    var facC = (float)climateMap.InnerSize / _regionChunkSize;
                    var climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
                    var climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
                    var climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
                    var climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));
                    schematic.PlaceRespectingBlockLayers(api.World.BlockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, struc.resolvedRockTypeRemaps, struc.replacewithblocklayersBlockids, GenStructures.ReplaceMetaBlocks);
                    break;
                }
                case EnumStructurePlacement.Underwater:
                    break;
                case EnumStructurePlacement.Underground:
                {
                    if (struc.resolvedRockTypeRemaps != null)
                    {
                        schematic.PlaceReplacingBlocks(api.World.BlockAccessor, api.World, pos, schematic.ReplaceMode, struc.resolvedRockTypeRemaps, null, GenStructures.ReplaceMetaBlocks);
                    }
                    else
                    {
                        schematic.Place(api.World.BlockAccessor, api.World, pos, GenStructures.ReplaceMetaBlocks);
                    }
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return TextCommandResult.Success($"placing structure: {struc.Name} :: {schematic.FromFileName} placement: {struc.Placement}");
        }
    }
}
