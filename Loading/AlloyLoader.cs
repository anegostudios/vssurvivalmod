using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintagestoryAPI.Common.Assets;

namespace Vintagestory.ServerMods 
{
    public class AlloyLoader : ModBase
    {
        ICoreServerAPI api;

        public override double ExecuteOrder()
        {
            return 1;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;

            api.Event.SaveGameLoaded(OnSaveGameLoaded);
        }

        private void OnSaveGameLoaded()
        {
            Dictionary<AssetLocation, MetalAlloy> alloys = api.Assets.GetMany<MetalAlloy>(api.Server.Logger, AssetCategory.alloys);

            foreach (var val in alloys)
            {
                val.Value.Resolve(api.World);
                api.RegisterMetalAlloy(val.Value);
            }

            api.World.Logger.Event("{0} metal alloys loaded", alloys.Count);
        }
    }
}