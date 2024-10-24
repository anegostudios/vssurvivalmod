using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class GenStoryStructures : ModStdWorldGen
    {
        private WorldGenStoryStructuresConfig scfg;
        private LCGRandom strucRand; // Deterministic random
        private LCGRandom grassRand; // Deterministic random

        public OrderedDictionary<string, StoryStructureLocation> storyStructureInstances = new OrderedDictionary<string, StoryStructureLocation>();
        public bool StoryStructureInstancesDirty = false;

        private Cuboidi[] structureLocations;
        private IWorldGenBlockAccessor worldgenBlockAccessor;

        private ICoreServerAPI api;

        private bool genStoryStructures;
        public BlockLayerConfig blockLayerConfig;
        private Cuboidi tmpCuboid = new Cuboidi();

        private int mapheight;
        private ClampedSimplexNoise grassDensity;
        private ClampedSimplexNoise grassHeight;

        public SimplexNoise distort2dx;
        public SimplexNoise distort2dz;

        public override double ExecuteOrder() { return 0.2; }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(InitWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Vegetation, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);

                api.Event.WorldgenHook(GenerateHookStructure, "standard", "genHookStructure");
            }

            api.ModLoader.GetModSystem<GenStructures>().OnPreventSchematicPlaceAt += OnPreventSchematicPlaceAt;

            api.Event.ServerRunPhase(EnumServerRunPhase.RunGame,() =>
            {
                if (!genStoryStructures) return;
                api.ChatCommands.Create("tpstoryloc")
                    .WithDescription("Teleport to a story structure instance")
                    .RequiresPrivilege(Privilege.controlserver)
                    .RequiresPlayer()
                    .WithArgs(api.ChatCommands.Parsers.WordRange("code", scfg.Structures.Select(s=> s.Code).ToArray()))
                    .HandleWith(OnTpStoryLoc);
            });


            api.ChatCommands
                .Create("setstorystrucpos")
                .WithDescription("Set the location of a story structure")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(api.ChatCommands.Parsers.Word("code"), api.ChatCommands.Parsers.WorldPosition("position"), api.ChatCommands.Parsers.OptionalBool("confirm"))
                .HandleWith(OnSetStoryStructurePos)
                .Validate()
            ;
        }

        private TextCommandResult OnSetStoryStructurePos(TextCommandCallingArgs args)
        {
            var storyStruc = scfg.Structures.FirstOrDefault(st => st.Code == (string)args[0]);
            if (storyStruc == null)
            {
                return TextCommandResult.Error("No such story structure exist in assets");
            }

            if ((bool)args[2] != true)
            {
                return TextCommandResult.Success("Ok, will move the story structure location to this position. Make sure that there is a lot of unoccupied chunks all around. Add 'true' to the command to confirm. After this is done, you will have to regenerate chunks in this area, e.g. via /wgen regen 7 to recreate 192x192 blocks in all directions");
            }

            var pos = ((Vec3d)args[1]).AsBlockPos;
            pos.Y = 1; // Lets hardcode RA y-pos for now
            GenMaps genmaps = api.ModLoader.GetModSystem<GenMaps>();

            var schem = storyStruc.schematicData;
            int minX = pos.X - schem.SizeX / 2;
            int minZ = pos.Z - schem.SizeZ / 2;
            var cub = new Cuboidi(minX, pos.Y, minZ, minX + schem.SizeX - 1, pos.Y + schem.SizeY - 1, minZ + schem.SizeZ - 1);
            storyStructureInstances[storyStruc.Code] = new StoryStructureLocation()
            {
                Code = storyStruc.Code,
                CenterPos = pos,
                Location = cub,
                LandformRadius = storyStruc.LandformRadius,
                GenerationRadius = storyStruc.GenerationRadius,
                SkipGenerationFlags = storyStruc.SkipGenerationFlags
            };

            if (storyStruc.RequireLandform != null)
            {
                genmaps.ForceLandformAt(new ForceLandform()
                {
                    CenterPos = pos,
                    Radius = storyStruc.LandformRadius,
                    LandformCode = storyStruc.RequireLandform
                });
            }

            if (storyStruc.ForceTemperature != null || storyStruc.ForceRain != null)
            {
                genmaps.ForceClimateAt(new ForceClimate
                {
                    Radius = storyStructureInstances[storyStruc.Code].LandformRadius,
                    CenterPos = storyStructureInstances[storyStruc.Code].CenterPos,
                    Climate = (Climate.DescaleTemperature(storyStruc.ForceTemperature ?? 0f) << 16) + ((storyStruc.ForceRain ?? 0) << 8)
                });
            }

            structureLocations = storyStructureInstances.Select(val => val.Value.Location).ToArray();
            StoryStructureInstancesDirty = true;

            return TextCommandResult.Success("Ok, story structure location moved to this position. Regenerating chunks at the location should make it appear now.");
        }

        public void InitWorldGen()
        {
            genStoryStructures = api.World.Config.GetAsString("loreContent", "true").ToBool(true);
            if (!genStoryStructures) return;
            strucRand = new LCGRandom(api.WorldManager.Seed + 1095);
            var asset = api.Assets.Get("worldgen/storystructures.json");
            scfg = asset.ToObject<WorldGenStoryStructuresConfig>();

            grassRand = new LCGRandom(api.WorldManager.Seed);
            grassDensity = new ClampedSimplexNoise(new double[] { 4 }, new double[] { 0.5 }, grassRand.NextInt());
            grassHeight = new ClampedSimplexNoise(new double[] { 1.5 }, new double[] { 0.5 }, grassRand.NextInt());

            distort2dx = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 20980);
            distort2dz = new SimplexNoise(new double[] { 14, 9, 6, 3 }, new double[] { 1 / 100.0, 1 / 50.0, 1 / 25.0, 1 / 12.5 }, api.World.SeaLevel + 20981);
            mapheight = api.WorldManager.MapSizeY;

            blockLayerConfig = BlockLayerConfig.GetInstance(api);
            scfg.Init(api, blockLayerConfig.RockStrata, blockLayerConfig);

            // logic to upgrade the old StoryStructureLocation format to the new 1.20 so all needed values are set
            // currently this will only work for resonance archive since this is the first location and all others depend upon it
            // for upgraded worlds player would need to manually set the story locations anyway since we only generate story locations on new worlds
            foreach (var (code,loc) in storyStructureInstances)
            {
                if (loc.SkipGenerationFlags == null)
                {
                    var worldGenStoryStructure = scfg.Structures.FirstOrDefault(s => s.Code.Equals(code));
                    if (worldGenStoryStructure != null)
                    {
                        loc.SkipGenerationFlags = worldGenStoryStructure.SkipGenerationFlags;
                        loc.LandformRadius = worldGenStoryStructure.LandformRadius;
                        loc.GenerationRadius = worldGenStoryStructure.GenerationRadius;
                        // we can ignore DirX here since that is only needed to determine the initial position
                        // and in world where
                        StoryStructureInstancesDirty = true;
                    }
                }
            }

            if (api.WorldManager.SaveGame.IsNew)
            {
                DetermineStoryStructures();
            } else
            {
                SetupForceLandform();
            }
        }

        private TextCommandResult OnTpStoryLoc(TextCommandCallingArgs args)
        {
            if (args[0] is not string code) return TextCommandResult.Success();

            if (storyStructureInstances.TryGetValue(code, out var storystruc))
            {
                var pos = storystruc.CenterPos.Copy();
                pos.Y = (storystruc.Location.Y1 + storystruc.Location.Y2) / 2;
                args.Caller.Entity.TeleportTo(pos);
                return TextCommandResult.Success("Teleporting to " + code);
            }

            return TextCommandResult.Success("No such story structure, " + code);

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

        public bool IsInStoryStructure(int x, int z, int skipCategory, out string locationCode)
        {
            if (structureLocations == null)
            {
                locationCode = null;
                return false;
            }

            foreach (var (_, loc) in storyStructureInstances)
            {
                if (loc.SkipGenerationFlags == null) continue;   // radfast 23.9.24: seems .SkipGenerationFlags is null for the Lazaret when loading a pre-1.20 world for the first time

                var hasCategory = loc.SkipGenerationFlags.TryGetValue(skipCategory, out var checkRadius);
                if (loc.Location.Contains(x, z) && hasCategory)
                {
                    locationCode = loc.Code;
                    return true;
                }
                if (checkRadius > 0)
                {
                    if (loc.CenterPos.HorDistanceSqTo(x, z) < checkRadius * checkRadius)
                    {
                        locationCode = loc.Code;
                        return true;
                    }
                }
            }

            locationCode = null;
            return false;
        }

        public bool IsInStoryStructure(Vec3d position, int skipCategory, out string locationCode)
        {
            return IsInStoryStructure((int)position.X, (int)position.Z, skipCategory, out locationCode);
        }

        public bool IsInStoryStructure(BlockPos position, int skipCategory, out string locationCode)
        {
            return IsInStoryStructure(position.X, position.Z, skipCategory, out locationCode);
        }

        protected void DetermineStoryStructures()
        {
            var occupiedLocations = new List<Cuboidi>();

            foreach (var storyStructure in scfg.Structures)
            {
                BlockPos basePos = null;
                StoryStructureLocation dependentLocation = null;
                if (storyStructure.DependsOnStructure == "spawn")
                {
                    // Chicken and egg problem. The engine needs to generate spawn chunks and find a suitable spawn location before it can populate api.World.DefaultSpawnPosition
                    // Needs a better solution than hardcoding map middle
                    basePos = new BlockPos(api.World.BlockAccessor.MapSizeX / 2, 0, api.World.BlockAccessor.MapSizeZ / 2);
                }
                else
                {
                    if (storyStructureInstances.TryGetValue(storyStructure.DependsOnStructure, out dependentLocation))
                    {
                        basePos = dependentLocation.CenterPos.Copy();
                    }
                }

                if (basePos != null)
                {
                    // get -1 or 1 for direction in x axis
                    int dirX;
                    if (dependentLocation != null)
                    {
                        dirX = dependentLocation.DirX;
                    }
                    else
                    {
                        dirX = strucRand.NextFloat() > 0.5 ? -1 : 1;
                    }
                    // take random dir in z since dependent story structures are on the same xdir so z can be random
                    var dirZ = strucRand.NextFloat() > 0.5 ? -1 : 1;

                    var distanceX = storyStructure.MinSpawnDistX + strucRand.NextInt(storyStructure.MaxSpawnDistX + 1 - storyStructure.MinSpawnDistX);
                    var distanceZ = storyStructure.MinSpawnDistZ + strucRand.NextInt(storyStructure.MaxSpawnDistZ + 1 - storyStructure.MinSpawnDistZ);

                    var schem = storyStructure.schematicData;

                    var locationHeight = storyStructure.Placement is EnumStructurePlacement.Surface or EnumStructurePlacement.SurfaceRuin ? api.World.SeaLevel + schem.OffsetY : 1;
                    var pos = new BlockPos(
                        basePos.X + distanceX * dirX,
                        locationHeight,
                        basePos.Z + distanceZ * dirZ
                    );

                    int minX = pos.X - schem.SizeX / 2;
                    int minZ = pos.Z - schem.SizeZ / 2;
                    var cuboidi = new Cuboidi(minX, pos.Y, minZ, minX + schem.SizeX, pos.Y + schem.SizeY, minZ + schem.SizeZ);
                    storyStructureInstances[storyStructure.Code] = new StoryStructureLocation()
                    {
                        Code = storyStructure.Code,
                        CenterPos = pos,
                        Location = cuboidi,
                        LandformRadius = storyStructure.LandformRadius,
                        GenerationRadius = storyStructure.GenerationRadius,
                        DirX = dirX,
                        SkipGenerationFlags = storyStructure.SkipGenerationFlags
                    };
                    occupiedLocations.Add(cuboidi);
                }
                else
                {
                    api.Logger.Error("Could not find dependent structure/spawn to generate a structure spawn for: " + storyStructure.Code + ". Make sure that the dependent structure is before this one in the list.");
                }
                StoryStructureInstancesDirty = true;
            }

            structureLocations = occupiedLocations.ToArray();
            SetupForceLandform();
        }

        private void SetupForceLandform()
        {
            var genmaps = api.ModLoader.GetModSystem<GenMaps>();

            foreach (var val in scfg.Structures)
            {
                if (!storyStructureInstances.ContainsKey(val.Code)) continue;

                if (val.ForceTemperature != null || val.ForceRain != null)
                {
                    genmaps.ForceClimateAt(new ForceClimate
                    {
                        Radius = storyStructureInstances[val.Code].LandformRadius,
                        CenterPos = storyStructureInstances[val.Code].CenterPos,
                        Climate = (Climate.DescaleTemperature(val.ForceTemperature ?? 0f) << 16) + ((val.ForceRain ?? 0) << 8)
                    });
                }

                if (val.RequireLandform == null) continue;

                genmaps.ForceLandformAt(new ForceLandform()
                {
                    Radius = storyStructureInstances[val.Code].LandformRadius,
                    CenterPos = storyStructureInstances[val.Code].CenterPos,
                    LandformCode = val.RequireLandform
                });
            }
        }

        private void Event_GameWorldSave()
        {
            if (StoryStructureInstancesDirty)
            {
                api.WorldManager.SaveGame.StoreData("storystructurelocations", SerializerUtil.Serialize(storyStructureInstances));
                StoryStructureInstancesDirty = false;
            }
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
                structureLocations = storyStructureInstances.Select(val => val.Value.Location).ToArray();
            }
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
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
                    if (!strucInst.DidGenerate)
                    {
                        strucInst.DidGenerate = true;
                        StoryStructureInstancesDirty = true;
                    }
                    BlockPos startPos = new BlockPos(strucloc.X1, strucloc.Y1, strucloc.Z1);

                    var structure = scfg.Structures[i];

                    switch (structure.Placement)
                    {
                        case EnumStructurePlacement.SurfaceRuin:
                        {
                            int h;
                            if (strucInst.WorldgenHeight >= 0)
                            {
                                h = strucInst.WorldgenHeight;
                            }
                            else
                            {
                                h = chunks[0].MapChunk.WorldGenTerrainHeightMap[(startPos.Z % chunksize) * chunksize + (startPos.X % chunksize)];
                                strucInst.WorldgenHeight = h;
                                StoryStructureInstancesDirty = true;
                                strucloc.Y1 = h - structure.schematicData.SizeY + structure.schematicData.OffsetY;
                                strucloc.Y2 = strucloc.Y1 + structure.schematicData.SizeY;
                            }
                            startPos.Y = h - structure.schematicData.SizeY + structure.schematicData.OffsetY;
                            break;
                        }
                        case EnumStructurePlacement.Surface:
                            startPos.Y = api.World.SeaLevel + structure.schematicData.OffsetY;
                            break;
                    }

                    Block rockBlock = null;
                    if (structure.resolvedRockTypeRemaps != null)
                    {
                        if (string.IsNullOrEmpty(strucInst.RockBlockCode))
                        {
                            strucRand.InitPositionSeed(chunkX, chunkZ);

                            // only get the rock id from the current generating chunk and save it to the story structure
                            var lx = strucRand.NextInt(chunksize);
                            var lz = strucRand.NextInt(chunksize);
                            int posY;
                            // if we are on the surface we need to go down from worldgen height
                            if (structure.Placement is EnumStructurePlacement.Surface or EnumStructurePlacement.SurfaceRuin)
                            {
                                posY = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap[lz * chunksize + lx];
                            }
                            else
                            {
                                posY = startPos.Y + structure.schematicData.SizeY / 2 + strucRand.NextInt(structure.schematicData.SizeY / 2);
                            }

                            for (var j = 0; rockBlock == null && j < 10; j++)
                            {
                                var block = worldgenBlockAccessor.GetBlock(chunkX * chunksize + lx, posY, chunkZ * chunksize + lz, BlockLayersAccess.Solid);

                                if (block.BlockMaterial == EnumBlockMaterial.Stone)
                                {
                                    rockBlock = block;
                                    strucInst.RockBlockCode = block.Code.ToString();
                                    StoryStructureInstancesDirty = true;
                                }

                                if (structure.Placement is EnumStructurePlacement.Surface or EnumStructurePlacement.SurfaceRuin)
                                {
                                    posY--;
                                }
                                else
                                {
                                    posY = startPos.Y + structure.schematicData.SizeY / 2 + strucRand.NextInt(structure.schematicData.SizeY / 2);
                                }
                            }

                            if (string.IsNullOrEmpty(strucInst.RockBlockCode))
                            {
                                api.Logger.Warning($"Could not find rock block code for {strucInst.Code}");
                            }
                        }
                        else
                        {
                            rockBlock = worldgenBlockAccessor.GetBlock(new AssetLocation(strucInst.RockBlockCode));
                        }

                    }
                    int blocksPlaced = structure.schematicData.PlacePartial(chunks, worldgenBlockAccessor, api.World, chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll, structure.Placement, GenStructures.ReplaceMetaBlocks, GenStructures.ReplaceMetaBlocks,structure.resolvedRockTypeRemaps, structure.replacewithblocklayersBlockids, rockBlock);
                    if (blocksPlaced > 0)
                    {
                        if (structure.Placement is EnumStructurePlacement.Surface or EnumStructurePlacement.SurfaceRuin)
                        {
                           UpdateHeightmap(request, worldgenBlockAccessor);
                        }
                        if(structure.GenerateGrass)
                            GenerateGrass(request);
                    }
                    string code = structure.Code + ":" + structure.Schematics[0];

                    var region = chunks[0].MapChunk.MapRegion;

                    if (region.GeneratedStructures.FirstOrDefault(struc => struc.Code.Equals(code)) == null) {
                        region.AddGeneratedStructure(new GeneratedStructure() { Code = code, Group = structure.Group, Location = strucloc.Clone() });
                    }

                    if (blocksPlaced > 0 && structure.BuildProtected)
                    {
                        if (!structure.ExcludeSchematicSizeProtect)
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
                        if (structure.ExtraLandClaimX > 0 && structure.ExtraLandClaimZ > 0)
                        {
                            var struclocDeva = new Cuboidi(
                                strucloc.Center.X - structure.ExtraLandClaimX, 0, strucloc.Center.Z - structure.ExtraLandClaimZ,
                                strucloc.Center.X + structure.ExtraLandClaimX, api.WorldManager.MapSizeY, strucloc.Center.Z + structure.ExtraLandClaimZ);
                            var claims = api.World.Claims.Get(struclocDeva.Center.AsBlockPos);
                            if (claims == null || claims.Length == 0)
                            {
                                api.World.Claims.Add(new LandClaim()
                                {
                                    Areas = new List<Cuboidi>() { struclocDeva },
                                    Description = structure.BuildProtectionDesc,
                                    ProtectionLevel = 10,
                                    LastKnownOwnerName = structure.BuildProtectionName,
                                    AllowUseEveryone = true
                                });
                            }
                        }
                        if (structure.CustomLandClaims != null)
                        {
                            foreach (var buildProtect in structure.CustomLandClaims)
                            {
                                var cuboidi = buildProtect.Clone();
                                cuboidi.X1 += strucloc.X1;
                                cuboidi.X2 += strucloc.X1;
                                cuboidi.Y1 += strucloc.Y1;
                                cuboidi.Y2 += strucloc.Y1;
                                cuboidi.Z1 += strucloc.Z1;
                                cuboidi.Z2 += strucloc.Z1;
                                var claims = api.World.Claims.Get(cuboidi.Center.AsBlockPos);
                                if (claims == null || claims.Length == 0)
                                {
                                    api.World.Claims.Add(new LandClaim()
                                    {
                                        Areas = new List<Cuboidi>() { cuboidi },
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
            }
        }

        private void UpdateHeightmap(IChunkColumnGenerateRequest request, IWorldGenBlockAccessor worldGenBlockAccessor)
        {
            var updatedPositionsT = 0;
            var updatedPositionsR = 0;

            var rainHeightMap = request.Chunks[0].MapChunk.RainHeightMap;
            var terrainHeightMap = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap;
            for (int i = 0; i < rainHeightMap.Length; i++)
            {
                rainHeightMap[i] = 0;
                terrainHeightMap[i] = 0;
            }

            var mapSizeY = worldgenBlockAccessor.MapSizeY;
            var mapSize2D = chunksize * chunksize;
            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    var mapIndex = z * chunksize + x;
                    bool rainSet = false;
                    bool heightSet = false;
                    for (int posY = mapSizeY - 1; posY >= 0; posY--)
                    {
                        var y = posY % chunksize;
                        var chunk = request.Chunks[posY / chunksize];
                        var chunkIndex = (y * chunksize + z) * chunksize + x;
                        var blockId = chunk.Data[chunkIndex];
                        if (blockId != 0)
                        {
                            var newBlock = worldGenBlockAccessor.GetBlock(blockId);
                            var newRainPermeable = newBlock.RainPermeable;
                            var newSolid = newBlock.SideSolid[BlockFacing.UP.Index];
                            if (!newRainPermeable && !rainSet)
                            {
                                rainSet = true;
                                rainHeightMap[mapIndex] = (ushort)posY;
                                updatedPositionsR++;
                            }

                            if (newSolid && !heightSet)
                            {
                                heightSet = true;
                                terrainHeightMap[mapIndex] = (ushort)posY;
                                updatedPositionsT++;
                            }

                            if (updatedPositionsR >= mapSize2D && updatedPositionsT >= mapSize2D)
                                return;
                        }
                    }
                }
            }
        }

        private void GenerateGrass(IChunkColumnGenerateRequest request)
        {
            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            grassRand.InitPositionSeed(chunkX, chunkZ);

            var forestMap = chunks[0].MapChunk.MapRegion.ForestMap;
            var climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;

            ushort[] heightMap = chunks[0].MapChunk.RainHeightMap;

            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            int rdx = chunkX % regionChunkSize;
            int rdz = chunkZ % regionChunkSize;

            // Amount of data points per chunk
            float climateStep = (float)climateMap.InnerSize / regionChunkSize;
            float forestStep = (float)forestMap.InnerSize / regionChunkSize;

            // Retrieves the map data on the chunk edges
            int forestUpLeft = forestMap.GetUnpaddedInt((int)(rdx * forestStep), (int)(rdz * forestStep));
            int forestUpRight = forestMap.GetUnpaddedInt((int)(rdx * forestStep + forestStep), (int)(rdz * forestStep));
            int forestBotLeft = forestMap.GetUnpaddedInt((int)(rdx * forestStep), (int)(rdz * forestStep + forestStep));
            int forestBotRight = forestMap.GetUnpaddedInt((int)(rdx * forestStep + forestStep), (int)(rdz * forestStep + forestStep));

            var herePos = new BlockPos();


            for (int x = 0; x < chunksize; x++)
            {
                for (int z = 0; z < chunksize; z++)
                {
                    herePos.Set(chunkX * chunksize + x, 1, chunkZ * chunksize + z);
                    // Some weird randomnes stuff to hide fundamental bugs in the climate transition system :D T_T   (maybe not bugs but just fundamental shortcomings of using lerp on a very low resolution map)
                    int rnd = RandomlyAdjustPosition(herePos, out double distx, out double distz);


                    int posY = heightMap[z * chunksize + x];
                    if (posY >= mapheight) continue;

                    int climate = climateMap.GetUnpaddedColorLerped(
                        rdx * climateStep + climateStep * (x + (float)distx) / chunksize,
                        rdz * climateStep + climateStep * (z + (float)distz) / chunksize
                    );

                    int tempUnscaled = (climate >> 16) & 0xff;
                    float temp = Climate.GetScaledAdjustedTemperatureFloat(tempUnscaled, posY - TerraGenConfig.seaLevel + rnd);
                    float tempRel = Climate.GetAdjustedTemperature(tempUnscaled, posY - TerraGenConfig.seaLevel + rnd) / 255f;
                    float rainRel = Climate.GetRainFall((climate >> 8) & 0xff, posY + rnd) / 255f;
                    float forestRel = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, (float)x / chunksize, (float)z / chunksize) / 255f;

                    int rocky = chunks[0].MapChunk.WorldGenTerrainHeightMap[z * chunksize + x];
                    int chunkY = rocky / chunksize;
                    int lY = rocky % chunksize;
                    int index3d = (chunksize * lY + z) * chunksize + x;

                    int rockblockID = chunks[chunkY].Data.GetBlockIdUnsafe(index3d);
                    var hereblock = api.World.Blocks[rockblockID];
                    if (hereblock.BlockMaterial != EnumBlockMaterial.Soil)
                    {
                        continue;
                    }
                    PlaceTallGrass(x, posY, z, chunks, rainRel, tempRel, temp, forestRel);
                }
            }
        }

        public int RandomlyAdjustPosition(BlockPos herePos, out double distx, out double distz)
        {
            distx = distort2dx.Noise(herePos.X, herePos.Z);
            distz = distort2dz.Noise(herePos.X, herePos.Z);
            return (int)(distx / 5);
        }

        private void PlaceTallGrass(int x, int posY, int z, IServerChunk[] chunks, float rainRel, float tempRel, float temp, float forestRel)
        {
            double rndVal = blockLayerConfig.Tallgrass.RndWeight * grassRand.NextDouble() + blockLayerConfig.Tallgrass.PerlinWeight * grassDensity.Noise(x, z, -0.5f);

            double extraGrass = Math.Max(0, rainRel * tempRel - 0.25);

            if (rndVal <= GameMath.Clamp(forestRel - extraGrass, 0.05, 0.99) || posY >= mapheight - 1 || posY < 1) return;

            int blockId = chunks[posY / chunksize].Data[(chunksize * (posY % chunksize) + z) * chunksize + x];

            if (api.World.Blocks[blockId].Fertility <= grassRand.NextInt(100)) return;

            double gheight = Math.Max(0, grassHeight.Noise(x, z) * blockLayerConfig.Tallgrass.BlockCodeByMin.Length - 1);
            int start = (int)gheight + (grassRand.NextDouble() < gheight ? 1 : 0);

            for (int i = start; i < blockLayerConfig.Tallgrass.BlockCodeByMin.Length; i++)
            {
                TallGrassBlockCodeByMin bcbymin = blockLayerConfig.Tallgrass.BlockCodeByMin[i];

                if (forestRel <= bcbymin.MaxForest && rainRel >= bcbymin.MinRain && temp >= bcbymin.MinTemp)
                {
                    chunks[(posY + 1) / chunksize].Data[(chunksize * ((posY + 1) % chunksize) + z) * chunksize + x] = bcbymin.BlockId;
                    return;
                }
            }
        }

        public void GenerateHookStructure(IBlockAccessor blockAccessor, BlockPos pos, string param)
        {
            var code = new AssetLocation(param);
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
                int[] counts = new int[hookStruct.mainElements.Length];
                int[] maxCounts = new int[hookStruct.mainElements.Length];
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
                    maxCounts[structuresLength] = el.maxCount == 0 ? GlobalConstants.MaxWorldSizeY : el.maxCount;
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
                if (height < api.World.SeaLevel) height = api.World.SeaLevel;
                height = Math.Min(height, api.World.BlockAccessor.MapSizeY - 11);   // ensure enough space for most of the entrance, even if we are near the very top of the map

                for (int j = 0; j < 25; j++)
                {
                    indices.Clear();
                    for (int k = 0; k < counts.Length; k++) counts[k] = 0;
                    testHeight = pos.Y;
                    while (testHeight < height)
                    {
                        int i = rand.Next(structuresLength);
                        if (counts[i] >= maxCounts[i]) continue;   // try again if this rand structure already reached its max count in this set of indices
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
                                counts[i]++;   // Probably redundant due to the break statement below, but does no harm
                            }

                            int newDiff = testHeight + h - height;
                            if (newDiff < bestDiff)
                            {
                                bestDiff = newDiff;
                                bestIndices.Clear();
                                foreach (int ix in indices) bestIndices.Add(ix);
                                if (bestDiff == 0) j = 25;  // early exit if we already have an optimal set of indices, by fast-forwarding outer loop;
                            }

                            break;
                        }

                        indices.Add(i);
                        counts[i]++;
                        testHeight += h;
                    }
                }

                var posY = pos.Y;
                var entranceMinX = int.MaxValue;
                var entranceMinZ = int.MaxValue;
                var entranceMaxX = 0;
                var entranceMaxZ = 0;
                foreach (int ix in bestIndices)
                {
                    var struc = structures[ix];
                    var offset = offsets[ix];
                    BlockPos posPlace = pos.AddCopy(offset.X, offset.Y, offset.Z);
                    struc.PlaceRespectingBlockLayers(blockAccessor, api.World, posPlace, 0, 0, 0, 0, null, new int[0], GenStructures.ReplaceMetaBlocks, true, false, true);
                    pos.Y += struc.SizeY;

                    entranceMinX = Math.Min(entranceMinX, posPlace.X);
                    entranceMinZ = Math.Min(entranceMinZ, posPlace.Z);
                    entranceMaxX = Math.Max(entranceMaxX, posPlace.X+struc.SizeX);
                    entranceMaxZ = Math.Max(entranceMaxZ, posPlace.Z+struc.SizeY);
                }

                var entranceRegion = blockAccessor.GetMapRegion(pos.X / blockAccessor.RegionSize, pos.Z / blockAccessor.RegionSize);

                var location = new Cuboidi(entranceMinX, posY, entranceMinZ, entranceMaxX, pos.Y, entranceMaxZ);
                entranceRegion.AddGeneratedStructure(new GeneratedStructure()
                {
                    Code = param,
                    Group = hookStruct.group,
                    Location = location.Clone()
                });

                if (hookStruct.buildProtected)
                {
                    api.World.Claims.Add(new LandClaim
                    {
                        Areas = new List<Cuboidi> { location },
                        Description = hookStruct.buildProtectionDesc,
                        ProtectionLevel = 10,
                        LastKnownOwnerName = hookStruct.buildProtectionName,
                        AllowUseEveryone = true
                    });
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
            structTop.PlaceRespectingBlockLayers(blockAccessor, api.World, pos, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight, null, replaceblockids, GenStructures.ReplaceMetaBlocks, true, true);

            var locationEnd = new Cuboidi(pos.X, pos.Y, pos.Z, pos.X + structTop.SizeX, pos.Y + structTop.SizeY, pos.Z + structTop.SizeZ);
            region.AddGeneratedStructure(new GeneratedStructure()
            {
                Code = hookStruct.group,
                Group = hookStruct.group,
                Location = locationEnd.Clone()
            });
            // do not protect/land claim the top room since it rarely requires players to dig up some blocks
        }
    }
}
