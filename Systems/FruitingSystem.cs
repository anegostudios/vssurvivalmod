using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class FruitingSystem : ModSystem
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        //IClientNetworkChannel clientNwChannel;
        //IServerNetworkChannel serverNwChannel;

        public ICoreAPI Api;
        public FruitRendererSystem Renderer;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            this.Api = api;

            if (api.World is IClientWorldAccessor)
            {
                //(api as ICoreClientAPI).Event.RegisterRenderer(this, EnumRenderStage.Before, "fruitPre");
            }
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

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;
            base.StartServerSide(api);

            //api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            //api.Event.GameWorldSave += Event_GameWorldSave;
            //api.Event.ChunkDirty += Event_ChunkDirty;
        }

        private void onLoaded()
        {
            Renderer = new FruitRendererSystem(capi);
        }

        public override void Dispose()
        {
            base.Dispose();
            Renderer?.Dispose();
        }

        /// <summary>
        /// Add a fruit to render, with its germination date in gametime  (allows for it to be grown, transitioned etc)
        /// </summary>
        public void AddFruit(AssetLocation code, Vec3d position, FruitData data)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                Item fruit = Api.World.GetItem(code);
                if (fruit != null) Renderer.AddFruit(fruit, position, data);
            }
        }


        public void RemoveFruit(String fruitCode, Vec3d position)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                Item fruit = Api.World.GetItem(new AssetLocation(fruitCode));
                if (fruit != null) Renderer.RemoveFruit(fruit, position);
            }
        }

    }
}
