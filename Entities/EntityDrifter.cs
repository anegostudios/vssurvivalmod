using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent;

public class RemapAnimationManager : AnimationManager
{
    public Dictionary<string, string> Remaps = [];

    public string IdleAnimation = "crawlidle";

    public RemapAnimationManager()
    {

    }

    public RemapAnimationManager(Dictionary<string, string> remaps)
    {
        this.Remaps = remaps;
    }

    public override bool StartAnimation(string configCode)
    {
        if (Remaps.ContainsKey(configCode.ToLowerInvariant()))
        {
            configCode = Remaps[configCode];
        }

        StopIdle();

        return base.StartAnimation(configCode);
    }

    public override bool StartAnimation(AnimationMetaData animdata)
    {
        if (Remaps.ContainsKey(animdata.Animation))
        {
            animdata = animdata.Clone();
            animdata.Animation = Remaps[animdata.Animation];
            animdata.CodeCrc32 = AnimationMetaData.GetCrc32(animdata.Animation);
        }

        StopIdle();


        return base.StartAnimation(animdata);
    }

    public override void StopAnimation(string code)
    {
        base.StopAnimation(code);

        if (Remaps.ContainsKey(code))
        {
            base.StopAnimation(Remaps[code]);
        }
    }

    public override void TriggerAnimationStopped(string code)
    {
        base.TriggerAnimationStopped(code);

        if (entity.Alive && ActiveAnimationsByAnimCode.Count == 0)
        {
            StartAnimation(new AnimationMetaData() { Code = "idle", Animation = "idle", EaseOutSpeed = 10000, EaseInSpeed = 10000 });
        }
    }

    private void StopIdle()
    {
        StopAnimation(IdleAnimation);
    }
}

public class EntityDrifter : EntityHumanoid
{
    public EntityDrifter()
    {
    }

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        AnimationTrigger trigger = new() { OnControls = [EnumEntityActivity.Dead] };

        int oddsToAlter = properties.Attributes["oddsToAlter"].AsInt(5);
        string dieAnimationCode = properties.Attributes[" "].AsString("die");
        string alternativeDieAnimationCode = properties.Attributes["alternativeDieAnimationCode"].AsString("crawldie");

        if (EntityId % oddsToAlter == 0)
        {
            float[] collisionBox = properties.Attributes["alternativeCollisionBox"].AsArray([0.9f, 0.6f]);

            Dictionary<string, string> animationsRemapping = new()
            {
                { "idle", "crawlidle" },
                { "standwalk", "crawlwalk" },
                { "standlowwalk", "crawlwalk" },
                { "standrun", "crawlrun" },
                { "standidle", "crawlidle" },
                { "standdespair", "crawlemote" },
                { "standcry", "crawlemote" },
                { "standhurt", "crawlhurt" },
                { "standdie", "crawldie" }
            };

            if (properties.Attributes.KeyExists("animationsRemapping"))
            {
                animationsRemapping = properties.Attributes["animationsRemapping"].AsObject<Dictionary<string, string>>();
            }

            AnimationMetaData? dieAnimationMetaData = properties.Client.Animations.FirstOrDefault(a => a.Code == dieAnimationCode);
            AnimationMetaData? alternativeDieAnimationMetaData = properties.Client.Animations.FirstOrDefault(a => a.Code == alternativeDieAnimationCode);

            if (dieAnimationMetaData != null)
            {
                dieAnimationMetaData.TriggeredBy = null;
            }
            if (alternativeDieAnimationMetaData != null)
            {
                alternativeDieAnimationMetaData.TriggeredBy = trigger;
            }

            properties.CollisionBoxSize = new API.MathTools.Vec2f(collisionBox[0], collisionBox[1]);
            properties.SelectionBoxSize = new API.MathTools.Vec2f(collisionBox[0], collisionBox[1]);

            string idleAnimationCode = properties.Attributes["idleAnimationCode"].AsString("idle");
            string alternativeIdleAnimationCode = properties.Attributes["alternativeIdleAnimationCode"].AsString("crawlidle");

            AnimationMetaData? idleAnimationMetaData = properties.Client.Animations.FirstOrDefault(a => a.Code == idleAnimationCode);
            AnimationMetaData? alternativeIdleAnimationMetaData = properties.Client.Animations.FirstOrDefault(a => a.Code == alternativeIdleAnimationCode);

            if (idleAnimationMetaData != null)
            {
                idleAnimationMetaData.TriggeredBy = null;
            }
            if (alternativeIdleAnimationMetaData != null)
            {
                alternativeIdleAnimationMetaData.TriggeredBy = new AnimationTrigger() { DefaultAnim = true };
            }

            bool alternativeCanClimb = properties.Attributes["alternativeCanClimb"].AsBool(false);

            properties.CanClimb = alternativeCanClimb;

            AnimManager = new RemapAnimationManager(animationsRemapping) { IdleAnimation = alternativeIdleAnimationCode };
        }
        else
        {
            AnimationMetaData? dieAnimationMetaData = properties.Client.Animations.FirstOrDefault(a => a.Code == dieAnimationCode);
            AnimationMetaData? alternativeDieAnimationMetaData = properties.Client.Animations.FirstOrDefault(a => a.Code == alternativeDieAnimationCode);

            if (dieAnimationMetaData != null)
            {
                dieAnimationMetaData.TriggeredBy = trigger;
            }
            if (alternativeDieAnimationMetaData != null)
            {
                alternativeDieAnimationMetaData.TriggeredBy = null;
            }
        }

        base.Initialize(properties, api, InChunkIndex3d);
    }
}
