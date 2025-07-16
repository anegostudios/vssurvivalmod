using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// Interface for a BlockEntity which can accept a small gear (AngledGear) on the side - for example, LargeGear3
    /// </summary>
    public interface IGearAcceptor
    {
        bool CanAcceptGear(BlockPos pos);
        void AddGear(BlockPos pos);
        void RemoveGearAt(BlockPos pos);
    }
}
