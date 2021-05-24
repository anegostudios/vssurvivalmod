using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockGlacierIce : Block
    {


        public override bool ShouldMergeFace(int facingIndex, Block neighbourblock, int intraChunkIndex3d)
        {
            return this == neighbourblock;
        }
    }
}
