#nullable disable

using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockWaterWheel : BlockWindmillRotor
    {

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return
                base.GetPlacedBlockInteractionHelp(world, selection, forPlayer)
                .Where((wi) => wi.ActionLangCode != "heldhelp-addsails")
                .ToArray()
            ;
        }
    }
}
