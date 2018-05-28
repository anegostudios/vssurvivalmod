using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.ServerMods.NoObf
{
    public class TreeGenProperties
    {
        public NatFloat treesPerChunk;
        public NatFloat shrubsPerChunk;


        public float vinesMinRain;
        public int vinesMinTemp;

        public float descVineMinTempRel;

        public TreeVariant[] TreeGens;
        public TreeVariant[] ShrubGens;
    }
}
