using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemArmorStand : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);

            var x = blockSel.FullPosition.X;
            var y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
            var z = blockSel.FullPosition.Z;

            var blockPos = new BlockPos((int)x, y, (int)z);
            if (!byEntity.World.Claims.TryAccess(player, blockPos, EnumBlockAccessFlags.BuildOrBreak))
            {
                slot.MarkDirty();
                return;
            }

            if (!(byEntity is EntityPlayer) || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            EntityProperties type = byEntity.World.GetEntityType(this.Code);
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.Pos.X = x;
                entity.Pos.Y = y;
                entity.Pos.Z = z;
                entity.Pos.Yaw = byEntity.Pos.Yaw + GameMath.PIHALF;
                if (player?.PlayerUID != null)
                {
                    entity.WatchedAttributes.SetString("ownerUid", player.PlayerUID);
                }

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/block/torch"), entity, player);

                byEntity.World.SpawnEntity(entity);
                handling = EnumHandHandling.PreventDefaultAction;
            }

        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "heldhelp-place",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
