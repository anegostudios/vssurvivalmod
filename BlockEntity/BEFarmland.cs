using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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
    public class BlockEntityFarmland : BlockEntity, IFarmlandBlockEntity, IAnimalFoodSource, IBlockShapeSupplier
    {
        protected static Random rand = new Random();
        protected static CodeAndChance[] weedNames;
        protected static float totalWeedChance;

        public static OrderedDictionary<string, float> Fertilities = new OrderedDictionary<string, float>{
            { "verylow", 5 },
            { "low", 25 },
            { "medium", 50 },
            { "compost", 65 },
            { "high", 80 },
        };

        // How many hours this block can retain water before becoming dry
        protected double totalHoursWaterRetention = 24.5;

        protected BlockPos upPos;
        protected long growListenerId;
        // Total game hours from where on it can enter the next growth stage
        protected double totalHoursForNextStage;
        // The last time fertility increase was checked
        protected double totalHoursFertilityCheck;

        // Stored values
        protected float[] nutrients = new float[3];
        protected double lastWateredTotalHours = 0;

        // When watering with a watering can, not stored values
        protected float currentlyWateredSeconds;
        protected long lastWateredMs;


        public int originalFertility; // The fertility the soil will recover to (the soil from which the farmland was made of)
        protected TreeAttribute cropAttrs = new TreeAttribute();

        protected int delayGrowthBelowSunLight = 19;
        protected float lossPerLevel = 0.1f; 
        
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            upPos = base.pos.UpCopy();

            if (api is ICoreServerAPI)
            {
                if (HasUnripeCrop())
                {
                    growListenerId = RegisterGameTickListener(CheckGrow, 2000);
                }

                RegisterGameTickListener(SlowTick, 15000);

                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
            }

            Block block = api.World.BlockAccessor.GetBlock(base.pos);
            if (block.Attributes != null)
            {
                delayGrowthBelowSunLight = block.Attributes["delayGrowthBelowSunLight"].AsInt(19);
                lossPerLevel = block.Attributes["lossPerLevel"].AsFloat(0.1f);

                if (weedNames == null)
                {
                    weedNames = block.Attributes["weedBlockCodes"].AsObject<CodeAndChance[]>();
                    for (int i = 0; weedNames != null && i < weedNames.Length; i++)
                    {
                        totalWeedChance += weedNames[i].Chance;
                    }
                }
                
            }
        }


        internal void CreatedFromSoil(Block block)
        {
            string fertility = block.LastCodePart(1);
            if (block is BlockFarmland)
            {
                fertility = block.LastCodePart();
            }
            originalFertility = (int)Fertilities[fertility];

            nutrients[0] = originalFertility;
            nutrients[1] = originalFertility;
            nutrients[2] = originalFertility;

            totalHoursFertilityCheck = api.World.Calendar.TotalHours;
        }


        private void SlowTick(float dt)
        {
            FindNearbyWater();

            if (IsWatered)
            {
                Block block = api.World.BlockAccessor.GetBlock(base.pos);
                if (block.BlockMaterial == EnumBlockMaterial.Air)
                {
                    // Block must have been removed already. This code should not get excecuted in that case but it does. I don't know why yet.
                    // OnBlockRemoved should have removed the game tick listener during removal.
                    api.World.BlockAccessor.RemoveBlockEntity(base.pos);
                    return;
                }

                if (HasUnripeCrop() && growListenerId == 0)
                {
                    growListenerId = RegisterGameTickListener(CheckGrow, 2000);
                }
            }

            UpdateFarmlandBlock();


            Block upblock = api.World.BlockAccessor.GetBlock(upPos);
            if (upblock == null) return;

            double hoursPassed = api.World.Calendar.TotalHours - totalHoursFertilityCheck;

            // Let's increase fertility every 3-4 game hours
            double hoursRequired = (3 + rand.NextDouble());
            int fertilityGained = 0;
            bool growTallGrass = false;
            while (hoursPassed > hoursRequired)
            {
                fertilityGained++;
                hoursPassed -= hoursRequired;
                totalHoursFertilityCheck += hoursRequired;

                hoursRequired = (2 + rand.NextDouble());
                growTallGrass |= rand.NextDouble() < 0.006;
            }

            if (upblock.BlockMaterial == EnumBlockMaterial.Air && growTallGrass)
            {
                double rnd = rand.NextDouble() * totalWeedChance;
                for (int i = 0; i < weedNames.Length; i++)
                {
                    rnd -= weedNames[i].Chance;
                    if (rnd <= 0)
                    {
                        Block weedsBlock = api.World.GetBlock(weedNames[i].Code);
                        if (weedsBlock != null)
                        {
                            api.World.BlockAccessor.SetBlock(weedsBlock.BlockId, upPos);
                        }
                        break;
                    }
                }  
            }
            

            if (fertilityGained > 0 && (nutrients[0] < originalFertility || nutrients[1] < originalFertility || nutrients[2] < originalFertility))
            {
                EnumSoilNutrient? currentlyConsumedNutrient = null;
                if (upblock.CropProps != null)
                {
                    currentlyConsumedNutrient = upblock.CropProps.RequiredNutrient;
                    fertilityGained /= 3;
                    if (HasRipeCrop()) fertilityGained = 0;
                }

                if (currentlyConsumedNutrient != EnumSoilNutrient.N) nutrients[0] = Math.Min(originalFertility, nutrients[0] + fertilityGained);
                if (currentlyConsumedNutrient != EnumSoilNutrient.P) nutrients[1] = Math.Min(originalFertility, nutrients[1] + fertilityGained);
                if (currentlyConsumedNutrient != EnumSoilNutrient.K) nutrients[2] = Math.Min(originalFertility, nutrients[2] + fertilityGained);

                api.World.BlockAccessor.MarkBlockEntityDirty(base.pos);
            }
        }


        protected void FindNearbyWater()
        {
            // 1. Watered check
            bool waterNearby = false;

            for (int dx = -3; dx <= 3 && !waterNearby; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (api.World.BlockAccessor.GetBlock(base.pos.X + dx, base.pos.Y, base.pos.Z + dz).LiquidCode == "water")
                    {
                        waterNearby = true;
                        break;
                    }
                }
            }

            if (waterNearby)
            {
                lastWateredTotalHours = api.World.Calendar.TotalHours;
            }
        }

        private void CheckGrow(float dt)
        {
            double hoursNextStage = GetHoursForNextStage();

            if (api.World.ElapsedMilliseconds - lastWateredMs > 10000)
            {
                currentlyWateredSeconds = Math.Max(0, currentlyWateredSeconds - dt);
            }

            // If chunk got loaded after a long inactivity, test for water right away, otherwise crops wont fast forward
            if (!IsWatered && api.World.Calendar.TotalHours - lastWateredTotalHours >= totalHoursWaterRetention - 1)
            {
                FindNearbyWater();
            }

            // Slow down growth on bad light levels
            int sunlight = api.World.BlockAccessor.GetLightLevel(base.pos.UpCopy(), EnumLightLevelType.MaxLight);
            double lightGrowthSpeedFactor = GameMath.Clamp(1 - (delayGrowthBelowSunLight - sunlight) * lossPerLevel, 0, 1);

            if (lightGrowthSpeedFactor <= 0)
            {
                return;
            }

            double lightHoursPenalty = hoursNextStage / lightGrowthSpeedFactor - hoursNextStage;
            double totalHoursNextGrowthState = totalHoursForNextStage + lightHoursPenalty;


            while (api.World.Calendar.TotalHours > totalHoursNextGrowthState)
            {
                // Did the farmland run out of water at this time?
                if (lastWateredTotalHours - totalHoursNextGrowthState < -totalHoursWaterRetention)
                {
                    totalHoursForNextStage = api.World.Calendar.TotalHours + hoursNextStage;
                    break;
                }

                TryGrowCrop(totalHoursForNextStage);
                totalHoursForNextStage += hoursNextStage;
                totalHoursNextGrowthState = totalHoursForNextStage + lightHoursPenalty;
            }
            
            if (HasRipeCrop())
            {
                api.Event.UnregisterGameTickListener(growListenerId);
                growListenerId = 0;
            }
        }

        public double GetHoursForNextStage()
        {
            Block block = GetCrop();
            if (block == null) return 99999999;

            float stageHours = 24 * block.CropProps.TotalGrowthDays / block.CropProps.GrowthStages;

            stageHours *= LowNutrientPenalty(block.CropProps.RequiredNutrient);

            // Add a bit random to it (+/- 10%)
            stageHours *= (float)(0.9 + 0.2 * rand.NextDouble());

            
            return stageHours;
        }

        public float LowNutrientPenalty(EnumSoilNutrient nutrient)
        {
            if (nutrients[(int)nutrient] > 75) return 1/1.1f;
            if (nutrients[(int)nutrient] > 50) return 1;
            if (nutrients[(int)nutrient] > 35) return 1/0.9f;
            if (nutrients[(int)nutrient] > 20) return 1/0.6f;
            if (nutrients[(int)nutrient] > 5) return 1/0.3f;
            return 1/0.1f;
        }

        public float DeathChance(int nutrientIndex)
        {
            if (nutrients[nutrientIndex] <= 5) return 0.5f;
            return 0f;
        }

        internal bool TryPlant(Block block)
        {
            if (CanPlant() && block.CropProps != null)
            {
                api.World.BlockAccessor.SetBlock(block.BlockId, upPos);
                totalHoursForNextStage = api.World.Calendar.TotalHours + GetHoursForNextStage();

                if (HasUnripeCrop() && api is ICoreServerAPI && growListenerId == 0)
                {
                    growListenerId = RegisterGameTickListener(CheckGrow, 2000);
                }

                foreach (CropBehavior behavior in block.CropProps.Behaviors)
                {
                    behavior.OnPlanted(api);
                }

                return true;
            }

            return false;
        }

        internal bool CanPlant()
        {
            Block block = api.World.BlockAccessor.GetBlock(upPos);
            return block == null || block.BlockMaterial == EnumBlockMaterial.Air;
        }
        

        internal bool HasUnripeCrop()
        {
            Block block = GetCrop();
            return block != null && CropStage(block) < block.CropProps.GrowthStages;
        }

        internal bool HasRipeCrop()
        {
            Block block = GetCrop();
            return block != null && CropStage(block) >= block.CropProps.GrowthStages;
        }

        internal bool TryGrowCrop(double currentTotalHours)
        {
            Block block = GetCrop();
            if (block == null) return false;

            int currentGrowthStage = CropStage(block);
            if (currentGrowthStage < block.CropProps.GrowthStages)
            {
                int newGrowthStage = currentGrowthStage + 1;

                Block nextBlock = api.World.GetBlock(block.CodeWithParts("" + newGrowthStage));
                if (nextBlock == null) return false;

                if (block.CropProps.Behaviors != null)
                {
                    EnumHandling handled = EnumHandling.PassThrough;
                    bool result = false;
                    foreach (CropBehavior behavior in block.CropProps.Behaviors)
                    {
                        result = behavior.TryGrowCrop(api, this, currentTotalHours, newGrowthStage, ref handled);
                        if (handled == EnumHandling.PreventSubsequent) return result;
                    }
                    if (handled == EnumHandling.PreventDefault) return result;
                }

                api.World.BlockAccessor.SetBlock(nextBlock.BlockId, upPos);
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


        void UpdateFarmlandBlock()
        {
            // v1.10: Let's get rid of the mechanic that the farmland exchanges blocks
            // it will just stay forever the original fertility block. For this, well make it so
            // that in v1.10 they slowly restore to their original fertility blocks and then in v1.11 we remove the ExchangeBlock altogether


            int nowLevel = FertilityLevel(originalFertility);// FertilityLevel((nutrients[0] + nutrients[1] + nutrients[2]) / 3);
            Block farmlandBlock = api.World.BlockAccessor.GetBlock(pos);
            Block nextFarmlandBlock = api.World.GetBlock(farmlandBlock.CodeWithParts(IsWatered ? "moist" : "dry", Fertilities.GetKeyAtIndex(nowLevel)));

            if (nextFarmlandBlock == null)
            {
                api.World.BlockAccessor.RemoveBlockEntity(pos);
                return;
            }

            if (farmlandBlock.BlockId != nextFarmlandBlock.BlockId)
            {
                api.World.BlockAccessor.ExchangeBlock(nextFarmlandBlock.BlockId, pos);
                api.World.BlockAccessor.MarkBlockEntityDirty(pos);
                api.World.BlockAccessor.MarkBlockDirty(pos);
            }
        }


        int FertilityLevel(float fertiltyValue)
        {
            int i = 0;
            foreach (var val in Fertilities) {
                if (val.Value >= fertiltyValue) return i;
                i++;
            }
            return 3;
        }

        internal Block GetCrop()
        {
            Block block = api.World.BlockAccessor.GetBlock(upPos);
            if (block == null || block.CropProps == null) return null;
            return block;
        }


        internal int CropStage(Block block)
        {
            int stage = 0;
            int.TryParse(block.LastCodePart(), out stage);
            return stage;
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            nutrients[0] = tree.GetFloat("n");
            nutrients[1] = tree.GetFloat("p");
            nutrients[2] = tree.GetFloat("k");
            lastWateredTotalHours = tree.GetDouble("lastWateredTotalHours");
            originalFertility = tree.GetInt("originalFertility");

            if (tree.HasAttribute("totalHoursForNextStage"))
            {
                totalHoursForNextStage = tree.GetDouble("totalHoursForNextStage");
                totalHoursFertilityCheck = tree.GetDouble("totalHoursFertilityCheck");
            } else
            {
                // Pre v1.5.1
                totalHoursForNextStage = tree.GetDouble("totalDaysForNextStage") * 24;
                totalHoursFertilityCheck = tree.GetDouble("totalDaysFertilityCheck") * 24;
            }

            TreeAttribute cropAttrs = tree["cropAttrs"] as TreeAttribute;
            if (cropAttrs == null) cropAttrs = new TreeAttribute();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("n", nutrients[0]);
            tree.SetFloat("p", nutrients[1]);
            tree.SetFloat("k", nutrients[2]);
            tree.SetDouble("lastWateredTotalHours", lastWateredTotalHours);
            tree.SetInt("originalFertility", originalFertility);
            tree.SetDouble("totalHoursForNextStage", totalHoursForNextStage);
            tree.SetDouble("totalHoursFertilityCheck", totalHoursFertilityCheck);
            tree["cropAttrs"] = cropAttrs;
        }


        public override string GetBlockInfo(IPlayer forPlayer)
        {
            return
                "Nutrient Levels: N " + (int)nutrients[0] + "%, P " + (int)nutrients[1] + "%, K " + (int)nutrients[2] + "%\n" +
                "Growth Speeds: N " + Math.Round(100*1/LowNutrientPenalty(EnumSoilNutrient.N),0) + "%, P " + Math.Round(100*1/LowNutrientPenalty(EnumSoilNutrient.P),0) + "%, K " + Math.Round(100*1/LowNutrientPenalty(EnumSoilNutrient.K),0) + "%\n"
            ;
        }

        public void WaterFarmland(float dt, bool waterNeightbours = true)
        {
            currentlyWateredSeconds += dt;
            lastWateredMs = api.World.ElapsedMilliseconds;

            if (currentlyWateredSeconds > 1f)
            {
                if (IsWatered && waterNeightbours)
                {
                    foreach (BlockFacing neib in BlockFacing.HORIZONTALS) {
                        BlockPos npos = pos.AddCopy(neib);
                        BlockEntityFarmland bef = api.World.BlockAccessor.GetBlockEntity(npos) as BlockEntityFarmland;
                        if (bef != null) bef.WaterFarmland(1.01f, false);
                    }
                }

                lastWateredTotalHours = api.World.Calendar.TotalHours;
                UpdateFarmlandBlock();
                currentlyWateredSeconds--;
            }

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
                return totalHoursFertilityCheck;
            }
        }

        public float[] Nutrients
        {
            get
            {
                return nutrients;
            }
        }

        public bool IsWatered
        {
            get
            {
                return lastWateredTotalHours > 0 && (api.World.Calendar.TotalHours - lastWateredTotalHours) < totalHoursWaterRetention;
            }
        }

        public int OriginalFertility
        {
            get
            {
                return originalFertility;
            }
        }

        public BlockPos Pos
        {
            get
            {
                return base.pos;
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
        public bool IsSuitableFor(Entity entity)
        {
            Block cropBlock = GetCrop();
            if (cropBlock == null) return false;
            if (cropBlock.Code.Path.Contains("pumpkin")) return false;

            string[] diet = entity.Properties.Attributes?["diet"]?.AsArray<string>();
            return diet != null && diet.Contains("crops");
        }

        public float ConsumeOnePortion()
        {
            Block cropBlock = GetCrop();
            if (cropBlock == null) return 0;

            api.World.BlockAccessor.BreakBlock(upPos, null);

            return 0.5f;
        }

        public bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            int nowLevel = FertilityLevel((nutrients[0] + nutrients[1] + nutrients[2]) / 3);
            Block farmlandBlock = api.World.BlockAccessor.GetBlock(pos);
            Block nextFarmlandBlock = api.World.GetBlock(farmlandBlock.CodeWithParts(IsWatered ? "moist" : "dry", Fertilities.GetKeyAtIndex(nowLevel)));

            mesher.AddMeshData((api as ICoreClientAPI).TesselatorManager.GetDefaultBlockMesh(nextFarmlandBlock));

            return true;
        }

        public Vec3d Position => pos.ToVec3d().Add(0.5, 1, 0.5);
        public string Type => "food";
        #endregion
    }
}
