using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Item to easily add code for debugging purposes
    /// </summary>
    public sealed class ItemDebugTool : Item
    {
        private enum EnumItemDebugToolMode
        {
            AttackSelf,
            HealSelf
        }



        private EnumItemDebugToolMode mode = EnumItemDebugToolMode.AttackSelf;



        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.Handled;

            mode = (EnumItemDebugToolMode)(((int)mode + 1) % Enum.GetValues<EnumItemDebugToolMode>().Length);

            if (byEntity.Api is ICoreServerAPI serverApi && byEntity is EntityPlayer playerEntity && playerEntity.Player is IServerPlayer player)
            {
                serverApi.SendIngameError(player, $"Mode: {mode}", $"Mode: {mode}");
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            return true;
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.Handled;

            switch (mode)
            {
                case EnumItemDebugToolMode.AttackSelf:
                    attackSelf(slot, byEntity, blockSel, entitySel);
                    break;
                case EnumItemDebugToolMode.HealSelf:
                    healSelf(slot, byEntity, blockSel, entitySel);
                    break;
            }
        }

        public override bool OnHeldAttackStep(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySel)
        {
            return secondsPassed < 0.01;
        }



        private void attackSelf(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            DamageSource damageSource = new()
            {
                Source = EnumDamageSource.Entity,
                Type = EnumDamageType.SlashingAttack,
                DamageTier = 1,
                KnockbackStrength = 1,
                SourceEntity = byEntity,
                CauseEntity = byEntity,
                SourceBlock = null,
                SourcePos = null,
                HitPosition = byEntity.Pos.XYZ,
                Duration = TimeSpan.Zero,
                TicksPerDuration = 1,
                DamageOverTimeType = 0
            };

            byEntity.ReceiveDamage(damageSource, 10);
        }

        private void healSelf(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            DamageSource damageSource = new()
            {
                Source = EnumDamageSource.Player,
                Type = EnumDamageType.Heal,
                DamageTier = 2,
                KnockbackStrength = 1,
                SourceEntity = byEntity,
                CauseEntity = byEntity,
                SourceBlock = null,
                SourcePos = null,
                HitPosition = byEntity.Pos.XYZ,
                Duration = TimeSpan.Zero,
                TicksPerDuration = 1,
                DamageOverTimeType = 0
            };

            byEntity.ReceiveDamage(damageSource, 100);
        }
    }
}
