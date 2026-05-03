using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumBobberState
    {
        Baiting = 0, // Waiting to see if there's something to catch
        NoFishNearby = 1, // Waiting to catch a non-entity fish or junk
        FishNearby = 2, // Waiting to catch an entity fish
        NoEntityFishCatch = 3, // Catching a non-entity fish
        JunkCatch = 4, // Catching junk
        EntityFishCatch = 5 // Catching an entity fish
    }


    public class ModSystemFishDepletion : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        protected ICoreServerAPI sapi;
        protected Dictionary<BlockPos, CreatureHarvest> harvestedLocations = new Dictionary<BlockPos, CreatureHarvest>();

        public int Scale = 8;
        public static int MaxHarvestablePerLocation = 12;
        public static double RestoreFishAfterDays = 14;

        public override double ExecuteOrder() => 1;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.RegisterGameTickListener(restoreFish, 10000, sapi.World.Rand.Next(1000));
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("harvestedFishLocations", harvestedLocations);
        }

        private void Event_SaveGameLoaded()
        {
            try
            {
                harvestedLocations = sapi.WorldManager.SaveGame.GetData<Dictionary<BlockPos, CreatureHarvest>>("harvestedFishLocations");
            }
            catch
            {
                // Don't care if this is corrupted data, its unessential
            }

            if (harvestedLocations == null)
            {
                harvestedLocations = new Dictionary<BlockPos, CreatureHarvest>();
            }
        }

        private void restoreFish(float dt)
        {
            var positions = new List<BlockPos>(harvestedLocations.Keys);
            var totaldays = sapi.World.Calendar.TotalDays;
            foreach (var pos in positions)
            {
                if (totaldays - harvestedLocations[pos].TotalDays > RestoreFishAfterDays)
                {
                    harvestedLocations.Remove(pos);
                }
            }
        }

        public void AddHarvest(BlockPos pos, int quantity)
        {
            CreatureHarvest harvest;
            harvestedLocations.TryGetValue(pos / Scale, out harvest);
            harvestedLocations[pos / Scale] = new CreatureHarvest() {
                TotalDays = sapi.World.Calendar.TotalDays,
                Quantity = harvest.Quantity + quantity
            };
        }

        public float GetHarvestAmount(BlockPos pos)
        {
            if (harvestedLocations.TryGetValue(pos / Scale, out var harvest))
            {
                return harvest.Quantity;
            }

            return 0;
        }
    }




    // Ey, kurwa bober
    public class EntityBobber : EntityProjectile, IRopeRippedListener
    {
        protected float swimmingAccum = 0f;
        protected float catchAccum = 0f;
        protected float junkCatchChance = 0f;
        protected EnumBobberState bobberState;
        public EntityFish caughtFish;
        protected EntityPartitioning ep;
        protected bool wasSwimming;


        public override float MaterialDensity => 50;
        public override bool ApplyGravity => true;
        public override bool CanCollect(Entity byEntity) => false;
        protected override bool TryAttackEntity(double impactSpeed)
        {
            return true;
        }

        public ItemStack BaitStack { get; set; }        

        public long AttachedToEntityId
        {
            get { return WatchedAttributes.GetLong("attachedToEntityId"); }
            set { WatchedAttributes.SetLong("attachedToEntityId", value); }
        }

        // The time stamp in total hours when the bobber was cast. 
        public double CastTotalHours
        {
            get { return WatchedAttributes.GetDouble("castTotalHours"); }
            set { WatchedAttributes.SetDouble("castTotalHours", value); }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            ep = api.ModLoader.GetModSystem<EntityPartitioning>();

            junkCatchChance = properties.Attributes["junkCatchChance"].AsFloat(0);

            impactSound = null;

            BaitStack?.ResolveBlockOrItem(Api.World);

            var cm = api.ModLoader.GetModSystem<ClothManager>();
            var cs = cm.GetClothSystemAttachedToEntity(EntityId);

            if (api.World.GetEntityById(AttachedToEntityId) == null)
            {   
                if (cs != null) cm.UnregisterCloth(cs.ClothId);
                Die();
            }
        }

        bool ripSet;

        public override void OnGameTick(float dt)
        {
            if (World.Side == EnumAppSide.Server)
            {
                onServertick(dt);
            }

            // Since we don't set CanRip on spawn, this value is not synced after setting it
            if (!ripSet && Api.Side == EnumAppSide.Client)
            {
                var cm = Api.ModLoader.GetModSystem<ClothManager>();
                var cs = cm.GetClothSystemAttachedToEntity(EntityId);
                if (cs != null) cs.CanRip = true;
                ripSet = true;
            }            
            

            if (Swimming)
            {
                Pos.Pitch *= 0.95f;
                Pos.Roll *= 0.95f;
                swimmingAccum += dt;

                // Copied from Entity.cs because we don't run the base method
                if (World.EntityDebugMode)
                {
                    DebugAttributes.SetString("AttachedToEntityId", "" + AttachedToEntityId);
                    UpdateDebugAttributes();
                    DebugAttributes.MarkAllDirty();
                }

                return; // Skip base entity ticking
            }

            base.OnGameTick(dt);
        }

        
        private void onServertick(float dt)
        {
            float abundance = 0.5f;
            getRandomFishEntityProperties(__instance.BaitStack, out abundance, false);

            if (Swimming && !wasSwimming)
            {
                wasSwimming = true;
                if (Api.World.EntityDebugMode)
                {
                    printLocationDebugInfo();
                }
            }

            switch(bobberState)
            {
                case EnumBobberState.Baiting:
                {
                    // Wait 1 second after casting, then check for nearby entities
                    if (swimmingAccum > 1f) 
                    {
                        Entity nearestEntity = ep.GetNearestEntity(__instance.Pos.XYZ, 20.0, (Entity e) => e is EntityFish, EnumEntitySearchType.Creatures);
                        bobberState = (nearestEntity != null) ? EnumBobberState.FishNearby : EnumBobberState.NoFishNearby;
                    }
                    break;
                }
                case EnumBobberState.FishNearby:
                {
                    // Catch entity if it comes close enough, or assume it's gone if it doesn't arrive after 15 seconds
                    if (swimmingAccum > 15f) 
                    {
                        bobberState = EnumBobberState.NoFishNearby;
                    }
                    else 
                    {
                        Entity nearestEntity = ep.GetNearestEntity(Pos.XYZ, 1.0, (Entity e) => e is EntityFish, EnumEntitySearchType.Creatures);
                        if (nearestEntity != null) 
                        {
                            bobberState = EnumBobberState.EntityFishCatch;
                            caughtFish = nearestEntity as EntityFish;
                            catchAccum += dt;
                            playCatchEffects();
                        }
                    }
                    break;    
                }
                case EnumBobberState.NoFishNearby:
                {
                    // Wait according to abundance, then catch junk or fish from stock
                    if (abundance > 0 && swimmingAccum > 5.0 / Math.Max(0.04, abundance)) 
                    {
                        bobberState = Api.World.Rand.NextDouble() < (double) junkCatchChance ? EnumBobberState.JunkCatch : EnumBobberState.NoEntityFishCatch; 
                        catchAccum += dt;
                        playCatchEffects();
                    }   
                    break;
                }
                case EnumBobberState.NoEntityFishCatch:
                case EnumBobberState.JunkCatch:
                case EnumBobberState.EntityFishCatch: 
                {
                    // Wait 0.7 seconds for player to reel in catch, then reset
                    if (catchAccum > 0.7f) 
                    {
                        if (caughtFish != null)
                        {
                            AiTaskManager taskManager = caughtFish.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                            IAiTask aiTask = taskManager?.GetTask("fleebobber");
                            if (aiTask != null)
                            {
                                taskManager.ExecuteTask(aiTask, aiTask.Slot);
                            }
                            caughtFish = null;
                        }
                        
                        BaitStack = null; 
                        WatchedAttributes.MarkPathDirty("baitStack");
                        catchAccum = 0f; 
                        swimmingAccum = 0f;
                        bobberState = EnumBobberState.Baiting;
                    }
                    else
                    {
                        catchAccum += dt;
                    }
                    break;
                }
            }
        }


        public void TryCatchFish(EntityAgent entityCatcher)
        {
            ItemStack[] drops = [];

            switch(bobberState)
            {
                case EnumBobberState.EntityFishCatch:
                {
                    // Kill entity and take its drops
                    if (caughtFish != null && caughtFish.Alive)
                    {
                        caughtFish.Die(EnumDespawnReason.Expire);
                        drops = caughtFish.GetDrops(World, caughtFish.Pos.XYZInt.AsBlockPos, (entityCatcher as EntityPlayer)?.Player);
                    }
                    break;
                }
                case EnumBobberState.NoEntityFishCatch:
                {
                    // Harvest stock fish and age it according to abundance if possible
                    float abundance = 0f;
                    EntityProperties fishCatch = getRandomFishEntityProperties(BaitStack, out abundance, false);

                    ItemStack fishStack = fishCatch.Drops[0].ResolvedItemstack;
                    string age = (Api.World.Rand.NextDouble() < (double) abundance) ? "adult" : "juvenile";
                    CollectibleObject agedFish = Api.World.GetItem(fishStack.Collectible.CodeWithVariant("age", age));
                    fishStack = agedFish != null ? new ItemStack(agedFish) : fishStack.Clone();

                    drops = [fishStack];

                    Api.ModLoader.GetModSystem<ModSystemFishDepletion>().AddHarvest(Pos.XYZ.AsBlockPos, 1);

                    break;
                }
                case EnumBobberState.JunkCatch:
                {
                    // Get weighted random junk from pool
                    WeightedBlockDropItemstack[] junkCatches = Properties.Attributes["junkCatches"].AsObject<WeightedBlockDropItemstack[]>();
                    junkCatches.Shuffle(Api.World.Rand);
                    double total = 0d;
                    foreach (WeightedBlockDropItemstack junkCatch in junkCatches)
                    {
                        total += junkCatch.Weight;
                    }

                    double selection = Api.World.Rand.NextDouble() * total;
                    foreach (WeightedBlockDropItemstack junkCatch in junkCatches)
                    {
                        selection -= junkCatch.Weight;
                        if (selection < 0d)
                        {
                            junkCatch.Resolve(Api.World, "bobber junk catch", Code);
                            drops = [junkCatch.ResolvedItemstack.Clone()];
                            break;
                        }
                    }
                    break;
                }
            }

            BaitStack = null;
            WatchedAttributes.MarkPathDirty("baitStack");

            foreach (ItemStack drop in drops)
            {
                if (!entityCatcher.TryGiveItemStack(drop))
                {
                    World.SpawnItemEntity(drop, entityCatcher.Pos.XYZ);
                }
            }
        }


        private void printLocationDebugInfo()
        {
            getRandomFishEntityProperties(null, out _, true);
        }


        private EntityProperties getRandomFishEntityProperties(ItemStack baitStack, out float abundanceValue, bool printDebug = false)
        {
            abundanceValue = 0f;

            ClimateCondition climate = World.BlockAccessor.GetClimateAt(Pos.AsBlockPos, EnumGetClimateMode.WorldGenValues);
            if (climate == null) return null;

            pondSize = pondSize < 0 ? getPondSize() : pondSize; 
            if (pondSize < 100) return null; // Check this early so that tiny ponds return quickly and with 0 abundance

            List<EntityProperties> spawnable = [];
            Block block = World.BlockAccessor.GetBlock(Pos.XYZ.AsBlockPos, 2);
            string bait = BaitStack?.Collectible.Attributes?["baitTag"].AsString() ?? "nobait";

            // Get animal spawn maps for this region
            Vec3d xYZ = Pos.XYZ;
            int regionSize = World.BlockAccessor.RegionSize;
            int animalMapsPerRegion = regionSize / TerraGenConfig.animalMapScale;
            int xInRegion = xYZ.XInt % regionSize;
            int zInRegion = xYZ.ZInt % regionSize;
            float xInAnimalMap = GameMath.Clamp((float)xInRegion / (float)regionSize * (float)animalMapsPerRegion, 0f, animalMapsPerRegion - 1);
            float zInAnimalMap = GameMath.Clamp((float)zInRegion / (float)regionSize * (float)animalMapsPerRegion, 0f, animalMapsPerRegion - 1);
            IMapRegion mapRegion = World.BlockAccessor.GetMapRegion(xYZ.XInt / regionSize, xYZ.ZInt / regionSize);
 
            // 1. Combined filter by animal map, climate, and bait 
            foreach (EntityProperties entityType in World.EntityTypes)
            {
                BaseSpawnConditions spawnConditions = entityType.Server.SpawnConditions?.Runtime ?? entityType.Server.SpawnConditions?.Worldgen as BaseSpawnConditions;
                ClimateSpawnCondition spawnClimate = entityType.Server.SpawnConditions?.Climate ?? spawnConditions;
                string mapCode = entityType.Server.SpawnConditions?.Climate?.MapCode ?? entityType.Server.SpawnConditions?.Runtime?.MapCode ?? entityType.Server.SpawnConditions?.Worldgen?.MapCode;

                if (mapCode != null && spawnClimate.MatchesClimate(climate) && spawnConditions.CanSpawnInside(block))
                {
                    bool likesBait = entityType.Attributes["baitTags"].AsArray<string>().Contains<string>(bait);
                    ByteDataMap2D animalMap = mapRegion.AnimalSpawnMaps.Get(mapCode);

                    if (likesBait && animalMap.GetUnpaddedLerped(xInAnimalMap, zInAnimalMap) > 128f)
                    {
                        spawnable.Add(entityType);
                    }
                }
            }

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("1. Found suitable fish types: " + string.Join(", ", spawnable.Select(props => props.Code)));
            }

            if (spawnable.Count == 0) return null;

            // 2. Now filter by overall "fish frequency map"
            double noisyAbundance = (Api.ModLoader.GetModSystem<FishingSupportModSystem>().NoiseGen.Noise(xYZ.X, xYZ.Z) - 0.4f) * 3.0;
            abundanceValue = (float)GameMath.Clamp(noisyAbundance, 0.2f, 1.0); 
            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("2. Fish frequency map value: " + abundanceValue);
            }

            // 3. Now filter by pond size
            abundanceValue *= pondSize / 1200f;
            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("3. Pond size: " + pondSize);
            }

            // 4. Now filter by fish depletion map
            float alreadyHarvested = Api.ModLoader.GetModSystem<ModSystemFishDepletion>().GetHarvestAmount(Pos.XYZ.AsBlockPos);
            float maxHarvestable = ModSystemFishDepletion.MaxHarvestablePerLocation * 0.8f;
            float remainingHarvestable = 1f - GameMath.Clamp(alreadyHarvested / maxHarvestable - 0.2f, 0f, 1f);
            abundanceValue *= remainingHarvestable;
            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("4. Fish depletion here " + ((1 - remainingHarvestable) * 100) + "% (caught: " + alreadyHarvested + ")");
            }

            // 5. Pick a random one from the leftovers
            var result = spawnable[Api.World.Rand.Next(spawnable.Count)];
            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("5. Randomly selected fish: " + result.Code);
            }

            return result;
        }


        Queue<FastVec3i> bfsQueue = new Queue<FastVec3i>();
        HashSet<FastVec3i> visited = new HashSet<FastVec3i>();

        int pondSize = -1;

        /// <summary>
        /// Returns the amount of water blocks. Search algorithm caps out at 1200 blocks
        /// </summary>
        /// <returns></returns>
        private int getPondSize()
        {
            var ba = Api.World.BlockAccessor;
            var pos = Pos.XYZ.AsBlockPos;

            visited.Clear();
            bfsQueue.Clear();

            BlockPos curPos = Pos.AsBlockPos;

            bfsQueue.Enqueue(new FastVec3i(curPos.X, curPos.Y, curPos.Z));

            // not up, we're already at the surface
            var faces = new BlockFacing[] { BlockFacing.NORTH, BlockFacing.EAST, BlockFacing.SOUTH, BlockFacing.WEST, BlockFacing.DOWN };

            int waterBlocks = 0;

            while (bfsQueue.Count > 0)
            {
                FastVec3i bpos = bfsQueue.Dequeue();
                foreach (BlockFacing facing in faces)
                {
                    curPos.Set(bpos.X + facing.Normali.X, bpos.Y + facing.Normali.Y, bpos.Z + facing.Normali.Z);
                    var ipos = new FastVec3i(curPos);
                    if (visited.Contains(ipos)) continue;

                    visited.Add(ipos);
                    if (waterBlocks > 1200) return waterBlocks;

                    var block = ba.GetBlock(curPos, BlockLayersAccess.Fluid);
                    if (block.Id != 0)
                    {
                        bfsQueue.Enqueue(ipos);
                        waterBlocks++;
                    }
                }
            }

            return waterBlocks;
        }

        private void playCatchEffects()
        {
            Pos.Motion.Y -= 0.1f;
            World.PlaySoundAt("sounds/environment/mediumsplash", this);
            EntityPos pos = Pos;
            double width = SelectionBox.XSize;
            double height = SelectionBox.YSize;
            SplashParticleProps.BasePos.Set(pos.X - width / 2, pos.Y - 0.75, pos.Z - width / 2);
            SplashParticleProps.AddPos.Set(width, 0.75, width);

            SplashParticleProps.AddVelocity.Set((float)GameMath.Clamp(pos.Motion.X * 30f, -2, 2), 1, (float)GameMath.Clamp(pos.Motion.Z * 30f, -2, 2));
            SplashParticleProps.QuantityMul = 5;

            World.SpawnParticles(SplashParticleProps);
        }

        public override double SwimmingOffsetY => 0.2f;


        public override void FromBytes(BinaryReader reader, bool isSync)
        {
            base.FromBytes(reader, isSync);

            BaitStack = WatchedAttributes.GetItemstack("baitStack", null);
            if (Api?.World != null) BaitStack?.ResolveBlockOrItem(Api.World);
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            WatchedAttributes.SetItemstack("baitStack", BaitStack);

            base.ToBytes(writer, forClient);
        }

        public void OnRopeRipped(ClothSystem cs)
        {
            Die();
        }
    }
}
