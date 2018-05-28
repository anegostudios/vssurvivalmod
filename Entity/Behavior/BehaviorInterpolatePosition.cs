using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorInterpolatePosition : EntityBehavior
    {
        double posDiffX, posDiffY, posDiffZ;
        double yawDiff, rollDiff, pitchDiff;

        long lastServerPosMilliseconds = -600;

        long lastGameMs;

        public EntityBehaviorInterpolatePosition(Entity entity) : base(entity)
        {
        }

        public override void OnGameTick(float deltaTime)
        {
            long nowEllapsedMillisecond = entity.World.ElapsedMilliseconds;
            long serverPosAgeMs = nowEllapsedMillisecond - lastServerPosMilliseconds;

            // Lag. Stop extrapolation (extrapolation begins after 200ms)
            if (serverPosAgeMs > 220)
            {
                return;
            }

            //if (entity.ServerPos.Motion.LengthSq() == 0) return;

            double percent = (nowEllapsedMillisecond - lastGameMs) / 200.0;

            int signPX = Math.Sign(posDiffX);
            int signPY = Math.Sign(posDiffY);
            int signPZ = Math.Sign(posDiffZ);

            entity.Pos.X += GameMath.Clamp(entity.ServerPos.X - entity.Pos.X, -signPX * percent * posDiffX, signPX * percent * posDiffX);
            entity.Pos.Y += GameMath.Clamp(entity.ServerPos.Y - entity.Pos.Y, -signPY * percent * posDiffY, signPY * percent * posDiffY);
            entity.Pos.Z += GameMath.Clamp(entity.ServerPos.Z - entity.Pos.Z, -signPZ * percent * posDiffZ, signPZ * percent * posDiffZ);

            int signR = Math.Sign(rollDiff);
            int signY = Math.Sign(yawDiff);
            int signP = Math.Sign(pitchDiff);

            // Dunno why the 0.7, but it's too fast otherwise
            entity.Pos.Roll += 0.7f * (float)GameMath.Clamp(entity.ServerPos.Roll - entity.Pos.Roll, -signR * percent * rollDiff, signR * percent * rollDiff);
            entity.Pos.Yaw += 0.7f * (float)GameMath.Clamp(GameMath.AngleRadDistance(entity.Pos.Yaw, entity.ServerPos.Yaw), -signY * percent * yawDiff, signY * percent * yawDiff);
            entity.Pos.Yaw = entity.Pos.Yaw % GameMath.TWOPI;


            entity.Pos.Pitch += 0.7f * (float)GameMath.Clamp(GameMath.AngleRadDistance(entity.Pos.Pitch, entity.ServerPos.Pitch), -signP * percent * pitchDiff, signP * percent * pitchDiff);
            entity.Pos.Pitch = entity.Pos.Pitch % GameMath.TWOPI;

            lastGameMs = nowEllapsedMillisecond;
        }


        public override void OnReceivedServerPos(ref EnumHandling handled)
        {
            // Don't interpolate for ourselves
            if (entity == ((IClientWorldAccessor)entity.World).Player.Entity) return;

            posDiffX = entity.ServerPos.X - entity.Pos.X;
            posDiffY = entity.ServerPos.Y - entity.Pos.Y;
            posDiffZ = entity.ServerPos.Z - entity.Pos.Z;

            rollDiff = entity.ServerPos.Roll - entity.Pos.Roll;
            yawDiff = entity.ServerPos.Yaw - entity.Pos.Yaw;
            pitchDiff = entity.ServerPos.Pitch - entity.Pos.Pitch;

            lastServerPosMilliseconds = entity.World.ElapsedMilliseconds;
            lastGameMs = entity.World.ElapsedMilliseconds;
             
            handled = EnumHandling.PreventDefault;

            //Console.WriteLine("got " + entity.ServerPos.XYZ + " for entity id "+entity.EntityId+" at " + lastServerPosMilliseconds);
        }

        public override string PropertyName()
        {
            return "lerppos";
        }
    }
}
