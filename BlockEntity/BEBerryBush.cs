using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityBerryBush : BlockEntity, IAnimalFoodSource
    {
        static Random rand = new Random();

        double lastCheckAtTotalDays = 0;
        double transitionHoursLeft = -1;
        double? totalDaysForNextStageOld = null; // old v1.13 data format, here for backwards compatibility

        RoomRegistry roomreg;
        public int roomness;

        public BlockEntityBerryBush() : base()
        {

        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreServerAPI)
            {
                if (transitionHoursLeft <= 0)
                {
                    transitionHoursLeft = GetHoursForNextStage();
                    lastCheckAtTotalDays = api.World.Calendar.TotalDays;
                }

                RegisterGameTickListener(CheckGrow, 8000);

                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                roomreg = Api.ModLoader.GetModSystem<RoomRegistry>();

                if (totalDaysForNextStageOld != null)
                {
                    transitionHoursLeft = ((double)totalDaysForNextStageOld - Api.World.Calendar.TotalDays) * Api.World.Calendar.HoursPerDay;
                }
            }
        }


        private void CheckGrow(float dt)
        {
            if (Block.Attributes == null)
                {
#if DEBUG
                Api.World.Logger.Notification("Ghost berry bush block entity at {0}. Block.Attributes is null, will remove game tick listener", Pos);
                foreach (long handlerId in TickHandlers)
                {
                    Api.Event.UnregisterGameTickListener(handlerId);
                }
#endif
                return;
            }

            // In case this block was imported from another older world. In that case lastCheckAtTotalDays would be a future date.
            lastCheckAtTotalDays = Math.Min(lastCheckAtTotalDays, Api.World.Calendar.TotalDays);


            // We don't need to check more than one year because it just begins to loop then
            double daysToCheck = GameMath.Mod(Api.World.Calendar.TotalDays - lastCheckAtTotalDays, Api.World.Calendar.DaysPerYear);

            bool changed = false;

            while (daysToCheck > 1f / Api.World.Calendar.HoursPerDay)
            {
                if (!changed)
                {
                    if (Api.World.BlockAccessor.GetRainMapHeightAt(Pos) > Pos.Y) // Fast pre-check
                    {
                        Room room = roomreg?.GetRoomForPosition(Pos);
                        roomness = (room != null && room.SkylightCount > room.NonSkylightCount && room.ExitCount == 0) ? 1 : 0;
                    }
                    else
                    {
                        roomness = 0;
                    }
                }

                changed = true;

                daysToCheck -= 1f / Api.World.Calendar.HoursPerDay;

                lastCheckAtTotalDays += 1f / Api.World.Calendar.HoursPerDay;
                transitionHoursLeft -= 1f;

                ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.ForSuppliedDateValues, lastCheckAtTotalDays);
                if (conds == null) return;
                if (roomness > 0)
                {
                    conds.Temperature += 5;
                }

                bool reset = conds.Temperature < Block.Attributes["resetBelowTemperature"].AsFloat(-999);
                bool stop = conds.Temperature < Block.Attributes["stopBelowTemperature"].AsFloat(-999);
                bool revert = conds.Temperature < Block.Attributes["revertBlockBelowTemperature"].AsFloat(-999);

                if (stop || reset)
                {
                    transitionHoursLeft += 1f;
                    
                    if (reset)
                    {
                        transitionHoursLeft = GetHoursForNextStage();
                        if (Block.Variant["state"] != "empty" && revert)
                        {
                            Block nextBlock = Api.World.GetBlock(Block.CodeWithVariant("state", "empty"));
                            Api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, Pos);
                        }
                        

                    }

                    continue;
                }

                if (transitionHoursLeft <= 0)
                {
                    if (!DoGrow()) return;
                    transitionHoursLeft = GetHoursForNextStage();
                }
            }

            if (changed) MarkDirty(false);
        }

        public double GetHoursForNextStage()
        {
            if (IsRipe()) return (4 * (5 + rand.NextDouble()) * 0.8) * Api.World.Calendar.HoursPerDay;

            return ((5 + rand.NextDouble()) * 0.8) * Api.World.Calendar.HoursPerDay;
        }

        public bool IsRipe()
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            return block.LastCodePart() == "ripe";
        }

        bool DoGrow()
        { 
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            string nowCodePart = block.LastCodePart();
            string nextCodePart = (nowCodePart == "empty") ? "flowering" : ((nowCodePart == "flowering") ? "ripe" : "empty");


            AssetLocation loc = block.CodeWithParts(nextCodePart);
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


        

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            transitionHoursLeft = tree.GetDouble("transitionHoursLeft");

            if (tree.HasAttribute("totalDaysForNextStage")) // Pre 1.13 format
            {
                totalDaysForNextStageOld = tree.GetDouble("totalDaysForNextStage");
            }

            lastCheckAtTotalDays = tree.GetDouble("lastCheckAtTotalDays");

            roomness = tree.GetInt("roomness");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("transitionHoursLeft", transitionHoursLeft);
            tree.SetDouble("lastCheckAtTotalDays", lastCheckAtTotalDays);

            tree.SetInt("roomness", roomness);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            double daysleft = transitionHoursLeft / Api.World.Calendar.HoursPerDay;

            /*if (forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                return "" + daysleft;
            }*/

            if (block.LastCodePart() == "ripe")
            {
                return;
            }

            string code = (block.LastCodePart() == "empty") ? "flowering" : "ripen";

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
                sb.AppendLine(Lang.Get("+5°C from greenhouse"));
            }
        }



        #region IAnimalFoodSource impl
        public bool IsSuitableFor(Entity entity)
        {
            if (!IsRipe()) return false;

            string[] diet = entity.Properties.Attributes?["blockDiet"]?.AsArray<string>();
            if (diet == null) return false;

            return diet.Contains("Berry");
        }

        public float ConsumeOnePortion()
        {
            AssetLocation loc = Block.CodeWithParts("empty");
            if (!loc.Valid)
            {
                Api.World.BlockAccessor.RemoveBlockEntity(Pos);
                return 0f;
            }

            Block nextBlock = Api.World.GetBlock(loc);
            if (nextBlock?.Code == null) return 0f;

            var bbh = Block.GetBehavior<BlockBehaviorHarvestable>();
            if (bbh?.harvestedStack != null)
            {
                ItemStack dropStack = bbh.harvestedStack.GetNextItemStack();
                Api.World.PlaySoundAt(bbh.harvestingSound, Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
                Api.World.SpawnItemEntity(dropStack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }


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

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }
    }
}
