using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Newtonsoft.Json;

namespace Vintagestory.GameContent;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class CollectibleBehaviorHealingItem : CollectibleBehavior, ICanHealCreature
{
    [JsonProperty]
    public float Health { get; set; } = 1;
    [JsonProperty]
    public float ApplicationTimeSec { get; set; } = 2;
    [JsonProperty]
    public float MaxApplicationTimeSec { get; set; } = 10;
    [JsonProperty]
    public int Ticks { get; set; } = 10;
    [JsonProperty]
    public float EffectDurationSec { get; set; } = 10;
    [JsonProperty]
    public bool CancelInAir { get; set; } = true;
    [JsonProperty]
    public bool CancelWhileSwimming { get; set; } = false;
    [JsonProperty]
    public AssetLocation? Sound { get; set; } = new AssetLocation("game:sounds/player/poultice");
    [JsonProperty]
    public AssetLocation? AppliedSound { get; set; } = new AssetLocation("game:sounds/player/poultice-applied");
    [JsonProperty]
    public float SoundRange { get; set; } = 8;
    [JsonProperty]
    public bool CanRevive { get; set; } = true;
    [JsonProperty]
    public bool AffectedByArmor { get; set; } = true;
    [JsonProperty]
    public float DelayToCancelSec { get; set; } = 0.5f;

    protected IProgressBar? progressBarRender;
    protected ILoadedSound? applicationSound;
    protected ICoreAPI? api;

    protected float secondsUsedToCancel = 0;

    public CollectibleBehaviorHealingItem(CollectibleObject collectable) : base(collectable) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        if (properties.Exists)
        {
            JsonUtil.Populate<CollectibleBehaviorHealingItem>(properties.Token, this);
        }
    }

    public override void OnLoaded(ICoreAPI api)
    {
        if (Sound != null)
        {
            applicationSound = (api as ICoreClientAPI)?.World.LoadSound(new SoundParams() { DisposeOnFinish = false, Location = Sound, ShouldLoop = true, Range = SoundRange });
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

        float progress = secondsUsed / (GetApplicationTime(byEntity)); // + (byEntity.World.Side == EnumAppSide.Client ? 0.3f : 0) - i am not sure for what purpose, we already make sure XXXStop() gets called server side. And it causes github bug #6719
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
        if (revivableBehavior != null && CanRevive && !targetEntity.Alive)
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
                Duration = TimeSpan.FromSeconds(EffectDurationSec),
                TicksPerDuration = Ticks
            };

            targetEntity.ReceiveDamage(damageSource, Health);
        }

        if (AppliedSound != null)
        {
            byEntity.World.PlaySoundAt(AppliedSound, byEntity, null, false, SoundRange);
        }

        slot.TakeOut(1);
        slot.MarkDirty();
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        dsc.AppendLine(Lang.Get("healing-item-info", $"{Health:F1}", $"{EffectDurationSec:F1}", $"{ApplicationTimeSec:F1}"));
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
        return true;
    }

    protected virtual float GetApplicationTime(Entity byEntity)
    {
        float healingEffectiveness = 0;

        if (AffectedByArmor)
        {
            healingEffectiveness = byEntity.Stats.GetBlended("healingeffectivness");
            healingEffectiveness = Math.Clamp(healingEffectiveness, 0, 2) - 1;
        }

        if (healingEffectiveness < 0)
        {
            return ApplicationTimeSec + (ApplicationTimeSec - MaxApplicationTimeSec) * healingEffectiveness;
        }

        if (healingEffectiveness > 0)
        {
            return ApplicationTimeSec * (1 - healingEffectiveness);
        }

        return ApplicationTimeSec;
    }

    protected virtual Entity GetTargetEntity(ItemSlot slot, EntityAgent byEntity, EntitySelection? entitySelection)
    {
        Entity targetEntity = byEntity;
        Entity? selectedEntity = entitySelection?.Entity;

        if (selectedEntity == null)
        {
            return targetEntity;
        }

        EntityBehaviorHealth? healthBehavior = selectedEntity.GetBehavior<EntityBehaviorHealth>();

        if (
            byEntity.Controls.CtrlKey &&
            !byEntity.Controls.Forward &&
            !byEntity.Controls.Backward &&
            !byEntity.Controls.Left &&
            !byEntity.Controls.Right &&
            CanHeal(selectedEntity) &&
            healthBehavior != null &&
            healthBehavior.IsHealable(byEntity, slot))
        {
            targetEntity = selectedEntity;
        }

        return targetEntity;
    }

    protected virtual bool CancelApplication(Entity entity) => (!entity.OnGround && !entity.Swimming && CancelInAir) || (entity.Swimming && CancelWhileSwimming);
}

