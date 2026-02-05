using System;
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

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityBerryBush : BlockEntity, IAnimalFoodSource
    {
        static Random rand = new Random();
        const float intervalHours = 2f;

        double lastCheckAtTotalDays = 0;
        double transitionHoursLeft = -1;
        double? totalDaysForNextStageOld = null; // old v1.13 data format, here for backwards compatibility

        RoomRegistry roomreg;
        public int roomness;

        public bool Pruned;
        public double LastPrunedTotalDays;

        float resetBelowTemperature = 0;
        float resetAboveTemperature = 0;
        float stopBelowTemperature = 0;
        float stopAboveTemperature = 0;
        float revertBlockBelowTemperature = 0;
        float revertBlockAboveTemperature = 0;

        float growthRateMul = 1f;
        NatFloat nextStageMonths = NatFloat.create(EnumDistribution.UNIFORM, 0.98f, 0.09f);

        public string[] creatureDietFoodTags;

        public bool IsEmpty => Block.Variant["state"] == "empty";
        public bool IsFlowering => Block.Variant["state"] == "flowering";
        public bool IsRipe => Block.Variant["state"] == "ripe";

        public BlockEntityBerryBush() : base()
        {

        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            growthRateMul = (float)Api.World.Config.GetDecimal("cropGrowthRateMul", growthRateMul);

            if (api is ICoreServerAPI)
            {
                creatureDietFoodTags = Block.Attributes["foodTags"].AsArray<string>();

                if (transitionHoursLeft <= 0)
                {
                    transitionHoursLeft = GetHoursForNextStage();
                    lastCheckAtTotalDays = api.World.Calendar.TotalDays;
                }

                if (Api.World.Config.GetBool("processCrops", true))
                {
                    RegisterGameTickListener(CheckGrow, 8000);
                }

                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                roomreg = Api.ModLoader.GetModSystem<RoomRegistry>();

                if (totalDaysForNextStageOld != null)
                {
                    transitionHoursLeft = ((double)totalDaysForNextStageOld - Api.World.Calendar.TotalDays) * Api.World.Calendar.HoursPerDay;
                }

                
            }
        }

        public void Prune()
        {
            Pruned = true;
            LastPrunedTotalDays = Api.World.Calendar.TotalDays;
            MarkDirty(true);
        }

        protected virtual void CheckGrow(float dt)
        {
            if (!(Api as ICoreServerAPI).World.IsFullyLoadedChunk(Pos)) return;

            if (Block.Attributes == null)
            {
#if DEBUG
                Api.World.Logger.Notification("Ghost berry bush block entity at {0}. Block.Attributes is null, will remove game tick listener", Pos);
#endif
                UnregisterAllTickListeners();
                return;
            }

            // In case this block was imported from another older world. In that case lastCheckAtTotalDays and LastPrunedTotalDays would be a future date.
            lastCheckAtTotalDays = Math.Min(lastCheckAtTotalDays, Api.World.Calendar.TotalDays);
            LastPrunedTotalDays = Math.Min(LastPrunedTotalDays, Api.World.Calendar.TotalDays);


            // We don't need to check more than one year because it just begins to loop then
            double daysToCheck = GameMath.Mod(Api.World.Calendar.TotalDays - lastCheckAtTotalDays, Api.World.Calendar.DaysPerYear);

            float intervalDays = intervalHours / Api.World.Calendar.HoursPerDay;
            if (daysToCheck <= intervalDays) return;

            if (Api.World.BlockAccessor.GetRainMapHeightAt(Pos) > Pos.Y) // Fast pre-check
            {
                Room room = roomreg?.GetRoomForPosition(Pos);
                roomness = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;
            }
            else
            {
                roomness = 0;
            }

            ClimateCondition conds = null;
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

                bool reset =
                    temperature < resetBelowTemperature ||
                    temperature > resetAboveTemperature;

                bool stop =
                    temperature < stopBelowTemperature ||
                    temperature > stopAboveTemperature;
                
                if (stop || reset)
                {
                    if (!IsRipe) transitionHoursLeft += intervalHours;
                    
                    if (reset)
                    {
                        bool revert =
                            temperature < revertBlockBelowTemperature ||
                            temperature > revertBlockAboveTemperature;

                        if (!IsRipe) transitionHoursLeft = GetHoursForNextStage();
                        if (revert && !IsEmpty)
                        {
                            Block nextBlock = Api.World.GetBlock(Block.CodeWithVariant("state", "empty"));
                            Api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, Pos);
                        }
                        

                    }

                    continue;
                }

                if (Pruned && Api.World.Calendar.TotalDays - LastPrunedTotalDays > Api.World.Calendar.DaysPerYear)
                {
                    Pruned = false;
                }

                if (transitionHoursLeft <= 0)
                {
                    if (!DoGrow()) return;
                }
            }

            MarkDirty(false);
        }

        public override void OnExchanged(Block block)
        {
            base.OnExchanged(block);
            UpdateTransitionsFromBlock();
            transitionHoursLeft = GetHoursForNextStage();
        }

        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);
            UpdateTransitionsFromBlock();
        }

        protected virtual void UpdateTransitionsFromBlock()
        {
            // In case we have a Block which is not a BerryBush block (why does this happen?)
            if (Block?.Attributes == null)
            {
                resetBelowTemperature = stopBelowTemperature = revertBlockBelowTemperature = -999;
                resetAboveTemperature = stopAboveTemperature = revertBlockAboveTemperature = 999;
                nextStageMonths = NatFloat.create(EnumDistribution.UNIFORM, 0.98f, 0.09f);
                return;
            }
            // These Attributes lookups are costly because Newtonsoft JSON lib ~~sucks~~ uses a weird approximation to a Dictionary in JToken.TryGetValue() but it can ignore case
            resetBelowTemperature = Block.Attributes["resetBelowTemperature"].AsFloat(-999);
            resetAboveTemperature = Block.Attributes["resetAboveTemperature"].AsFloat(999);
            stopBelowTemperature = Block.Attributes["stopBelowTemperature"].AsFloat(-999);
            stopAboveTemperature = Block.Attributes["stopAboveTemperature"].AsFloat(999);
            revertBlockBelowTemperature = Block.Attributes["revertBlockBelowTemperature"].AsFloat(-999);
            revertBlockAboveTemperature = Block.Attributes["revertBlockAboveTemperature"].AsFloat(999);
            nextStageMonths = Block.Attributes["nextStageMonths"].AsObject<NatFloat>(nextStageMonths);
        }

        public virtual double GetHoursForNextStage()
        {
            if (IsRipe) return 4 * nextStageMonths.nextFloat() * Api.World.Calendar.DaysPerMonth * Api.World.Calendar.HoursPerDay;

            return nextStageMonths.nextFloat() * Api.World.Calendar.DaysPerMonth * Api.World.Calendar.HoursPerDay / growthRateMul;
        }

        protected virtual bool DoGrow()
        {
            AssetLocation loc = Block.CodeWithVariant("state", IsEmpty ? "flowering" : (IsFlowering ? "ripe" : "empty"));
            if (!loc.Valid)
            {
                Api.World.BlockAccessor.RemoveBlockEntity(Pos);
                return false;
            }

            Block nextBlock = Api.World.GetBlock(loc);
            if (nextBlock?.Code == null) return false;

            Api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, Pos);

            MarkDirty(true);
            return true;
        }


        

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            transitionHoursLeft = tree.GetDouble("transitionHoursLeft");

            if (tree.HasAttribute("totalDaysForNextStage")) // Pre 1.13 format
            {
                totalDaysForNextStageOld = tree.GetDouble("totalDaysForNextStage");
            }

            lastCheckAtTotalDays = tree.GetDouble("lastCheckAtTotalDays");

            roomness = tree.GetInt("roomness");
            Pruned = tree.GetBool("pruned");
            LastPrunedTotalDays = tree.GetDecimal("lastPrunedTotalDays");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("transitionHoursLeft", transitionHoursLeft);
            tree.SetDouble("lastCheckAtTotalDays", lastCheckAtTotalDays);
            tree.SetBool("pruned", Pruned);
            tree.SetInt("roomness", roomness);
            tree.SetDouble("lastPrunedTotalDays", LastPrunedTotalDays);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            double daysleft = transitionHoursLeft / Api.World.Calendar.HoursPerDay;

            if (IsRipe) return;

            string code = IsEmpty ? "flowering" : "ripen";

            if (daysleft < 1)
            {
                sb.AppendLine(Lang.Get("berrybush-"+ code + "-1day"));
            }
            else
            {
                sb.AppendLine(Lang.Get("berrybush-" + code + "-xdays", (int)daysleft));
            }

            if (roomness > 0)
            {
                sb.AppendLine(Lang.Get("greenhousetempbonus"));
            }
        }



        #region IAnimalFoodSource impl
        public bool IsSuitableFor(Entity entity, CreatureDiet diet)
        {
            if (diet == null || !IsRipe) return false;
            return diet.Matches(EnumFoodCategory.NoNutrition, this.creatureDietFoodTags);
        }

        public float ConsumeOnePortion(Entity entity)
        {
            AssetLocation loc = Block.CodeWithVariant("state", "empty");
            if (!loc.Valid)
            {
                Api.World.BlockAccessor.RemoveBlockEntity(Pos);
                return 0f;
            }

            Block nextBlock = Api.World.GetBlock(loc);
            if (nextBlock?.Code == null) return 0f;

            var bbh = Block.GetBehavior<BlockBehaviorHarvestable>();
            bbh?.harvestedStacks?.Foreach(harvestedStack => { Api.World.SpawnItemEntity(harvestedStack?.GetNextItemStack(), Pos); });
            Api.World.PlaySoundAt(bbh?.harvestingSound, Pos, 0);


            Api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, Pos);
            MarkDirty(true);

            return 0.1f;
        }

        public Vec3d Position => base.Pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "food";
        #endregion


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

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (Pruned)
            {
                mesher.AddMeshData((Block as BlockBerryBush).GetPrunedMesh(Pos));
                return true;
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }
    }
}
