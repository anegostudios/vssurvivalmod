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
    public class BlockBomb : Block
    {
        public override bool OnTryIgniteBlock(IEntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            BlockEntityBomb bebomb = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBomb;
            handling = EnumHandling.PreventDefault;

            return (bebomb == null || bebomb.IsLit) ? false : secondsIgniting < 0.75f;
        }

        public override void OnTryIgniteBlockOver(IEntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (secondsIgniting < 0.7f) return;

            handling = EnumHandling.PreventDefault;

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            BlockEntityBomb bebomb = byPlayer.Entity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityBomb;
            bebomb?.OnIgnite(byPlayer);
        }


        public override void OnBlockExploded(IWorldAccessor world, BlockPos pos, BlockPos explosionCenter, EnumBlastType blastType)
        {
            BlockEntityBomb bebomb = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBomb;
            bebomb?.OnBlockExploded(pos);
        }
    }
}
