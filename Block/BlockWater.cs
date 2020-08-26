using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWater : Block
    {
        public string Flow => Variant["flow"];
        public string Height => Variant["height"];

        bool freezable;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            freezable = Flow == "still" && Height == "7";
        }


        public override bool ShouldPlayAmbientSound(IWorldAccessor world, BlockPos pos)
        {
            // Play water wave sound when above is air and below is a solid block
            return
                world.BlockAccessor.GetBlock(pos.X, pos.Y + 1, pos.Z).Id == 0 &&
                world.BlockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z).SideSolid[BlockFacing.UP.Index];
        }




        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            if (freezable && offThreadRandom.NextDouble() < 0.6)
            {
                ClimateCondition conds = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
                if (conds.Temperature < -4)
                {
                    int rainY = world.BlockAccessor.GetRainMapHeightAt(pos);
                    if (rainY <= pos.Y)
                    {
                        for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                        {
                            BlockFacing facing = BlockFacing.HORIZONTALS[i];
                            if (world.BlockAccessor.GetBlock(pos.AddCopy(facing)).Replaceable < 6000)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            Block waterBlock = world.GetBlock(new AssetLocation("lakeice"));
            world.BlockAccessor.SetBlock(waterBlock.Id, pos);
        }
    }
}
