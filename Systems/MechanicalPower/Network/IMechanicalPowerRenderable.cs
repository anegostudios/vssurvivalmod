using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    /// <summary>
    /// Any mechanical power Block Entity which can be rendered by MechBlockRenderer
    /// </summary>
    public interface IMechanicalPowerRenderable
    {
        float AngleRad { get; }
        Block Block { get; }
        BlockPos Position { get; }
        Vec4f LightRgba { get; }
        int[] AxisSign { get; }
        CompositeShape Shape { get; }
    }
}
