using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
    public class BlockEntityFarmland : BlockEntity, IFarmlandBlockEntity, IAnimalFoodSource
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
        // Total game hours from where on it can enter the next growth stage
        protected double totalHoursForNextStage;
        // The last time fertility increase was checked
        protected double totalHoursLastUpdate;

        // Stored values
        protected float[] nutrients = new float[3];
        protected float[] slowReleaseNutrients = new float[3];
        protected double lastWateredTotalHours = 0;
        protected double lastWaterSearchedTotalHours = 0;

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
            upPos = base.Pos.UpCopy();

            if (api is ICoreServerAPI)
            {
                RegisterGameTickListener(Update, 3500);

                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                wsys = api.ModLoader.GetModSystem<WeatherSystemBase>();
            }

            
            if (Block.Attributes != null)
            {
                delayGrowthBelowSunLight = Block.Attributes["delayGrowthBelowSunLight"].AsInt(19);
                lossPerLevel = Block.Attributes["lossPerLevel"].AsFloat(0.1f);

                if (weedNames == null)
                {
                    weedNames = Block.Attributes["weedBlockCodes"].AsObject<CodeAndChance[]>();
                    for (int i = 0; weedNames != null && i < weedNames.Length; i++)
                    {
                        totalWeedChance += weedNames[i].Chance;
                    }
                }
                
            }
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
        }

        public void CreatedFromSoil(Block block)
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

            totalHoursLastUpdate = Api.World.Calendar.TotalHours;
        }

        public bool OnBlockInteract(IPlayer byPlayer)
        {
            JsonObject obj = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack?.Collectible?.Attributes?["fertilizerProps"];
            if (obj == null || !obj.Exists) return false;
            FertilizerProps props = obj.AsObject<FertilizerProps>();
            if (props == null) return false;

            slowReleaseNutrients[0] = Math.Min(150, slowReleaseNutrients[0] + props.N);
            slowReleaseNutrients[1] = Math.Min(150, slowReleaseNutrients[1] + props.P);
            slowReleaseNutrients[2] = Math.Min(150, slowReleaseNutrients[2] + props.K);

            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            Api.World.PlaySoundAt(Api.World.BlockAccessor.GetBlock(base.Pos).Sounds.Hit, base.Pos.X + 0.5, base.Pos.Y + 0.75, base.Pos.Z + 0.5, byPlayer, true, 12);

            MarkDirty(false);
            return true;
        }


        bool farmlandIsAtChunkEdge = false;

        protected EnumWaterSearchResult FindNearbyWater()
        {
            // 1. Watered check
            bool waterNearby = false;
            farmlandIsAtChunkEdge = false;

            Api.World.BlockAccessor.SearchBlocks(
                new BlockPos(Pos.X - 3, Pos.Y, Pos.Z - 3),
                new BlockPos(Pos.X + 3, Pos.Y, Pos.Z + 3),
                (block, pos) =>
                {
                    if (block.LiquidCode == "water")
                    {
                        waterNearby = true;
                        return false;
                    }
                    return true;
                },
                (cx, cy, cz) => farmlandIsAtChunkEdge = true
            );

            if (farmlandIsAtChunkEdge) return EnumWaterSearchResult.Deferred;

            lastWaterSearchedTotalHours = Api.World.Calendar.TotalHours;


            for (int dx = -3; dx <= 3 && !waterNearby; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (Api.World.BlockAccessor.GetBlock(base.Pos.X + dx, base.Pos.Y, base.Pos.Z + dz).LiquidCode == "water")
                    {
                        waterNearby = true;
                        break;
                    }
                }
            }

            if (waterNearby)
            {
                if (!IsWatered) MarkDirty(true);
                lastWateredTotalHours = Api.World.Calendar.TotalHours;
                return EnumWaterSearchResult.Found;
            }

            return EnumWaterSearchResult.NotFound;
        }






        WeatherSystemBase wsys;
        Vec3d tmpPos = new Vec3d();


        private void Update(float dt)
        {
            double hoursNextStage = GetHoursForNextStage();
            bool nearbyWaterTested = false;
            bool nearbyWaterFound = false;

            double nowTotalHours = Api.World.Calendar.TotalHours;



            tmpPos.Set(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            double rainLevel = wsys.GetRainFall(tmpPos);

            bool hasRain = rainLevel > 0.01 && Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y + 1;

            if (hasRain)
            {
                currentlyWateredSeconds += (float)rainLevel / 3f;
                lastWateredMs = Api.World.ElapsedMilliseconds;

                if (currentlyWateredSeconds > 1f)
                {
                    lastWateredTotalHours = Api.World.Calendar.TotalHours + 12;
                    UpdateFarmlandBlock();
                    currentlyWateredSeconds--;
                }

            }

            

            if (Api.World.ElapsedMilliseconds - lastWateredMs > 10000)
            {
                currentlyWateredSeconds = Math.Max(0, currentlyWateredSeconds - dt);
            }

            if (!IsWatered && nowTotalHours - lastWateredTotalHours >= totalHoursWaterRetention - 1 && (nowTotalHours - lastWaterSearchedTotalHours) >= 0.5f)
            {
                nearbyWaterTested = true;
                EnumWaterSearchResult res = FindNearbyWater();
                if (res == EnumWaterSearchResult.Deferred) return; // Wait with updating until neighbouring chunks are loaded

                nearbyWaterFound = res == EnumWaterSearchResult.Found;
            }

            double hoursPassed = nowTotalHours - totalHoursLastUpdate;
            if (hoursPassed < 1) return;

            // Slow down growth on bad light levels
            int sunlight = Api.World.BlockAccessor.GetLightLevel(base.Pos.UpCopy(), EnumLightLevelType.MaxLight);
            double lightGrowthSpeedFactor = GameMath.Clamp(1 - (delayGrowthBelowSunLight - sunlight) * lossPerLevel, 0, 1);

            Block upblock = Api.World.BlockAccessor.GetBlock(upPos);
            
            double lightHoursPenalty = hoursNextStage / lightGrowthSpeedFactor - hoursNextStage;
            double totalHoursNextGrowthState = totalHoursForNextStage + lightHoursPenalty;

            EnumSoilNutrient? currentlyConsumedNutrient = null;
            if (upblock.CropProps != null)
            {
                currentlyConsumedNutrient = upblock.CropProps.RequiredNutrient;
            }

            // Let's increase fertility every 3-4 game hours
            double hourIntervall = (3 + rand.NextDouble());            
            bool growTallGrass = false;
            float[] npkRegain = new float[3];

            
            // Fast forward in 3-4 hour intervalls
            while ((nowTotalHours - totalHoursLastUpdate) > hourIntervall)
            {
                totalHoursLastUpdate += hourIntervall;

                growTallGrass |= rand.NextDouble() < 0.006;

                hourIntervall = (3 + rand.NextDouble());

                bool ripe = HasRipeCrop();

                // Rule 1: Fertility increase up to original levels by 1 every 3-4 ingame hours
                // Rule 2: Fertility does not increase with a ripe crop on it
                npkRegain[0] = ripe ? 0 : 0.33f;
                npkRegain[1] = ripe ? 0 : 0.33f;
                npkRegain[2] = ripe ? 0 : 0.33f;

                // Rule 3: Fertility increase up 3 times slower for the currently growing crop
                if (currentlyConsumedNutrient != null)
                {
                    npkRegain[(int)currentlyConsumedNutrient] /= 3;
                }
                
                for (int i = 0; i < 3; i++)
                {
                    nutrients[i] += Math.Max(0, npkRegain[i] + Math.Min(0, originalFertility - nutrients[i] - npkRegain[i]));

                    // Rule 4: Slow release fertilizer can fertilize up to 100 fertility
                    if (slowReleaseNutrients[i] > 0)
                    {
                        nutrients[i] = Math.Min(100, nutrients[i] + Math.Min(0.1f, slowReleaseNutrients[i]));
                        slowReleaseNutrients[i] = Math.Max(0, slowReleaseNutrients[i] - 0.5f);
                    }
                    else
                    {
                        // Rule 5: Once the slow release fertilizer is consumed, the soil will slowly return to its original fertility

                        if (nutrients[i] > originalFertility)
                        {
                            nutrients[i] = Math.Max(originalFertility, nutrients[i] - 0.05f);
                        }
                    }
                }


                if (nearbyWaterTested && nearbyWaterFound)
                {
                    lastWateredTotalHours = totalHoursLastUpdate;
                } else
                {
                    if (!nearbyWaterTested && !IsWatered && Api.World.Calendar.TotalHours - lastWateredTotalHours >= totalHoursWaterRetention - 1 && (totalHoursLastUpdate - lastWaterSearchedTotalHours) >= 0.5f)
                    {
                        nearbyWaterTested = true;
                        EnumWaterSearchResult res = FindNearbyWater();
                        if (res == EnumWaterSearchResult.Deferred)
                        {
                            totalHoursLastUpdate -= hourIntervall;
                            return; // Wait with updating until neighbouring chunks are loaded
                        }
                        nearbyWaterFound = res == EnumWaterSearchResult.Found;
                    }
                }

                // Did the farmland run out of water at this time?
                if (lastWateredTotalHours - totalHoursNextGrowthState < -totalHoursWaterRetention)
                {
                    totalHoursForNextStage = Api.World.Calendar.TotalHours + hoursNextStage;
                    continue;
                }

                if (totalHoursNextGrowthState <= totalHoursLastUpdate)
                {
                    TryGrowCrop(totalHoursForNextStage);
                    totalHoursForNextStage += hoursNextStage;
                    totalHoursNextGrowthState = totalHoursForNextStage + lightHoursPenalty;
                }
            }



            if (growTallGrass && upblock.BlockMaterial == EnumBlockMaterial.Air)
            {
                double rnd = rand.NextDouble() * totalWeedChance;
                for (int i = 0; i < weedNames.Length; i++)
                {
                    rnd -= weedNames[i].Chance;
                    if (rnd <= 0)
                    {
                        Block weedsBlock = Api.World.GetBlock(weedNames[i].Code);
                        if (weedsBlock != null)
                        {
                            Api.World.BlockAccessor.SetBlock(weedsBlock.BlockId, upPos);
                        }
                        break;
                    }
                }
            }


            UpdateFarmlandBlock();
            Api.World.BlockAccessor.MarkBlockEntityDirty(base.Pos);
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

        internal bool CanPlant()
        {
            Block block = Api.World.BlockAccessor.GetBlock(upPos);
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

                Api.World.BlockAccessor.SetBlock(nextBlock.BlockId, upPos);
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
            Block farmlandBlock = Api.World.BlockAccessor.GetBlock(base.Pos);
            Block nextFarmlandBlock = Api.World.GetBlock(farmlandBlock.CodeWithParts(IsWatered ? "moist" : "dry", Fertilities.GetKeyAtIndex(nowLevel)));

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


        internal int FertilityLevel(float fertiltyValue)
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
            Block block = Api.World.BlockAccessor.GetBlock(upPos);
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

            slowReleaseNutrients[0] = tree.GetFloat("slowN");
            slowReleaseNutrients[1] = tree.GetFloat("slowP");
            slowReleaseNutrients[2] = tree.GetFloat("slowK");

            lastWateredTotalHours = tree.GetDouble("lastWateredTotalHours");
            lastWaterSearchedTotalHours = tree.GetDouble("lastWaterSearchedTotalHours");
            originalFertility = tree.GetInt("originalFertility");

            if (tree.HasAttribute("totalHoursForNextStage"))
            {
                totalHoursForNextStage = tree.GetDouble("totalHoursForNextStage");
                totalHoursLastUpdate = tree.GetDouble("totalHoursFertilityCheck");
            } else
            {
                // Pre v1.5.1
                totalHoursForNextStage = tree.GetDouble("totalDaysForNextStage") * 24;
                totalHoursLastUpdate = tree.GetDouble("totalDaysFertilityCheck") * 24;
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

            tree.SetFloat("slowN", slowReleaseNutrients[0]);
            tree.SetFloat("slowP", slowReleaseNutrients[1]);
            tree.SetFloat("slowK", slowReleaseNutrients[2]);

            tree.SetDouble("lastWateredTotalHours", lastWateredTotalHours);
            tree.SetDouble("lastWaterSearchedTotalHours", lastWaterSearchedTotalHours);
            tree.SetInt("originalFertility", originalFertility);
            tree.SetDouble("totalHoursForNextStage", totalHoursForNextStage);
            tree.SetDouble("totalHoursFertilityCheck", totalHoursLastUpdate);
            tree["cropAttrs"] = cropAttrs;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.AppendLine(Lang.Get("Nutrient Levels: N {0}%, P {1}%, K {2}%", Math.Round(nutrients[0],1), Math.Round(nutrients[1],1), Math.Round(nutrients[2],1)));
            float snn = (float)Math.Round(slowReleaseNutrients[0],1);
            float snp = (float)Math.Round(slowReleaseNutrients[1],1);
            float snk = (float)Math.Round(slowReleaseNutrients[2],1);
            if (snn > 0 || snp > 0 || snk > 0)
            {
                dsc.AppendLine(Lang.Get("Slow Release Nutrients: N {0}%, P {1}%, K {2}%", snn, snp, snk));
            }

            dsc.AppendLine(Lang.Get("Growth speeds: N Crop: {0}%, P Crop: {1}%, K Crop: {2}%", Math.Round(100 * 1 / LowNutrientPenalty(EnumSoilNutrient.N), 0), Math.Round(100 * 1 / LowNutrientPenalty(EnumSoilNutrient.P), 0), Math.Round(100 * 1 / LowNutrientPenalty(EnumSoilNutrient.K), 0)));

            dsc.ToString();
        }


        public void WaterFarmland(float dt, bool waterNeightbours = true)
        {
            currentlyWateredSeconds += dt;
            lastWateredMs = Api.World.ElapsedMilliseconds;

            if (currentlyWateredSeconds > 1f)
            {
                if (IsWatered && waterNeightbours)
                {
                    foreach (BlockFacing neib in BlockFacing.HORIZONTALS) {
                        BlockPos npos = base.Pos.AddCopy(neib);
                        BlockEntityFarmland bef = Api.World.BlockAccessor.GetBlockEntity(npos) as BlockEntityFarmland;
                        if (bef != null) bef.WaterFarmland(1.01f, false);
                    }
                }

                lastWateredTotalHours = Api.World.Calendar.TotalHours;
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

        public bool IsWatered
        {
            get
            {
                return lastWateredTotalHours > 0 && (Api.World.Calendar.TotalHours - lastWateredTotalHours) < totalHoursWaterRetention;
            }
        }

        public int OriginalFertility
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
        public bool IsSuitableFor(Entity entity)
        {
            Block cropBlock = GetCrop();
            if (cropBlock == null) return false;

            AssetLocation[] diet = entity.Properties.Attributes?["blockDiet"]?.AsArray<AssetLocation>();
            if (diet == null) return false;

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

            Api.World.BlockAccessor.BreakBlock(upPos, null);

            return 1f;
        }

        public Vec3d Position => base.Pos.ToVec3d().Add(0.5, 1, 0.5);
        public string Type => "food";
        #endregion

        BlockPos IFarmlandBlockEntity.Pos => this.Pos;

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            return false;

            // Just doesn't look right anymore when fertilized soil turns its texture into compost or terra preta
            /*if (originalFertility == 0) return false;

            int nowLevel = FertilityLevel((nutrients[0] + nutrients[1] + nutrients[2]) / 3);
            Block farmlandBlock = api.World.BlockAccessor.GetBlock(pos);
            Block nextFarmlandBlock = api.World.GetBlock(farmlandBlock.CodeWithParts(IsWatered ? "moist" : "dry", Fertilities.GetKeyAtIndex(nowLevel)));

            mesher.AddMeshData((api as ICoreClientAPI).TesselatorManager.GetDefaultBlockMesh(nextFarmlandBlock));

            return true;*/
        }
        


        protected enum EnumWaterSearchResult
        {
            Found, 
            NotFound,
            Deferred
        }
    }
}
