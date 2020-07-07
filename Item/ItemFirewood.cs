using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    class ItemFirewood : Item
    {

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity?.World == null || !byEntity.Controls.Sneak) return;

            BlockPos onBlockPos = blockSel.Position;
            Block block = byEntity.World.BlockAccessor.GetBlock(onBlockPos);

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            if (byPlayer == null) return;


            if (!byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }


            

            if (block is BlockFirepit)
            {
                // Prevent placing firewoodpiles when trying to construct firepits
                return;
            } else
            {

                BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(onBlockPos);
                if (be is BlockEntityFirewoodPile)
                {
                    BlockEntityFirewoodPile pile = (BlockEntityFirewoodPile)be;
                    if (pile.OnPlayerInteract(byPlayer))
                    {
                        handling = EnumHandHandling.PreventDefaultAction;

                        ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                        return;
                    }
                }

                be = byEntity.World.BlockAccessor.GetBlockEntity(onBlockPos.AddCopy(blockSel.Face));
                if (be is BlockEntityFirewoodPile)
                {
                    BlockEntityFirewoodPile pile = (BlockEntityFirewoodPile)be;
                    if (pile.OnPlayerInteract(byPlayer))
                    {
                        handling = EnumHandHandling.PreventDefaultAction;

                        ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                        return;
                    }
                }

                block = byEntity.World.GetBlock(new AssetLocation("firewoodpile"));
                if (block == null) return;
                BlockPos pos = onBlockPos.Copy();
                if (byEntity.World.BlockAccessor.GetBlock(pos).Replaceable < 6000) pos.Add(blockSel.Face);

                bool ok = ((BlockFirewoodPile)block).Construct(slot, byEntity.World, pos, byPlayer);

                Cuboidf[] collisionBoxes = byEntity.World.BlockAccessor.GetBlock(pos).GetCollisionBoxes(byEntity.World.BlockAccessor, pos);

                if (collisionBoxes != null && collisionBoxes.Length > 0 && CollisionTester.AabbIntersect(collisionBoxes[0], pos.X, pos.Y, pos.Z, byPlayer.Entity.CollisionBox, byPlayer.Entity.SidedPos.XYZ))
                {
                    byPlayer.Entity.SidedPos.Y += collisionBoxes[0].Y2 - (byPlayer.Entity.SidedPos.Y - (int)byPlayer.Entity.SidedPos.Y);
                }

                if (ok)
                {
                    handling = EnumHandHandling.PreventDefaultAction;
                    ((byEntity as EntityPlayer)?.Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                }
            }

        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
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
