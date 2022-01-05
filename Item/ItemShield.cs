using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemShield : Item
    {
        float offY;
        float curOffY = 0;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            curOffY = offY = FpHandTransform.Translation.Y;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.HandFp)
            {
                bool sneak = capi.World.Player.Entity.Controls.Sneak;

                curOffY += ((sneak ? 0.4f : offY) - curOffY) * renderinfo.dt * 8;

                renderinfo.Transform.Translation.X = curOffY;
                renderinfo.Transform.Translation.Y = curOffY * 1.2f;
                renderinfo.Transform.Translation.Z = curOffY * 1.2f;
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            var attr = inSlot.Itemstack?.ItemAttributes?["shield"];
            if (attr == null || !attr.Exists) return;

            float acdmgabsorb = attr["damageAbsorption"]["active"].AsFloat(0);
            float acchance = attr["protectionChance"]["active"].AsFloat(0);

            float padmgabsorb = attr["damageAbsorption"]["passive"].AsFloat(0);
            float pachance = attr["protectionChance"]["passive"].AsFloat(0);

            dsc.AppendLine(Lang.Get("shield-stats", (int)(100*acchance), (int)(100*pachance), acdmgabsorb, padmgabsorb));
        }
    }
}
