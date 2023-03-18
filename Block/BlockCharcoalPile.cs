using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class BlockCharcoalPile : BlockLayeredSlowDig
    {

        public override float RandomSoundPitch(IWorldAccessor world)
        {
            return (float)world.Rand.NextDouble() * 0.24f + 0.88f;
        }
    }
}
