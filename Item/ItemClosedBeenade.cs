using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemClosedBeenade : Item
    {
        protected AssetLocation thrownEntityTypeCode;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            string defaultCode = "thrownbeenade";
            thrownEntityTypeCode = AssetLocation.Create(Attributes?["thrownEntityTypeCode"].AsString(defaultCode) ?? defaultCode, Code.Domain);
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity byEntity)
        {
            return null;
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null && byEntity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockSkep) return;

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.Attributes.SetInt("aiming", 1);
            byEntity.AnimManager.StartAnimation("aim");

            handling = EnumHandHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.EnsureDefaultValues();

                float offset = GameMath.Clamp(secondsUsed * 3, 0, 2f);

                tf.Translation.Set(offset, -offset / 4f, 0);
            }

            return true;
        }


        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (byEntity.Attributes.GetInt("aiming") == 0) return;

            byEntity.Attributes.SetInt("aiming", 0);
            byEntity.StopAnimation("aim");

            if (secondsUsed < 0.35f) return;

            float damage = 0.5f;

            ItemStack stack = slot.TakeOut(1);
            slot.MarkDirty();

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/throw"), byEntity, byPlayer, false, 8);

            EntityProperties type = byEntity.World.GetEntityType(thrownEntityTypeCode);
            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);
            ((EntityThrownBeenade)entity).FiredBy = byEntity;
            ((EntityThrownBeenade)entity).Damage = damage;
            ((EntityThrownBeenade)entity).ProjectileStack = stack;
            

            EntityProjectile.SpawnThrownEntity(entity, byEntity, 0.75, -0.2, 0, 0.5, 0.21, 0.25);
            byEntity.StartAnimation("throw");

            if (byEntity is EntityPlayer) RefillSlotIfEmpty(slot, byEntity, (itemstack) => itemstack.Collectible.Code == Code);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (inSlot.Itemstack.Collectible.Attributes == null) return;
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-throw",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}