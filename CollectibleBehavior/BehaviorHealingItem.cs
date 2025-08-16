using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent;

public class HealOverTimeConfig
{
    public float Health { get; set; } = 1;
    public float ApplicationTimeSec { get; set; } = 2;
    public float MaxApplicationTimeSec { get; set; } = 10;
    public int Ticks { get; set; } = 10;
    public float EffectDurationSec { get; set; } = 10;
    public bool CancelInAir { get; set; } = true;
    public bool CancelWhileSwimming { get; set; } = false;
    public AssetLocation? Sound { get; set; } = new AssetLocation("game:sounds/player/poultice");
    public AssetLocation? AppliedSound { get; set; } = new AssetLocation("game:sounds/player/poultice-applied");
    public float SoundRange { get; set; } = 8;
    public bool CanRevive { get; set; } = true;
    public bool AffectedByArmor { get; set; } = true;
    public float DelayToCancelSec { get; set; } = 0.5f;
}

public class BehaviorHealingItem : CollectibleBehavior, ICanHealCreature
{
    public HealOverTimeConfig Config { get; set; } = new();

    protected IProgressBar? progressBarRender;
    protected ILoadedSound? applicationSound;
    protected ICoreAPI? api;

    protected float secondsUsedToCancel = 0;

    public BehaviorHealingItem(CollectibleObject collectable) : base(collectable)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        Config = properties.AsObject<HealOverTimeConfig>();
    }

    public override void OnLoaded(ICoreAPI api)
    {
        if (Config.Sound != null)
        {
            applicationSound = (api as ICoreClientAPI)?.World.LoadSound(new SoundParams() { DisposeOnFinish = false, Location = Config.Sound, ShouldLoop = true, Range = Config.SoundRange });
        }

        this.api = api;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (CancelApplication(byEntity))
        {
            return;
        }

        handHandling = EnumHandHandling.PreventDefault;
        handling = EnumHandling.PreventSubsequent;

        api?.World.RegisterCallback(_ => applicationSound?.Stop(), (int)GetApplicationTime(byEntity) * 1000);

        if (api?.Side == EnumAppSide.Client)
        {
            ModSystemProgressBar progressBarSystem = api.ModLoader.GetModSystem<ModSystemProgressBar>();
            progressBarSystem.RemoveProgressbar(progressBarRender);
            progressBarRender = progressBarSystem.AddProgressbar();
        }

        secondsUsedToCancel = 0;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (!CancelApplication(byEntity))
        {
            secondsUsedToCancel = 0;
        }
        else
        {
            if (secondsUsedToCancel == 0) secondsUsedToCancel = secondsUsed;
        }

        if (CancelApplication(byEntity) && secondsUsed - secondsUsedToCancel > 0.5)
        {
            return false;
        }

        if (applicationSound?.HasStopped == true)
        {
            applicationSound.Start();
        }

        applicationSound?.SetPosition((float)byEntity.Pos.X, (float)byEntity.Pos.InternalY, (float)byEntity.Pos.Z);

        handling = EnumHandling.Handled;

        float progress = secondsUsed / (GetApplicationTime(byEntity) + (byEntity.World.Side == EnumAppSide.Client ? 0.3f : 0));
        if (progressBarRender != null)
        {
            progressBarRender.Progress = progress;
        }
        return progress < 1;
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        applicationSound?.Stop();
        api?.ModLoader.GetModSystem<ModSystemProgressBar>()?.RemoveProgressbar(progressBarRender);
        return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, ref EnumHandling handling)
    {
        applicationSound?.Stop();
        api?.ModLoader.GetModSystem<ModSystemProgressBar>()?.RemoveProgressbar(progressBarRender);

        handling = EnumHandling.Handled;

        if (secondsUsed < GetApplicationTime(byEntity) || byEntity.World.Side != EnumAppSide.Server)
        {
            return;
        }

        Entity targetEntity = GetTargetEntity(slot, byEntity, entitySel);

        EntityBehaviorPlayerRevivable? revivableBehavior = targetEntity.GetBehavior<EntityBehaviorPlayerRevivable>();
        if (revivableBehavior != null && Config.CanRevive && !targetEntity.Alive)
        {
            revivableBehavior.AttemptRevive();
        }
        else
        {
            DamageSource damageSource = new()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal,
                DamageTier = 0,
                Duration = TimeSpan.FromSeconds(Config.EffectDurationSec),
                TicksPerDuration = Config.Ticks
            };

            targetEntity.ReceiveDamage(damageSource, Config.Health);
        }

        if (Config.AppliedSound != null)
        {
            byEntity.World.PlaySoundAt(Config.AppliedSound, byEntity, null, false, Config.SoundRange);
        }

        slot.TakeOut(1);
        slot.MarkDirty();
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        dsc.AppendLine(Lang.Get("healing-item-info", $"{Config.Health:F1}", $"{Config.EffectDurationSec:F1}", $"{Config.ApplicationTimeSec:F1}"));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        return [
            new()
            {
                ActionLangCode = "game:heldhelp-heal",
                MouseButton = EnumMouseButton.Right,
            }
        ];
    }

    public virtual WorldInteraction[] GetHealInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
    {
        return [
            new()
            {
                ActionLangCode = "heldhelp-heal",
                HotKeyCode = "ctrl",
                MouseButton = EnumMouseButton.Right,
            }
        ];
    }

    public virtual bool CanHeal(Entity target)
    {
        int minGenerationToAllowHealing = target.Properties.Attributes?["minGenerationToAllowHealing"].AsInt(-1) ?? -1;
        return target is EntityPlayer || (minGenerationToAllowHealing >= 0 && minGenerationToAllowHealing >= target.WatchedAttributes.GetInt("generation", 0));
    }

    protected virtual float GetApplicationTime(Entity byEntity)
    {
        float healingEffectiveness = 0;

        if (Config.AffectedByArmor)
        {
            healingEffectiveness = byEntity.Stats.GetBlended("healingeffectivness");
            healingEffectiveness = Math.Clamp(healingEffectiveness, 0, 2) - 1;
        }

        if (healingEffectiveness < 0)
        {
            return Config.ApplicationTimeSec + (Config.ApplicationTimeSec - Config.MaxApplicationTimeSec) * healingEffectiveness;
        }

        if (healingEffectiveness > 0)
        {
            return Config.ApplicationTimeSec * (1 - healingEffectiveness);
        }

        return Config.ApplicationTimeSec;
    }

    protected virtual Entity GetTargetEntity(ItemSlot slot, EntityAgent byEntity, EntitySelection? entitySelection)
    {
        Entity targetEntity = byEntity;
        Entity? selectedEntity = entitySelection?.Entity;

        if (selectedEntity == null)
        {
            return targetEntity;
        }

        EntityBehaviorHealth? selectedEntityHealthBehavior = selectedEntity.GetBehavior<EntityBehaviorHealth>();

        if (
            byEntity.Controls.CtrlKey &&
            !byEntity.Controls.Forward &&
            !byEntity.Controls.Backward &&
            !byEntity.Controls.Left &&
            !byEntity.Controls.Right &&
            selectedEntityHealthBehavior != null &&
            selectedEntityHealthBehavior.IsHealable(byEntity, slot))
        {
            targetEntity = selectedEntity;
        }

        return targetEntity;
    }

    protected virtual bool CancelApplication(Entity entity) => !entity.OnGround && !entity.Swimming && Config.CancelInAir || entity.Swimming && Config.CancelWhileSwimming;
}

