using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemCoal : ItemPileable
    {
        protected override AssetLocation PileBlockCode => new AssetLocation("coalpile");


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            var inforgeprops = Attributes["inForge"];

            var tpd = inforgeprops["tempGainDeg"].AsInt();
            dsc.AppendLine(Lang.Get("coal-forgetemp-mod", 700 + tpd));
            var dm = inforgeprops["durationMul"].AsFloat();
            dsc.AppendLine(Lang.Get("coal-forgedur-mod", 2f * dm));
        }
    }
}
