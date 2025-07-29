using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public static class BlockUtil
    {
        public static ItemStack[] GetKnifeStacks(ICoreAPI api)
        {
            return ObjectCacheUtil.GetOrCreate<ItemStack[]>(api, "knifeStacks", () => [.. api.World.Items.Where(item => item.Tool == EnumTool.Knife)
                                                                                                         .Select(item => new ItemStack(item))]);
        }
    }
}
