using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorPlayerPhysics : EntityBehaviorControlledPhysics
    {
        public EntityBehaviorPlayerPhysics(EntityPlayer entity) : base(entity)
        {
        }

        public override void Initialize(EntityType config, JsonObject typeAttributes)
        {
            base.Initialize(config, typeAttributes);
        }

        public override void OnGameTick(float deltaTime)
        {
            accumulator += deltaTime;

            if (accumulator > 1)
            {
                accumulator = 1;
            }

            //Console.WriteLine(deltaTime);

            while (accumulator >= GlobalConstants.PhysicsFrameTime)
            {
                ((EntityPlayer)entity).PhysicsUpdateWatcher?.Invoke(accumulator - GlobalConstants.PhysicsFrameTime);
                GameTick(entity, GlobalConstants.PhysicsFrameTime);
                accumulator -= GlobalConstants.PhysicsFrameTime;
            }
        }

        public override void GameTick(Entity entity, float dt)
        {
            EntityPlayer entityplayer = entity as EntityPlayer;
            EntityControls controls = entityplayer.Controls;

            string playerUID = entity.WatchedAttributes.GetString("playerUID");
            IPlayer player = entity.World.PlayerByUid(playerUID);
            if (entity.World is IServerWorldAccessor && ((IServerPlayer)player).ConnectionState != EnumClientState.Playing) return;

            if (player != null)
            {
                IClientWorldAccessor clientWorld = entity.World as IClientWorldAccessor;

                // We pretend the entity is flying to disable gravity so that EntityBehaviorInterpolatePosition system 
                // can work better
                controls.IsFlying = player.WorldData.FreeMove || (clientWorld != null && clientWorld.Player.ClientId != player.ClientId);
                controls.NoClip = player.WorldData.NoClip;
                controls.MovespeedMultiplier = player.WorldData.MoveSpeedMultiplier;
            }

            EntityPos pos = entity.World is IServerWorldAccessor ? entity.ServerPos : entity.Pos;

            


            if (controls.TriesToMove && player is IClientPlayer)
            {
                IClientPlayer cplr = player as IClientPlayer;

                float prevYaw = pos.Yaw;

                if (entity.Swimming)
                {
                    float prevPitch = pos.Pitch;
                    pos.Yaw = cplr.CameraYaw;
                    pos.Pitch = cplr.CameraPitch;
                    controls.CalcMovementVectors(pos, dt);
                    pos.Yaw = prevYaw;
                    pos.Pitch = prevPitch;
                }
                else
                {
                    pos.Yaw = cplr.CameraYaw;
                    controls.CalcMovementVectors(pos, dt);
                    pos.Yaw = prevYaw;
                }

                float desiredYaw = (float)Math.Atan2(controls.WalkVector.X, controls.WalkVector.Z) - GameMath.PIHALF;
                
                float yawDist = GameMath.AngleRadDistance(entityplayer.WalkYaw, desiredYaw);
                entityplayer.WalkYaw += GameMath.Clamp(yawDist, -10 * dt, 10 * dt);
                entityplayer.WalkYaw = GameMath.Mod(entityplayer.WalkYaw, GameMath.TWOPI);

                if (entity.Swimming)
                {
                    float desiredPitch = -(float)Math.Sin(pos.Pitch); // (float)controls.FlyVector.Y * GameMath.PI;
                    float pitchDist = GameMath.AngleRadDistance(entityplayer.WalkPitch, desiredPitch);
                    entityplayer.WalkPitch += GameMath.Clamp(pitchDist, -2 * dt, 2 * dt);
                    entityplayer.WalkPitch = GameMath.Mod(entityplayer.WalkPitch, GameMath.TWOPI);

                    //Console.WriteLine(entityplayer.WalkPitch);
                } else
                {
                    entityplayer.WalkPitch = 0;
                }
            } else
            {

                controls.CalcMovementVectors(pos, dt);
            }
            
            TickEntityPhysics(pos, controls, dt);
        }
    }
}
