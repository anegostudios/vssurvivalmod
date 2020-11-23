using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class BlockEntitySapling : BlockEntity
    {
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

            NatFloat growthDays = NatFloat.create(EnumDistribution.UNIFORM, 6.5f, 1.5f);

            if (block?.Attributes != null)
            {
                growthDays = block.Attributes["growthDays"].AsObject(growthDays);
            }

            totalHoursTillGrowth = Api.World.Calendar.TotalHours + growthDays.nextFloat(1, Api.World.Rand) * 24;
        }

        
        private void CheckGrow(float dt)
        {
            if (Api.World.Calendar.TotalHours < totalHoursTillGrowth) return;

            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Pos, EnumGetClimateMode.NowValues);
            if (conds.Temperature < 5)
            {
                return;
            }

            if (conds.Temperature < 0)
            {
                totalHoursTillGrowth = Api.World.Calendar.TotalHours + (float)Api.World.Rand.NextDouble() * 72;
                return;
            }

            int chunksize = Api.World.BlockAccessor.ChunkSize;
            foreach (BlockFacing facing in BlockFacing.HORIZONTALS)
            {
                Vec3i dir = facing.Normali;
                int x = Pos.X + dir.X * chunksize;
                int z = Pos.Z + dir.Z * chunksize;

                // Not at world edge and chunk is not loaded? We must be at the edge of loaded chunks. Wait until more chunks are generated
                if (Api.World.BlockAccessor.IsValidPos(x, Pos.Y, z) && Api.World.BlockAccessor.GetChunkAtBlockPos(x, Pos.Y, z) == null) return;
            }

            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            string treeGenCode = block.Attributes?["treeGen"].AsString(null);

            if (treeGenCode == null)
            {
                Api.Event.UnregisterGameTickListener(growListenerId);
                return;
            }

            AssetLocation code = new AssetLocation(treeGenCode);
            ICoreServerAPI sapi = Api as ICoreServerAPI;

            ITreeGenerator gen;
            if (!sapi.World.TreeGenerators.TryGetValue(code, out gen))
            {
                Api.Event.UnregisterGameTickListener(growListenerId);
                return;
            }

            Api.World.BlockAccessor.SetBlock(0, Pos);
            Api.World.BulkBlockAccessor.ReadFromStagedByDefault = true;
            float size = 0.6f + (float)Api.World.Rand.NextDouble() * 0.5f;
            sapi.World.TreeGenerators[code].GrowTree(Api.World.BulkBlockAccessor, Pos.DownCopy(), size, 0, 0);

            Api.World.BulkBlockAccessor.Commit();
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("totalHoursTillGrowth", totalHoursTillGrowth);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            totalHoursTillGrowth = tree.GetDouble("totalHoursTillGrowth", 0);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            double hoursleft = totalHoursTillGrowth - Api.World.Calendar.TotalHours;
            double daysleft = hoursleft / Api.World.Calendar.HoursPerDay;

            if (daysleft <= 1) {
                dsc.AppendLine(Lang.Get("Will grow in less than a day"));
            } else
            {
                dsc.AppendLine(Lang.Get("Will grow in about {0} days", (int)daysleft));
            }
        }

    }
}
