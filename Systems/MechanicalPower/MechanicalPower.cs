using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class MechanicalPowerMod : ModSystem
    {
        public MechNetworkRenderer Renderer;
        
        ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.ObjectCache["mechPowerMod"] = this;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api;

            api.Event.BlockTexturesLoaded += onLoaded;
            api.Event.LeaveWorld += () =>
            {
                Renderer?.Dispose();
            };
        }


        private void onLoaded()
        {
            Renderer = new MechNetworkRenderer(capi, this);
        }

        internal void RemoveDevice(IMechanicalPowerDeviceVS device)
        {
            Renderer?.RemoveDevice(device);
        }

        internal void AddDevice(IMechanicalPowerDeviceVS device)
        {
            Renderer?.AddDevice(device);
        }
        
    }
}
