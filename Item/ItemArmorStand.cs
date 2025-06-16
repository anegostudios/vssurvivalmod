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


            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
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
                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = byEntity.SidedPos.Yaw + GameMath.PIHALF;
                if (player?.PlayerUID != null)
                {
                    entity.WatchedAttributes.SetString("ownerUid", player.PlayerUID);
                }
                
                entity.Pos.SetFrom(entity.ServerPos);

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