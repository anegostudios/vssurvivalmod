using ProperVersion;
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
using Vintagestory.API.Common.CommandAbbr;

#nullable disable

namespace Vintagestory.GameContent
{
    public enum EnumBobberState
    {
        Baiting = 0,
        NoFishNearby = 1,
        FishNearby = 2,
        NoEntityFishCatch = 3,
        JunkCatch = 4,
        NoCatch = 5
    }


    public class ModSystemFishDepletion : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        protected ICoreServerAPI sapi;
        protected Dictionary<BlockPos, CreatureHarvest> harvestedLocations = new Dictionary<BlockPos, CreatureHarvest>();

        public int Scale = 8;
        public static int MaxHarvestablePerLocation = 15;
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
            harvestedLocations[pos / Scale] = new CreatureHarvest() { TotalDays = sapi.World.Calendar.TotalDays, Quantity = harvest.Quantity + quantity };
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




    // Kurwa bobber
    public class EntityBobber : EntityProjectile
    {
        public override float MaterialDensity => 50;
        public override bool ApplyGravity => true;
        public override bool CanCollect(Entity byEntity) => false;
        protected override bool TryAttackEntity(double impactSpeed)
        {
            return true;
        }

        public ItemStack BaitStack { get; set; }

        EntityPartitioning ep;

        public long AttachedToEntityId
        {
            get { return WatchedAttributes.GetLong("attachedToEntityId"); }
            set { WatchedAttributes.SetLong("attachedToEntityId", value); }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            ep = api.ModLoader.GetModSystem<EntityPartitioning>();

            junkCatchChance = properties.Attributes["junkCatchChance"].AsFloat(0);

            impactSound = null;

            BaitStack?.ResolveBlockOrItem(Api.World);
        }

        public override void SetRotationFromMotion()
        {
            base.SetRotationFromMotion();
        }

        float swimmingAccum = 0f;
        float accum = 0f;
        float catchAccum = 0f;
        float junkCatchChance = 0f;

        EnumBobberState bobberState;
        public EntityFish caughtFish;
        public override void OnGameTick(float dt)
        {
            if (World.Side == EnumAppSide.Server)
            {
                onServertick(dt);
            }

            if (Swimming)
            {
                Pos.Pitch *= 0.95f;
                Pos.Roll *= 0.95f;
                swimmingAccum += dt;
                return;
            }

            base.OnGameTick(dt);
        }


        bool wasSwimming;
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

            if (swimmingAccum > 1)
            {
                if (bobberState == EnumBobberState.Baiting)
                {
                    var efish = ep.GetNearestEntity(Pos.XYZ, 20, (e) => e is EntityFish, EnumEntitySearchType.Creatures);
                    bobberState = efish == null ? EnumBobberState.NoFishNearby : EnumBobberState.FishNearby;
                }

                if (bobberState == EnumBobberState.FishNearby && swimmingAccum > 15)
                {
                    bobberState = EnumBobberState.NoFishNearby;
                }

                bool hasCatchable = HasCatchable(out var catchLikelihood);
                if (BaitStack == null) catchLikelihood /= 10;

                if (bobberState == EnumBobberState.NoFishNearby && catchLikelihood > 0 && swimmingAccum > 5 / Math.Max(0.04, catchLikelihood))
                {
                    if (Api.World.Rand.NextDouble() < junkCatchChance)
                    {
                        bobberState = EnumBobberState.JunkCatch;
                        catchAccum += dt;
                        playCatchEffects();
                    }
                    else
                    {

                        bobberState = EnumBobberState.NoEntityFishCatch;
                        catchAccum += dt;
                        if (hasCatchable)
                        {
                            playCatchEffects();
                        }
                        else
                        {
                            bobberState = EnumBobberState.NoCatch;
                            catchAccum = 0;
                            swimmingAccum = 0;
                        }
                    }
                }
            }


            if (catchAccum == 0)
            {
                accum += dt;
                if (accum > 0.2f)
                {
                    var efish = ep.GetNearestEntity(Pos.XYZ, 1, (e) => e is EntityFish, EnumEntitySearchType.Creatures);
                    if (efish != null)
                    {
                        caughtFish = efish as EntityFish;
                        catchAccum += dt;
                        playCatchEffects();
                    }
                }
            }
            else
            {
                if (catchAccum > 0.7f)
                {
                    catchAccum = 0;
                    caughtFish = null;
                    bobberState = EnumBobberState.Baiting;

                    if (caughtFish != null && caughtFish.Pos.DistanceTo(Pos) < 1)
                    {
                        var tm = caughtFish.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                        var task = tm?.GetTask("fleebobber");
                        if (task != null && task.ShouldExecute())
                        {
                            tm.ExecuteTask(task, task.Slot);
                        }
                    }
                }
            }
        }


        public bool HasCatchable(out float abundanceValue)
        {
            if (caughtFish != null && caughtFish.Alive)
            {
                abundanceValue = 1;
                return true;
            }

            return getRandomFishEntityProperties(out abundanceValue) != null;
        }

        public void TryCatchFish(EntityAgent entityCatcher)
        {
            if (caughtFish != null && caughtFish.Alive)
            {
                BaitStack = null;
                WatchedAttributes.MarkPathDirty("baitStack");

                caughtFish.Die(EnumDespawnReason.Expire);
                var dropStacks = caughtFish.GetDrops(World, caughtFish.Pos.XYZInt.AsBlockPos, (entityCatcher as EntityPlayer)?.Player);
                if (dropStacks == null) return;

                foreach (var dropStack in dropStacks)
                {
                    if (!entityCatcher.TryGiveItemStack(dropStack))
                    {
                        World.SpawnItemEntity(dropStack, entityCatcher.Pos.XYZ);
                    }
                }

                return;
            }

            if (bobberState == EnumBobberState.NoEntityFishCatch)
            {
                EntityProperties etype = getRandomFishEntityProperties(out float abundancevalue);
                if (etype == null) return;

                BaitStack = null;
                WatchedAttributes.MarkPathDirty("baitStack");

                var collObj = etype.Drops[0].ResolvedItemstack.Collectible;
                var age = Api.World.Rand.NextDouble() > abundancevalue ? "adult" : "juvenile";
                CollectibleObject deadFishItem = Api.World.GetItem(collObj.CodeWithVariant("age", age));
                if (deadFishItem == null) deadFishItem = collObj;

                ItemStack dropStack;
                if (deadFishItem != null)
                {
                    dropStack = new ItemStack(deadFishItem);
                    dropStack.ResolveBlockOrItem(Api.World);
                } else
                {
                    dropStack = etype.Drops[0].ResolvedItemstack.Clone();
                }

                if (!entityCatcher.TryGiveItemStack(dropStack))
                {
                    World.SpawnItemEntity(dropStack, entityCatcher.Pos.XYZ);
                }

                Api.ModLoader.GetModSystem<ModSystemFishDepletion>().AddHarvest(Pos.XYZ.AsBlockPos, 1);
            }

            if (bobberState == EnumBobberState.JunkCatch)
            {
                BaitStack = null;
                WatchedAttributes.MarkPathDirty("baitStack");


                var drops = Properties.Attributes["junkCatches"].AsObject<WeightedBlockDropItemstack[]>();
                float totalWeight = 0;
                foreach (var drop in drops)
                {
                    totalWeight += drop.Weight;
                }
                var rndval = Api.World.Rand.NextDouble() * totalWeight;

                drops.Shuffle(Api.World.Rand);
                foreach (var drop in drops)
                {
                    rndval -= drop.Weight;
                    if (rndval < 0)
                    {
                        drop.Resolve(Api.World, "bobber junk catch", Code);
                        var dropStack = drop.ResolvedItemstack.Clone();
                        if (!entityCatcher.TryGiveItemStack(dropStack))
                        {
                            World.SpawnItemEntity(dropStack, entityCatcher.Pos.XYZ);
                        }
                        break;
                    }
                }
            }
        }


        private void printLocationDebugInfo()
        {
            getRandomFishEntityProperties(out _, true);
        }


        BlockPos tmpPos = new BlockPos(0);
        private EntityProperties getRandomFishEntityProperties(out float abundanceValue, bool printDebug = false)
        {
            var pos = Pos.XYZ;
            tmpPos.Set(pos.XInt, (int)(World.SeaLevel * 1.09), pos.ZInt);

            abundanceValue = 0;

            var climate = World.BlockAccessor.GetClimateAt(tmpPos, EnumGetClimateMode.WorldGenValues);
            if (climate == null)
            {
                return null;
            }

            // 1. Prefilter by suitable climate
            List<KeyValuePair<string, EntityProperties>> suitableFishPropsWithMapCode = new List<KeyValuePair<string, EntityProperties>>();
            foreach (var etype in World.EntityTypes)
            {
                string mapcode = etype.Server.SpawnConditions?.Climate?.MapCode ?? etype.Server.SpawnConditions?.Runtime?.MapCode ?? etype.Server.SpawnConditions?.Worldgen?.MapCode;
                if (mapcode != null)
                {
                    ClimateSpawnCondition spawnconds = etype.Server.SpawnConditions?.Climate ?? (ClimateSpawnCondition)etype.Server.SpawnConditions?.Runtime ?? (ClimateSpawnCondition)etype.Server.SpawnConditions?.Worldgen;
                    if (spawnconds.MatchesClimate(climate))
                    {
                        suitableFishPropsWithMapCode.Add(new KeyValuePair<string, EntityProperties>(mapcode, etype));
                    }
                }
            }

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("1. Climate suitable fish types: " + string.Join(", ", suitableFishPropsWithMapCode.Select(kv => kv.Value.Code)));
            }

            // 2. Now filter by animal map matching
            var regionSize = World.BlockAccessor.RegionSize;
            var mr = World.BlockAccessor.GetMapRegion(pos.XInt / regionSize, pos.ZInt / regionSize);
            int noiseSizeDensityMap = regionSize / TerraGenConfig.animalMapScale;
            int posX = pos.XInt;
            int posZ = pos.ZInt;
            List<EntityProperties> suitableFishProps = new List<EntityProperties>();
            foreach (var val in mr.AnimalSpawnMaps)
            {
                int lx = posX % regionSize;
                int lz = posZ % regionSize;

                float posXInRegionOre = GameMath.Clamp((float)lx / regionSize * noiseSizeDensityMap, 0, noiseSizeDensityMap - 1);
                float posZInRegionOre = GameMath.Clamp((float)lz / regionSize * noiseSizeDensityMap, 0, noiseSizeDensityMap - 1);

                float density = val.Value.GetUnpaddedLerped(posXInRegionOre, posZInRegionOre);
                if (density > 128)
                {
                    var eprops = suitableFishPropsWithMapCode.FirstOrDefault(fprops => fprops.Key == val.Key).Value;
                    if (eprops != null)
                    {
                        suitableFishProps.Add(eprops);
                    }
                }
            }

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("2. After fish type map filter: " + string.Join(", ", suitableFishProps.Select(props => props.Code)));
            }

            if (suitableFishProps.Count == 0) return null;


            // 3. Now filter by overal "fish frequency map"
            var noiseval = (Api.ModLoader.GetModSystem<FishingSupportModSystem>().NoiseGen.Noise(pos.X, pos.Z) - 0.4f) * 3;
            abundanceValue = (float)GameMath.Clamp(noiseval, 0, 1);

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("3. Fish frequency map value: " + abundanceValue);
            }

            if (noiseval <= 0) return null;

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
            float harvestedHere = Api.ModLoader.GetModSystem<ModSystemFishDepletion>().GetHarvestAmount(Pos.XYZ.AsBlockPos);

            float max = ModSystemFishDepletion.MaxHarvestablePerLocation * 0.8f;
            var mul = 1 - GameMath.Clamp(harvestedHere / max - 0.2f, 0, 1);
            abundanceValue *= mul;

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("5. Fish depletion here: " + ((1-mul) * 100) + "% (caught: "+harvestedHere+")");
            }


            // 6. Pick a random one from the leftovers
            var fishprops = suitableFishProps[Api.World.Rand.Next(suitableFishProps.Count)];

            if (printDebug)
            {
                System.Diagnostics.Debug.WriteLine("6. Randomly selected fish: " + fishprops.Code);
            }

            return fishprops;
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
    }
}
