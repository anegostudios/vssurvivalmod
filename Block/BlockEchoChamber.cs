using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEchoChamber : Block
    {

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel.Position == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            BlockEntityEchoChamber beec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityEchoChamber;

            if (beec != null)
            {
                beec.OnInteract(world, byPlayer);
            }

            return true;
        }
    }
}
