using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vintagestory.ServerMods
{
    public enum EnumDepositPlacement
    {
        FollowSurfaceBelow, // Follows heightmap       (0 = 0 blocks below surface, 1 = 1 block below surface, 2 = 2 blocks below surface...)
        FollowSurface,      // Follows heightmap       (0 = map bottom, ..., 1 = surface)
        Anywhere,           // Don't follow anything   (0 = map bottom, ..., 1 = map top)
        FollowSeaLevel,      // Follows sealevel        (0 = map bottom, ..., 1 = sealevel)
        Straight
    }
}
