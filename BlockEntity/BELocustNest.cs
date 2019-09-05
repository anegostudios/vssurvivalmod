using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityLocustNest : BlockEntitySpawner
    {
        long herdId;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);   
        }

        protected override long GetNextHerdId()
        {
            if (herdId == 0)
            {
                herdId = base.GetNextHerdId();
            }

            return herdId;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            Data = new BESpawnerData()
            {
                EntityCodes = new string[] { "locust" },
                InGameHourInterval = 1,
                MaxCount = api.World.Rand.Next(9) + 5,
                SpawnArea = new Cuboidi(-5, -5, -5, 5, 0, 5),
                GroupSize = 2 + api.World.Rand.Next(2),
                SpawnOnlyAfterImport = false,
                InitialSpawnQuantity = 6 + api.World.Rand.Next(13)
            };
        }

        protected override void DoSpawn(EntityProperties entityType, Vec3d spawnPosition, long herdId)
        {
            int dy = 0;
            while (dy < 15 && !api.World.BlockAccessor.GetBlock((int)spawnPosition.X, (int)spawnPosition.Y + dy, (int)spawnPosition.Z).SideSolid[BlockFacing.DOWN.Index])
            {
                dy++;
            }
            if (dy >= 15) return;

            spawnPosition.Y += dy - 1;

            base.DoSpawn(entityType, spawnPosition, herdId);
        }


        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            herdId = tree.GetLong("herdId");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetLong("herdId", herdId);
        }
    }
}