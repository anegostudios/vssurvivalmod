using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
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
        NoCatch = 5, // Nothing currently catchable, so wait a bit
        EntityFishCatch = 6 // Catching an entity fish
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

            junkCatchChance = properties.Attributes["junkCatchChance"].AsFloat(junkCatchChance);

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
                if (cs != null)
                {
                    cs.CanRip = true;
                    ripSet = true;
                }
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
                        Entity fishEntity = ep.GetNearestEntity(Pos.XYZ, 20.0, e => e is EntityFish, EnumEntitySearchType.Creatures);
                        bobberState = fishEntity == null ? EnumBobberState.NoFishNearby : EnumBobberState.FishNearby;
                    }
                    break;
                }
                case EnumBobberState.FishNearby:
                {
                    // Catch entity if it comes close enough, or assume it's gone if it doesn't arrive after 15 seconds
                    if (swimmingAccum > 15f)
                    {
                        bobberState = EnumBobberState.NoFishNearby;
                        break;
                    }

                    Entity fishEntity = ep.GetNearestEntity(Pos.XYZ, 1.0, e => e is EntityFish, EnumEntitySearchType.Creatures);
                    string bait = BaitStack?.Collectible.Attributes?["baitTag"].AsString() ?? "nobait";
                    if (fishEntity != null && fishEntity.Properties.Attributes["baitTags"].AsArray<string>().Contains(bait))
                    {
                        bobberState = EnumBobberState.EntityFishCatch;
                        caughtFish = (EntityFish)fishEntity;
                        catchAccum += dt;
                        playCatchEffects();
                    }
                    break;
                }
                case EnumBobberState.NoFishNearby:
                {
                    if (!Swimming) break;
                    // Wait according to abundance, then catch junk or fish from stock
                    bool hasCatchable = HasCatchable(BaitStack, out float abundance);
                    if (abundance > 0 && swimmingAccum > 5.0 / Math.Max(0.04, abundance))
                    {
                        bobberState = Api.World.Rand.NextDouble() < junkCatchChance ? EnumBobberState.JunkCatch : hasCatchable ? EnumBobberState.NoEntityFishCatch : EnumBobberState.NoCatch;
                        if (bobberState != EnumBobberState.NoCatch)
                        {
                            catchAccum += dt;
                            playCatchEffects();
                        }
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
                            AiTaskManager taskManager = caughtFish.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager;
                            IAiTask aiTask = taskManager?.GetTask("fleebobber");
                            if (aiTask != null && aiTask.ShouldExecute())
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
                        break;
                    }

                    catchAccum += dt;
                    break;
                }
                case EnumBobberState.NoCatch:
                {
                    // Make sure we're not baiting constantly when there's no catch to be made.
                    if (swimmingAccum > 60f)
                    {
                        catchAccum = 0f;
                        swimmingAccum = 0f;
                        bobberState = EnumBobberState.Baiting;
                    }
                    break;
                }
            }
        }


        public bool HasCatchable(ItemStack baitStack, out float abundanceValue)
        {
            if (caughtFish != null && caughtFish.Alive)
            {
                abundanceValue = 1;
                return true;
            }

            return getRandomFishEntityProperties(baitStack, out abundanceValue) != null;
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
                    EntityProperties fishCatch = getRandomFishEntityProperties(BaitStack, out float abundance);
                    if (fishCatch?.Drops[0].ResolvedItemstack is not ItemStack fishStack) return;

                    string age = Api.World.Rand.NextDouble() < abundance ? "adult" : "juvenile";
                    CollectibleObject agedFish = Api.World.GetItem(fishStack.Collectible.CodeWithVariant("age", age));
                    fishStack = agedFish != null ? new ItemStack(agedFish) : fishStack.Clone();

                    drops = [fishStack];

                    Api.ModLoader.GetModSystem<ModSystemFishDepletion>().AddHarvest(Pos.XYZ.AsBlockPos, 1);
                    break;
                }
                case EnumBobberState.JunkCatch:
                {
                    // Get weighted random junk from pool
                    WeightedBlockDropItemstack[] junkCatches = Properties.Attributes?["junkCatches"].AsObject<WeightedBlockDropItemstack[]>()?.Shuffle(Api.World.Rand);
                    if (junkCatches == null) return;

                    double selection = Api.World.Rand.NextDouble() * junkCatches.Sum(junkCatch => junkCatch.Weight);
                    foreach (WeightedBlockDropItemstack junkCatch in junkCatches)
                    {
                        selection -= junkCatch.Weight;
                        if (selection < 0d && junkCatch.Resolve(Api.World, "bobber junk catch", Code))
                        {
                            drops = [junkCatch.ResolvedItemstack.Clone()];
                            break;
                        }
                    }
                    break;
                }
            }

            if (drops.Length <= 0) return;

            BaitStack = null;
            WatchedAttributes.MarkPathDirty("baitStack");

            foreach (ItemStack drop in drops)
            {
                if (!entityCatcher.TryGiveItemStack(drop))
                {
                    World.SpawnItemEntity(drop, entityCatcher.Pos.XYZ);
                }
            }

            ItemSlot slot = entityCatcher.ActiveHandItemSlot;
            (slot?.Itemstack?.Collectible as ItemFishingPole)?.DamageItem(World, entityCatcher, slot);
            slot?.MarkDirty();
        }


        private void printLocationDebugInfo()
        {
            getRandomFishEntityProperties(null, out _, true);
        }


        BlockPos tmpPos = new BlockPos(0);
        private EntityProperties getRandomFishEntityProperties(ItemStack baitStack, out float abundanceValue, bool printDebug = false)
        {
            var pos = Pos.XYZ;
            tmpPos.Set(pos.XInt, (int)(World.SeaLevel * 1.09), pos.ZInt); // Adjust for the 9% above sea level climate check

            abundanceValue = 0f;

            var climate = World.BlockAccessor.GetClimateAt(tmpPos, EnumGetClimateMode.WorldGenValues);
            if (climate == null) return null;

            tmpPos.Set(pos.XInt, pos.YInt, pos.ZInt); // We have to set the location back for the rest of the checks
            Block block = World.BlockAccessor.GetBlock(tmpPos, BlockLayersAccess.Fluid);

            // 1. Prefilter by suitable climate
            List<KeyValuePair<string?, EntityProperties>> suitableEntityTypesWithMapCode = [];
            foreach (EntityProperties entityType in World.EntityTypes)
            {
                SpawnConditions spawnConditions = entityType.Server.SpawnConditions;
                if (spawnConditions == null) continue;

                BaseSpawnConditions baseSpawnConditions = spawnConditions.Runtime ?? spawnConditions.Worldgen as BaseSpawnConditions;
                if (baseSpawnConditions == null) continue;

                ClimateSpawnCondition spawnClimate = spawnConditions.Climate ?? baseSpawnConditions;

                // Then we make sure it can spawn in the current climate
                if (!spawnClimate.MatchesClimate(climate) || !baseSpawnConditions.CanSpawnInside(block)) continue;

                string? mapCode = spawnConditions.Climate?.MapCode ?? baseSpawnConditions.MapCode;

                suitableEntityTypesWithMapCode.Add(new KeyValuePair<string?, EntityProperties>(mapCode, entityType));
            }

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("1. Climate suitable fish types: " + string.Join(", ", suitableEntityTypesWithMapCode.Select(kv => kv.Value.Code)));
            }

            if (suitableEntityTypesWithMapCode.Count == 0) return null;

            // 2. Now filter by animal map matching
            int regionSize = World.BlockAccessor.RegionSize;
            int animalMapsPerRegion = regionSize / TerraGenConfig.animalMapScale;
            int xInRegion = pos.XInt % regionSize;
            int zInRegion = pos.ZInt % regionSize;
            float xInAnimalMap = GameMath.Clamp((float)xInRegion / (float)regionSize * (float)animalMapsPerRegion, 0f, animalMapsPerRegion - 1);
            float zInAnimalMap = GameMath.Clamp((float)zInRegion / (float)regionSize * (float)animalMapsPerRegion, 0f, animalMapsPerRegion - 1);
            IMapRegion mapRegion = World.BlockAccessor.GetMapRegion(pos.XInt / regionSize, pos.ZInt / regionSize);

            List<EntityProperties> suitableEntityTypes = [];
            foreach ((string? mapCode, var entityType) in suitableEntityTypesWithMapCode)
            {
                if (mapCode == null || mapRegion.AnimalSpawnMaps.Get(mapCode).GetUnpaddedLerped(xInAnimalMap, zInAnimalMap) > 128f)
                {
                    suitableEntityTypes.Add(entityType);
                }
            }

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("2. After fish type map filter: " + string.Join(", ", suitableEntityTypes.Select(props => props.Code)));
            }

            if (suitableEntityTypes.Count == 0) return null;

            // 3. Now filter by overall "fish frequency map"
            var fishingSupportSystem = Api.ModLoader.GetModSystem<FishingSupportModSystem>();
            if (fishingSupportSystem?.FishFrequencyMapNoise == null) return null;

            double noisyAbundance = (fishingSupportSystem.FishFrequencyMapNoise.Noise(pos.X, pos.Z) - 0.4f) * 3.0;
            abundanceValue = (float)GameMath.Clamp(noisyAbundance, 0.2f, 1.0);

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("3. Fish frequency map value: " + abundanceValue);
            }

            if (abundanceValue <= 0) return null;

            // 4. Now filter by pond size
            if (pondSize < 0) // Lets cache this value
            {
                pondSize = getPondSize();
            }

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("4. Pond size: " + pondSize);
            }

            if (pondSize < 100) return null;

            abundanceValue *= pondSize / 1200f;

            // 5. Now filter by fish depletion map
            float alreadyHarvested = Api.ModLoader.GetModSystem<ModSystemFishDepletion>().GetHarvestAmount(tmpPos);
            float maxHarvestable = ModSystemFishDepletion.MaxHarvestablePerLocation * 0.8f;
            float remainingHarvestable = 1f - GameMath.Clamp(alreadyHarvested / maxHarvestable - 0.2f, 0f, 1f);
            abundanceValue *= remainingHarvestable;

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine($"5. Fish depletion here {(1 - remainingHarvestable) * 100}% (caught: {alreadyHarvested})");
            }

            if (abundanceValue <= 0) return null;

            // 6. Now filter by bait
            string baitTag = baitStack?.Collectible.Attributes?["baitTag"].AsString() ?? "nobait";
            suitableEntityTypes = [.. suitableEntityTypes.Where(entityType => entityType.Attributes?["baitTags"].AsArray<string>()?.Contains(baitTag) ?? false)];

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("6. After bait filter: " + string.Join(", ", suitableEntityTypes.Select(props => props.Code)));
            }

            if (suitableEntityTypes.Count == 0) return null;

            // 7. Pick a random one from the leftovers
            var selectedEntityType = suitableEntityTypes[Api.World.Rand.Next(suitableEntityTypes.Count)];

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("7. Randomly selected fish: " + selectedEntityType.Code);
            }

            return selectedEntityType;
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


        public override void FromBytes(ref BinarySpanReader reader, bool isSync)
        {
            base.FromBytes(ref reader, isSync);

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

            EntityAgent entity = Api.World.GetEntityById(AttachedToEntityId) as EntityAgent;
            if (entity?.ActiveHandItemSlot is ItemSlot slot && slot.Itemstack?.Collectible is ItemFishingPole poleObj)
            {
                slot.Itemstack.Attributes.RemoveAttribute("fishing");
                slot.Itemstack.Attributes.RemoveAttribute("bobberEntityId");
                slot.Itemstack.Attributes.RemoveAttribute("fishingEntityId");
                poleObj.DamageItem(Api.World, entity, slot);
                slot.MarkDirty();
            }
        }
    }
}
