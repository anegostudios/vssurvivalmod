using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent;

public class BEBehaviorSoilNutrition : BlockEntityBehavior
{
    protected static Random rand = new Random();
    public static OrderedDictionary<string, float> Fertilities = new()
    {
        { "verylow", 5 },
        { "low", 25 },
        { "medium", 50 },
        { "compost", 65 },
        { "high", 80 }
    };

    public float[] NpkNutrients { get; protected set; } = new float[3];
    // The fertility the soil will recover to (the soil from which the farmland was made of)
    protected int[] originalNpkNutrients = new int[3];
    /// <summary>
    /// Nutrients provided by fertilizer
    /// </summary>
    protected float[] slowReleaseNpkNutrients = new float[3];
    // The last time fertility increase was checked
    protected double totalHoursLastUpdate;

    ModSystemFarming modSys;
    BlockPos cropPos;

    public BEBehaviorSoilNutrition(BlockEntity blockentity) : base(blockentity)
    {
        cropPos = blockentity.Pos.UpCopy();
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        modSys = api.ModLoader.GetModSystem<ModSystemFarming>();
    }


    public void OnCreatedFromSoil(Block block)
    {
        string fertility = block.LastCodePart(1);
        if (block is BlockFarmland)
        {
            fertility = block.LastCodePart();
        }
        originalNpkNutrients[0] = (int)Fertilities[fertility];
        originalNpkNutrients[1] = (int)Fertilities[fertility];
        originalNpkNutrients[2] = (int)Fertilities[fertility];

        NpkNutrients[0] = originalNpkNutrients[0];
        NpkNutrients[1] = originalNpkNutrients[1];
        NpkNutrients[2] = originalNpkNutrients[2];

        totalHoursLastUpdate = Api.World.Calendar.TotalHours;

        //tryUpdateMoistureLevel(Api.World.Calendar.TotalDays, true);
    }

    public ICrop GetCrop()
    {
        return Api.World.BlockAccessor.GetBlock(cropPos).GetInterface<ICrop>(Api.World, cropPos);
    }

    protected void Update(float dt)
    {
        if (!(Api as ICoreServerAPI).World.IsFullyLoadedChunk(Pos)) return;

        double nowTotalHours = Api.World.Calendar.TotalHours;
        double hourIntervall = 3 + rand.NextDouble();

        ICrop cropBlock = GetCrop();
        bool hasCrop = cropBlock != null;
        bool skyExposed = Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= (hasCrop ? Pos.Y + 1 : Pos.Y);

        if ((nowTotalHours - totalHoursLastUpdate) < hourIntervall)
        {
            if (totalHoursLastUpdate > nowTotalHours)
            {
                // We need to rollback time when the blockEntity saved date is ahead of the calendar date: can happen if a schematic is imported
                double rollback = totalHoursLastUpdate - nowTotalHours;
                totalHoursLastUpdate = nowTotalHours;
            }
            else
            {
                return;
            }
        }

        // Slow down growth on bad light levels
        int lightpenalty = 0;
        if (!modSys.Allowundergroundfarming)
        {
            lightpenalty = Math.Max(0, Api.World.SeaLevel - Pos.Y);
        }

        EnumSoilNutrient? currentlyConsumedNutrient = cropBlock?.RequiredNutrient;

        // Let's increase fertility every 3-4 game hours
        bool growTallGrass = false;

        // Don't update more than a year
        totalHoursLastUpdate = Math.Max(totalHoursLastUpdate, nowTotalHours - Api.World.Calendar.DaysPerYear * Api.World.Calendar.HoursPerDay);

        ClimateCondition? conds = null;

        // Fast forward in 3-4 hour intervalls
        while ((nowTotalHours - totalHoursLastUpdate) > hourIntervall)
        {
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

            // Stop growth and fertility recovery below zero degrees
            // 10% growth speed at 1°C
            // 20% growth speed at 2°C and so on
            float growthChance = GameMath.Clamp(conds.Temperature / 10f, 0, 10);
            if (rand.NextDouble() > growthChance)
            {
                continue;
            }

            growTallGrass |= rand.NextDouble() < 0.006;

            updateSoilFertility(currentlyConsumedNutrient, cropBlock?.Ripe == true);
        }


        /*var upblock = Api.World.BlockAccessor.GetBlock(cropPos);
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
                        Api.World.BlockAccessor.SetBlock(weedsBlock.BlockId, cropPos);
                    }

                    break;
                }
            }
        }
        */

        updateFertilizerQuad();
        //UpdateFarmlandBlock();
        Api.World.BlockAccessor.MarkBlockEntityDirty(Pos);
    }

    protected void updateFertilizerQuad()
    {
       /* if (capi == null) return;
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
        }*/
    }

    protected void genFertilizerQuad()
    {
        /*var shape = capi.Assets.TryGet(new AssetLocation("shapes/block/farmland-fertilizer.json")).ToObject<Shape>();

        capi.Tesselator.TesselateShape(new TesselationMetaData()
        {
            TypeForLogging = "farmland fertilizer quad",
            TexSource = this,
        
        }, shape, out fertilizerQuad);*/
    }


    private void updateSoilFertility(EnumSoilNutrient? currentlyConsumedNutrient, bool hasRipeCrop)
    {
        float[] npkRegain = new float[3];
        // Rule 1: Fertility increase up to original levels by 1 every 3-4 ingame hours
        // Rule 2: Fertility does not increase with a ripe crop on it
        npkRegain[0] = hasRipeCrop ? 0 : modSys.FertilityRecoverySpeed;
        npkRegain[1] = hasRipeCrop ? 0 : modSys.FertilityRecoverySpeed;
        npkRegain[2] = hasRipeCrop ? 0 : modSys.FertilityRecoverySpeed;

        // Rule 3: Fertility increase up 3 times slower for the currently growing crop
        if (currentlyConsumedNutrient != null)
        {
            npkRegain[(int)currentlyConsumedNutrient] /= 3;
        }

        for (int i = 0; i < 3; i++)
        {
            NpkNutrients[i] += Math.Max(0, npkRegain[i] + Math.Min(0, originalNpkNutrients[i] - NpkNutrients[i] - npkRegain[i]));

            // Rule 4: Slow release fertilizer can fertilize up to 100 fertility
            if (slowReleaseNpkNutrients[i] > 0)
            {
                float release = Math.Min(0.25f, slowReleaseNpkNutrients[i]); // Don't use fertilityRecoverySpeed value as min here, doesn't look meaningful to do that here

                NpkNutrients[i] = Math.Min(100, NpkNutrients[i] + release);
                slowReleaseNpkNutrients[i] = Math.Max(0, slowReleaseNpkNutrients[i] - release);
            }
            else
            {
                // Rule 5: Once the slow release fertilizer is consumed, the soil will slowly return to its original fertility
                if (NpkNutrients[i] > originalNpkNutrients[i])
                {
                    NpkNutrients[i] = Math.Max(originalNpkNutrients[i], NpkNutrients[i] - 0.05f);
                }
            }
        }

        /*if (fertilizerOverlayStrength != null && fertilizerOverlayStrength.Count > 0)
        {
            var codes = fertilizerOverlayStrength.Keys.ToArray();
            foreach (var code in codes)
            {
                var newStr = fertilizerOverlayStrength[code] - fertilityRecoverySpeed;
                if (newStr < 0) fertilizerOverlayStrength.Remove(code);
                else fertilizerOverlayStrength[code] = newStr;
            }
        }*/
    }


    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        NpkNutrients[0] = tree.GetFloat("n");
        NpkNutrients[1] = tree.GetFloat("p");
        NpkNutrients[2] = tree.GetFloat("k");
        slowReleaseNpkNutrients[0] = tree.GetFloat("slowN");
        slowReleaseNpkNutrients[1] = tree.GetFloat("slowP");
        slowReleaseNpkNutrients[2] = tree.GetFloat("slowK");
        originalNpkNutrients[0] = tree.GetInt("originalFertilityN");
        originalNpkNutrients[1] = tree.GetInt("originalFertilityP");
        originalNpkNutrients[2] = tree.GetInt("originalFertilityK");
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("n", NpkNutrients[0]);
        tree.SetFloat("p", NpkNutrients[1]);
        tree.SetFloat("k", NpkNutrients[2]);
        tree.SetFloat("slowN", slowReleaseNpkNutrients[0]);
        tree.SetFloat("slowP", slowReleaseNpkNutrients[1]);
        tree.SetFloat("slowK", slowReleaseNpkNutrients[2]);
        tree.SetInt("originalFertilityN", originalNpkNutrients[0]);
        tree.SetInt("originalFertilityP", originalNpkNutrients[1]);
        tree.SetInt("originalFertilityK", originalNpkNutrients[2]);

    }
}
