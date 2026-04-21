using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class CreaturePopulation
    {
        public double PopulationSize = 10;
        public double RestorationMul = 1.05f;
        public double RetorationDaysInterval = 10;
    }

  /*  public class ModSystemCreatureDepletion : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        protected ICoreServerAPI sapi;
        protected Dictionary<BlockPos, CreatureHarvest> harvestedLocations = new Dictionary<BlockPos, CreatureHarvest>();

        protected Dictionary<AssetLocation, CreaturePopulation> creaturePopulations = new Dictionary<AssetLocation, CreaturePopulation>();

        public int Scale = 64;

        

        public override double ExecuteOrder() => 1;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            
            api.Event.RegisterGameTickListener(restoreFish, 10000, sapi.World.Rand.Next(1000));
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("harvestedFishLocations", harvestedLocations);
        }

        private void Event_SaveGameLoaded()
        {
            try
            {
                harvestedLocations = sapi.WorldManager.SaveGame.GetData<Dictionary<BlockPos, CreatureHarvest>>("harvestedFishLocations");
            }
            catch
            {
                // Don't care if this is corrupted data, its unessential
            }

            if (harvestedLocations == null)
            {
                harvestedLocations = new Dictionary<BlockPos, CreatureHarvest>();
            }
        }

        private void restoreFish(float dt)
        {
            var positions = new List<BlockPos>(harvestedLocations.Keys);
            var totaldays = sapi.World.Calendar.TotalDays;
            foreach (var pos in positions)
            {
                if (totaldays - harvestedLocations[pos].TotalDays > RestoreFishAfterDays)
                {
                    harvestedLocations.Remove(pos);
                }
            }
        }

        public void AddHarvest(BlockPos pos, int quantity)
        {
            CreatureHarvest harvest;
            harvestedLocations.TryGetValue(pos / Scale, out harvest);
            harvestedLocations[pos / Scale] = new CreatureHarvest()
            {
                TotalDays = sapi.World.Calendar.TotalDays,
                Quantity = harvest.Quantity + quantity
            };
        }

        public float GetHarvestAmount(BlockPos pos)
        {
            if (harvestedLocations.TryGetValue(pos / Scale, out var harvest))
            {
                return harvest.Quantity;
            }

            return 0;
        }
    }
  */



    public class CustomSpawnConditions : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            sapi.Event.OnTrySpawnGroupNearOffthread += Event_OnTrySpawnGroupNearOffthread;
        }

        private bool Event_OnTrySpawnGroupNearOffthread(IBlockAccessor blockaccessor, EntityProperties properties, BlockPos pos)
        {
            if (properties.Attributes?["harshWinterSpawnRate"].Exists == true)
            {
                bool harshWinters = sapi.World.Config.GetString("harshWinters").ToBool(true);
                if (harshWinters && sapi.World.Calendar.GetSeason(pos) == EnumSeason.Winter)
                {
                    float spawnRate = properties.Attributes?["harshWinterSpawnRate"].AsFloat() ?? 1;
                    return sapi.World.Rand.NextDouble() < spawnRate;
                }
            }            

            float newWorldSpawnDelayHours = properties.Attributes?["newWorldSpawnDelayHours"].AsFloat() ?? 0;
            if (sapi.World.Calendar.ElapsedHours < newWorldSpawnDelayHours)
            {
                return false;
            }

            return true;
        }

    }
}
