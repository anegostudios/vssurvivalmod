using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockRefractoryBrick : Block
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            int resis = (int)(100 * Attributes["heatResistance"].AsFloat());

            dsc.AppendLine(Lang.Get("Heat resistance: {0}%", resis));
        }

        public override double GetBlastResistance(IWorldAccessor world, BlockPos pos, Vec3f blastDirectionVector, EnumBlastType blastType)
        {
            return 0.5;
        }

    }
}
