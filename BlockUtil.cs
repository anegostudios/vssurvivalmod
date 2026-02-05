using System;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public static class BlockUtil
    {
        [Obsolete("Call ObjectCacheUtil.GetToolStacks instead")]
        public static ItemStack[] GetKnifeStacks(ICoreAPI api) => ObjectCacheUtil.GetToolStacks(api, EnumTool.Knife);
    }
}
