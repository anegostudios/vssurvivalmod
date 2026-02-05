using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

#nullable disable

namespace Vintagestory.ServerMods
{
    public class WgenCommandsExt : ModSystem
    {
        ICoreServerAPI api;

        private int _regionSize;
        private int _chunksize;
        private int _regionChunkSize;
        private WorldGenStructuresConfig _scfg;

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
        }



        private void OnGameWorldLoaded()
        {
            _regionSize = api.WorldManager.RegionSize;
        }

        private void CreateCommands()
        {

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.GetOrCreate("wgen")
                .BeginSubCommand("testvillage")
                    .WithDescription("Testvillage command")
                    .RequiresPlayer()
                    .HandleWith(OnCmdTestVillage)
                .EndSubCommand()
            ;
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
                        .WithArgs(parsers.Int("structure_index"), parsers.OptionalInt("schematic_index"), parsers.OptionalIntRange("rotation_index", 0, 3))
                        .HandleWith(OnStructuresSpawn)
                    .EndSubCommand()
                    .BeginSubCommand("list")
                        .WithDescription("List structures with their indices for the /dev structure spawn command")
                        .WithArgs(parsers.OptionalInt("structure_num"))
                        .HandleWith(OnStructuresList)
                    .EndSubCommand()
                .EndSubCommand()
                ;
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
                    return TextCommandResult.Success($"Generated after {i + 1} tries");
                }
            }

            return TextCommandResult.Error("Unable to generate, likely not flat enough here.");
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
            var pos = args.Caller.Player.CurrentBlockSelection?.Position.AddCopy(0, struc.OffsetY ?? 0, 0) ?? args.Caller.Pos.AsBlockPos.AddCopy(0, struc.OffsetY ?? 0, 0);

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
                        schematic.PlaceRespectingBlockLayers(api.World.BlockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, struc.resolvedRockTypeRemaps, struc.replacewithblocklayersBlockids, GlobalConfig.ReplaceMetaBlocks);
                        break;
                    }
                case EnumStructurePlacement.Underwater:
                    break;
                case EnumStructurePlacement.Underground:
                    {
                        if (struc.resolvedRockTypeRemaps != null)
                        {
                            schematic.PlaceReplacingBlocks(api.World.BlockAccessor, api.World, pos, schematic.ReplaceMode, struc.resolvedRockTypeRemaps, null, GlobalConfig.ReplaceMetaBlocks);
                        }
                        else
                        {
                            schematic.Place(api.World.BlockAccessor, api.World, pos, GlobalConfig.ReplaceMetaBlocks);
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
