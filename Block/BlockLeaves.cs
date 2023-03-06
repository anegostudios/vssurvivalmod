using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLeaves : BlockWithLeavesMotion
    {
        string climateColorMapInt;
        string seasonColorMapInt;

        public override string ClimateColorMapForMap => climateColorMapInt;
        public override string SeasonColorMapForMap => seasonColorMapInt;


        public override void OnCollectTextures(ICoreAPI api, ITextureLocationDictionary textureDict)
        {
            base.OnCollectTextures(api, textureDict);

            climateColorMapInt = ClimateColorMap;
            seasonColorMapInt = SeasonColorMap;
            string grown = Code.SecondCodePart();
            if (grown.StartsWith("grown"))
            {
                if (!int.TryParse(grown.Substring(5), out ExtraColorBits)) ExtraColorBits = 0;
            }

            // Branchy leaves
            if (api.Side == EnumAppSide.Client && SeasonColorMap == null)
            {
                climateColorMapInt = (api as ICoreClientAPI).TesselatorManager.GetCachedShape(Shape.Base)?.Elements[0].ClimateColorMap;
                seasonColorMapInt = (api as ICoreClientAPI).TesselatorManager.GetCachedShape(Shape.Base)?.Elements[0].SeasonColorMap;
            }
        }


        public override bool ShouldReceiveServerGameTicks(IWorldAccessor world, BlockPos pos, Random offThreadRandom, out object extra)
        {
            extra = null;
            return offThreadRandom.NextDouble() < 0.15;
        }


        public override void OnServerGameTick(IWorldAccessor world, BlockPos pos, object extra = null)
        {
            TreeAttribute tree = new TreeAttribute();
            tree.SetInt("x", pos.X);
            tree.SetInt("y", pos.Y);
            tree.SetInt("z", pos.Z);
            world.Api.Event.PushEvent("testForDecay", tree);
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.GetBlock(CodeWithParts("placed", LastCodePart())));
        }


        public override bool DisplacesLiquids(IBlockAccessor blockAccess, BlockPos pos)
        {
            return false;  //Needed for branchy leaves (which have solid sides, perhaps they shouldn't?)
        }
    }
}
