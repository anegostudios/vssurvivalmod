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
        public static int SkipHotSpringsgHashCode;
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
            SkipHotSpringsgHashCode = BitConverter.ToInt32(SHA256.HashData("hotsprings"u8.ToArray()));
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

            GlobalConfig = GlobalConfig.GetInstance(api);
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
        public string GetIntersectingStructure(Vec3d position, int skipCategory)
        {
            return modSys.GetStoryStructureCodeAt(position, skipCategory);
        }

        /// <summary>
        /// <inheritdoc cref="SkipGenerationAt(Vintagestory.API.MathTools.Vec3d,int,out string)"/>
        /// </summary>
        /// <param name="x"></param>
        /// <param name="z"></param>
        /// <param name="skipCategory">The hashcode of the string category from storystructure.json skipGenerationCategories. The strings from storystructure.json are first converted to lowercase before getting the hash code.</param>
        /// <param name="locationCode"></param>
        /// <returns></returns>
        public string GetIntersectingStructure(int x, int z, int skipCategory)
        {
            return modSys.GetStoryStructureCodeAt(x, z,skipCategory);
        }

        /// <summary>
        /// <inheritdoc cref="SkipGenerationAt(Vintagestory.API.MathTools.Vec3d,int,out string)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="skipCategory">The hashcode of the string category from storystructure.json skipGenerationCategories. The strings from storystructure.json are first converted to lowercase before getting the hash code.</param>
        /// <param name="locationCode"></param>
        public string GetIntersectingStructure(BlockPos position, int skipCategory)
        {
            return modSys.GetStoryStructureCodeAt(position, skipCategory);
        }
    }
}
