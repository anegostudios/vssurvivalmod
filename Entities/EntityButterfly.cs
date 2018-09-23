using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityButterfly : EntityAgent
    {
        static EntityButterfly() {
            AiTaskManager.RegisterTaskType("butterflywander", typeof(AiTaskButterflyWander));
        }

        public override bool IsInteractable => false;

        public override void OnGameTick(float dt)
        {
            if (World.Side == EnumAppSide.Server)
            {
                base.OnGameTick(dt);
                return;
            }

            if (ServerPos.Y < Pos.Y - 0.25 && !Collided)
            {
                SetAnimation("glide", 1);
            } else
            {
                SetAnimation("fly", 2);
            }
            
            base.OnGameTick(dt);

            if (ServerPos.SquareDistanceTo(Pos.XYZ) > 0.01)
            {
                float desiredYaw = (float)Math.Atan2(ServerPos.X - Pos.X, ServerPos.Z - Pos.Z);

                float yawDist = GameMath.AngleRadDistance(LocalPos.Yaw, desiredYaw);
                Pos.Yaw += GameMath.Clamp(yawDist, -10 * dt, 10 * dt);
                Pos.Yaw = Pos.Yaw % GameMath.TWOPI;
            }
        }

        private void SetAnimation(string animCode, float speed)
        {
            AnimationMetaData animMeta = null;
            if (!ActiveAnimationsByAnimCode.TryGetValue(animCode, out animMeta))
            {
                animMeta = new AnimationMetaData()
                {
                    Code = animCode,
                    Animation = animCode,
                    AnimationSpeed = speed,                   
                };

                ActiveAnimationsByAnimCode.Clear();
                ActiveAnimationsByAnimCode[animMeta.Animation] = animMeta;
                return;
            }

            animMeta.AnimationSpeed = speed;
        }

        public override void OnReceivedServerAnimations(int[] activeAnimations, int activeAnimationsCount, float[] activeAnimationSpeeds)
        {
            // We control animations entirely client side
        }
    }
}
