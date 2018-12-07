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
    public class BlockForge : Block
    {
        public override bool OnTryIgniteBlock(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            BlockEntityForge bea = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityForge;
            handling = EnumHandling.PreventDefault;

            if (bea==null || !bea.CanIgnite)
            {
                return false;
            }

            if (secondsIgniting > 0.25f && (int)(30 * secondsIgniting) % 9 == 1)
            {
                Random rand = byEntity.World.Rand;
                Vec3d dpos = new Vec3d(pos.X + 2 / 8f + 4 / 8f * rand.NextDouble(), pos.Y + 7 / 8f, pos.Z + 2 / 8f + 4 / 8f * rand.NextDouble());

                Block blockFire = byEntity.World.GetBlock(new AssetLocation("fire"));

                AdvancedParticleProperties props = blockFire.ParticleProperties[blockFire.ParticleProperties.Length - 1];
                props.basePos = dpos;
                props.Quantity.avg = 1;

                IPlayer byPlayer = null;
                if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

                byEntity.World.SpawnParticles(props, byPlayer);

                props.Quantity.avg = 0;
            }

            return secondsIgniting < 2f;
        }

        public override void OnTryIgniteBlockOver(EntityAgent byEntity, BlockPos pos, float secondsIgniting, ref EnumHandling handling)
        {
            if (secondsIgniting < 1.95f) return;

            handling = EnumHandling.PreventDefault;

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            BlockEntityForge bea = byEntity.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityForge;
            bea?.TryIgnite();
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityForge bea = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityForge;
            if (bea != null)
            {
                return bea.OnPlayerInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
