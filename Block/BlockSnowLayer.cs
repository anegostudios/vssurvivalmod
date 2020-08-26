using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockSnowLayer : BlockLayered
    {
        int height;
        bool canMelt;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            height = Variant["height"].ToInt();
            snowLevel = height;

            notSnowCovered = api.World.GetBlock(0);
            snowCovered1 = api.World.GetBlock(CodeWithVariant("cover", "1"));
            snowCovered2 = api.World.GetBlock(CodeWithVariant("cover", "2"));
            snowCovered3 = api.World.GetBlock(CodeWithVariant("cover", "3"));

            canMelt = api.World.Config.GetBool("snowAccum", true);
        }

        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            ClimateCondition conds = world.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
            return canMelt && offThreadRandom.NextDouble() < GameMath.Clamp((conds.Temperature - 0.5f) / 15f, 0, 1);
        }

        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            if (height == 1)
            {
                world.BlockAccessor.SetBlock(0, pos);
                return;
            }

            Block block = world.GetBlock(CodeWithVariant("height", ""+(height - 1)));
            world.BlockAccessor.SetBlock(block.Id, pos);
        }
    }
}
