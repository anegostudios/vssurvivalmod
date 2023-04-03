using System.Diagnostics;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public abstract class ModStdWorldGen : ModSystem
    {
        public GlobalConfig GlobalConfig;
        public int chunksize;
        GenStoryStructures modSys;


        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public void LoadGlobalConfig(ICoreServerAPI api)
        {
            modSys = api.ModLoader.GetModSystem<GenStoryStructures>();

            chunksize = api.World.BlockAccessor.ChunkSize;

            IAsset asset = api.Assets.Get("worldgen/global.json");
            GlobalConfig = asset.ToObject<GlobalConfig>();

            GlobalConfig.defaultRockId = api.World.GetBlock(GlobalConfig.defaultRockCode).BlockId;
            GlobalConfig.waterBlockId = api.World.GetBlock(GlobalConfig.waterBlockCode).BlockId;
            GlobalConfig.saltWaterBlockId = api.World.GetBlock(GlobalConfig.saltWaterBlockCode).BlockId;
            GlobalConfig.lakeIceBlockId = api.World.GetBlock(GlobalConfig.lakeIceBlockCode).BlockId;
            GlobalConfig.lavaBlockId = api.World.GetBlock(GlobalConfig.lavaBlockCode).BlockId;
            GlobalConfig.basaltBlockId = api.World.GetBlock(GlobalConfig.basaltBlockCode).BlockId;
            GlobalConfig.mantleBlockId = api.World.GetBlock(GlobalConfig.mantleBlockCode).BlockId;
        }

        public bool SkipGenerationAt(Vec3d position, EnumWorldGenPass pass)
        {
            if (pass == EnumWorldGenPass.Vegetation)
            {
                return modSys.IsInStoryStructure(position);
            }

            return false;
        }

        public bool SkipGenerationAt(BlockPos position, EnumWorldGenPass pass)
        {
            if (pass == EnumWorldGenPass.Vegetation)
            {
                return modSys.IsInStoryStructure(position);
            }

            return false;
        }
    }
}
