using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.ServerMods.NoObf
{
    class RockStrataGen
    {
        public ushort blockId;
        public float thickness;
        public float heightErosion;

        public float multiplier;
        public float offset;
        public float amplitude;

        // http://fooplot.com/plot/gxmsgg9olv
        public double GetValue(double x)
        {
            return Math.Max(0, 1 - multiplier * (x - offset) * (x - offset)) * amplitude;
        }



    }
}
