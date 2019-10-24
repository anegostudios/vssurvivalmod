using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class BlockEntitySapling : BlockEntity
    {
        static Random rand = new Random();
        
        double totalHoursTillGrowth;
        long growListenerId;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreServerAPI)
            {   
                growListenerId = RegisterGameTickListener(CheckGrow, 2000);
            }

            
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            Block block = Api.World.BlockAccessor.GetBlock(Pos);

            double minDays = 5;
            double maxDays = 8;

            if (block?.Attributes != null)
            {
                minDays = block.Attributes["minGrowthDays"].AsDouble(5);
                maxDays = block.Attributes["maxGrowthDays"].AsDouble(8);
            }

            totalHoursTillGrowth = Api.World.Calendar.TotalHours + (minDays + (maxDays - minDays) * rand.NextDouble()) * 24;
        }

        
        private void CheckGrow(float dt)
        {
            if (Api.World.Calendar.TotalHours < totalHoursTillGrowth) return;

            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            string treeGenCode = block.Attributes?["treeGen"].AsString(null);

            if (treeGenCode == null)
            {
                Api.Event.UnregisterGameTickListener(growListenerId);
                return;
            }

            AssetLocation code = new AssetLocation(treeGenCode);
            ICoreServerAPI sapi = Api as ICoreServerAPI;

            ITreeGenerator gen = null;
            if (!sapi.World.TreeGenerators.TryGetValue(code, out gen))
            {
                Api.Event.UnregisterGameTickListener(growListenerId);
                return;
            }

            Api.World.BlockAccessor.SetBlock(0, Pos);
            Api.World.BulkBlockAccessor.ReadFromStagedByDefault = true;
            float size = 0.6f + (float)Api.World.Rand.NextDouble() * 0.5f;
            sapi.World.TreeGenerators[code].GrowTree(Api.World.BulkBlockAccessor, Pos.DownCopy(), size);

            Api.World.BulkBlockAccessor.Commit();
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("totalHoursTillGrowth", totalHoursTillGrowth);
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAtributes(tree, worldForResolving);

            totalHoursTillGrowth = tree.GetDouble("totalHoursTillGrowth", 0);
        }
        
    }
}
