using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Datastructures;

namespace Vintagestory.ServerMods
{
    public abstract class MapLayerBase : NoiseBase
    {
        internal IntDataMap2D inputMap;
        internal IntDataMap2D outputMap;

        public MapLayerBase(long seed) : base(seed)
        {
            
        }

        public abstract int[] GenLayer(int xCoord, int zCoord, int sizeX, int sizeZ);

        public void SetInputMap(IntDataMap2D inputMap, IntDataMap2D outputMap)
        {
            this.inputMap = inputMap;
            this.outputMap = outputMap;
        }

        public void DebugDrawBitmap(DebugDrawMode mode, int x, int z, string name)
        {
            if (!Debug) return;
            DebugDrawBitmap(mode, GenLayer(x + DebugXCoord, z + DebugZCoord, 512, 512), 512, 512, name);
        }

        public void DebugDrawBitmap(DebugDrawMode mode, int x, int z, int size, string name)
        {
            if (!Debug) return;
            DebugDrawBitmap(mode, GenLayer(x + DebugXCoord, z + DebugZCoord, size, size), size, size, name);
        }

    }
}
