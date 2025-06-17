
#nullable disable
namespace Vintagestory.ServerMods
{
    public abstract class MapLayerTransformBase : MapLayerBase
    {
        internal MapLayerBase parent;

        public MapLayerTransformBase(long seed, MapLayerBase parent) : base(seed)
        {
            this.parent = parent;
        }
    }
}
