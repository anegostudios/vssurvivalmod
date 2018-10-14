using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class BlockEntityWateringCan : BlockEntity
    {
        public float SecondsWateringLeft;
        BlockWateringCan ownBlock;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = api.World.BlockAccessor.GetBlock(this.pos) as BlockWateringCan;
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (byItemStack != null)
            {
                SecondsWateringLeft = (byItemStack.Block as BlockWateringCan).GetRemainingWateringSeconds(byItemStack);
            }
        }

        public override void FromTreeAtributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAtributes(tree, worldAccessForResolve);

            SecondsWateringLeft = tree.GetFloat("secondsWateringLeft");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetFloat("secondsWateringLeft", SecondsWateringLeft);
        }

        public override string GetBlockInfo(IPlayer forPlayer)
        {
            double perc = Math.Round(100 * SecondsWateringLeft / ownBlock.CapacitySeconds);
            if (perc < 1)
            {
                return Lang.Get("Empty");
            }
            else
            {
                return Lang.Get("{0}% full", perc);
            }
        }

    }
}
