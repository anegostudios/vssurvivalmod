using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    class BlockThermalDifference :  Block
    {
        public static Dictionary<string, int> TempuratureValues;

        public static List<string> DynamicTemps;

        public BlockThermalDifference() : base()
        {
            TempuratureValues = new Dictionary<string, int>
            {
                { "game:lava-still-7",3000 }
            };
            DynamicTemps = new List<string>
            {
                "game:firepit-lit",
                "game:firepit-extinct"
            };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            //get above and below blocks (see GetTempOfBlockOnFace)
            //get temps for those blocks.
            //then temp above - temp below (if negative, multiply by -1)
            //then display the result to the user.



            int topBlockTemp = GetTempOfBlockOnFace(BlockFacing.UP, blockSel, world);
            int bottomBlockTemp = GetTempOfBlockOnFace(BlockFacing.DOWN, blockSel, world);

            if(topBlockTemp - bottomBlockTemp < 0)
            {
                //bottom - top
                world.Logger.Log(EnumLogType.Debug, $"The Current temp difference is: {bottomBlockTemp - topBlockTemp}");
            }
            else
            {
                //top - bottom.
                world.Logger.Log(EnumLogType.Debug, $"The Current temp difference is: {topBlockTemp - bottomBlockTemp}");
            }

            return true;
        }

        int GetTempOfBlockOnFace(BlockFacing direction, BlockSelection currentPos, IWorldAccessor world)
        {
            BlockPos inputBlockpos = currentPos.Position.AddCopy(direction);

            Block block = world.BlockAccessor.GetBlock(inputBlockpos);

            world.Logger.Log(EnumLogType.Debug, $"Getting temp for: {block.Code.ToString()}");

            if(TempuratureValues.ContainsKey(block.Code.ToString()))
            {
                return TempuratureValues[block.Code.ToString()];
            }

            //TODO: add method to block entities getting their tempuratures.

            if(DynamicTemps.Contains(block.Code.ToString())) //the firepit... for now.
            {
                BlockEntityFirepit befirepit = (BlockEntityFirepit)world.BlockAccessor.GetBlockEntity(inputBlockpos);
                if(befirepit != null)
                    return (int)befirepit.furnaceTemperature;
            }

            return 20;
        }
    }
}
