using Vintagestory.API.Common;

namespace Vintagestory.ServerMods
{
    public class SaplingControl : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide side)
        {
            return true;
        }

        public override double ExecuteOrder()
        {
            return 0;
        }

        public override void StartPre(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("Sapling", typeof(BlockEntitySapling));
        }
    }
}
