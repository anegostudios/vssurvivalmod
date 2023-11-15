using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public static class BlockUtil
    {
        public static ItemStack[] GetKnifeStacks(ICoreAPI api)
        {
            return ObjectCacheUtil.GetOrCreate(api, "knifeStacks", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Items)
                {
                    if (obj.Tool == EnumTool.Knife)
                    {
                        stacks.Add(new ItemStack(obj));
                    }
                }
                return stacks.ToArray();
            });
        }

    }
}
