using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public interface IBellowsAirReceiver
    {
        void BlowAirInto(float amount, BlockFacing direction);
    }
}
