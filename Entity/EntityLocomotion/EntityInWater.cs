using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class EntityInWater : EntityLocomotion
    {
        public long lastWaterJump = 0;
        public long lastPush = 0;

        float push;

        public override bool Applicable(Entity entity, EntityPos pos, EntityControls controls)
        {
            return entity.FeetInLiquid;
        }

        public override void DoApply(float dt, Entity entity, EntityPos pos, EntityControls controls)
        {
            
            if (entity.Swimming)
            {
                if ((controls.TriesToMove || controls.Jump) && entity.World.ElapsedMilliseconds - lastPush > 2000)
                {
                    push = 8f;
                    lastPush = entity.World.ElapsedMilliseconds;
                    string playerUID = entity is EntityPlayer ? ((EntityPlayer)entity).PlayerUID : null;
                    entity.PlayEntitySound("swim", playerUID == null ? null : entity.World.PlayerByUid(playerUID));
                    EntityAgent.SplashParticleProps.BasePos.Set(pos.X, pos.Y + 0.25f, pos.Z);
                    entity.World.SpawnParticles(EntityAgent.AirBubbleParticleProps);
                }
                else
                {
                    push = Math.Max(1, push - 0.1f);
                }

                double yMot = 0;
                if (controls.Jump)
                {
                    yMot = push * 0.001f;
                } else
                {
                    yMot = controls.FlyVector.Y * (1 + push) * 0.03f;
                }


                pos.Motion.Add(
                    controls.FlyVector.X * (1+push) * 0.03f, 
                    yMot,
                    controls.FlyVector.Z * (1 + push) * 0.03f
                );


            }

            
            // http://fooplot.com/plot/kg6l1ikyx2
            /*float x = entity.Pos.Motion.Length();
            if (x > 0)
            {
                pos.Motion.Normalize();
                pos.Motion *= (float)Math.Log(x + 1) / 1.5f;

            }*/
        }
    }
}
