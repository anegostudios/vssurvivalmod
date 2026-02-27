using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent
{
    public class ModSystemStopRaiseShieldAnim : ModSystem
    {
        ICoreClientAPI? capi;
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI? api)
        {
            capi = api;
            if (capi == null) return;
            capi.Event.AfterActiveSlotChanged += _ => maybeStopRaiseShield();
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (capi == null) return;
            capi.World.Player.InventoryManager.GetHotbarInventory().SlotModified += _ => maybeStopRaiseShield();
        }

        private void maybeStopRaiseShield()
        {
            if (capi?.World?.Player == null) return;
            var entityPlayer = capi.World.Player.Entity;
            if (entityPlayer.RightHandItemSlot.Itemstack?.Item is not ItemShield && entityPlayer.AnimManager.IsAnimationActive(ItemShield.RaiseShieldRightAnim))
            {
                entityPlayer.AnimManager.StopAnimation(ItemShield.RaiseShieldRightAnim);
            }
        }
    }

    public class ItemShield : Item, IAttachableToEntity
    {
        protected IAttachableToEntity? AttachableToEntity;
        #region IAttachableToEntity
        public int RequiresBehindSlots { get; set; } = 0;
        string? IAttachableToEntity.GetCategoryCode(ItemStack stack) => AttachableToEntity?.GetCategoryCode(stack);
        CompositeShape? IAttachableToEntity.GetAttachedShape(ItemStack stack, string slotCode) => AttachableToEntity?.GetAttachedShape(stack, slotCode);
        string[]? IAttachableToEntity.GetDisableElements(ItemStack stack) => AttachableToEntity?.GetDisableElements(stack);
        string[]? IAttachableToEntity.GetKeepElements(ItemStack stack) => AttachableToEntity?.GetKeepElements(stack);
        string? IAttachableToEntity.GetTexturePrefixCode(ItemStack stack) => AttachableToEntity?.GetTexturePrefixCode(stack);
        void IAttachableToEntity.CollectTextures(ItemStack itemstack, Shape intoShape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
            => AttachableToEntity?.CollectTextures(itemstack, intoShape, texturePrefixCode, intoDict);
        public bool IsAttachable(Entity toEntity, ItemStack itemStack) => Attributes["isAttachable"].AsBool(true);

        #endregion
        public override void OnLoaded(ICoreAPI coreApi)
        {
            base.OnLoaded(coreApi);
            AttachableToEntity = IAttachableToEntity.FromAttributes(this);
        }

        public const string RaiseShieldLeftAnim  = "raiseshield-left";
        public const string RaiseShieldRightAnim = "raiseshield-right";

        private static void SetAnimActive(IAnimationManager anim, string name, bool active)
        {
            var isActive = anim.IsAnimationActive(name);
            if (active && !isActive) anim.StartAnimation(name);
            else if (!active && isActive) anim.StopAnimation(name);
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            var inLeftHand = byEntity.LeftHandItemSlot == slot;
            var animThisHand  = inLeftHand ? RaiseShieldLeftAnim  : RaiseShieldRightAnim;
            var animOtherHand = inLeftHand ? RaiseShieldRightAnim : RaiseShieldLeftAnim;
            var shouldRaise = byEntity.Controls.Sneak && !byEntity.Controls.RightMouseDown;
            SetAnimActive(byEntity.AnimManager, animThisHand,  shouldRaise);
            SetAnimActive(byEntity.AnimManager, animOtherHand, false);
            base.OnHeldIdle(slot, byEntity);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            var shieldAttributes = inSlot.Itemstack?.ItemAttributes?["shield"];
            if (shieldAttributes is not { Exists: true }) return;

            if (shieldAttributes["protectionChance"]["active-projectile"].Exists)
            {
                var activeProjectileProtChance = shieldAttributes["protectionChance"]["active-projectile"].AsFloat();
                var passiveProjectileProtChance = shieldAttributes["protectionChance"]["passive-projectile"].AsFloat();
                var projectileDamageAbsorption = shieldAttributes["projectileDamageAbsorption"].AsFloat();
                dsc.AppendLine("<strong>" + Lang.GetWithFallback("shield-projectile-protection", "Projectile protection") + "</strong>");
                dsc.AppendLine(Lang.Get("shield-stats", (int)(100 * activeProjectileProtChance), (int)(100 * passiveProjectileProtChance), projectileDamageAbsorption));
                dsc.AppendLine();
            }

            var damageAbsorption = shieldAttributes["damageAbsorption"].AsFloat();
            var activeProtChance = shieldAttributes["protectionChance"]["active"].AsFloat();
            var passiveProtChance = shieldAttributes["protectionChance"]["passive"].AsFloat();

            dsc.AppendLine("<strong>" + Lang.GetWithFallback("shield-melee-protection", "Melee attack protection") + "</strong>");
            dsc.AppendLine(Lang.Get("shield-stats", (int)(100 * activeProtChance), (int)(100 * passiveProtChance), damageAbsorption));
            dsc.AppendLine();
        }
    }
}
