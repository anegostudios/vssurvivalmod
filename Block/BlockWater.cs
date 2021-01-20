using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWater : Block, VintagestoryAPI.Common.Collectible.Block.IBlockFlowing
    {
        public string Flow { get; set; }
        public Vec3i FlowNormali { get; set; }
        public bool IsLava => false;

        public string Height { get; set; }

        bool freezable;
        Block iceBlock;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Flow = Variant["flow"] is string f ? string.Intern(f) : null; ;
            FlowNormali = Flow != null ? Cardinal.FromInitial(Flow)?.Normali : null;
            Height = Variant["height"] is string h ? string.Intern(h) : null; ;

            freezable = Flow == "still" && Height == "7";
            iceBlock = api.World.GetBlock(new AssetLocation("lakeice"));
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
            if (!GlobalConstants.MeltingFreezingEnabled) return false;

            if (freezable && offThreadRandom.NextDouble() < 0.6)
            {
                int rainY = world.BlockAccessor.GetRainMapHeightAt(pos);
                if (rainY <= pos.Y)
                {
                    for (int i = 0; i < BlockFacing.HORIZONTALS.Length; i++)
                    {
                        BlockFacing facing = BlockFacing.HORIZONTALS[i];
                        if (world.BlockAccessor.GetBlock(pos.AddCopy(facing)).Replaceable < 6000)
                        {
                            ClimateCondition conds = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
                            if (conds != null && conds.Temperature < -4)
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
            world.BlockAccessor.SetBlock(iceBlock.Id, pos);
        }
    }
}
