using System;
using System.Security.Cryptography;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public abstract class ModStdWorldGen : ModSystem
    {
        public static int SkipStructuresgHashCode;
        public static int SkipPatchesgHashCode;
        public static int SkipCavesgHashCode;
        public static int SkipTreesgHashCode;
        public static int SkipShurbsgHashCode;
        public static int SkipStalagHashCode;
        public static int SkipHostSpringsgHashCode;
        public static int SkipRivuletsgHashCode;
        public static int SkipPondgHashCode;
        public static int SkipCreaturesgHashCode;

        static ModStdWorldGen()
        {
            SkipStructuresgHashCode = BitConverter.ToInt32(SHA256.HashData("structures"u8.ToArray()));
            SkipPatchesgHashCode = BitConverter.ToInt32(SHA256.HashData("patches"u8.ToArray()));
            SkipCavesgHashCode = BitConverter.ToInt32(SHA256.HashData("caves"u8.ToArray()));
            SkipTreesgHashCode = BitConverter.ToInt32(SHA256.HashData("trees"u8.ToArray()));
            SkipShurbsgHashCode = BitConverter.ToInt32(SHA256.HashData("shrubs"u8.ToArray()));
            SkipHostSpringsgHashCode = BitConverter.ToInt32(SHA256.HashData("hostsprings"u8.ToArray()));
            SkipRivuletsgHashCode = BitConverter.ToInt32(SHA256.HashData("rivulets"u8.ToArray()));
            SkipStalagHashCode = BitConverter.ToInt32(SHA256.HashData("stalag"u8.ToArray()));
            SkipPondgHashCode = BitConverter.ToInt32(SHA256.HashData("pond"u8.ToArray()));
            SkipCreaturesgHashCode = BitConverter.ToInt32(SHA256.HashData("creatures"u8.ToArray()));
        }
        public GlobalConfig GlobalConfig;
        protected const int chunksize = GlobalConstants.ChunkSize;
        internal GenStoryStructures modSys;


        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public void LoadGlobalConfig(ICoreServerAPI api)
        {
            modSys = api.ModLoader.GetModSystem<GenStoryStructures>();

            IAsset asset = api.Assets.Get("worldgen/global.json");
            GlobalConfig = asset.ToObject<GlobalConfig>();

            GlobalConfig.defaultRockId = api.World.GetBlock(GlobalConfig.defaultRockCode)?.BlockId ?? 0;
            GlobalConfig.waterBlockId = api.World.GetBlock(GlobalConfig.waterBlockCode)?.BlockId ?? 0;
            GlobalConfig.saltWaterBlockId = api.World.GetBlock(GlobalConfig.saltWaterBlockCode)?.BlockId ?? 0;
            GlobalConfig.lakeIceBlockId = api.World.GetBlock(GlobalConfig.lakeIceBlockCode)?.BlockId ?? 0;
            GlobalConfig.lavaBlockId = api.World.GetBlock(GlobalConfig.lavaBlockCode)?.BlockId ?? 0;
            GlobalConfig.basaltBlockId = api.World.GetBlock(GlobalConfig.basaltBlockCode)?.BlockId ?? 0;
            GlobalConfig.mantleBlockId = api.World.GetBlock(GlobalConfig.mantleBlockCode)?.BlockId ?? 0;
        }

        /// <summary>
        /// Checks weather the provided position is inside a story structures schematics for the specified skipCategory, or it's radius.
        /// If the Radius for the storystrcuture should be also checked is defined in the json as int as part of the skipGenerationCategories. Does only check 2D from story locations center.
        /// If the Radius at skipGenerationCategories is 0 then only the structures cuboid is checked.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="skipCategory">The hash of the string category from storystructure.json skipGenerationCategories. The strings from storystructure.json are first converted to lowercase before getting the hash. A Dictionary of the hashes and radius is stored with the story location in the savegame when a location is generated. See the <see cref="ModStdWorldGen"/> static constructor how to compute your own.</param>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        public bool SkipGenerationAt(Vec3d position, int skipCategory, out string locationCode)
        {
            return modSys.IsInStoryStructure(position, skipCategory, out locationCode);
        }

        /// <summary>
        /// <inheritdoc cref="SkipGenerationAt(Vintagestory.API.MathTools.Vec3d,int,out string)"/>
        /// </summary>
        /// <param name="x"></param>
        /// <param name="z"></param>
        /// <param name="skipCategory">The hashcode of the string category from storystructure.json skipGenerationCategories. The strings from storystructure.json are first converted to lowercase before getting the hash code.</param>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        public bool SkipGenerationAt(int x, int z, int skipCategory, out string locationCode)
        {
            return modSys.IsInStoryStructure(x, z,skipCategory, out locationCode);
        }

        /// <summary>
        /// <inheritdoc cref="SkipGenerationAt(Vintagestory.API.MathTools.Vec3d,int,out string)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="skipCategory">The hashcode of the string category from storystructure.json skipGenerationCategories. The strings from storystructure.json are first converted to lowercase before getting the hash code.</param>
        /// <param name="locationCode"></param>
        public bool SkipGenerationAt(BlockPos position, int skipCategory, out string locationCode)
        {
            return modSys.IsInStoryStructure(position, skipCategory, out locationCode);
        }
    }
}
