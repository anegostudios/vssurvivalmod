using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockCokeOvenDoor : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockPos pos = blockSel.Position;

            if (Variant["state"] == "closed")
            {
                world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariant("state", "opened")).Id, pos);
                if (world.Side == EnumAppSide.Server) world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                world.PlaySoundAt(new AssetLocation("sounds/block/cokeovendoor-open"), pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true);
            } else
            {
                world.BlockAccessor.SetBlock(world.GetBlock(CodeWithVariant("state", "closed")).Id, pos);
                if (world.Side == EnumAppSide.Server) world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);
                world.PlaySoundAt(new AssetLocation("sounds/block/cokeovendoor-close"), pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true);
            }

            return true;
        }


    }
}
