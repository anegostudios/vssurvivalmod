using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public abstract class ModStdWorldGen : ModSystem
    {
        public GlobalConfig GlobalConfig;
        public int chunksize;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public void LoadGlobalConfig(ICoreServerAPI api)
        {
            chunksize = api.World.BlockAccessor.ChunkSize;

            IAsset asset = api.Assets.Get("worldgen/global.json");
            GlobalConfig = asset.ToObject<GlobalConfig>();

            GlobalConfig.defaultRockId = api.World.GetBlock(GlobalConfig.defaultRockCode).BlockId;
            GlobalConfig.waterBlockId = api.World.GetBlock(GlobalConfig.waterBlockCode).BlockId;
            GlobalConfig.lakeIceBlockId = api.World.GetBlock(GlobalConfig.lakeIceBlockCode).BlockId;
            GlobalConfig.lavaBlockId = api.World.GetBlock(GlobalConfig.lavaBlockCode).BlockId;
            GlobalConfig.basaltBlockId = api.World.GetBlock(GlobalConfig.basaltBlockCode).BlockId;
            GlobalConfig.mantleBlockId = api.World.GetBlock(GlobalConfig.mantleBlockCode).BlockId;
        }
    }
}
