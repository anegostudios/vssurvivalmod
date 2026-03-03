using System;
using System.Diagnostics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

#nullable disable

public class BEBehaviorFruitingBush : BlockEntityBehavior, IAnimalFoodSource, ILongInteractable, IHarvestable
{
    protected static readonly float[] NoNutrients = new float[3];

    protected NatFloat nextStageMonths = NatFloat.create(EnumDistribution.UNIFORM, 0.98f, 0.09f);

    protected BlockEntitySoilNutrition BESoil => Api.World.BlockAccessor.GetBlockEntity<BlockEntitySoilNutrition>(soilPos);
    protected float[] npkNutrients => BESoil?.Nutrients ?? NoNutrients;
    protected ICoreClientAPI capi;
    protected BlockPos soilPos;
    protected BlockBehaviorFruitingBush bhBush;
    protected double lastCheckAtTotalDays = 0;
    protected double transitionHoursLeft = -1;
    public FruitingBushState BState;


    public BEBehaviorFruitingBush(BlockEntity blockentity) : base(blockentity)
    {
        BState = new FruitingBushState();
    }
    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        soilPos = Blockentity.Pos.DownCopy();
        capi = api as ICoreClientAPI;

        bhBush = Block.GetBehavior<BlockBehaviorFruitingBush>();

        nextStageMonths = bhBush.GrowthProperties?["nextStageMonths"].AsObject<NatFloat>(nextStageMonths) ?? nextStageMonths;

        if (api is ICoreServerAPI)
        {
            if (transitionHoursLeft <= 0)
            {
                transitionHoursLeft = GetHoursForNextStage();
                lastCheckAtTotalDays = api.World.Calendar.TotalDays;
            }

            api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);

            var belowBlock = api.World.BlockAccessor.GetBlock(Pos.DownCopy());
            var be = Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy());
            if (be == null && belowBlock.Fertility > 0)
            {
                // Can't spawn right away, chunk thread crashes then
                Blockentity.RegisterDelayedCallback(spawnBe, 500 + api.World.Rand.Next(500));
            } else
            {
                if (!(be is BlockEntitySoilNutrition))
                {
                    // Cannot grow here
                    Api.World.BlockAccessor.SetBlock(0, Pos);
                }
            }
        }
    }

    private void spawnBe(float dt)
    {
        var belowBlock = Api.World.BlockAccessor.GetBlock(Pos.DownCopy());
        var be = Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy());
        if (be == null && belowBlock.Fertility > 0)
        {
            Api.World.BlockAccessor.SpawnBlockEntity("BerryBushFarmland", Pos.DownCopy());
            be = Api.World.BlockAccessor.GetBlockEntity(Pos.DownCopy());
            if (be is BlockEntitySoilNutrition besn)
            {
                besn.OnCreatedFromSoil(belowBlock);
            }
        }
    }

    public void OnGrownFromCutting(string traits)
    {
        BState.WildBushState = null;
        BState.Traits = traits.Split(",");
    }

    public FarmlandFastForwardUpdate onUpdate()
    {
        double totalDays = Api.World.Calendar.TotalDays;
        if (totalDays < BState.MatureTotalDays) return null;

        if (BState.Growthstate == EnumFruitingBushGrowthState.Young)
        {
            Blockentity.MarkDirty(true);
            BState.Growthstate = EnumFruitingBushGrowthState.Mature;
        }

        return (double hourIntervall, ClimateCondition conds, double lightGrowthSpeedFactor, bool growthPaused) =>
        {
            transitionHoursLeft -= hourIntervall;

            if (BState.Growthstate == EnumFruitingBushGrowthState.Dormant)
            {
                if (conds.Temperature > bhBush.LeaveDormantAboveTemperature)
                {
                    setGrowthState(EnumFruitingBushGrowthState.Mature);
                }
                return;
            }

            bool pause = conds.Temperature < bhBush.PauseGrowthBelowTemperature || conds.Temperature > bhBush.PauseGrowthAboveTemperature;
            if (pause) return;

            bool reset = conds.Temperature < bhBush.ResetGrowthBelowTemperature || conds.Temperature > bhBush.ResetGrowthAboveTemperature;
            if (reset)
            {
                if (BState.Growthstate == EnumFruitingBushGrowthState.Flowering || BState.Growthstate == EnumFruitingBushGrowthState.Ripening || BState.Growthstate == EnumFruitingBushGrowthState.Ripe)
                {
                    setGrowthState(EnumFruitingBushGrowthState.Mature);
                }
                return;
            }

            bool goDormant = conds.Temperature < bhBush.GoDormantBelowTemperature;
            if (goDormant)
            {
                setGrowthState(EnumFruitingBushGrowthState.Dormant);
                return;
            }

            if (transitionHoursLeft <= 0)
            {
                // Looping through 1,2,3,4, 1,2,3,4, ...
                setGrowthState((EnumFruitingBushGrowthState)(1 + GameMath.Mod((int)BState.Growthstate, 4)));

                if (BState.Growthstate == EnumFruitingBushGrowthState.Ripe)
                {
                    var hs = GetHealthState();
                    float amount = 0;
                    switch (hs)
                    {
                        case EnumFruitingBushHealthState.Bountiful: amount = 0.1f; break;
                        case EnumFruitingBushHealthState.Healthy: amount = 0.07f; break;
                        case EnumFruitingBushHealthState.Struggling: amount = 0.04f; break;
                        case EnumFruitingBushHealthState.Barren: amount = 0.01f; break;
                    }
                    BESoil.ConsumeNutrients(EnumSoilNutrient.N, amount);
                    BESoil.ConsumeNutrients(EnumSoilNutrient.P, amount);
                    BESoil.ConsumeNutrients(EnumSoilNutrient.K, amount);
                }

                transitionHoursLeft = GetHoursForNextStage();
            }
        };
    }


    protected void setGrowthState(EnumFruitingBushGrowthState state)
    {
        BState.Growthstate = state;
        Blockentity.MarkDirty(true);
    }

    public virtual double GetHoursForNextStage()
    {
        if (BState.Growthstate == EnumFruitingBushGrowthState.Ripe) return 4 * nextStageMonths.nextFloat() * Api.World.Calendar.DaysPerMonth * Api.World.Calendar.HoursPerDay;
        return nextStageMonths.nextFloat() * Api.World.Calendar.DaysPerMonth * Api.World.Calendar.HoursPerDay / bhBush.GrowthRateMul;
    }

    public override void OnBlockPlaced(ItemStack byItemStack = null)
    {
        BState.PlantedTotalDays = Api.World.Calendar.TotalDays;
        BState.MatureTotalDays = Api.World.Calendar.DaysPerMonth * (3 + 3 * Api.World.Rand.NextDouble());

        if (byItemStack == null)
        {
            BState.WildBushState = (EnumFruitingBushHealthState)(GameMath.MurmurHash3Mod(Pos.X, Pos.Y + 1, Pos.Z, 4));
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        var healthState = GetHealthState();

        dsc.AppendLine(Lang.Get("Health state: {0}", Lang.Get("healthstate-" + healthState.ToString().ToLowerInvariant())));
        dsc.AppendLine(Lang.Get("Growth state: {0}", Lang.Get("growthstate-" + BState.Growthstate.ToString().ToLowerInvariant())));

        var bens = Api.World.BlockAccessor.GetBlockEntity<BlockEntitySoilNutrition>(Pos.DownCopy());
        if (bens != null)
        {
            bens.GetBlockInfo(forPlayer, dsc);
        }
    }


    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        BState.FromTreeAttributes(tree, worldAccessForResolve);

        if (BState.Traits == null)
        {
            genTraits(worldAccessForResolve);
        }
    }

    private void genTraits(IWorldAccessor world)
    {
        BState.Traits = new string[0];
        for (int i = 0; i < FruitingBushState.AllTraits.Length; i++)
        {
            var t = FruitingBushState.AllTraits[i];
            if (world.Rand.NextDouble() < 0.15)
            {
                BState.Traits = BState.Traits.Append(t[world.Rand.Next(2)]);
            }
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        BState.ToTreeAttributes(tree);
    }

    public EnumFruitingBushHealthState GetHealthState()
    {
        if (BState.WildBushState != null) return (EnumFruitingBushHealthState)BState.WildBushState;
        float avg = (npkNutrients[0] + npkNutrients[1] + npkNutrients[2]) / 3f;
        if (avg < 0.1) return EnumFruitingBushHealthState.Barren;
        if (avg < 0.3) return EnumFruitingBushHealthState.Struggling;
        if (avg < 0.8) return EnumFruitingBushHealthState.Healthy;
        return EnumFruitingBushHealthState.Bountiful;
    }

    #region Interact

    public bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        var besn = world.BlockAccessor.GetBlockEntity<BlockEntitySoilNutrition>(Pos.DownCopy());
        if (besn?.OnBlockInteract(byPlayer) == true)
        {
            handling = EnumHandling.PreventDefault;
            Blockentity.MarkDirty(true);
            return true;
        }

        if (BState.Growthstate == EnumFruitingBushGrowthState.Ripe)
        {
            handling = EnumHandling.PreventDefault;
            return true;
        }

        return false;
    }


    public bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (BState.Growthstate == EnumFruitingBushGrowthState.Ripe)
        {
            handling = EnumHandling.PreventDefault;
            playHarvestEffects(byPlayer, blockSel, bhBush.harvestedStacks[0].ResolvedItemstack);
            return world.Side == EnumAppSide.Client ? secondsUsed < bhBush.harvestTime : true;
        }

        return false;
    }

    protected void playHarvestEffects(IPlayer byPlayer, BlockSelection blockSel, ItemStack particlestack)
    {
        IWorldAccessor world = Api.World;
        if (world.Rand.NextDouble() < 0.05)
        {
            world.PlaySoundAt(bhBush.HarvestingSound, blockSel.Position, 0, byPlayer);
        }

        if (world.Side == EnumAppSide.Client)
        {
            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
            if (world.Rand.NextDouble() < 0.25)
            {
                if (particlestack != null)
                {
                    world.SpawnCubeParticles(blockSel.Position.ToVec3d().Add(blockSel.HitPosition), particlestack, 0.25f, 1, 0.5f, byPlayer, new Vec3f(0, 1, 0));
                } else
                {
                    world.SpawnCubeParticles(Pos, Pos.ToVec3d(), 0.25f, 1, 0.5f, byPlayer, new Vec3f(0, 1, 0));
                }
            }
        }
    }

    float[] dropRates = [0f, 0.5f, 1f, 1.5f];

    public void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        if (BState.Growthstate == EnumFruitingBushGrowthState.Ripe)
        {
            handling = EnumHandling.PreventDefault;
            if (secondsUsed > bhBush.harvestTime - 0.05f && bhBush.harvestedStacks != null && world.Side == EnumAppSide.Server)
            {
                float dropRate = 1;

                if (Block.Attributes?.IsTrue("forageStatAffected") == true)
                {
                    dropRate *= byPlayer.Entity.Stats.GetBlended("forageDropRate");
                }

                bhBush.harvestedStacks.Foreach(harvestedStack =>
                {
                    ItemStack stack = harvestedStack.GetNextItemStack(dropRate);
                    if (stack == null) return;
                    var origStack = stack.Clone();

                    stack.StackSize = GameMath.RoundRandom(Api.World.Rand, stack.StackSize * dropRates[(int)GetHealthState()]);

                    var quantity = stack.StackSize;
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        world.SpawnItemEntity(stack, blockSel.Position);
                    }
                    world.Logger.Audit("{0} Took {1}x{2} from {3} at {4}.",
                        byPlayer.PlayerName,
                        quantity,
                        stack.Collectible.Code,
                        Block.Code,
                        blockSel.Position
                    );

                    TreeAttribute tree = new TreeAttribute();
                    tree["itemstack"] = new ItemstackAttribute(origStack.Clone());
                    tree["byentityid"] = new LongAttribute(byPlayer.Entity.EntityId);
                    world.Api.Event.PushEvent("onitemcollected", tree);
                });

                if (bhBush.Tool != null)
                {
                    var toolSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    toolSlot.Itemstack?.Collectible.DamageItem(world, byPlayer.Entity, toolSlot);
                }

                world.PlaySoundAt(bhBush.HarvestingSound, blockSel.Position, 0, byPlayer);

                BState.Growthstate = EnumFruitingBushGrowthState.Mature;
                Blockentity.MarkDirty(true);
            }
        }
    }


    public bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling)
    {
        return false;
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        if (BESoil != null)
        {
            var tfMatrix = new Matrixf().Translate(0, -15 / 16f, 0).Values;
            mesher.AddMeshData(BESoil.FertilizerQuad, tfMatrix);
        }

        return base.OnTesselation(mesher, tessThreadTesselator);
    }


    #endregion

    #region IAnimalFoodSource impl
    public bool IsSuitableFor(Entity entity, CreatureDiet diet)
    {
        if (diet == null || BState.Growthstate != EnumFruitingBushGrowthState.Ripe) return false;
        return diet.Matches(EnumFoodCategory.NoNutrition, bhBush.CreatureDietFoodTags);
    }

    public float ConsumeOnePortion(Entity entity)
    {
        var bbh = Block.GetBehavior<BlockBehaviorHarvestable>();
        bbh?.harvestedStacks?.Foreach(harvestedStack => { Api.World.SpawnItemEntity(harvestedStack?.GetNextItemStack(), Pos); });
        Api.World.PlaySoundAt(bbh?.harvestingSound, Pos, 0);

        BState.Growthstate = EnumFruitingBushGrowthState.Mature;
        Blockentity.MarkDirty(true);
        return 0.1f;
    }

    public Vec3d Position => base.Pos.ToVec3d().Add(0.5, 0.5, 0.5);
    public string Type => "food";


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

    #endregion

    #region Knife cutting
    public bool IsHarvestable(ItemSlot slot, Entity forEntity)
    {
        return slot.Itemstack?.Collectible.GetTool(slot) == EnumTool.Knife && (Api.World.Calendar.TotalDays - BState.LastCuttingTakenTotalDays) / Api.World.Calendar.DaysPerYear >= 2;
    }

    public AssetLocation HarvestableSound => bhBush.HarvestingSound;

    public float GetHarvestDuration(ItemSlot slot, Entity forEntity)
    {
        // Happens to also get called during InteractStep
        var eplr = forEntity as EntityPlayer;
        playHarvestEffects(eplr?.Player, eplr.BlockSelection, null);

        return bhBush.cuttingTime;
    }

    public void SetHarvested(IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        var block = Api.World.GetBlock(AssetLocation.Create(Block.Attributes["cuttingBlockCode"].AsString(), Block.Code.Domain));
        var cuttingStack = new ItemStack(block);

        cuttingStack.Attributes.SetString("traits", string.Join(",", BState.Traits));

        if (!byPlayer.InventoryManager.TryGiveItemstack(cuttingStack))
        {
            Api.World.SpawnItemEntity(cuttingStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }
        BState.LastCuttingTakenTotalDays = Api.World.Calendar.TotalDays;
        Blockentity.MarkDirty(true);
    }

    


    #endregion

}
