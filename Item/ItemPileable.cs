using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public abstract class ItemPileable : Item
    {
        protected abstract AssetLocation PileBlockCode { get; }

        public virtual bool IsPileable => true;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!IsPileable)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            BlockPos onBlockPos = blockSel.Position;
            
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;

            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(onBlockPos);

            if (be is BlockEntityLabeledChest || be is BlockEntitySignPost || be is BlockEntitySign || be is BlockEntityBloomery || be is BlockEntityFirepit || be is BlockEntityForge || be is BlockEntityCrate) return;

            if (be is IBlockEntityItemPile)
            {
                IBlockEntityItemPile pile = (IBlockEntityItemPile)be;
                if (pile.OnPlayerInteract(byPlayer))
                {
                    handling = EnumHandHandling.PreventDefaultAction;

                    ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return;
                }
            }

            be = byEntity.World.BlockAccessor.GetBlockEntity(onBlockPos.AddCopy(blockSel.Face));
            if (be is IBlockEntityItemPile)
            {
                IBlockEntityItemPile pile = (IBlockEntityItemPile)be;
                if (pile.OnPlayerInteract(byPlayer))
                {
                    handling = EnumHandHandling.PreventDefaultAction;

                    ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                    return;
                }
            }

            Block block = byEntity.World.GetBlock(PileBlockCode);
            if (block == null) return;
            BlockPos pos = onBlockPos.Copy();
            if (byEntity.World.BlockAccessor.GetBlock(pos).Replaceable < 6000) pos.Add(blockSel.Face);

            bool ok = ((IBlockItemPile)block).Construct(slot, byEntity.World, pos, byPlayer);

            Cuboidf[] collisionBoxes = byEntity.World.BlockAccessor.GetBlock(pos).GetCollisionBoxes(byEntity.World.BlockAccessor, pos);

            if (collisionBoxes != null && collisionBoxes.Length > 0 && CollisionTester.AabbIntersect(collisionBoxes[0], pos.X, pos.Y, pos.Z, byPlayer.Entity.SelectionBox, byPlayer.Entity.SidedPos.XYZ))
            {
                byPlayer.Entity.SidedPos.Y += collisionBoxes[0].Y2 - (byPlayer.Entity.SidedPos.Y - (int)byPlayer.Entity.SidedPos.Y);
            }

            if (ok)
            {
                handling = EnumHandHandling.PreventDefaultAction;
                ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            } else
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            }
            

        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (!IsPileable)
            {
                return base.GetHeldInteractionHelp(inSlot);
            }

            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    HotKeyCode = "sneak",
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }


    }
}
