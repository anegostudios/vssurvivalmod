using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityBerryBush : BlockEntity
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

            if (api is ICoreServerAPI && !IsRipe())
            {
                if (totalDaysForNextStage == 0)
                {
                    totalDaysForNextStage = api.World.Calendar.TotalDays + GetDaysForNextStage();
                }

                growListenerId = RegisterGameTickListener(CheckGrow, 2000);
            }
        }


        private void CheckGrow(float dt)
        {
            bool didGrow = api.World.Calendar.TotalDays > totalDaysForNextStage;

            while (api.World.Calendar.TotalDays > totalDaysForNextStage)
            {
                DoGrow();
                totalDaysForNextStage += GetDaysForNextStage();
            }

            // If ripe we can unregister the timer
            // The harvesting mechanic in BlockBehaviorHarvestable performs a SetBlock
            // so it will re-initalize our blockentity, causing a new listener to be registered,
            // so we're done here \o/
            if (didGrow && IsRipe())
            {
                api.Event.UnregisterGameTickListener(growListenerId);
                growListenerId = 0;
            }
        }

        public double GetDaysForNextStage()
        {
            return 5 + rand.NextDouble();
        }

        public bool IsRipe()
        {
            Block block = api.World.BlockAccessor.GetBlock(pos);
            return block.LastCodePart() == "ripe";
        }

        void DoGrow()
        { 
            Block block = api.World.BlockAccessor.GetBlock(pos);
            string nextCodePart = (block.LastCodePart() == "empty") ? "flowering" : "ripe";

            AssetLocation loc = block.CodeWithParts(nextCodePart);
            if (loc.Path.Length == 0) return;
            Block nextBlock = api.World.GetBlock(loc);
            if (nextBlock?.Code == null) return;

            api.World.BlockAccessor.ExchangeBlock(nextBlock.BlockId, pos);
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
    }
}
