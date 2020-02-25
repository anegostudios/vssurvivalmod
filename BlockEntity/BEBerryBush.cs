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

        // Total game hours from where on it can enter the next growth stage
        double totalDaysForNextStage;

        long growListenerId;


        public BlockEntityBerryBush() : base()
        {

        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreServerAPI)
            {
                if (totalDaysForNextStage == 0)
                {
                    totalDaysForNextStage = api.World.Calendar.TotalDays + GetDaysForNextStage();
                }

                growListenerId = RegisterGameTickListener(CheckGrow, 8000);

                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
            }
        }


        private void CheckGrow(float dt)
        {
            while (Api.World.Calendar.TotalDays > totalDaysForNextStage)
            {
                DoGrow();
                totalDaysForNextStage += GetDaysForNextStage();
            }
        }

        public double GetDaysForNextStage()
        {
            if (IsRipe()) return 4 * (5 + rand.NextDouble()) * 0.8;

            return (5 + rand.NextDouble()) * 0.8;
        }

        public bool IsRipe()
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            return block.LastCodePart() == "ripe";
        }

        void DoGrow()
        { 
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            string nowCodePart = block.LastCodePart();
            string nextCodePart = (nowCodePart == "empty") ? "flowering" : ((nowCodePart == "flowering") ? "ripe" : "empty");


            AssetLocation loc = block.CodeWithParts(nextCodePart);
            if (!loc.Valid)
            {
                Api.World.BlockAccessor.RemoveBlockEntity(Pos);
                return;
            }

            Block nextBlock = Api.World.GetBlock(loc);
            if (nextBlock?.Code == null) return;

            Api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, Pos);
            MarkDirty(true);
        }



        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);
            totalDaysForNextStage = tree.GetDouble("totalDaysForNextStage");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("totalDaysForNextStage", totalDaysForNextStage);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            double daysleft = totalDaysForNextStage - Api.World.Calendar.TotalDays;

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
    }
}
