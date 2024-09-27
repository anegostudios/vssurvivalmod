using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Vintagestory.GameContent
{
    public class PersonalizedAnimationManager : AnimationManager
    {
        public string Personality;
        public HashSet<string> PersonalizedAnimations = new HashSet<string>(new string[] { "welcome", "idle", "walk", "run", "attack", "laugh", "hurt", "nod", "idle2" });

        public bool All;

        public override bool StartAnimation(string configCode)
        {
            if (PersonalizedAnimations.Contains(configCode.ToLowerInvariant()))
            {
                if (Personality == "formal" || Personality == "rowdy" || Personality == "lazy")
                {
                    StopAnimation(Personality + "-idle");
                    StopAnimation(Personality + "-idle2");
                }

                return StartAnimation(new AnimationMetaData()
                {
                    Animation = Personality + "-" + configCode,
                    Code = Personality + "-" + configCode,
                    BlendMode = EnumAnimationBlendMode.Average,
                    EaseOutSpeed = 10000,
                    EaseInSpeed = 10000
                }.Init());
            }

            return base.StartAnimation(configCode);
        }

        public override bool StartAnimation(AnimationMetaData animdata)
        {
            if ((animdata.Code == "idle2" || animdata.Code == "laugh") && ActiveAnimationsByAnimCode.ContainsKey(Personality + "-welcome")) return false;

            if (Personality == "formal" || Personality == "rowdy" || Personality == "lazy")
            {
                StopAnimation(Personality + "-idle");
                StopAnimation(Personality + "-laugh");
                StopAnimation(Personality + "-idle2");
            }

            if (All | PersonalizedAnimations.Contains(animdata.Animation.ToLowerInvariant()))
            {
                animdata = animdata.Clone();
                animdata.Animation = Personality + "-" + animdata.Animation;
                animdata.Code = animdata.Animation;
                animdata.CodeCrc32 = AnimationMetaData.GetCrc32(animdata.Code);

            }

            return base.StartAnimation(animdata);
        }

        public override void StopAnimation(string code)
        {
            base.StopAnimation(code);
            base.StopAnimation(Personality + "-" + code);
        }

        public override void TriggerAnimationStopped(string code)
        {
            base.TriggerAnimationStopped(code);

            if (entity.Alive && ActiveAnimationsByAnimCode.Count == 0)
            {
                StartAnimation(new AnimationMetaData() { Code = "idle", Animation = "idle", EaseOutSpeed = 10000, EaseInSpeed = 10000 });
            }
        }

    }


    public class TraderPersonality
    {
        public float ChorldDelayMul = 1;
        public float PitchModifier = 1;
        public float VolumneModifier = 1;

        public TraderPersonality(float chordDelayMul, float pitchModifier, float volumneModifier)
        {
            ChorldDelayMul = chordDelayMul;
            PitchModifier = pitchModifier;
            VolumneModifier = volumneModifier;
        }
    }

}
