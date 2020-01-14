using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class SpawnOppurtunity
    {
        public EntityProperties ForType;
        public Vec3d Pos;
    }

    public class GenCreatures : ModStdWorldGen
    {
        ICoreServerAPI api;
        Random rnd;
        int worldheight;
        IWorldGenBlockAccessor wgenBlockAccessor;
        CollisionTester collisionTester = new CollisionTester();

       
        Dictionary<EntityProperties, EntityProperties[]> entityTypeGroups = new Dictionary<EntityProperties, EntityProperties[]>();


        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override double ExecuteOrder()
        {
            return 0.1;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            if (DoDecorationPass)
            {
                api.Event.InitWorldGenerator(initWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.PreDone, "standard");

                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            wgenBlockAccessor = chunkProvider.GetBlockAccessor(true);
        }


        private void initWorldGen()
        {
            LoadGlobalConfig(api);
            rnd = new Random(api.WorldManager.Seed - 18722);
            chunksize = api.WorldManager.ChunkSize;
            worldheight = api.WorldManager.MapSizeY;

            Dictionary<AssetLocation, EntityProperties> entityTypesByCode = new Dictionary<AssetLocation, EntityProperties>();

            for (int i = 0; i < api.World.EntityTypes.Count; i++)
            {
                entityTypesByCode[api.World.EntityTypes[i].Code] = api.World.EntityTypes[i];
            }

            for (int i = 0; i < api.World.EntityTypes.Count; i++)
            {
                if (api.World.EntityTypes[i].Server?.SpawnConditions?.Worldgen == null) continue;

                List<EntityProperties> grouptypes = new List<EntityProperties>();

                EntityProperties type = api.World.EntityTypes[i];
                grouptypes.Add(type);

                AssetLocation[] companions = type.Server.SpawnConditions.Worldgen.Companions;
                if (companions == null) continue;

                for (int j = 0; j < companions.Length; j++)
                {
                    EntityProperties cptype = null;
                    if (entityTypesByCode.TryGetValue(companions[j], out cptype))
                    {
                        grouptypes.Add(cptype);
                    }
                }

                entityTypeGroups[type] = grouptypes.ToArray();
            }
        }


        int climateUpLeft;
        int climateUpRight;
        int climateBotLeft;
        int climateBotRight;

        int forestUpLeft;
        int forestUpRight;
        int forestBotLeft;
        int forestBotRight;

        int shrubsUpLeft;
        int shrubsUpRight;
        int shrubsBotLeft;
        int shrubsBotRight;


        private void OnChunkColumnGen(IServerChunk[] chunks, int chunkX, int chunkZ, ITreeAttribute chunkGenParams = null)
        {
            IntMap climateMap = chunks[0].MapChunk.MapRegion.ClimateMap;
            ushort[] heightMap = chunks[0].MapChunk.WorldGenTerrainHeightMap;

            int regionChunkSize = api.WorldManager.RegionSize / chunksize;
            int rlX = chunkX % regionChunkSize;
            int rlZ = chunkZ % regionChunkSize;

            float facC = (float)climateMap.InnerSize / regionChunkSize;
            climateUpLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC));
            climateUpRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC));
            climateBotLeft = climateMap.GetUnpaddedInt((int)(rlX * facC), (int)(rlZ * facC + facC));
            climateBotRight = climateMap.GetUnpaddedInt((int)(rlX * facC + facC), (int)(rlZ * facC + facC));

            IntMap forestMap = chunks[0].MapChunk.MapRegion.ForestMap;
            float facF = (float)forestMap.InnerSize / regionChunkSize;
            forestUpLeft = forestMap.GetUnpaddedInt((int)(rlX * facF), (int)(rlZ * facF));
            forestUpRight = forestMap.GetUnpaddedInt((int)(rlX * facF + facF), (int)(rlZ * facF));
            forestBotLeft = forestMap.GetUnpaddedInt((int)(rlX * facF), (int)(rlZ * facF + facF));
            forestBotRight = forestMap.GetUnpaddedInt((int)(rlX * facF + facF), (int)(rlZ * facF + facF));

            IntMap shrubMap = chunks[0].MapChunk.MapRegion.ShrubMap;
            float facS = (float)shrubMap.InnerSize / regionChunkSize;
            shrubsUpLeft = shrubMap.GetUnpaddedInt((int)(rlX * facS), (int)(rlZ * facS));
            shrubsUpRight = shrubMap.GetUnpaddedInt((int)(rlX * facS + facS), (int)(rlZ * facS));
            shrubsBotLeft = shrubMap.GetUnpaddedInt((int)(rlX * facS), (int)(rlZ * facS + facS));
            shrubsBotRight = shrubMap.GetUnpaddedInt((int)(rlX * facS + facS), (int)(rlZ * facS + facS));


            Vec3d posAsVec = new Vec3d();
            BlockPos pos = new BlockPos();

            foreach (var val in entityTypeGroups)
            {
                EntityProperties entitytype = val.Key;
                float tries = entitytype.Server.SpawnConditions.Worldgen.TriesPerChunk.nextFloat(1, rnd);

                while (tries-- > rnd.NextDouble())
                {
                    int dx = rnd.Next(chunksize);
                    int dz = rnd.Next(chunksize);

                    pos.Set(chunkX * chunksize + dx, 0, chunkZ * chunksize + dz);

                    pos.Y = 
                        entitytype.Server.SpawnConditions.Worldgen.TryOnlySurface ? 
                        heightMap[dz * chunksize + dx] + 1: 
                        rnd.Next(worldheight)
                    ;
                    posAsVec.Set(pos.X + 0.5, pos.Y + 0.005, pos.Z + 0.5);

                    TrySpawnGroupAt(pos, posAsVec, entitytype, val.Value);
                }
            }
        }


        List<SpawnOppurtunity> spawnPositions = new List<SpawnOppurtunity>();

        private void TrySpawnGroupAt(BlockPos origin, Vec3d posAsVec, EntityProperties entityType, EntityProperties[] grouptypes)
        {
            BlockPos pos = origin.Copy();

            float xRel = (float)(posAsVec.X % chunksize) / chunksize;
            float zRel = (float)(posAsVec.Z % chunksize) / chunksize;

            int climate = GameMath.BiLerpRgbColor(xRel, zRel, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
            float temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat((climate >> 16) & 0xff, (int)posAsVec.Y - TerraGenConfig.seaLevel);
            float rain = ((climate >> 8) & 0xff) / 255f;
            float forestDensity = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, xRel, zRel);
            float shrubDensity = GameMath.BiLerp(shrubsUpLeft, shrubsUpRight, shrubsBotLeft, shrubsBotRight, xRel, zRel);

            int spawned = 0;

            WorldGenSpawnConditions sc = entityType.Server.SpawnConditions.Worldgen;
            bool hasCompanions = sc.Companions != null && sc.Companions.Length > 0;

            spawnPositions.Clear();

            int nextGroupSize = 0;
            int tries = 10;
            while (nextGroupSize <= 0 && tries-- > 0)
            {
                float val = sc.GroupSize.nextFloat();
                nextGroupSize = (int)val + ((val - (int)val) > rnd.NextDouble() ? 1 : 0);
            }

            
            for (int i = 0; i < nextGroupSize*4 + 5; i++)
            {
                if (spawned >= nextGroupSize) break;

                EntityProperties typeToSpawn = entityType;

                // First entity 80% chance to spawn the dominant creature, every subsequent only 20% chance for males (or even lower if more than 5 companion types)
                double dominantChance = i == 0 ? 0.8 : Math.Min(0.2, 1f / grouptypes.Length);

                if (grouptypes.Length > 1 && rnd.NextDouble() > dominantChance)
                {
                    typeToSpawn = grouptypes[1 + rnd.Next(grouptypes.Length - 1)];
                }

                posAsVec.Set(pos.X + 0.5, pos.Y + 0.005, pos.Z + 0.5);

                IBlockAccessor blockAccesssor = wgenBlockAccessor.GetChunkAtBlockPos(pos.X, pos.Y, pos.Z) == null ? api.World.BlockAccessor : wgenBlockAccessor;

                IMapChunk mapchunk = blockAccesssor.GetMapChunkAtBlockPos(pos);
                if (mapchunk != null)
                {
                    ushort[] heightMap = mapchunk.WorldGenTerrainHeightMap;

                    pos.Y =
                        sc.TryOnlySurface ?
                        heightMap[((int)pos.Z % chunksize) * chunksize + ((int)pos.X % chunksize)] + 1 :
                        pos.Y
                    ;

                    xRel = (float)(posAsVec.X % chunksize) / chunksize;
                    zRel = (float)(posAsVec.Z % chunksize) / chunksize;

                    climate = GameMath.BiLerpRgbColor(xRel, zRel, climateUpLeft, climateUpRight, climateBotLeft, climateBotRight);
                    temp = TerraGenConfig.GetScaledAdjustedTemperatureFloat((climate >> 16) & 0xff, (int)posAsVec.Y - TerraGenConfig.seaLevel);
                    rain = ((climate >> 8) & 0xff) / 255f;
                    forestDensity = GameMath.BiLerp(forestUpLeft, forestUpRight, forestBotLeft, forestBotRight, xRel, zRel) / 255f;
                    shrubDensity = GameMath.BiLerp(shrubsUpLeft, shrubsUpRight, shrubsBotLeft, shrubsBotRight, xRel, zRel) / 255f;


                    if (CanSpawnAt(blockAccesssor, typeToSpawn, pos, posAsVec, sc, rain, temp, forestDensity, shrubDensity))
                    {
                        spawnPositions.Add(new SpawnOppurtunity() { ForType = typeToSpawn, Pos = posAsVec.Clone() });
                        spawned++;
                    }
                }

                pos.X = origin.X + ((rnd.Next(11) - 5) + (rnd.Next(11) - 5)) / 2;
                pos.Z = origin.Z + ((rnd.Next(11) - 5) + (rnd.Next(11) - 5)) / 2;
            }


            // Only spawn if the group reached the minimum group size
            if (spawnPositions.Count >= nextGroupSize)
            {
                long herdId = api.WorldManager.GetNextUniqueId();

                foreach (SpawnOppurtunity so in spawnPositions)
                {
                    Entity ent = CreateEntity(so.ForType, so.Pos);
                    if (ent is EntityAgent)
                    {
                        (ent as EntityAgent).HerdId = herdId;
                    }

                    if (wgenBlockAccessor.GetChunkAtBlockPos(pos.X, pos.Y, pos.Z) == null)
                    {
                        api.World.SpawnEntity(ent);
                    }
                    else
                    {
                        wgenBlockAccessor.AddEntity(ent);
                    }
                }
                
            }
        }


        private Entity CreateEntity(EntityProperties entityType, Vec3d spawnPosition)
        {
            Entity entity = api.ClassRegistry.CreateEntity(entityType);
            entity.ServerPos.SetPos(spawnPosition);
            entity.ServerPos.SetYaw((float)rnd.NextDouble() * GameMath.TWOPI);
            entity.Pos.SetFrom(entity.ServerPos);
            entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            entity.Attributes.SetString("origin", "worldgen");
            return entity;
        }





        private bool CanSpawnAt(IBlockAccessor blockAccessor, EntityProperties type, BlockPos pos, Vec3d posAsVec, BaseSpawnConditions sc, float rain, float temp, float forestDensity, float shrubsDensity)
        {
            if (!api.World.BlockAccessor.IsValidPos(pos)) return false;

            float? lightLevel = blockAccessor.GetLightLevel(pos, EnumLightLevelType.MaxLight);

            if (lightLevel == null) return false;
            if (sc.MinLightLevel > lightLevel || sc.MaxLightLevel < lightLevel) return false;
            if (sc.MinTemp > temp || sc.MaxTemp < temp) return false;
            if (sc.MinRain > rain || sc.MaxRain < rain) return false;
            if (sc.MinForest > forestDensity || sc.MaxForest < forestDensity) return false;
            if (sc.MinShrubs > shrubsDensity || sc.MaxShrubs < shrubsDensity) return false;
            if (sc.MinForestOrShrubs > Math.Max(forestDensity, shrubsDensity)) return false;
            

            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            if (!belowBlock.CanCreatureSpawnOn(blockAccessor, pos.DownCopy(), type, sc))
            { 
                return false;
            }

            Block block = blockAccessor.GetBlock(pos);
            if (!block.WildCardMatch(sc.InsideBlockCodes)) return false;

            Cuboidf collisionBox = type.SpawnCollisionBox.OmniNotDownGrowBy(0.1f);

            return !IsColliding(collisionBox, posAsVec);
        }




        // Custom implementation for mixed generating/loaded chunk access, since we can spawn entities just fine in either loaded or still generating chunks
        public bool IsColliding(Cuboidf entityBoxRel, Vec3d pos)
        {
            BlockPos blockPos = new BlockPos();
            IBlockAccessor blockAccess;
            int chunksize = wgenBlockAccessor.ChunkSize;

            Cuboidd entityCuboid = entityBoxRel.ToDouble().Translate(pos);
            Vec3d blockPosAsVec = new Vec3d();

            int minX = (int)(entityBoxRel.X1 + pos.X);
            int minY = (int)(entityBoxRel.Y1 + pos.Y);
            int minZ = (int)(entityBoxRel.Z1 + pos.Z);
            int maxX = (int)Math.Ceiling(entityBoxRel.X2 + pos.X);
            int maxY = (int)Math.Ceiling(entityBoxRel.Y2 + pos.Y);
            int maxZ = (int)Math.Ceiling(entityBoxRel.Z2 + pos.Z);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        blockAccess = wgenBlockAccessor;
                        IWorldChunk chunk = wgenBlockAccessor.GetChunkAtBlockPos(x, y, z);
                        if (chunk == null)
                        {
                            chunk = api.World.BlockAccessor.GetChunkAtBlockPos(x, y, z);
                            blockAccess = api.World.BlockAccessor;
                        }
                        if (chunk == null) return true;

                        chunk.Unpack();

                        int index = ((y % chunksize) * chunksize + (z % chunksize)) * chunksize + (x % chunksize);
                        Block block = api.World.Blocks[chunk.Blocks[index]];

                        blockPos.Set(x, y, z);
                        blockPosAsVec.Set(x, y, z);

                        Cuboidf[] collisionBoxes = block.GetCollisionBoxes(blockAccess, blockPos);
                        for (int i = 0; collisionBoxes != null && i < collisionBoxes.Length; i++)
                        {
                            Cuboidf collBox = collisionBoxes[i];
                            if (collBox != null && entityCuboid.Intersects(collBox, blockPosAsVec)) return true;
                        }
                    }
                }
            }
            
            return false;
        }
    }
}
