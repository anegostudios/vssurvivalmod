using System.Globalization;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorAmmunition(CollectibleObject collObj) : CollectibleBehavior(collObj)
    {
        public string MunitionProjectileCode { get; set; } = string.Empty;
        public ProjectileJsonConfig? Config { get; protected set; } = new();

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            if (properties.Exists) properties.Token.Populate(this);
            Config = properties.AsObject<ProjectileJsonConfig>();
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            if (Config == null) return;

            string dmg = (Config.Damage > 0 ? "+" : "") + Config.Damage.ToString(CultureInfo.CurrentCulture);
            string dmgType = Config.DamageType.ToString().ToLowerInvariant();
            if (Config.Damage != 0) dsc.AppendLine(Lang.Get("munition-" + dmgType + "-" + (Config.Damage > 0 ? "add" : "remove"), dmg));

            float breakChanceOnImpact = 1 - Config.DropOnImpactChance;
            if (breakChanceOnImpact > 0) dsc.AppendLine(Lang.Get("breakchanceonimpact", (int)(breakChanceOnImpact * 100)));
        }
    }
}
