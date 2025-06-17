using Vintagestory.API.Client;
using Vintagestory.GameContent;

#nullable disable

namespace VSSurvivalMod.Systems.ChiselModes
{
    public class OneByChiselMode : ChiselMode
    {
        public override int ChiselSize => 1;

        public override DrawSkillIconDelegate DrawAction(ICoreClientAPI capi) => ItemClay.Drawcreate1_svg;
    }
}
