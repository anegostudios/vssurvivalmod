using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.GameContent
{
    public class CloudTile
    {
        public byte MaxDensity;
        public byte UpDownDensity;

        public byte NorthFaceDensity;
        public byte EastFaceDensity;
        public byte SouthFaceDensity;
        public byte WestFaceDensity;
        public byte Brightness;
        
        public int XOffset; // Grid position
        public float YOffset;
        public int ZOffset; // Grid position
    }
}
