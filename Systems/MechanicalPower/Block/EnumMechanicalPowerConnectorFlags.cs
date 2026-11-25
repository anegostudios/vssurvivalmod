using System;

namespace Vintagestory.GameContent.Mechanics
{
    // The shaft end a part has, or accepts on a machine.
    [Flags]
    public enum EnumMechanicalPowerConnectorFlags
    {
        Cross = 1,
        Square = 2,
        All = Cross | Square
    }
}
