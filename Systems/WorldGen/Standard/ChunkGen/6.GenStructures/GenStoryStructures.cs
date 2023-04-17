using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class StoryStructureLocation
    {
        [ProtoMember(1)]
        public string Code;
        [ProtoMember(2)]
        public BlockPos CenterPos;
        [ProtoMember(3)]
        public bool DidGenerate;
        [ProtoMember(4)]
        public Cuboidi Location;
    }

    public class HookGeneratedStructure
    {
        [JsonProperty]
        public PathAndOffset[] mainElements;
        [JsonProperty]
        public Dictionary<string, PathAndOffset> endElements;
        [JsonProperty]
        public int offsetX;
        [JsonProperty]
        public int offsetY;
        [JsonProperty]
        public int offsetZ;
        [JsonProperty]
        public int endOffsetY;
        [JsonProperty]
        public AssetLocation[] ReplaceWithBlocklayers;
        [JsonProperty]
        public int mainsizeX;
        [JsonProperty]
        public int mainsizeZ;
    }

    public class PathAndOffset
    {
        [JsonProperty]
        public string path;
        [JsonProperty]
        public int dx;
        [JsonProperty]
        public int dy;
        [JsonProperty]
        public int dz;
    }

    public class GenStoryStructures : ModStdWorldGen
    {
        WorldGenStoryStructuresConfig scfg;
        LCGRandom strucRand; // Deterministic random

        public OrderedDictionary<string, StoryStructureLocation> storyStructureInstances = new OrderedDictionary<string, StoryStructureLocation>();
        Cuboidi[] structureLocations;


        IWorldGenBlockAccessor worldgenBlockAccessor;


        float genAngle;
        float angleRange = 90 * GameMath.DEG2RAD;

        LCGRandom rand;
        ICoreServerAPI api;

        bool genStoryStructures;
        BlockLayerConfig blockLayerConfig;

        public override double ExecuteOrder() { return 0.2; }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(initWorldGen, "standard");
                api.Event.ChunkColumnGeneration(onChunkColumnGen, EnumWorldGenPass.Vegetation, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

                api.Event.WorldgenHook(GenerateHookStructure, "standard", "genHookStructure");
            }

            api.ModLoader.GetModSystem<GenStructures>().OnPreventSchematicPlaceAt += OnPreventSchematicPlaceAt;

            api.RegisterCommand("tpstoryloc", "", "", onTpStoryLoc, Privilege.controlserver);

            api.ChatCommands
                .Create("setstorystrucpos")
                .WithDescription("Set the location of a story structure")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.Word("code"), api.ChatCommands.Parsers.WorldPosition("position"), api.ChatCommands.Parsers.OptionalBool("confirm"))
                .HandleWith(onSetStoryStructurePos)
                .Validate()
            ;
        }

        private TextCommandResult onSetStoryStructurePos(TextCommandCallingArgs args)
        {
            var storyStruc = scfg.Structures.FirstOrDefault(st => st.Code == (string)args[0]);
            if (storyStruc == null)
            {
                return TextCommandResult.Error("No such story structure exist in assets");
            }

            if ((bool)args[2] != true)
            {
                return TextCommandResult.Success("Ok, will move the story structure location to this position. Make sure that there is a lot of unoccupied chunks all around. Add 'true' to the command to confirm. After this is done, you will have to regenerate chunks in this area, e.g. via /wgen regenr 7 to recreate 192x192 blocks in all directions");
            }

            var pos = ((Vec3d)args[1]).AsBlockPos;
            pos.Y = 1; // Lets hardcode RA y-pos for now
            GenMaps genmaps = api.ModLoader.GetModSystem<GenMaps>();

            foreach (var val in scfg.Structures)
            {
                float angle = genAngle + rand.NextFloat() * angleRange - angleRange / 2;
                float distance = val.MinSpawnDist + rand.NextFloat() * (val.MaxSpawnDist - val.MinSpawnDist);

                var schem = val.schematicData;
                int minX = pos.X - schem.SizeX / 2;
                int minZ = pos.Z - schem.SizeZ / 2;
                var cub = new Cuboidi(minX, pos.Y, minZ, minX + schem.SizeX - 1, pos.Y + schem.SizeY - 1, minZ + schem.SizeZ - 1);
                storyStructureInstances[val.Code] = new StoryStructureLocation()
                {
                    Code = val.Code,
                    CenterPos = pos,
                    Location = cub
                };

                if (val.RequireLandform != null)
                {
                    Rectanglei areacuboid = new Rectanglei(pos.X - val.LandformSizeX / 2, pos.Z - val.LandformSizeZ / 2, val.LandformSizeX, val.LandformSizeZ);

                    genmaps.ForceLandformAt(new ForceLandform()
                    {
                        Area = areacuboid,
                        LandformCode = val.RequireLandform
                    });
                }
            }

            this.structureLocations = storyStructureInstances.Select(val => val.Value.Location).ToArray();

            return TextCommandResult.Success("Ok, story structure location moved to this position. Regenerating chunks at the location should make it appear now.");
        }

        public void initWorldGen()
        {
            genStoryStructures = api.World.Config.GetAsString("loreContent", "true").ToBool(true);
            if (!genStoryStructures) return;

            chunksize = api.World.BlockAccessor.ChunkSize;
            strucRand = new LCGRandom(api.WorldManager.Seed + 1095);
            IAsset asset = api.Assets.Get("worldgen/storystructures.json");
            scfg = asset.ToObject<WorldGenStoryStructuresConfig>();
            RockStrataConfig rockstrata = scfg.Init(api, strucRand);

            asset = api.Assets.Get("worldgen/blocklayers.json");
            blockLayerConfig = asset.ToObject<BlockLayerConfig>();
            blockLayerConfig.ResolveBlockIds(api, rockstrata);

            if (api.WorldManager.SaveGame.IsNew)
            {
                determineStoryStructures();
            }
        }

        private void onTpStoryLoc(IServerPlayer player, int groupId, CmdArgs args)
        {
            string code = args.PopWord();
            if (code == null) return;

            if (storyStructureInstances.TryGetValue(code, out var storystruc))
            {
                var pos = storystruc.CenterPos.Copy();
                pos.Y = (storystruc.Location.Y1 + storystruc.Location.Y2) / 2;
                player.Entity.TeleportTo(pos);
                player.SendMessage(groupId, "Teleporting to " + code, EnumChatType.CommandSuccess);
            }
            else
            {
                player.SendMessage(groupId, "No such story structure, " + code, EnumChatType.CommandSuccess);
            }

        }

        private bool OnPreventSchematicPlaceAt(BlockPos pos, Cuboidi schematicLocation)
        {
            if (structureLocations == null) return false;

            for (int i = 0; i < structureLocations.Length; i++)
            {
                if (structureLocations[i].Intersects(schematicLocation)) return true;
            }

            return false;
        }

        public bool IsInStoryStructure(Vec3d position)
        {
            if (structureLocations == null) return false;

            for (int i = 0; i < structureLocations.Length; i++)
            {
                var loc = structureLocations[i];
                if (loc.Contains(position)) return true;
            }

            return false;
        }

        public bool IsInStoryStructure(BlockPos position)
        {
            if (structureLocations == null) return false;

            for (int i = 0; i < structureLocations.Length; i++)
            {
                var loc = structureLocations[i];
                if (loc.Contains(position)) return true;
            }

            return false;
        }

        protected void determineStoryStructures()
        {
            // Chicken and egg problem. The engine needs to generate spawn chunks and find a suitable spawn location before it can populate api.World.DefaultSpawnPosition
            // Needs a better solution than hardcoding map middle
            BlockPos spawnPos = new BlockPos(api.World.BlockAccessor.MapSizeX / 2, 0, api.World.BlockAccessor.MapSizeZ / 2);

            GenMaps genmaps = api.ModLoader.GetModSystem<GenMaps>();

            List<Cuboidi> occupiedLocations = new List<Cuboidi>();

            foreach (var val in scfg.Structures)
            {
                float angle = genAngle + rand.NextFloat() * angleRange - angleRange / 2;
                float distance = val.MinSpawnDist + rand.NextFloat() * (val.MaxSpawnDist - val.MinSpawnDist);

                BlockPos pos = new BlockPos(
                    (int)(spawnPos.X + distance * Math.Cos(angle)),
                    1,
                    (int)(spawnPos.Z + distance * Math.Sin(angle))
                );

                var schem = val.schematicData;
                int minX = pos.X - schem.SizeX / 2;
                int minZ = pos.Z - schem.SizeZ / 2;
                var cub = new Cuboidi(minX, pos.Y, minZ, minX + schem.SizeX - 1, pos.Y + schem.SizeY - 1, minZ + schem.SizeZ - 1);
                storyStructureInstances[val.Code] = new StoryStructureLocation()
                {
                    Code = val.Code,
                    CenterPos = pos,
                    Location = cub
                };

                occupiedLocations.Add(cub);

                if (val.RequireLandform != null)
                {
                    Rectanglei areacuboid = new Rectanglei(pos.X - val.LandformSizeX / 2, pos.Z - val.LandformSizeZ / 2, val.LandformSizeX, val.LandformSizeZ);

                    genmaps.ForceLandformAt(new ForceLandform()
                    {
                        Area = areacuboid,
                        LandformCode = val.RequireLandform
                    });
                }
            }

            this.structureLocations = occupiedLocations.ToArray();
        }

        private void Event_GameWorldSave()
        {
            api.WorldManager.SaveGame.StoreData("storystructurelocations", SerializerUtil.Serialize(storyStructureInstances));
        }

        private void Event_SaveGameLoaded()
        {
            var strucs = api.WorldManager.SaveGame.GetData<OrderedDictionary<string, StoryStructureLocation>>("storystructurelocations");
            if (strucs == null)
            {
                // Old world. What do we do here?
            }
            else
            {
                storyStructureInstances = strucs;

                this.structureLocations = storyStructureInstances.Select(val => val.Value.Location).ToArray();
            }

            
        }


        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);

            rand = new LCGRandom(api.WorldManager.Seed ^ 8991827198);
            genAngle = rand.NextFloat() * GameMath.TWOPI;
        }



        Cuboidi tmpCuboid = new Cuboidi();



        private void onChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            if (!genStoryStructures) return;
            if (structureLocations == null) return;

            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;
            tmpCuboid.Set(chunkX * chunksize, 0, chunkZ * chunksize, chunkX * chunksize + chunksize, chunks.Length * chunksize, chunkZ * chunksize + chunksize);
            worldgenBlockAccessor.BeginColumn();

            for (int i = 0; i < structureLocations.Length; i++)
            {
                var strucloc = structureLocations[i];
                if (strucloc.Intersects(tmpCuboid))
                {
                    var strucInst = storyStructureInstances.GetValueAtIndex(i);
                    strucInst.DidGenerate = true;
                    BlockPos startPos = new BlockPos(strucloc.X1, strucloc.Y1, strucloc.Z1);

                    var structure = scfg.Structures[i];

                    int blocksPlaced = structure.schematicData.PlacePartial(chunks, worldgenBlockAccessor, api.World, chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll, true);

                    string code = structure.Code + ":" + structure.Schematics[0];

                    var region = chunks[0].MapChunk.MapRegion;

                    if (region.GeneratedStructures.FirstOrDefault(struc => struc.Code.Equals(code)) == null) {
                        region.GeneratedStructures.Add(new GeneratedStructure() { Code = code, Group = structure.Group, Location = strucloc.Clone() });
                        region.DirtyForSaving = true;
                    }

                    if (blocksPlaced > 0 && structure.BuildProtected)
                    {
                        var claims = api.World.Claims.Get(strucloc.Center.AsBlockPos);
                        if (claims == null || claims.Length == 0)
                        {
                            api.World.Claims.Add(new LandClaim()
                            {
                                Areas = new List<Cuboidi>() { strucloc },
                                Description = structure.BuildProtectionDesc,
                                ProtectionLevel = 10,
                                LastKnownOwnerName = structure.BuildProtectionName,
                                AllowUseEveryone = true
                            });
                        }
                    }
                }
            }
        }

        public void GenerateHookStructure(IBlockAccessor blockAccessor, BlockPos pos, AssetLocation code)
        {
            api.Logger.VerboseDebug("Worldgen hook generation event fired, with code " + code);

            var mapchunk = blockAccessor.GetMapChunkAtBlockPos(pos);

            IAsset assetMain = api.Assets.TryGet(code.WithPathPrefixOnce("worldgen/hookgeneratedstructures/").WithPathAppendixOnce(".json"));
            if (assetMain == null || mapchunk == null)
            {
                api.Logger.Error("Worldgen hook event failed: " + (mapchunk == null ? "bad coordinates" : code + "* not found"));
                return;
            }
            HookGeneratedStructure hookStruct = assetMain.ToObject<HookGeneratedStructure>();
            int mainsizeX = hookStruct.mainsizeX;
            int mainsizeZ = hookStruct.mainsizeZ;

            int minX = pos.X - mainsizeX / 2 - 2;
            int maxX = pos.X + mainsizeX / 2 + 2;
            int minZ = pos.Z - mainsizeZ / 2 - 2;
            int maxZ = pos.Z + mainsizeZ / 2 + 2;
            List<int> heights = new List<int>((maxX - minX + 1) * (maxZ - minZ + 1));
            int maxheight = 0;
            int minheight = int.MaxValue;
            int x, z;
            for (x = minX; x <= maxX; x++)
            {
                for (z = minZ; z <= maxZ; z++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    heights.Add(h);
                    maxheight = Math.Max(maxheight, h);
                    minheight = Math.Min(minheight, h);
                }
            }
            x = Math.Max(mainsizeX, mainsizeZ);   // make the next test square
            minX = pos.X - x / 2;
            maxX = pos.X + x / 2;
            minZ = pos.Z - x / 2;
            maxZ = pos.Z + x / 2;
            int weightedHeightW = 1;   // used to detect whether downwards slope is East-West etc
            int weightedHeightE = 1;
            int weightedHeightN = 1;
            int weightedHeightS = 1;
            x = minX - 2;
            for (z = minZ; z <= maxZ; z++)
            {
                mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                weightedHeightW += h;
            }
            x = maxX + 2;
            for (z = minZ; z <= maxZ; z++)
            {
                mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                weightedHeightE += h;
            }
            z = minZ - 2;
            for (x = minX; x <= maxX; x++)
            {
                mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                weightedHeightN += h;
            }
            z = maxZ + 2;
            for (x = minX; x <= maxX; x++)
            {
                mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                weightedHeightS += h;
            }




            if (hookStruct.mainElements.Length > 0)
            {
                pos = pos.AddCopy(hookStruct.offsetX, hookStruct.offsetY, hookStruct.offsetZ);
                Vec3i[] offsets = new Vec3i[hookStruct.mainElements.Length];
                BlockSchematicStructure[] structures = new BlockSchematicStructure[hookStruct.mainElements.Length];
                int structuresLength = 0;
                foreach (var el in hookStruct.mainElements)
                {
                    IAsset asset = api.Assets.TryGet(new AssetLocation(code.Domain, "worldgen/" + el.path + ".json"));
                    if (asset == null)
                    {
                        api.Logger.Notification("Worldgen hook event elements: path not found: " + el.path);
                        continue;
                    }
                    var structure = asset.ToObject<BlockSchematicStructure>();
                    structure.Init(blockAccessor);
                    structures[structuresLength] = structure;
                    offsets[structuresLength++] = new Vec3i(el.dx, el.dy, el.dz);
                }

                Random rand = api.World.Rand;
                List<int> indices = new List<int>();
                List<int> bestIndices = new List<int>();
                int bestDiff = int.MaxValue;
                int testHeight;

                heights.Sort();
                int n = Math.Min(5, heights.Count);
                int height = 0;
                for (int j = 0; j < n; j++) height += heights[j];
                height = (height / n) + hookStruct.endOffsetY;
                if (maxheight - minheight < 5 && height - minheight < 2) height++;  // place it one block higher on relatively flat ground

                for (int j = 0; j < 25; j++)
                {
                    indices.Clear();
                    testHeight = pos.Y;
                    while (testHeight < height)
                    {
                        int i = rand.Next(structuresLength);
                        int h = structures[i].SizeY;
                        if (testHeight + h > height)
                        {
                            if (testHeight + h - height > height - testHeight)  // is the one below closer to height than the one above?
                            {
                                h = (height - testHeight) * 2;   // fix the newDiff to be (height - testHeight) in this case;
                            }
                            else
                            {
                                indices.Add(i);
                            }

                            int newDiff = testHeight + h - height;
                            if (newDiff < bestDiff)
                            {
                                bestDiff = newDiff;
                                bestIndices.Clear();
                                foreach (int ix in indices) bestIndices.Add(ix);
                                if (bestDiff == 0) i = 25;  // early exit if we already have an optimal set of indices, by fast-forwarding outer loop;
                            }

                            break;
                        }

                        indices.Add(i);
                        testHeight += h;
                    }
                }

                foreach (int ix in bestIndices)
                {
                    var struc = structures[ix];
                    var offset = offsets[ix];
                    BlockPos posPlace = pos.AddCopy(offset.X, offset.Y, offset.Z);
                    struc.PlaceRespectingBlockLayers(blockAccessor, api.World, posPlace, 0, 0, 0, 0, null, new int[0], true, true);
                    pos.Y += struc.SizeY;
                }
            }

            string topside;
            if (weightedHeightW < weightedHeightE)
            {
                if (weightedHeightW < weightedHeightN && weightedHeightW < weightedHeightS) topside = "w";
                else topside = weightedHeightS < weightedHeightN ? "s" : "n";
            }
            else
            {
                if (weightedHeightE < weightedHeightN && weightedHeightE < weightedHeightS) topside = "e";
                else topside = weightedHeightS < weightedHeightN ? "s" : "n";
            }
            BlockSchematicStructure structTop;
            if (!hookStruct.endElements.TryGetValue(topside, out PathAndOffset endElement))
            {
                api.Logger.Notification("Worldgen hook event incomplete: no end structure for " + topside);
                return;
            }
            IAsset assetTop = api.Assets.Get(new AssetLocation(code.Domain, endElement.path));
            structTop = assetTop?.ToObject<BlockSchematicStructure>();
            if (structTop == null)
            {
                api.Logger.Notification("Worldgen hook event incomplete: " + endElement.path + " not found");
                return;
            }

            int[] replaceblockids;
            if (hookStruct.ReplaceWithBlocklayers != null)
            {
                replaceblockids = new int[hookStruct.ReplaceWithBlocklayers.Length];
                for (int i = 0; i < replaceblockids.Length; i++)
                {
                    Block block = api.World.GetBlock(hookStruct.ReplaceWithBlocklayers[i]);
                    if (block == null)
                    {
                        api.Logger.Error(string.Format("Hook structure with code {0} has replace block layer {1} defined, but no such block found!", code, hookStruct.ReplaceWithBlocklayers[i]));
                        return;
                    }
                    else
                    {
                        replaceblockids[i] = block.Id;
                    }

                }
            }
            else replaceblockids = new int[0];

            int climateUpLeft, climateUpRight, climateBotLeft, climateBotRight;
            IMapRegion region = mapchunk.MapRegion;
            IntDataMap2D climateMap = region.ClimateMap;
            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            int rlX = pos.X / chunksize % regionChunkSize;
            int rlZ = pos.Z / chunksize % regionChunkSize;
            float facC = (float)climateMap.InnerSize / regionChunkSize;
            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            structTop.blockLayerConfig = blockLayerConfig;   // For other structures this is done by WorldGenStructureBase at loading time
            structTop.Init(blockAccessor);

            pos.Add(endElement.dx, endElement.dy, endElement.dz);
            structTop.PlaceRespectingBlockLayers(blockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, null, replaceblockids, true, true, true);
        }

    }
}
