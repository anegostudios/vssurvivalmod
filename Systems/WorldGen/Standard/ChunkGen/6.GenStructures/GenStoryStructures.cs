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
        public string mainPath;
        [JsonProperty]
        public string endPath;
        [JsonProperty]
        public int offsetX;
        [JsonProperty]
        public int offsetY;
        [JsonProperty]
        public int offsetZ;
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


        public override double ExecuteOrder() { return 0.92; }

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
        }

        public void initWorldGen()
        {
            genStoryStructures = api.World.Config.GetAsString("loreContent", "true").ToBool(true);
            if (!genStoryStructures) return;

            chunksize = api.World.BlockAccessor.ChunkSize;
            strucRand = new LCGRandom(api.WorldManager.Seed + 1095);
            IAsset asset = api.Assets.Get("worldgen/storystructures.json");
            scfg = asset.ToObject<WorldGenStoryStructuresConfig>();
            scfg.Init(api, strucRand);

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



        private void onChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams)
        {
            if (!genStoryStructures) return;
            if (structureLocations == null) return;

            tmpCuboid.Set(chunkX * chunksize, 0, chunkZ * chunksize, chunkX * chunksize + chunksize, chunks.Length * chunksize, chunkZ * chunksize + chunksize);

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
                api.Logger.Notification("Worldgen hook event failed: " + (mapchunk == null ? "bad coordinates" : code + "* not found"));
                return;
            }
            HookGeneratedStructure main = assetMain.ToObject<HookGeneratedStructure>();

            List<IAsset> assets = api.Assets.GetManyInCategory("worldgen", main.mainPath, code.Domain); 
            if (assets.Count == 0 || mapchunk == null)
            {
                api.Logger.Notification("Worldgen hook event failed: " + main.mainPath + "* not found");
                return;
            }

            pos = pos.AddCopy(main.offsetX, main.offsetY, main.offsetZ);
            BlockSchematicStructure[] structures = new BlockSchematicStructure[assets.Count];
            int i = 0;
            foreach (IAsset asset in assets)
            {
                var structure = asset.ToObject<BlockSchematicStructure>();
                structure.Init(blockAccessor);
                structures[i++] = structure;
            }

            int minX = pos.X - 2;
            int maxX = pos.X + 3;
            int minZ = pos.Z - 2;
            int maxZ = pos.Z + 3;
            int height = 65535;
            int maxheight = 0;
            for (int x = minX; x < maxX; x++)
            {
                for (int z = minZ; z < maxZ; z++)
                {
                    mapchunk = blockAccessor.GetMapChunk(x / chunksize, z / chunksize);
                    int h = mapchunk.WorldGenTerrainHeightMap[(z % chunksize) * chunksize + (x % chunksize)];
                    height = Math.Min(height, h);
                    maxheight = Math.Max(maxheight, h);
                }
            }
            
            if (maxheight > height + 1) height+= (maxheight - height) / 3;  // Put the top higher in sloping areas

            Random rand = api.World.Rand;
            while (pos.Y < height - 1)
            {
                var struc = structures[rand.Next(structures.Length)];
                struc.PlaceRespectingBlockLayers(blockAccessor, api.World, pos, 0, 0, 0, 0, null, new int[0], true, true);
                pos.Y += struc.SizeY;
            }

            IAsset assetTop = api.Assets.Get(new AssetLocation(code.Domain, main.endPath));
            var structTop = assetTop?.ToObject<BlockSchematicStructure>();
            if (structTop == null)
            {
                api.Logger.Notification("Worldgen hook event incomplete: " + main.endPath + " not found");
                return;
            }
            structTop.Init(blockAccessor);
            structTop.PlaceRespectingBlockLayers(blockAccessor, api.World, pos, 0, 0, 0, 0, null, new int[0], true, true);
        }

    }
}
