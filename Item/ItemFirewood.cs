using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    class ItemFirewood : Item
    {

        public override bool OnHeldInteractStart(IItemSlot slot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return false;

            BlockPos onBlockPos = blockSel.Position;
            Block block = byEntity.World.BlockAccessor.GetBlock(onBlockPos);

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

            
            if (byPlayer == null) return false;

            if (block is BlockFirepit)
            {
                bool constructed = ((BlockFirepit)block).Construct(byEntity.World, onBlockPos, slot.Itemstack.Collectible.CombustibleProps);
                if (constructed)
                {
                    if (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
                    return true;
                }

                return false;
            } else
            {

                BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(onBlockPos);
                if (be is BlockEntityFirewoodPile)
                {
                    BlockEntityFirewoodPile pile = (BlockEntityFirewoodPile)be;
                    if (pile.OnPlayerInteract(byPlayer)) return true;
                }

                be = byEntity.World.BlockAccessor.GetBlockEntity(onBlockPos.AddCopy(blockSel.Face));
                if (be is BlockEntityFirewoodPile)
                {
                    BlockEntityFirewoodPile pile = (BlockEntityFirewoodPile)be;
                    if (pile.OnPlayerInteract(byPlayer)) return true;
                }

                block = byEntity.World.GetBlock(new AssetLocation("firewoodpile"));
                if (block == null) return false;
                BlockPos pos = onBlockPos.AddCopy(blockSel.Face);
                bool ok = ((BlockFirewoodPile)block).Construct(slot, byEntity.World, pos, byPlayer);

                Cuboidf[] collisionBoxes = byEntity.World.BlockAccessor.GetBlock(pos).GetCollisionBoxes(byEntity.World.BlockAccessor, pos);

                if (collisionBoxes != null && collisionBoxes.Length > 0 && CollisionTester.AabbIntersect(collisionBoxes[0], pos.X, pos.Y, pos.Z, byPlayer.Entity.CollisionBox, byPlayer.Entity.LocalPos.XYZ))
                {
                    byPlayer.Entity.LocalPos.Y += collisionBoxes[0].Y2 - (byPlayer.Entity.LocalPos.Y - (int)byPlayer.Entity.LocalPos.Y);
                }

                return ok;

            }

        }
    }
}
