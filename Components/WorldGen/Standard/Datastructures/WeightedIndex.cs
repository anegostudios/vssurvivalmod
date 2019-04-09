using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.ServerMods
{
    public struct WeightedIndex
    {
        public int Index;
        public float Weight;

        public WeightedIndex(int index, float weight)
        {
            this.Index = index;
            this.Weight = weight;
        }
    }
}
