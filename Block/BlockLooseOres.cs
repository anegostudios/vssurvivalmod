using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLooseOres : Block
    {
        public string MotherRock
        {
            get
            {
                return Variant["rock"];
            }
        }


        public string OreName
        {
            get
            {
                return Variant["ore"];
            }
        }

        public string InfoText
        {
            get
            {
                StringBuilder dsc = new StringBuilder();
                dsc.AppendLine(Lang.Get("ore-in-rock", Lang.Get("ore-" + OreName), Lang.Get("rock-" + MotherRock)));

                return dsc.ToString();
            }
        }




        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            dsc.AppendLine(InfoText);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return InfoText + "\n" + base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

    }
}
