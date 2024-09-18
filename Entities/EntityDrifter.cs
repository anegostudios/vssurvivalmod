using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public class RemapAnimationManager : AnimationManager
    {
        public Dictionary<string, string> remaps;

        public string idleAnim = "crawlidle";

        public RemapAnimationManager()
        {

        }

        public RemapAnimationManager(Dictionary<string, string> remaps)
        {
            this.remaps = remaps;
        }

        public override bool StartAnimation(string configCode)
        {
            if (remaps.ContainsKey(configCode.ToLowerInvariant()))
            {
                configCode = remaps[configCode];
            }

            StopIdle();

            return base.StartAnimation(configCode);
        }

        private void StopIdle()
        {
            StopAnimation(idleAnim);
        }

        public override bool StartAnimation(AnimationMetaData animdata)
        {
            if (remaps.ContainsKey(animdata.Animation))
            {
                animdata = animdata.Clone();
                animdata.Animation = remaps[animdata.Animation];
                animdata.CodeCrc32 = AnimationMetaData.GetCrc32(animdata.Animation);
            }

            StopIdle();


            return base.StartAnimation(animdata);
        }

        public override void StopAnimation(string code)
        {
            base.StopAnimation(code);

            if (remaps.ContainsKey(code))
            {
                base.StopAnimation(remaps[code]);
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

        public override void OnReceivedServerAnimations(int[] activeAnimations, int activeAnimationsCount, float[] activeAnimationSpeeds)
        {
            base.OnReceivedServerAnimations(activeAnimations, activeAnimationsCount, activeAnimationSpeeds);
        }
    }


    public class EntityDrifter : EntityHumanoid
    {
        public EntityDrifter()
        {
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            var trigger = new AnimationTrigger() { OnControls = new EnumEntityActivity[] { EnumEntityActivity.Dead } };

            if (EntityId % 5 == 0)
            {
                properties.Client.Animations.FirstOrDefault(a => a.Code == "die").TriggeredBy = null;
                properties.Client.Animations.FirstOrDefault(a => a.Code == "crawldie").TriggeredBy = trigger;

                properties.CollisionBoxSize = new API.MathTools.Vec2f(0.9f, 0.6f);
                properties.SelectionBoxSize = new API.MathTools.Vec2f(0.9f, 0.6f);

                properties.Client.Animations[2].TriggeredBy = null;
                properties.Client.Animations[5].TriggeredBy = new AnimationTrigger() { DefaultAnim = true };

                properties.CanClimb = false;

                AnimManager = new RemapAnimationManager(new Dictionary<string, string>()
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
                });
            } else
            {
                properties.Client.Animations.FirstOrDefault(a => a.Code == "die").TriggeredBy = trigger;
                properties.Client.Animations.FirstOrDefault(a => a.Code == "crawldie").TriggeredBy = null;
            }

            base.Initialize(properties, api, InChunkIndex3d);


        }
    }
}
