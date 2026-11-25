using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockFern : BlockPlant, ICustomTreeFellingBehavior
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            waveFlagMinY = 0.25f;
            tallGrassColorMapping = true;
        }
    }
}
