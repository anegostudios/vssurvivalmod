using System;
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

public class BEBehaviorFruitingBush : BlockEntityBehavior, IAnimalFoodSource, ILongInteractable, IHarvestable
{
    protected const float intervalHours = 2f;
    protected static readonly float[] NoNutrients = new float[3];

    protected NatFloat nextStageMonths = NatFloat.create(EnumDistribution.UNIFORM, 0.98f, 0.09f);
    protected float[] npkNutrients => Api.World.BlockAccessor.GetBlockEntity(soilPos)?.GetBehavior<BEBehaviorSoilNutrition>()?.NpkNutrients ?? NoNutrients;
    protected ICoreClientAPI capi;
    protected RoomRegistry roomreg;
    protected BlockPos soilPos;
    protected BlockBehaviorFruitingBush bhBush;
    protected double lastCheckAtTotalDays = 0;
    protected double transitionHoursLeft = -1;

    public int roomness;
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
        roomreg = Api.ModLoader.GetModSystem<RoomRegistry>();

        bhBush = Block.GetBehavior<BlockBehaviorFruitingBush>();

        nextStageMonths = bhBush.GrowthProperties?["nextStageMonths"].AsObject<NatFloat>(nextStageMonths) ?? nextStageMonths;

        if (api is ICoreServerAPI)
        {
            if (transitionHoursLeft <= 0)
            {
                transitionHoursLeft = GetHoursForNextStage();
                lastCheckAtTotalDays = api.World.Calendar.TotalDays;
            }

            if (Api.World.Config.GetBool("processCrops", true))
            {
                //Blockentity.RegisterGameTickListener(growthCheck, 8000, api.World.Rand.Next(3000));
            }

            api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
        }
    }

    private void growthCheck(float dt)
    {
        double totalDays = Api.World.Calendar.TotalDays;
        if (totalDays < BState.MatureTotalDays) return;

        if (BState.Growthstate == EnumFruitingBushGrowthState.Young)
        {
            Blockentity.MarkDirty(true);
            BState.Growthstate = EnumFruitingBushGrowthState.Mature;
        }

        if (!(Api as ICoreServerAPI).World.IsFullyLoadedChunk(Pos)) return;

        if (Block.Attributes == null)
        {
#if DEBUG
            Api.World.Logger.Notification("Ghost berry bush block entity at {0}. Block.Attributes is null, will remove game tick listener", Pos);
#endif
            Blockentity.UnregisterAllTickListeners();
            return;
        }

        // In case this block was imported from another older world. In that case lastCheckAtTotalDays and LastPrunedTotalDays would be a future date.
        lastCheckAtTotalDays = Math.Min(lastCheckAtTotalDays, Api.World.Calendar.TotalDays);

        // We don't need to check more than one year because it just begins to loop then
        double daysToCheck = GameMath.Mod(Api.World.Calendar.TotalDays - lastCheckAtTotalDays, Api.World.Calendar.DaysPerYear);

        float intervalDays = intervalHours / Api.World.Calendar.HoursPerDay;
        if (daysToCheck <= intervalDays) return;

        roomness = getRoomness();

        ClimateCondition? conds = null;
        float baseTemperature = 0;
        while (daysToCheck > intervalDays)
        {
            daysToCheck -= intervalDays;
            lastCheckAtTotalDays += intervalDays;
            transitionHoursLeft -= intervalHours;

            if (conds == null)
            {
                conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, lastCheckAtTotalDays);
                if (conds == null) return;
                baseTemperature = conds.WorldGenTemperature;
            }
            else
            {
                conds.Temperature = baseTemperature;  // Keep resetting the field we are interested in, because it can be modified by the OnGetClimate event
                Api.World.BlockAccessor.GetClimateAt(Pos, conds, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, lastCheckAtTotalDays);
            }

            float temperature = conds.Temperature;
            if (roomness > 0)
            {
                temperature += 5;
            }

            if (BState.Growthstate == EnumFruitingBushGrowthState.Dormant)
            {
                if (temperature > bhBush.LeaveDormantAboveTemperature)
                {
                    setGrowthState(EnumFruitingBushGrowthState.Mature);
                }
                continue;
            }


            bool pause = temperature < bhBush.PauseGrowthBelowTemperature || temperature > bhBush.PauseGrowthAboveTemperature;
            if (pause) continue;

            bool reset = temperature < bhBush.ResetGrowthBelowTemperature || temperature > bhBush.ResetGrowthAboveTemperature;
            if (reset)
            {
                if (BState.Growthstate == EnumFruitingBushGrowthState.Flowering || BState.Growthstate == EnumFruitingBushGrowthState.Ripening || BState.Growthstate == EnumFruitingBushGrowthState.Ripe)
                {
                    setGrowthState(EnumFruitingBushGrowthState.Mature);
                }
                continue;
            }

            bool goDormant = temperature < bhBush.GoDormantBelowTemperature;
            if (goDormant)
            {
                setGrowthState(EnumFruitingBushGrowthState.Dormant);
                continue;
            }

            if (transitionHoursLeft <= 0)
            {
                // Looping through 1,2,3,4, 1,2,3,4, ...
                setGrowthState((EnumFruitingBushGrowthState)(1 + GameMath.Mod((int)BState.Growthstate, 4)));
                transitionHoursLeft = GetHoursForNextStage();
            }
        }

        Blockentity.MarkDirty(false);
    }

    private int getRoomness()
    {
        if (Api.World.BlockAccessor.GetRainMapHeightAt(Pos) > Pos.Y) // Fast pre-check
        {
            Room? room = roomreg?.GetRoomForPosition(Pos);
            return (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;
        }

        return 0;
    }

    private void setGrowthState(EnumFruitingBushGrowthState state)
    {
        BState.Growthstate = state;
        Blockentity.MarkDirty(true);
    }

    public virtual double GetHoursForNextStage()
    {
        if (BState.Growthstate == EnumFruitingBushGrowthState.Ripe) return 4 * nextStageMonths.nextFloat() * Api.World.Calendar.DaysPerMonth * Api.World.Calendar.HoursPerDay;
        return nextStageMonths.nextFloat() * Api.World.Calendar.DaysPerMonth * Api.World.Calendar.HoursPerDay / bhBush.GrowthRateMul;
    }

    public override void OnBlockPlaced(ItemStack? byItemStack = null)
    {
        BState.PlantedTotalDays = Api.World.Calendar.TotalDays;
        BState.MatureTotalDays = Api.World.Calendar.DaysPerMonth * (6 + 6 * Api.World.Rand.NextDouble());

        // DEBUG
        BState.Growthstate = (EnumFruitingBushGrowthState)(1 + GameMath.MurmurHash3Mod(Pos.X, Pos.Y + 1, Pos.Z, 5));
        BState.WildBushState = (EnumFruitingBushHealthState)(GameMath.MurmurHash3Mod(Pos.X, Pos.Y + 1, Pos.Z, 4));
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        var healthState = GetHealthState();

        dsc.AppendLine(Lang.Get("Health state: {0}", Lang.Get("healthstate-" + healthState.ToString().ToLowerInvariant())));
        dsc.AppendLine(Lang.Get("Growht state: {0}", Lang.Get("growthstate-" + BState.Growthstate.ToString().ToLowerInvariant())));
        if (getRoomness() > 0)
        {
            dsc.AppendLine(Lang.Get("greenhousetempbonus"));
        }
    }


    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);
        BState.FromTreeAttributes(tree, worldAccessForResolve);
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

    private void playHarvestEffects(IPlayer byPlayer, BlockSelection blockSel, ItemStack? particlestack)
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
                    ItemStack? stack = harvestedStack.GetNextItemStack(dropRate);
                    if (stack == null) return;
                    var origStack = stack.Clone();
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
        return slot.Itemstack?.Collectible.Tool == EnumTool.Knife && (Api.World.Calendar.TotalDays - BState.LastCuttingTakenTotalDays) / Api.World.Calendar.DaysPerYear >= 2;
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
        if (!byPlayer.InventoryManager.TryGiveItemstack(cuttingStack))
        {
            Api.World.SpawnItemEntity(cuttingStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }
        BState.LastCuttingTakenTotalDays = Api.World.Calendar.TotalDays;
        Blockentity.MarkDirty(true);
    }

    #endregion

}
