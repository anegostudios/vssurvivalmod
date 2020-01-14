using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;


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
