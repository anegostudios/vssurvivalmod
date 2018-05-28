using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class EntityTypeLoader : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override double ExecuteOrder()
        {
            return 0.4;
        }

        public override bool AllowRuntimeReload()
        {
            return false;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Dictionary<AssetLocation, EntityType> configs = api.Assets.GetMany<EntityType>(api.Server.Logger, "entities/");
            foreach (EntityType config in configs.Values)
            {
                if (!config.Enabled) continue;

                config.InitClass(api.Assets);
                api.RegisterEntityClass(config.Class, config);
            }
        }

        
    }
}
