using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent;

class BlockCandle : BlockBunchOCandles
{
    public BlockCandle()
    {
        candleWickPositions = new[] { new Vec3f(7 + 0.8f, 4, 7 + 0.8f) };
    }
    
    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        QuantityCandles = 1;
    }
}