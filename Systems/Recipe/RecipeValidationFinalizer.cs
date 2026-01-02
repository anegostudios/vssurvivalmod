using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods
{
    public class RecipeValidationFinalizer : ModSystem
    {
        public override double ExecuteOrder()
        {
            return 2;
        }

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            RecipeValidationErrors.ThrowIfAny();
        }
    }
}
