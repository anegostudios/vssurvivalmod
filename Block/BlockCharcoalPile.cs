using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Vintagestory.GameContent
{
    public class BlockCharcoalPile : BlockLayeredSlowDig
    {

        public override float RandomSoundPitch(IWorldAccessor world)
        {
            return (float)world.Rand.NextDouble() * 0.24f + 0.88f;
        }
    }
}
