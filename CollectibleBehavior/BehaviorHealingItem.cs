using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent;

#nullable disable

public class HealOverTimeConfig
{
    public float Health { get; set; } = 1;
    public float ApplicationTimeSec { get; set; } = 2;
    public float MaxApplicationTimeSec { get; set; } = 10;
    public int Ticks { get; set; } = 10;
    public float EffectDurationSec { get; set; } = 10;
    public bool CancelInAir { get; set; } = true;
}



public class BehaviorHealingItem : CollectibleBehavior, ICanHealCreature
{
    IProgressBar renderer;

    public BehaviorHealingItem(CollectibleObject collectable) : base(collectable)
    {
    }

    public HealOverTimeConfig Config { get; set; } = new();

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);

        Config = properties.AsObject<HealOverTimeConfig>();
    }

    public override void OnLoaded(ICoreAPI api)
    {
        applicationSound = (api as ICoreClientAPI)?.World.LoadSound(new SoundParams() { DisposeOnFinish = false, Location = new AssetLocation("game:sounds/player/poultice"), ShouldLoop = true, Range = 8 });
        this.api = api;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        handHandling = EnumHandHandling.PreventDefault;
        handling = EnumHandling.PreventSubsequent;

        api.World.RegisterCallback(_ => applicationSound?.Stop(), (int)GetApplicationTime(byEntity) * 1000);

        if (api.Side == EnumAppSide.Client)
        {
            var mspb = api.ModLoader.GetModSystem<ModSystemProgressBar>();
            mspb.RemoveProgressbar(renderer);
            renderer = mspb.AddProgressbar();
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (Config.CancelInAir && !byEntity.OnGround)
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
        if (renderer != null)
        {
            renderer.Progress = progress;
        }
        return progress < 1;
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        applicationSound?.Stop();
        api.ModLoader.GetModSystem<ModSystemProgressBar>()?.RemoveProgressbar(renderer);
        return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, ref EnumHandling handling)
    {
        applicationSound?.Stop();
        api.ModLoader.GetModSystem<ModSystemProgressBar>()?.RemoveProgressbar(renderer);

        handling = EnumHandling.Handled;

        if (secondsUsed < GetApplicationTime(byEntity) || byEntity.World.Side != EnumAppSide.Server)
        {
            return;
        }

        Entity targetEntity = getTargetEntity(slot, byEntity, entitySel);

        var hotBh = targetEntity.GetBehavior<BehaviorDamageOverTime>();
        hotBh.ApplyEffect(
            EnumDamageSource.Internal,
            EnumDamageType.Heal,
            0,
            Config.Health,
            TimeSpan.FromSeconds(Config.EffectDurationSec),
            Config.Ticks
        );

        byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/poultice-applied"), byEntity, null, false, 8);

        slot.TakeOut(1);
        slot.MarkDirty();
    }

    private static Entity getTargetEntity(ItemSlot slot, EntityAgent byEntity, EntitySelection? entitySel)
    {
        Entity targetEntity = byEntity;
        Entity selectedEntity = entitySel?.Entity;

        if (selectedEntity != null)
        {
            EntityBehaviorHealth? selectedEntityHealthBehavior = selectedEntity.GetBehavior<EntityBehaviorHealth>();

            if (
                byEntity.Controls.CtrlKey &&
                !byEntity.Controls.Forward &&
                !byEntity.Controls.Backward &&
                !byEntity.Controls.Left &&
                !byEntity.Controls.Right &&
                selectedEntityHealthBehavior.IsHealable(byEntity, slot))
            {
                targetEntity = selectedEntity;
            }
        }

        return targetEntity;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        dsc.AppendLine(Lang.Get("healing-item-info", $"{Config.Health:F1}", $"{Config.EffectDurationSec:F1}", $"{Config.ApplicationTimeSec:F1}"));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        return new WorldInteraction[] {
            new()
            {
                ActionLangCode = "game:heldhelp-heal",
                MouseButton = EnumMouseButton.Right,
            }
        };
    }

    public bool CanHeal(Entity entity)
    {
        int minGenerationToAllowHealing = entity.Properties.Attributes?["minGenerationToAllowHealing"].AsInt(-1) ?? -1;
        return minGenerationToAllowHealing >= 0 && minGenerationToAllowHealing >= entity.WatchedAttributes.GetInt("generation", 0);
    }

    public WorldInteraction[] GetHealInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
    {
        return new WorldInteraction[] {
            new()
            {
                ActionLangCode = "heldhelp-heal",
                HotKeyCode = "sprint",
                MouseButton = EnumMouseButton.Right,
            }
        };
    }

    private ILoadedSound? applicationSound;
    private ICoreAPI api = null!;

    private float GetApplicationTime(Entity byEntity)
    {
        float healingEffectiveness = byEntity.Stats.GetBlended("healingeffectivness");
        healingEffectiveness = Math.Clamp(healingEffectiveness, 0, 2) - 1;

        if (healingEffectiveness < 0)
        {
            return Config.ApplicationTimeSec + (Config.MaxApplicationTimeSec - Config.ApplicationTimeSec) * (-healingEffectiveness);
        }

        if (healingEffectiveness > 0)
        {
            return Config.ApplicationTimeSec * (1 - healingEffectiveness);
        }

        return Config.ApplicationTimeSec;
    }
}

