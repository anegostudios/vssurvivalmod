using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

#nullable disable

public delegate void FarmlandFastForwardUpdate(double hourIntervall, ClimateCondition conds, double lightGrowthSpeedFactor, bool growthPaused);
public delegate void FarmlandUpdateEnd();

// Base class for farmland crops that fast forward growth with nutrient and moisture tracking.
public class BlockEntitySoilNutrition : BlockEntityFastForwardGrowth, ITexPositionSource
{
    public static API.Datastructures.OrderedDictionary<string, float> Fertilities = new()
    {
        { "verylow", 5 },
        { "low", 25 },
        { "medium", 50 },
        { "compost", 65 },
        { "high", 80 }
    };

    protected HashSet<string> PermaBoosts = new HashSet<string>();
    // How many hours this block can retain water before becoming dry
    protected float totalHoursWaterRetention;
    
    // Stored values
    protected float[] nutrients = new float[3];
    protected float[] slowReleaseNutrients = new float[3];
    protected Dictionary<string, float> fertilizerOverlayStrength = null;
    // 0 = bone dry, 1 = completely soggy
    protected float moistureLevel = 0;
    protected double lastWaterSearchedTotalHours;
    // The fertility the soil will recover to (the soil from which the farmland was made of)
    protected int[] originalFertility = new int[3];
    protected bool saltExposed;
    protected bool farmlandIsAtChunkEdge = false;
    // 0 = Unknown
    // 1 = too hot
    // 2 = too cold
    // 3 = saltwater
    protected float[] damageAccum = new float[Enum.GetValues(typeof(EnumCropStressType)).Length];
    protected Vec3d tmpPos = new Vec3d();
    protected float lastWaterDistance = 99;
    protected double lastMoistureLevelUpdateTotalDays;
    
    
    protected float fertilityRecoverySpeed = 0.25f;
    protected float growthRateMul = 1f;
    protected TextureAtlasPosition fertilizerTexturePos;
    protected ICoreClientAPI capi;
    

    public MeshData FertilizerQuad;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        
        capi = api as ICoreClientAPI;
        totalHoursWaterRetention = Api.World.Calendar.HoursPerDay * 4; // Water stays for 4 days
        fertilityRecoverySpeed = Api.World.Config.GetFloat("fertilityRecoverySpeed", fertilityRecoverySpeed);
        growthRateMul = (float)Api.World.Config.GetDecimal("cropGrowthRateMul", growthRateMul);

        if (api is ICoreServerAPI)
        {
            if (Api.World.Config.GetBool("processCrops", true))
            {
                RegisterGameTickListener(Update, 3500, rand.Next(3500));
            }
        }

        updateFertilizerQuad();
    }


    public override void OnCreatedFromSoil(Block block, TreeAttribute existingFertilityData = null)
    {
        base.OnCreatedFromSoil(block, existingFertilityData);

        if (existingFertilityData == null)
        {
            string strfertility = block.Variant["fertility"];
            float val = 0;
            if (strfertility == null || !Fertilities.TryGetValue(strfertility, out val)) val = 25;
            originalFertility[0] = (int)val;
            originalFertility[1] = (int)val;
            originalFertility[2] = (int)val;

            nutrients[0] = originalFertility[0];
            nutrients[1] = originalFertility[1];
            nutrients[2] = originalFertility[2];
        } else
        {
            loadEnvData(existingFertilityData);
        }

        
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

        SoundAttributes? fertilizedSound = Api.World.BlockAccessor.GetBlock(base.Pos).Attributes?["fertilizedSound"].AsObject<SoundAttributes?>(null, Block.Code.Domain, true);
        Api.World.PlaySoundAt(fertilizedSound ?? new SoundAttributes(AssetLocation.Create("sounds/block/dirt"), true) { Range = 12 }, base.Pos, 0.25, byPlayer);

        MarkDirty(false);
        return true;
    }

    public virtual void OnCropBlockBroken()
    {
        for (int i = 0; i < damageAccum.Length; i++) damageAccum[i] = 0;
        MarkDirty(true);
    }

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
            result = EnumWaterSearchResult.Found;
            return waterDistance;
        }

        result = EnumWaterSearchResult.NotFound;
        return 99;
    }



    protected bool tryUpdateMoistureLevel(double totalDays, bool searchNearbyWater)
    {
        float dist = 99;
        if (searchNearbyWater)
        {
            dist = GetNearbyWaterDistance(out EnumWaterSearchResult res, 0);
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
    protected bool updateMoistureLevel(double totalDays, float waterDistance)
    {
        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y + RainHeightOffset;
        return updateMoistureLevel(totalDays, waterDistance, skyExposed);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns>true if it was a longer interval check (checked rain as well) so that an UpdateFarmlandBlock() is advisable</returns>
    protected bool updateMoistureLevel(double totalDays, float waterDistance, bool skyExposed, ClimateCondition baseClimate = null)
    {
        tmpPos.Set(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);

        float minMoisture = GameMath.Clamp(1 - waterDistance / 4f, 0, 1);

        if (lastMoistureLevelUpdateTotalDays > Api.World.Calendar.TotalDays)
        {
            // We need to rollback time when the blockEntity saved date is ahead of the calendar date: can happen if a schematic is imported
            lastMoistureLevelUpdateTotalDays = Api.World.Calendar.TotalDays;
            return false;
        }

        double hoursPassed = Math.Min((totalDays - lastMoistureLevelUpdateTotalDays) * Api.World.Calendar.HoursPerDay, totalHoursWaterRetention);
        if (hoursPassed < 0.03f)
        {
            // Get wet from a water source
            moistureLevel = Math.Max(moistureLevel, minMoisture);

            return false;
        }

        // Dry out
        moistureLevel = Math.Max(minMoisture, moistureLevel - (float)hoursPassed / totalHoursWaterRetention);

        // Get wet from all the rainfall since last update
        if (skyExposed)
        {
            if (baseClimate == null && hoursPassed > 0) baseClimate = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.WorldGenValues, totalDays - hoursPassed / Api.World.Calendar.HoursPerDay / 2);
            while (hoursPassed > 0)
            {
                double rainLevel = msFarming.Wsys.GetPrecipitation(Pos, totalDays - hoursPassed / Api.World.Calendar.HoursPerDay, baseClimate);
                moistureLevel = GameMath.Clamp(moistureLevel + (float)rainLevel / 3f, 0, 1);
                hoursPassed--;
            }
        }

        lastMoistureLevelUpdateTotalDays = totalDays;

        return true;
    }


    protected override void Update(float dt)
    {
        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y + RainHeightOffset;
        if (updateMoistureLevel(Api.World.Calendar.TotalHours / Api.World.Calendar.HoursPerDay, lastWaterDistance, skyExposed)) UpdateFarmlandBlock();
        base.Update(dt);
    }

    protected override void beginIntervalledUpdate(out FarmlandFastForwardUpdate onInterval, out FarmlandUpdateEnd onEnd)
    {
        bool nearbyWaterTested = false;
        bool growTallGrass = false;
        float waterDistance = 99;
        Block upblock = Api.World.BlockAccessor.GetBlock(upPos);
        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y + RainHeightOffset;

        EnumSoilNutrient? currentlyConsumedNutrient = null;
        if (upblock.CropProps != null)
        {
            currentlyConsumedNutrient = upblock.CropProps.RequiredNutrient;
        }

        onInterval = (hourIntervall, conds, lightGrowthSpeedFactor, growthPaused) =>
        {
            if (!nearbyWaterTested)
            {
                waterDistance = GetNearbyWaterDistance(out EnumWaterSearchResult res, (float)hourIntervall);
                if (res == EnumWaterSearchResult.Deferred) return; // Wait with updating until neighbouring chunks are loaded
                if (res == EnumWaterSearchResult.NotFound) waterDistance = 99;
                nearbyWaterTested = true;

                lastWaterDistance = waterDistance;
            }

            updateMoistureLevel(totalHoursLastUpdate / Api.World.Calendar.HoursPerDay, waterDistance, skyExposed, conds);

            if (!growthPaused)
            {
                updateSoilFertility(currentlyConsumedNutrient, RecoverFertility);
            }

            growTallGrass |= rand.NextDouble() < 0.006;
        };

        onEnd = () => {
            if (growTallGrass && upblock.BlockMaterial == EnumBlockMaterial.Air)
            {
                double rnd = rand.NextDouble() * msFarming.Config.TotalWeedChance;
                for (int i = 0; i < msFarming.Config.WeedBlockCodes.Length; i++)
                {
                    rnd -= msFarming.Config.WeedBlockCodes[i].Chance;
                    if (rnd <= 0)
                    {
                        Block weedsBlock = Api.World.GetBlock(msFarming.Config.WeedBlockCodes[i].Code);
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
        };
    }

    protected override void onRollback(double hoursrolledback)
    {
        base.onRollback(hoursrolledback);
        lastMoistureLevelUpdateTotalDays -= hoursrolledback;
        lastWaterSearchedTotalHours -= hoursrolledback;
    }



    protected void updateSoilFertility(EnumSoilNutrient? currentlyConsumedNutrient, bool recoverFertility)
    {
        float[] npkRegain = new float[3];
        // Rule 1: Fertility increase up to original levels by 1 every 3-4 ingame hours
        // Rule 2: Fertility does not increase with a ripe crop on it
        npkRegain[0] = recoverFertility ? fertilityRecoverySpeed : 0;
        npkRegain[1] = recoverFertility ? fertilityRecoverySpeed : 0;
        npkRegain[2] = recoverFertility ? fertilityRecoverySpeed : 0;

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
    }


    public float GetGrowthRate(EnumSoilNutrient nutrient)
    {
        // (x/70 - 0.143)^0.35
        // http://fooplot.com/#W3sidHlwZSI6MCwiZXEiOiIoeC83MC0wLjE0MyleMC4zNSIsImNvbG9yIjoiIzAwMDAwMCJ9LHsidHlwZSI6MTAwMCwid2luZG93IjpbIjAiLCIxMDAiLCIwIiwiMS4yNSJdfV0-

        float moistFactor = (float)Math.Pow(Math.Max(0.01, moistureLevel * 100 / 70 - 0.143), 0.35);

        return nutrients[(int)nutrient] switch
        {
            > 75 => moistFactor * 1.1f,
            > 50 => moistFactor * 1,
            > 35 => moistFactor * 0.9f,
            > 20 => moistFactor * 0.6f,
            > 5 => moistFactor * 0.3f,
            _ => moistFactor * 0.1f
        };
    }


    public void ConsumeNutrients(EnumSoilNutrient nutrient, float amount)
    {
        nutrients[(int)nutrient] = Math.Max(0, nutrients[(int)nutrient] - amount);
        UpdateFarmlandBlock();
        MarkDirty(true);
    }


    protected void UpdateFarmlandBlock()
    {
        int nowLevel = GetFertilityLevel((originalFertility[0] + originalFertility[1] + originalFertility[2]) / 3);
        Block hereblock = Api.World.BlockAccessor.GetBlock(Pos);

        var newCode = hereblock.CodeWithVariants(["state", "fertility"], [IsVisiblyMoist ? "moist" : "dry", Fertilities.GetKeyAtIndex(nowLevel)]);
        Block nextBlock = Api.World.GetBlock(newCode);

        if (hereblock.BlockId != nextBlock.BlockId)
        {
            Api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, Pos);
            Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
            Api.World.BlockAccessor.MarkBlockDirty(Pos);
        }
    }


    public int GetFertilityLevel(float fertiltyValue)
    {
        int i = 0;
        foreach (var val in Fertilities)
        {
            if (val.Value >= fertiltyValue) return i;
            i++;
        }
        return Fertilities.Count - 1;
    }



    protected void updateFertilizerQuad()
    {
        if (capi == null) return;
        AssetLocation loc = new AssetLocation();

        if (fertilizerOverlayStrength == null || fertilizerOverlayStrength.Count == 0)
        {
            bool dirty = FertilizerQuad != null;
            FertilizerQuad = null;
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

    protected virtual void genFertilizerQuad()
    {
        var shape = capi.Assets.TryGet(new AssetLocation("shapes/block/farmland-fertilizer.json")).ToObject<Shape>();

        capi.Tesselator.TesselateShape(new TesselationMetaData()
        {
            TypeForLogging = "farmland fertilizer quad",
            TexSource = this,
        }, shape, out FertilizerQuad);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        loadEnvData(tree);
        roomness = tree.GetInt("roomness");
        updateFertilizerQuad();
    }

    protected void loadEnvData(ITreeAttribute tree)
    {
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
        }
        else
        {
            originalFertility[0] = tree.GetInt("originalFertilityN");
            originalFertility[1] = tree.GetInt("originalFertilityP");
            originalFertility[2] = tree.GetInt("originalFertilityK");
        }

        lastMoistureLevelUpdateTotalDays = tree.GetDouble("lastMoistureLevelUpdateTotalDays");
        lastWaterDistance = tree.GetFloat("lastWaterDistance");
        saltExposed = tree.GetBool("saltExposed");

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
        }
        else fertilizerOverlayStrength = null;
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

        tree.SetBool("saltExposed", damageAccum[(int)EnumCropStressType.Salt] > 1);
        
        tree.SetDouble("lastMoistureLevelUpdateTotalDays", lastMoistureLevelUpdateTotalDays);
        tree.SetFloat("lastWaterDistance", lastWaterDistance);
        (tree as TreeAttribute).SetStringArray("permaBoosts", PermaBoosts.ToArray());
        tree.SetInt("roomness", roomness);


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

        if (ConsidersMoistureLevels)
        {
            float moisture = (float)Math.Round(moistureLevel * 100, 0);
            string colorm = ColorUtil.Int2Hex(GuiStyle.DamageColorGradient[(int)Math.Min(99, moisture)]);
            dsc.AppendLine(Lang.Get("farmland-moisture", colorm, moisture));
        }

        if (roomness > 0)
        {
            dsc.AppendLine(Lang.Get("greenhousetempbonus"));
        }

        if (saltExposed)
        {
            dsc.AppendLine(Lang.Get("farmland-saltdamage"));
        }
    }


    public void WaterFarmland(float dt, bool waterNeightbours = true)
    {
        float prevLevel = moistureLevel;
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

        if (moistureLevel - prevLevel > 0.05) MarkDirty(true);
    }


    public int Roomness => roomness;

    public bool IsVisiblyMoist
    {
        get { return moistureLevel > 0.1; }
    }
    protected virtual bool RecoverFertility => true;
    protected virtual bool ConsidersMoistureLevels => true;

    public float[] Nutrients => nutrients;
    public float MoistureLevel => moistureLevel;
    public int[] OriginalFertility => originalFertility;
    public BlockPos UpPos => upPos;
    public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    public TextureAtlasPosition this[string textureCode] => fertilizerTexturePos;

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        mesher.AddMeshData(FertilizerQuad);
        return false;
    }

    protected enum EnumWaterSearchResult
    {
        Found,
        NotFound,
        Deferred
    }

}
