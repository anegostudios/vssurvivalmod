using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class CodeAndChance
    {
        public AssetLocation Code;
        public float Chance;
    }

    // Definitions:
    // Farmland has 3 nutrient levels N, P and K
    // - Each nutrient level has a range of 0-100
    // - Every crop always only consumes one nutrient level (for now)
    // - Some crops require more nutrient than others
    // - Each crop has different total growth speed
    // - Each crop can have any amounts of growth stages
    // - Some crops can be harvested with right click, without destroying the crop
    public class BlockEntityFarmland : BlockEntity, IFarmlandBlockEntity, IAnimalFoodSource, ITexPositionSource
    {
        protected static Random rand = new Random();
        public static OrderedDictionary<string, float> Fertilities = new OrderedDictionary<string, float>{
            { "verylow", 5 },
            { "low", 25 },
            { "medium", 50 },
            { "compost", 65 },
            { "high", 80 },
        };

        
        protected HashSet<string> PermaBoosts = new HashSet<string>();
        // How many hours this block can retain water before becoming dry
        protected double totalHoursWaterRetention = 24.5;
        protected BlockPos upPos;
        // Total game hours from where on it can enter the next growth stage
        protected double totalHoursForNextStage;
        // The last time fertility increase was checked
        protected double totalHoursLastUpdate;
        // Stored values
        protected float[] nutrients = new float[3];
        protected float[] slowReleaseNutrients = new float[3];
        protected Dictionary<string, float> fertilizerOverlayStrength = null;

        // 0 = bone dry, 1 = completely soggy
        protected float moistureLevel = 0;
        protected double lastWaterSearchedTotalHours;
        protected TreeAttribute cropAttrs = new TreeAttribute();

        // The fertility the soil will recover to (the soil from which the farmland was made of)
        public int[] originalFertility = new int[3];

        protected bool unripeCropColdDamaged;
        protected bool unripeHeatDamaged;
        protected bool ripeCropColdDamaged;
        protected bool saltExposed;

        // 0 = Unknown
        // 1 = too hot
        // 2 = too cold
        // 3 = saltwater
        protected float[] damageAccum = new float[Enum.GetValues(typeof(EnumCropStressType)).Length];


        BlockFarmland blockFarmland;
        
        protected Vec3d tmpPos = new Vec3d();
        protected float lastWaterDistance = 99;
        protected double lastMoistureLevelUpdateTotalDays;

        public int roomness;


        protected bool allowundergroundfarming;
        protected bool allowcropDeath;

        protected float fertilityRecoverySpeed = 0.25f;
        protected float growthRateMul = 1f;

        protected MeshData fertilizerQuad;
        protected TextureAtlasPosition fertilizerTexturePos;
        ICoreClientAPI capi;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            blockFarmland = Block as BlockFarmland;
            if (blockFarmland == null) return;

            capi = api as ICoreClientAPI;

            totalHoursWaterRetention = Api.World.Calendar.HoursPerDay + 0.5;
            upPos = base.Pos.UpCopy();
            
            allowundergroundfarming = Api.World.Config.GetBool("allowUndergroundFarming", false);
            allowcropDeath = Api.World.Config.GetBool("allowCropDeath", true);
            fertilityRecoverySpeed = Api.World.Config.GetFloat("fertilityRecoverySpeed", fertilityRecoverySpeed);
            growthRateMul = (float)Api.World.Config.GetDecimal("cropGrowthRateMul", growthRateMul);

            if (api is ICoreServerAPI)
            {
                if (Api.World.Config.GetBool("processCrops", true))
                {
                    RegisterGameTickListener(Update, 3300 + rand.Next(400));
                }

                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
            }

            updateFertilizerQuad();
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
        }


        public void OnCreatedFromSoil(Block block)
        {
            string fertility = block.LastCodePart(1);
            if (block is BlockFarmland)
            {
                fertility = block.LastCodePart();
            }
            originalFertility[0] = (int)Fertilities[fertility];
            originalFertility[1] = (int)Fertilities[fertility];
            originalFertility[2] = (int)Fertilities[fertility];

            nutrients[0] = originalFertility[0];
            nutrients[1] = originalFertility[1];
            nutrients[2] = originalFertility[2];

            totalHoursLastUpdate = Api.World.Calendar.TotalHours;

            tryUpdateMoistureLevel(Api.World.Calendar.TotalDays, true);
        }

        public bool OnBlockInteract(IPlayer byPlayer)
        {
            var stack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            JsonObject obj = stack?.Collectible?.Attributes?["fertilizerProps"];
            if (obj == null || !obj.Exists) return false;
            FertilizerProps props = obj.AsObject<FertilizerProps>();
            if (props == null) return false;

            float nAdd = Math.Min(Math.Max(0, 150 - slowReleaseNutrients[0]), props.N);
            float pAdd = Math.Min(Math.Max(0, 150 - slowReleaseNutrients[1]), props.P);
            float kAdd = Math.Min(Math.Max(0, 150 - slowReleaseNutrients[2]), props.K);

            slowReleaseNutrients[0] += nAdd;
            slowReleaseNutrients[1] += pAdd;
            slowReleaseNutrients[2] += kAdd;

            if (props.PermaBoost != null && !PermaBoosts.Contains(props.PermaBoost.Code))
            {
                originalFertility[0] += props.PermaBoost.N;
                originalFertility[1] += props.PermaBoost.P;
                originalFertility[2] += props.PermaBoost.K;
                PermaBoosts.Add(props.PermaBoost.Code);
            }

            string fertCode = stack.Collectible.Attributes["fertilizerTextureCode"].AsString();
            if (fertCode != null)
            {
                if (fertilizerOverlayStrength == null) fertilizerOverlayStrength = new Dictionary<string, float>();
                fertilizerOverlayStrength.TryGetValue(fertCode, out var prevValue);
                fertilizerOverlayStrength[fertCode] = prevValue + Math.Max(nAdd, Math.Max(kAdd, pAdd));
            }

            updateFertilizerQuad();

            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            Api.World.PlaySoundAt(Api.World.BlockAccessor.GetBlock(base.Pos).Sounds.Hit, base.Pos.X + 0.5, base.Pos.Y + 0.75, base.Pos.Z + 0.5, byPlayer, true, 12);

            MarkDirty(false);
            return true;
        }

        public void OnCropBlockBroken()
        {
            ripeCropColdDamaged = false;
            unripeCropColdDamaged = false;
            unripeHeatDamaged = false;
            for (int i = 0; i < damageAccum.Length; i++) damageAccum[i] = 0;
            MarkDirty(true);
        }

        public ItemStack[] GetDrops(ItemStack[] drops)
        {
            BlockEntityDeadCrop beDeadCrop = Api.World.BlockAccessor.GetBlockEntity(upPos) as BlockEntityDeadCrop;
            bool isDead = beDeadCrop != null;

            if (!ripeCropColdDamaged && !unripeCropColdDamaged && !unripeHeatDamaged && !isDead) return drops;
            if (!Api.World.Config.GetString("harshWinters").ToBool(true)) return drops;

            List<ItemStack> stacks = new List<ItemStack>();

            var cropProps = GetCrop()?.CropProps;
            if (cropProps == null) return drops;

            float mul = 1f;
            if (ripeCropColdDamaged) mul = cropProps.ColdDamageRipeMul;
            if (unripeHeatDamaged || unripeCropColdDamaged) mul = cropProps.DamageGrowthStuntMul;
            if (isDead) mul = beDeadCrop.deathReason == EnumCropStressType.Eaten ? 0 : Math.Max(cropProps.ColdDamageRipeMul, cropProps.DamageGrowthStuntMul);

            for (int i = 0; i < drops.Length; i++)
            {
                ItemStack stack = drops[i];
                if (stack.Collectible.NutritionProps == null)
                {
                    stacks.Add(stack);
                    continue;
                }

                float q = stack.StackSize * mul;
                float frac = q - (int)q;
                stack.StackSize = (int)q + (Api.World.Rand.NextDouble() > frac ? 1 : 0);

                if (stack.StackSize > 0)
                {
                    stacks.Add(stack);
                }
            }

            MarkDirty(true);

            return stacks.ToArray();
        }

        bool farmlandIsAtChunkEdge = false;

        protected float GetNearbyWaterDistance(out EnumWaterSearchResult result, float hoursPassed)
        {
            // 1. Watered check
            float waterDistance = 99;
            farmlandIsAtChunkEdge = false;

            bool saltWater = false;

            Api.World.BlockAccessor.SearchFluidBlocks(
                new BlockPos(Pos.X - 4, Pos.Y, Pos.Z - 4),
                new BlockPos(Pos.X + 4, Pos.Y, Pos.Z + 4),
                (block, pos) =>
                {
                    if (block.LiquidCode == "water")
                    {
                        waterDistance = Math.Min(waterDistance, Math.Max(Math.Abs(pos.X - Pos.X), Math.Abs(pos.Z - Pos.Z)));
                    }
                    if (block.LiquidCode == "saltwater")
                    {
                        saltWater = true;
                    }

                    return true;
                },
                (cx, cy, cz) => farmlandIsAtChunkEdge = true
            );

            if (saltWater) damageAccum[(int)(EnumCropStressType.Salt)] += hoursPassed;

            result = EnumWaterSearchResult.Deferred;
            if (farmlandIsAtChunkEdge) return 99;

            lastWaterSearchedTotalHours = Api.World.Calendar.TotalHours;

            if (waterDistance < 4f)
            {
                //if (!IsWatered) MarkDirty(true);
                //lastWateredTotalHours = Api.World.Calendar.TotalHours;
                result = EnumWaterSearchResult.Found;
                return waterDistance;
            }

            result = EnumWaterSearchResult.NotFound;
            return 99;
        }






        bool tryUpdateMoistureLevel(double totalDays, bool searchNearbyWater)
        {
            float dist = 99;
            if (searchNearbyWater)
            {
                EnumWaterSearchResult res;
                dist = GetNearbyWaterDistance(out res, 0);
                if (res == EnumWaterSearchResult.Deferred) return false; // Wait with updating until neighbouring chunks are loaded
                if (res != EnumWaterSearchResult.Found) dist = 99;

                lastWaterDistance = dist;
            }

            if (updateMoistureLevel(totalDays, dist)) UpdateFarmlandBlock();

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>true if it was a longer interval check (checked rain as well) so that an UpdateFarmlandBlock() is advisable</returns>
        bool updateMoistureLevel(double totalDays, float waterDistance)
        {
            bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= (GetCrop() == null ? Pos.Y : Pos.Y + 1);
            return updateMoistureLevel(totalDays, waterDistance, skyExposed);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>true if it was a longer interval check (checked rain as well) so that an UpdateFarmlandBlock() is advisable</returns>
        bool updateMoistureLevel(double totalDays, float waterDistance, bool skyExposed, ClimateCondition baseClimate = null)
        {
            tmpPos.Set(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);

            float minMoisture = GameMath.Clamp(1 - waterDistance / 4f, 0, 1);

            double hoursPassed = Math.Min((totalDays - lastMoistureLevelUpdateTotalDays) * Api.World.Calendar.HoursPerDay, 48);
            if (hoursPassed < 0.03f)
            {
                // Get wet from a water source
                moistureLevel = Math.Max(moistureLevel, minMoisture);

                return false;
            }

            // Dry out
            moistureLevel = Math.Max(minMoisture, moistureLevel - (float)hoursPassed / 48f);

            // Get wet from all the rainfall since last update
            if (skyExposed)
            {
                if (baseClimate == null && hoursPassed > 0) baseClimate = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.WorldGenValues, totalDays - hoursPassed / Api.World.Calendar.HoursPerDay / 2);
                while (hoursPassed > 0)
                {
                    double rainLevel = blockFarmland.wsys.GetPrecipitation(Pos, totalDays - hoursPassed / Api.World.Calendar.HoursPerDay, baseClimate);
                    moistureLevel = GameMath.Clamp(moistureLevel + (float)rainLevel / 3f, 0, 1);
                    hoursPassed--;
                }
            }

            lastMoistureLevelUpdateTotalDays = totalDays;

            return true;
        }


        private void Update(float dt)
        {
            if (!(Api as ICoreServerAPI).World.IsFullyLoadedChunk(Pos)) return;

            double nowTotalHours = Api.World.Calendar.TotalHours;
            double hourIntervall = 3 + rand.NextDouble();

            Block cropBlock = GetCrop();
            bool hasCrop = cropBlock != null;
            bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= (hasCrop ? Pos.Y + 1 : Pos.Y);

            if ((nowTotalHours - totalHoursLastUpdate) < hourIntervall)
            {
                if (totalHoursLastUpdate > nowTotalHours)
                {
                    // We need to rollback time because the blockEntity saved date is ahead of the calendar date: can happen if a schematic is imported
                    double rollback = totalHoursLastUpdate - nowTotalHours; 
                    totalHoursForNextStage -= rollback;
                    lastMoistureLevelUpdateTotalDays -= rollback;
                    lastWaterSearchedTotalHours -= rollback;
                    totalHoursLastUpdate = nowTotalHours;
                }
                else
                {
                    if (updateMoistureLevel(nowTotalHours, lastWaterDistance, skyExposed)) UpdateFarmlandBlock();
                    return;
                }
            }

            // Slow down growth on bad light levels
            int lightpenalty = 0;
            if (!allowundergroundfarming)
            {
                lightpenalty = Math.Max(0, Api.World.SeaLevel - Pos.Y);
            }

            int sunlight = Api.World.BlockAccessor.GetLightLevel(upPos, EnumLightLevelType.MaxLight);
            double lightGrowthSpeedFactor = GameMath.Clamp(1 - (blockFarmland.DelayGrowthBelowSunLight - sunlight - lightpenalty) * blockFarmland.LossPerLevel, 0, 1);

            Block upblock = Api.World.BlockAccessor.GetBlock(upPos);
            Block deadCropBlock = Api.World.GetBlock(new AssetLocation("deadcrop"));


            double hoursNextStage = GetHoursForNextStage();
            double lightHoursPenalty = hoursNextStage / lightGrowthSpeedFactor - hoursNextStage;

            double totalHoursNextGrowthState = totalHoursForNextStage + lightHoursPenalty;

            EnumSoilNutrient? currentlyConsumedNutrient = null;
            if (upblock.CropProps != null)
            {
                currentlyConsumedNutrient = upblock.CropProps.RequiredNutrient;
            }

            // Let's increase fertility every 3-4 game hours

            bool growTallGrass = false;
            float[] npkRegain = new float[3];

            float waterDistance = 99;

            // Don't update more than a year
            totalHoursLastUpdate = Math.Max(totalHoursLastUpdate, nowTotalHours - Api.World.Calendar.DaysPerYear * Api.World.Calendar.HoursPerDay);

            bool hasRipeCrop =  HasRipeCrop();

            if (!skyExposed) // Fast pre-check
            {
                Room room = blockFarmland.roomreg?.GetRoomForPosition(upPos);
                roomness = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;
            }
            else
            {
                roomness = 0;
            }

            bool nearbyWaterTested = false;
            ClimateCondition conds = null;

            // Fast forward in 3-4 hour intervalls
            while ((nowTotalHours - totalHoursLastUpdate) > hourIntervall)
            {
                if (!nearbyWaterTested)
                {
                    EnumWaterSearchResult res;
                    waterDistance = GetNearbyWaterDistance(out res, (float)hourIntervall);
                    if (res == EnumWaterSearchResult.Deferred) return; // Wait with updating until neighbouring chunks are loaded
                    if (res == EnumWaterSearchResult.NotFound) waterDistance = 99;
                    nearbyWaterTested = true;

                    lastWaterDistance = waterDistance;
                }

                totalHoursLastUpdate += hourIntervall;
                hourIntervall = 3 + rand.NextDouble();

                if (conds == null)
                {
                    conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHoursLastUpdate / Api.World.Calendar.HoursPerDay);
                    if (conds == null) break;
                }
                else
                {
                    Api.World.BlockAccessor.GetClimateAt(Pos, conds, EnumGetClimateMode.ForSuppliedDate_TemperatureRainfallOnly, totalHoursLastUpdate / Api.World.Calendar.HoursPerDay);
                }

                updateMoistureLevel(totalHoursLastUpdate / Api.World.Calendar.HoursPerDay, waterDistance, skyExposed, conds);

                if (roomness > 0)
                {
                    conds.Temperature += 5;
                }

                if (!hasCrop)
                {
                    ripeCropColdDamaged = false;
                    unripeCropColdDamaged = false;
                    unripeHeatDamaged = false;
                    for (int i = 0; i < damageAccum.Length; i++) damageAccum[i] = 0;
                }
                else
                {
                    if (cropBlock?.CropProps != null && conds.Temperature < cropBlock.CropProps.ColdDamageBelow)
                    {
                        if (hasRipeCrop)
                        {
                            ripeCropColdDamaged = true;
                        }
                        else
                        {
                            unripeCropColdDamaged = true;
                            damageAccum[(int)EnumCropStressType.TooCold] += (float)hourIntervall;
                        }
                    }
                    else
                    {
                        damageAccum[(int)EnumCropStressType.TooCold] = Math.Max(0, damageAccum[(int)EnumCropStressType.TooCold] - (float)hourIntervall / 10);
                    }

                    if (cropBlock?.CropProps != null && conds.Temperature > cropBlock.CropProps.HeatDamageAbove && hasCrop)
                    {
                        unripeHeatDamaged = true;
                        damageAccum[(int)EnumCropStressType.TooHot] += (float)hourIntervall;
                    }
                    else
                    {
                        damageAccum[(int)EnumCropStressType.TooHot] = Math.Max(0, damageAccum[(int)EnumCropStressType.TooHot] - (float)hourIntervall / 10);
                    }

                    for (int i = 0; i < damageAccum.Length; i++)
                    {
                        float dmg = damageAccum[i];
                        if (!allowcropDeath) dmg = damageAccum[i] = 0;

                        if (dmg > 48)
                        {
                            Api.World.BlockAccessor.SetBlock(deadCropBlock.Id, upPos);
                            var be = Api.World.BlockAccessor.GetBlockEntity(upPos) as BlockEntityDeadCrop;
                            be.Inventory[0].Itemstack = new ItemStack(cropBlock);
                            be.deathReason = (EnumCropStressType)i;
                            hasCrop = false;
                            break;
                        }
                    }
                }

                // Stop growth and fertility recovery below zero degrees
                // 10% growth speed at 1°C
                // 20% growth speed at 2°C and so on
                float growthChance = GameMath.Clamp(conds.Temperature / 10f, 0, 10);
                if (rand.NextDouble() > growthChance)
                {
                    continue;
                }

                growTallGrass |= rand.NextDouble() < 0.006;

                // Rule 1: Fertility increase up to original levels by 1 every 3-4 ingame hours
                // Rule 2: Fertility does not increase with a ripe crop on it
                npkRegain[0] = hasRipeCrop ? 0 : fertilityRecoverySpeed;
                npkRegain[1] = hasRipeCrop ? 0 : fertilityRecoverySpeed;
                npkRegain[2] = hasRipeCrop ? 0 : fertilityRecoverySpeed;

                // Rule 3: Fertility increase up 3 times slower for the currently growing crop
                if (currentlyConsumedNutrient != null)
                {
                    npkRegain[(int)currentlyConsumedNutrient] /= 3;
                }

                for (int i = 0; i < 3; i++)
                {
                    nutrients[i] += Math.Max(0, npkRegain[i] + Math.Min(0, originalFertility[i] - nutrients[i] - npkRegain[i]));

                    // Rule 4: Slow release fertilizer can fertilize up to 100 fertility
                    if (slowReleaseNutrients[i] > 0)
                    {
                        float release = Math.Min(0.25f, slowReleaseNutrients[i]); // Don't use fertilityRecoverySpeed value as min here, doesn't look meaningful to do that here

                        nutrients[i] = Math.Min(100, nutrients[i] + release);
                        slowReleaseNutrients[i] = Math.Max(0, slowReleaseNutrients[i] - release);
                    }
                    else
                    {
                        // Rule 5: Once the slow release fertilizer is consumed, the soil will slowly return to its original fertility
                        if (nutrients[i] > originalFertility[i])
                        {
                            nutrients[i] = Math.Max(originalFertility[i], nutrients[i] - 0.05f);
                        }
                    }
                }

                if (fertilizerOverlayStrength != null && fertilizerOverlayStrength.Count > 0)
                {
                    var codes = fertilizerOverlayStrength.Keys.ToArray();
                    foreach (var code in codes)
                    {
                        var newStr = fertilizerOverlayStrength[code] - fertilityRecoverySpeed;
                        if (newStr < 0) fertilizerOverlayStrength.Remove(code);
                        else fertilizerOverlayStrength[code] = newStr;
                    }
                }



                if (moistureLevel < 0.1)
                {
                    // Too dry to grow. Todo: Make it dependent on crop
                    continue;
                }

                if (totalHoursNextGrowthState <= totalHoursLastUpdate)
                {
                    TryGrowCrop(totalHoursForNextStage);
                    hasRipeCrop = HasRipeCrop();
                    totalHoursForNextStage += hoursNextStage;
                    totalHoursNextGrowthState = totalHoursForNextStage + lightHoursPenalty;

                    hoursNextStage = GetHoursForNextStage();
                }
            }



            if (growTallGrass && upblock.BlockMaterial == EnumBlockMaterial.Air)
            {
                double rnd = rand.NextDouble() * blockFarmland.TotalWeedChance;
                for (int i = 0; i < blockFarmland.WeedNames.Length; i++)
                {
                    rnd -= blockFarmland.WeedNames[i].Chance;
                    if (rnd <= 0)
                    {
                        Block weedsBlock = Api.World.GetBlock(blockFarmland.WeedNames[i].Code);
                        if (weedsBlock != null)
                        {
                            Api.World.BlockAccessor.SetBlock(weedsBlock.BlockId, upPos);
                        }

                        break;
                    }
                }
            }

            updateFertilizerQuad();
            UpdateFarmlandBlock();
            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
        }

        
        public double GetHoursForNextStage()
        {
            Block block = GetCrop();
            if (block == null) return 99999999;

            var totalDays = block.CropProps.TotalGrowthDays;
            // Backwards compatibility, if days are provided we convert it to months using the default configuration timescale
            // After, we convert it to the currently configured timescale
            // For example, if something is set to grow in 6 days and the amount of days per month has been changed to 30, the new growth time will be 15 days.
            if (totalDays > 0)
            {
                var defaultTimeInMonths = totalDays / 12;
                totalDays = defaultTimeInMonths * Api.World.Calendar.DaysPerMonth;
            }
            else
            {
                totalDays = block.CropProps.TotalGrowthMonths * Api.World.Calendar.DaysPerMonth;
            }

            float stageHours = Api.World.Calendar.HoursPerDay * totalDays / block.CropProps.GrowthStages;

            stageHours *= 1 / GetGrowthRate(block.CropProps.RequiredNutrient);

            // Add a bit random to it (+/- 10%)
            stageHours *= (float)(0.9 + 0.2 * rand.NextDouble());

            return stageHours / growthRateMul;
        }

        public float GetGrowthRate(EnumSoilNutrient nutrient)
        {
            // (x/70 - 0.143)^0.35
            // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiIoeC83MC0wLjE0MyleMC4zNSIsImNvbG9yIjoiIzAwMDAwMCJ9LHsidHlwZSI6MTAwMCwid2luZG93IjpbIjAiLCIxMDAiLCIwIiwiMS4yNSJdfV0-

            float moistFactor = (float)Math.Pow(Math.Max(0.01, moistureLevel * 100 / 70 - 0.143), 0.35);

            if (nutrients[(int)nutrient] > 75) return moistFactor * 1.1f;
            if (nutrients[(int)nutrient] > 50) return moistFactor * 1;
            if (nutrients[(int)nutrient] > 35) return moistFactor * 0.9f;
            if (nutrients[(int)nutrient] > 20) return moistFactor * 0.6f;
            if (nutrients[(int)nutrient] > 5) return moistFactor * 0.3f;
            return moistFactor * 0.1f;
        }

        public float GetGrowthRate()
        {
            var cropProps = GetCrop()?.CropProps;
            return cropProps == null ? 1.0f : GetGrowthRate(cropProps.RequiredNutrient);
        }

        public float GetDeathChance(int nutrientIndex)
        {
            if (nutrients[nutrientIndex] <= 5) return 0.5f;
            return 0f;
        }

        public bool TryPlant(Block block)
        {
            if (CanPlant() && block.CropProps != null)
            {
                Api.World.BlockAccessor.SetBlock(block.BlockId, upPos);
                totalHoursForNextStage = Api.World.Calendar.TotalHours + GetHoursForNextStage();

                foreach (CropBehavior behavior in block.CropProps.Behaviors)
                {
                    behavior.OnPlanted(Api);
                }

                return true;
            }

            return false;
        }

        public bool CanPlant()
        {
            Block block = Api.World.BlockAccessor.GetBlock(upPos);
            return block == null || block.BlockMaterial == EnumBlockMaterial.Air;
        }


        public bool HasUnripeCrop()
        {
            Block block = GetCrop();
            return block != null && GetCropStage(block) < block.CropProps.GrowthStages;
        }

        public bool HasRipeCrop()
        {
            Block block = GetCrop();
            return block != null && GetCropStage(block) >= block.CropProps.GrowthStages;
        }

        public bool TryGrowCrop(double currentTotalHours)
        {
            Block block = GetCrop();
            if (block == null) return false;

            int currentGrowthStage = GetCropStage(block);
            if (currentGrowthStage < block.CropProps.GrowthStages)
            {
                int newGrowthStage = currentGrowthStage + 1;

                Block nextBlock = Api.World.GetBlock(block.CodeWithParts("" + newGrowthStage));
                if (nextBlock == null) return false;

                if (block.CropProps.Behaviors != null)
                {
                    EnumHandling handled = EnumHandling.PassThrough;
                    bool result = false;
                    foreach (CropBehavior behavior in block.CropProps.Behaviors)
                    {
                        result = behavior.TryGrowCrop(Api, this, currentTotalHours, newGrowthStage, ref handled);
                        if (handled == EnumHandling.PreventSubsequent) return result;
                    }
                    if (handled == EnumHandling.PreventDefault) return result;
                }

                if (Api.World.BlockAccessor.GetBlockEntity(upPos) == null)
                    Api.World.BlockAccessor.SetBlock(nextBlock.BlockId, upPos);    //create any blockEntity if necessary (e.g. Bell Pepper and other fruiting crops)
                else
                    Api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, upPos);    //do not destroy existing blockEntity (e.g. Bell Pepper and other fruiting crops)
                ConsumeNutrients(block);
                return true;
            }

            return false;
        }

        private void ConsumeNutrients(Block cropBlock)
        {
            float nutrientLoss = cropBlock.CropProps.NutrientConsumption / cropBlock.CropProps.GrowthStages;
            nutrients[(int)cropBlock.CropProps.RequiredNutrient] = Math.Max(0, nutrients[(int)cropBlock.CropProps.RequiredNutrient] - nutrientLoss);
            UpdateFarmlandBlock();
        }


        public bool IsVisiblyMoist
        {
            get { return moistureLevel > 0.1; }
        }

        void UpdateFarmlandBlock()
        {
            // v1.10: Let's get rid of the mechanic that the farmland exchanges blocks
            // it will just stay forever the original fertility block. For this, well make it so
            // that in v1.10 they slowly restore to their original fertility blocks and then in v1.11 we remove the ExchangeBlock altogether


            int nowLevel = GetFertilityLevel((originalFertility[0] + originalFertility[1] + originalFertility[2]) / 3);// FertilityLevel((nutrients[0] + nutrients[1] + nutrients[2]) / 3);
            Block farmlandBlock = Api.World.BlockAccessor.GetBlock(base.Pos);
            Block nextFarmlandBlock = Api.World.GetBlock(farmlandBlock.CodeWithParts(IsVisiblyMoist ? "moist" : "dry", Fertilities.GetKeyAtIndex(nowLevel)));

            if (nextFarmlandBlock == null)
            {
                Api.World.BlockAccessor.RemoveBlockEntity(base.Pos);
                return;
            }

            if (farmlandBlock.BlockId != nextFarmlandBlock.BlockId)
            {
                Api.World.BlockAccessor.ExchangeBlock(nextFarmlandBlock.BlockId, base.Pos);
                Api.World.BlockAccessor.MarkBlockEntityDirty(base.Pos);
                Api.World.BlockAccessor.MarkBlockDirty(base.Pos);
            }
        }


        internal int GetFertilityLevel(float fertiltyValue)
        {
            int i = 0;
            foreach (var val in Fertilities)
            {
                if (val.Value >= fertiltyValue) return i;
                i++;
            }
            return Fertilities.Count - 1;
        }

        internal Block GetCrop()
        {
            Block block = Api.World.BlockAccessor.GetBlock(upPos);
            if (block == null || block.CropProps == null) return null;
            return block;
        }


        internal int GetCropStage(Block block)
        {
            int stage;
            int.TryParse(block.LastCodePart(), out stage);
            return stage;
        }


        private void updateFertilizerQuad()
        {
            if (capi == null) return;
            AssetLocation loc = new AssetLocation();

            if (fertilizerOverlayStrength == null || fertilizerOverlayStrength.Count == 0)
            {
                bool dirty = fertilizerQuad != null;
                fertilizerQuad = null;
                if (dirty) MarkDirty(true);
                return;
            }

            int i = 0;
            foreach (var val in fertilizerOverlayStrength)
            {
                string intensity = "low";
                if (val.Value > 50) intensity = "med";
                if (val.Value > 100) intensity = "high";

                if (i > 0) loc.Path += "++0~";
                loc.Path += "block/soil/farmland/fertilizer/" + val.Key + "-" + intensity;
                i++;
            }

            capi.BlockTextureAtlas.GetOrInsertTexture(loc, out _, out var newFertilizerTexturePos);

            if (fertilizerTexturePos != newFertilizerTexturePos)
            {
                this.fertilizerTexturePos = newFertilizerTexturePos;
                genFertilizerQuad();
                MarkDirty(true);
            }
        }

        private void genFertilizerQuad()
        {
            var shape = capi.Assets.TryGet(new AssetLocation("shapes/block/farmland-fertilizer.json")).ToObject<Shape>();

            capi.Tesselator.TesselateShape(new TesselationMetaData() { 
                TypeForLogging = "farmland fertilizer quad",
                TexSource = this,

            }, shape, out fertilizerQuad);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            nutrients[0] = tree.GetFloat("n");
            nutrients[1] = tree.GetFloat("p");
            nutrients[2] = tree.GetFloat("k");

            slowReleaseNutrients[0] = tree.GetFloat("slowN");
            slowReleaseNutrients[1] = tree.GetFloat("slowP");
            slowReleaseNutrients[2] = tree.GetFloat("slowK");

            moistureLevel = tree.GetFloat("moistureLevel");
            lastWaterSearchedTotalHours = tree.GetDouble("lastWaterSearchedTotalHours");
            
            if (!tree.HasAttribute("originalFertilityN"))
            {
                originalFertility[0] = tree.GetInt("originalFertility");
                originalFertility[1] = tree.GetInt("originalFertility");
                originalFertility[2] = tree.GetInt("originalFertility");
            } else
            {
                originalFertility[0] = tree.GetInt("originalFertilityN");
                originalFertility[1] = tree.GetInt("originalFertilityP");
                originalFertility[2] = tree.GetInt("originalFertilityK");
            }
            

            if (tree.HasAttribute("totalHoursForNextStage"))
            {
                totalHoursForNextStage = tree.GetDouble("totalHoursForNextStage");
                totalHoursLastUpdate = tree.GetDouble("totalHoursFertilityCheck");
            }
            else
            {
                // Pre v1.5.1
                totalHoursForNextStage = tree.GetDouble("totalDaysForNextStage") * 24;
                totalHoursLastUpdate = tree.GetDouble("totalDaysFertilityCheck") * 24;
            }

            lastMoistureLevelUpdateTotalDays = tree.GetDouble("lastMoistureLevelUpdateTotalDays");

            cropAttrs = tree["cropAttrs"] as TreeAttribute;
            if (cropAttrs == null) cropAttrs = new TreeAttribute();

            lastWaterDistance = tree.GetFloat("lastWaterDistance");

            unripeCropColdDamaged = tree.GetBool("unripeCropExposedToFrost");
            ripeCropColdDamaged = tree.GetBool("ripeCropExposedToFrost");
            unripeHeatDamaged = tree.GetBool("unripeHeatDamaged");
            saltExposed = tree.GetBool("saltExposed");

            roomness = tree.GetInt("roomness");

            string[] permaboosts = (tree as TreeAttribute).GetStringArray("permaBoosts");
            if (permaboosts != null)
            {
                PermaBoosts.AddRange(permaboosts);
            }

            ITreeAttribute ftree = tree.GetTreeAttribute("fertilizerOverlayStrength");

            if (ftree != null)
            {
                fertilizerOverlayStrength = new Dictionary<string, float>();
                foreach (var val in ftree)
                {
                    fertilizerOverlayStrength[val.Key] = (val.Value as FloatAttribute).value;
                }
            } else fertilizerOverlayStrength = null;

            updateFertilizerQuad();
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("n", nutrients[0]);
            tree.SetFloat("p", nutrients[1]);
            tree.SetFloat("k", nutrients[2]);

            tree.SetFloat("slowN", slowReleaseNutrients[0]);
            tree.SetFloat("slowP", slowReleaseNutrients[1]);
            tree.SetFloat("slowK", slowReleaseNutrients[2]);

            tree.SetFloat("moistureLevel", moistureLevel);
            tree.SetDouble("lastWaterSearchedTotalHours", lastWaterSearchedTotalHours);
            tree.SetInt("originalFertilityN", originalFertility[0]);
            tree.SetInt("originalFertilityP", originalFertility[1]);
            tree.SetInt("originalFertilityK", originalFertility[2]);

            tree.SetDouble("totalHoursForNextStage", totalHoursForNextStage);
            tree.SetDouble("totalHoursFertilityCheck", totalHoursLastUpdate);
            tree.SetDouble("lastMoistureLevelUpdateTotalDays", lastMoistureLevelUpdateTotalDays);
            tree.SetFloat("lastWaterDistance", lastWaterDistance);

            tree.SetBool("ripeCropExposedToFrost", ripeCropColdDamaged);
            tree.SetBool("unripeCropExposedToFrost", unripeCropColdDamaged);
            tree.SetBool("unripeHeatDamaged", unripeHeatDamaged);
            tree.SetBool("saltExposed", damageAccum[(int)EnumCropStressType.Salt] > 1);

            (tree as TreeAttribute).SetStringArray("permaBoosts", PermaBoosts.ToArray());

            tree.SetInt("roomness", roomness);

            tree["cropAttrs"] = cropAttrs;

            if (fertilizerOverlayStrength != null)
            {
                var ftree = new TreeAttribute();
                tree["fertilizerOverlayStrength"] = ftree;
                foreach (var val in fertilizerOverlayStrength)
                {
                    ftree.SetFloat(val.Key, val.Value);
                }
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            var cropProps = GetCrop()?.CropProps;

            if (cropProps != null)
            {
                dsc.AppendLine(Lang.Get("Required Nutrient: {0}", cropProps.RequiredNutrient));
                dsc.AppendLine(Lang.Get("Growth Stage: {0} / {1}", GetCropStage(GetCrop()), cropProps.GrowthStages));
                dsc.AppendLine();
            }

            dsc.AppendLine(Lang.Get("farmland-nutrientlevels", Math.Round(nutrients[0], 1), Math.Round(nutrients[1], 1), Math.Round(nutrients[2], 1)));
            float snn = (float)Math.Round(slowReleaseNutrients[0], 1);
            float snp = (float)Math.Round(slowReleaseNutrients[1], 1);
            float snk = (float)Math.Round(slowReleaseNutrients[2], 1);
            if (snn > 0 || snp > 0 || snk > 0)
            {
                List<string> nutrs = new List<string>();

                if (snn > 0) nutrs.Add(Lang.Get("+{0}% N", snn));
                if (snp > 0) nutrs.Add(Lang.Get("+{0}% P", snp));
                if (snk > 0) nutrs.Add(Lang.Get("+{0}% K", snk));

                dsc.AppendLine(Lang.Get("farmland-activefertilizer", string.Join(", ", nutrs)));
            }


            if (cropProps == null)
            {
                float speedn = (float)Math.Round(100 * GetGrowthRate(EnumSoilNutrient.N), 0);
                float speedp = (float)Math.Round(100 * GetGrowthRate(EnumSoilNutrient.P), 0);
                float speedk = (float)Math.Round(100 * GetGrowthRate(EnumSoilNutrient.K), 0);

                string colorn = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, speedn)]);
                string colorp = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, speedp)]);
                string colork = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, speedk)]);

                dsc.AppendLine(Lang.Get("farmland-growthspeeds", colorn, speedn, colorp, speedp, colork, speedk));
            }
            else
            {
                float speed = (float)Math.Round(100 * GetGrowthRate(cropProps.RequiredNutrient), 0);
                string color = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, speed)]);

                dsc.AppendLine(Lang.Get("farmland-growthspeed", color, speed, cropProps.RequiredNutrient));
            }

            float moisture = (float)Math.Round(moistureLevel * 100, 0);
            string colorm = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, moisture)]);

            dsc.AppendLine(Lang.Get("farmland-moisture", colorm, moisture));

            if ((ripeCropColdDamaged || unripeCropColdDamaged || unripeHeatDamaged) && cropProps != null)
            {
                if (ripeCropColdDamaged)
                {
                    dsc.AppendLine(Lang.Get("farmland-ripecolddamaged", (int)(cropProps.ColdDamageRipeMul * 100)));
                }
                else if (unripeCropColdDamaged)
                {
                    dsc.AppendLine(Lang.Get("farmland-unripecolddamaged", (int)(cropProps.DamageGrowthStuntMul * 100)));
                }
                else if (unripeHeatDamaged)
                {
                    dsc.AppendLine(Lang.Get("farmland-unripeheatdamaged", (int)(cropProps.DamageGrowthStuntMul * 100)));
                }
            }

            if (roomness > 0)
            {
                dsc.AppendLine(Lang.Get("greenhousetempbonus"));
            }

            if (saltExposed)
            {
                dsc.AppendLine(Lang.Get("farmland-saltdamage"));
            }


            dsc.ToString();
        }


        public void WaterFarmland(float dt, bool waterNeightbours = true)
        {
            moistureLevel = Math.Min(1, moistureLevel + dt / 2);

            if (waterNeightbours)
            {
                foreach (BlockFacing neib in BlockFacing.HORIZONTALS)
                {
                    BlockPos npos = base.Pos.AddCopy(neib);
                    BlockEntityFarmland bef = Api.World.BlockAccessor.GetBlockEntity(npos) as BlockEntityFarmland;
                    if (bef != null) bef.WaterFarmland(dt / 3, false);
                }
            }

            updateMoistureLevel(Api.World.Calendar.TotalDays, lastWaterDistance);
            UpdateFarmlandBlock();
        }

        public double TotalHoursForNextStage
        {
            get
            {
                return totalHoursForNextStage;
            }
        }

        public double TotalHoursFertilityCheck
        {
            get
            {
                return totalHoursLastUpdate;
            }
        }

        public float[] Nutrients
        {
            get
            {
                return nutrients;
            }
        }

        public float MoistureLevel
        {
            get
            {
                return moistureLevel;
            }
        }

        public int[] OriginalFertility
        {
            get
            {
                return originalFertility;
            }
        }


        public BlockPos UpPos
        {
            get
            {
                return upPos;
            }
        }

        public ITreeAttribute CropAttributes
        {
            get
            {
                return cropAttrs;
            }
        }


        #region IAnimalFoodSource impl
        public bool IsSuitableFor(Entity entity, string[] diet)
        {
            if (diet == null) return false;

            Block cropBlock = GetCrop();
            if (cropBlock == null) return false;

            for (int i = 0; i < diet.Length; i++)
            {
                if (cropBlock.WildCardMatch(diet[i])) return true;
            }

            return false;
        }

        public float ConsumeOnePortion()
        {
            Block cropBlock = GetCrop();
            if (cropBlock == null) return 0;

            Block deadCropBlock = Api.World.GetBlock(new AssetLocation("deadcrop"));
            Api.World.BlockAccessor.SetBlock(deadCropBlock.Id, upPos);
            var be = Api.World.BlockAccessor.GetBlockEntity(upPos) as BlockEntityDeadCrop;
            be.Inventory[0].Itemstack = new ItemStack(cropBlock);
            be.deathReason = EnumCropStressType.Eaten;
            return 1f;
        }

        public Vec3d Position => base.Pos.ToVec3d().Add(0.5, 1, 0.5);
        public string Type => "food";
        #endregion

        BlockPos IFarmlandBlockEntity.Pos => this.Pos;

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode] => fertilizerTexturePos;

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            mesher.AddMeshData(fertilizerQuad);
            return false;

            // Just doesn't look right anymore when fertilized soil turns its texture into compost or terra preta
            /*if (originalFertility == 0) return false;

            int nowLevel = FertilityLevel((nutrients[0] + nutrients[1] + nutrients[2]) / 3);
            Block farmlandBlock = api.World.BlockAccessor.GetBlock(pos);
            Block nextFarmlandBlock = api.World.GetBlock(farmlandBlock.CodeWithParts(IsWatered ? "moist" : "dry", Fertilities.GetKeyAtIndex(nowLevel)));

            mesher.AddMeshData((api as ICoreClientAPI).TesselatorManager.GetDefaultBlockMesh(nextFarmlandBlock));

            return true;*/
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }



        protected enum EnumWaterSearchResult
        {
            Found,
            NotFound,
            Deferred
        }
    }
}
