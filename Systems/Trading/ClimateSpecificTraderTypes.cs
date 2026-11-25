using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemClimateSpecificTraderTypes : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        ICoreServerAPI sapi;
        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.RegisterEventBusListener(onSpawnerSpawnAttempt, 1, "onattemptspawnerspawn");
        }

        private void onSpawnerSpawnAttempt(string eventName, ref EnumHandling handling, IAttribute data)
        {
            var tree = data as TreeAttribute;
            var type = tree.GetString("type");
            if (type.StartsWith("trader"))
            {
                var conds = sapi.World.BlockAccessor.GetClimateAt(tree.GetBlockPos("pos"), EnumGetClimateMode.WorldGenValues);
                if (conds.Temperature >= 15 && conds.Rainfall <= 0.4)
                {
                    tree.SetString("type", type.Replace("-temperate", "-desert"));
                } else if (conds.Temperature < -4)
                {
                    tree.SetString("type", type.Replace("-temperate", "-cold"));
                }
            }
        }
    }
}
