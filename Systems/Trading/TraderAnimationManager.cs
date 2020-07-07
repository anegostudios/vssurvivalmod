using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class TraderAnimationManager : AnimationManager
    {
        public string Personality;
        public HashSet<string> PersonalizedAnimations = new HashSet<string>(new string[] { "welcome", "idle", "walk", "run", "attack", "laugh", "hurt", "nod", "idle2" });



        public override bool StartAnimation(string configCode)
        {
            if (PersonalizedAnimations.Contains(configCode.ToLowerInvariant()))
            {
                if (Personality == "formal" || Personality == "rowdy" || Personality == "lazy")
                {
                    StopAnimation(Personality + "idle");
                    StopAnimation(Personality + "idle2");
                }

                return StartAnimation(new AnimationMetaData()
                {
                    Animation = Personality + configCode,
                    Code = Personality + configCode,
                    BlendMode = EnumAnimationBlendMode.Average,
                    EaseOutSpeed = 10000,
                    EaseInSpeed = 10000
                }.Init());
            }

            return base.StartAnimation(configCode);
        }

        public override bool StartAnimation(AnimationMetaData animdata)
        {
            if (Personality == "formal" || Personality == "rowdy" || Personality == "lazy")
            {
                StopAnimation(Personality + "idle");
                StopAnimation(Personality + "idle2");
            }

            if (PersonalizedAnimations.Contains(animdata.Animation.ToLowerInvariant()))
            {
                animdata = animdata.Clone();
                animdata.Animation = Personality + animdata.Animation;
                animdata.Code = animdata.Animation;
                animdata.CodeCrc32 = AnimationMetaData.GetCrc32(animdata.Code);

            }

            return base.StartAnimation(animdata);
        }

        public override void StopAnimation(string code)
        {
            base.StopAnimation(code);
            base.StopAnimation(Personality + code);
        }

        public override void OnAnimationStopped(string code)
        {
            base.OnAnimationStopped(code);

            if (entity.Alive && ActiveAnimationsByAnimCode.Count == 0)
            {
                StartAnimation(new AnimationMetaData() { Code = "idle", Animation = "idle", EaseOutSpeed = 10000, EaseInSpeed = 10000 });
            }
        }

    }


    public class TraderPersonality
    {
        public float TalkSpeedModifier = 1;
        public float PitchModifier = 1;
        public float VolumneModifier = 1;

        public TraderPersonality(float talkSpeedModifier, float pitchModifier, float volumneModifier)
        {
            TalkSpeedModifier = talkSpeedModifier;
            PitchModifier = pitchModifier;
            VolumneModifier = volumneModifier;
        }
    }

}
