using System;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorPassivePhysics : EntityBehavior
    {
        float accumulator;
        Vec3d outposition = new Vec3d();
        CollisionTester collisionTester = new CollisionTester();

        double waterDragValue = GlobalConstants.WaterDrag;
        double airDragValue = GlobalConstants.AirDragAlways;
        double groundDragFactor = 0.7f;
        double gravityPerSecond = GlobalConstants.GravityPerSecond;


        public EntityBehaviorPassivePhysics(Entity entity) : base(entity)
        {

        }

        public override void Initialize(EntityType config, JsonObject attributes)
        {
            waterDragValue = 1 - (1 - GlobalConstants.WaterDrag) * (float)attributes["waterDragFactor"].AsDouble(1);

            airDragValue = 1 - (1 - GlobalConstants.AirDragAlways) * (float)attributes["airDragFallingFactor"].AsDouble(1);

            groundDragFactor = 0.3 * (float)attributes["groundDragFactor"].AsDouble(1);

            gravityPerSecond = GlobalConstants.GravityPerSecond * (float)attributes["gravityFactor"].AsDouble(1f);            
        }


        public override void OnGameTick(float deltaTime)
        {
            if (entity.State == EnumEntityState.Inactive) return;

            EntityPos pos = entity.Pos;
            if (entity.World is IServerWorldAccessor)
            {
                pos = entity.ServerPos;
            }

            accumulator += deltaTime;

            if (accumulator > 1)
            {
                accumulator = 1;
            }

            float dt2 = 1f / 75;

            while (accumulator >= dt2)
            {
                DoPhysics(dt2, pos);
                accumulator -= dt2;
            }
        }

        public void DoPhysics(float dt, EntityPos pos)
        {
            Vec3d motionBefore = pos.Motion.Clone();
            bool feetInLiquidBefore = entity.FeetInLiquid;
            bool onGroundBefore = entity.OnGround;
            bool swimmingBefore = entity.Swimming;

            Block belowBlock = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)(pos.Y - 0.05f), (int)pos.Z);

            // On ground drag
            if (entity.OnGround) {
                if (!entity.FeetInLiquid)
                {
                    pos.Motion.X *= (1 - groundDragFactor * belowBlock.DragMultiplier);
                    pos.Motion.Z *= (1 - groundDragFactor * belowBlock.DragMultiplier);
                }
            }
            
            // Water or air drag
            if (entity.FeetInLiquid || entity.Swimming)
            {
                pos.Motion *= (float)Math.Pow(waterDragValue, dt * 33);
            } else
            {
                pos.Motion *= (float)Math.Pow(airDragValue, dt * 33);
            }
            

            // Gravity
            if (pos.Y > -100 && entity.ApplyGravity)
            {
                float fact = 1;
                if (entity.Swimming)
                {
                    Block aboveblock = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)(pos.Y + entity.SwimmingOffsetY + 1), (int)pos.Z);
                    
                    float swimmingDepth = Math.Min(1, 1 - (float)(pos.Y + entity.SwimmingOffsetY) + (int)(pos.Y + entity.SwimmingOffsetY) + (aboveblock.IsLiquid() ? 1 : 0));
                    fact = 1.5f * swimmingDepth * GameMath.Clamp(entity.MaterialDensity / aboveblock.MaterialDensity - 1, -0.1f, 1f) + (1 - swimmingDepth) / 70f;
                }

                pos.Motion.Y -= fact * (gravityPerSecond * dt + Math.Max(0, -0.015f * pos.Motion.Y));
            }

           
            Vec3d nextPosition = pos.XYZ + pos.Motion;

            bool falling = pos.Motion.Y < 0;

            collisionTester.ApplyTerrainCollision(entity, pos, ref outposition, true);

            


            if (entity.World.BlockAccessor.IsNotTraversable((int)(pos.X + pos.Motion.X), (int)pos.Y, (int)pos.Z))
            {
                outposition.X = pos.X;
            }
            if (entity.World.BlockAccessor.IsNotTraversable((int)pos.X, (int)(pos.Y + pos.Motion.Y), (int)pos.Z))
            {
                outposition.Y = pos.Y;
            }
            if (entity.World.BlockAccessor.IsNotTraversable((int)pos.X, (int)pos.Y, (int)(pos.Z + pos.Motion.Z)))
            {
                outposition.Z = pos.Z;
            }

            entity.OnGround = entity.CollidedVertically && falling;
            pos.SetPos(outposition);

           
            if ((nextPosition.X < outposition.X && pos.Motion.X < 0) || (nextPosition.X > outposition.X && pos.Motion.X > 0))
            {
                pos.Motion.X = 0;
            }

            if ((nextPosition.Y < outposition.Y && pos.Motion.Y < 0) || (nextPosition.Y > outposition.Y && pos.Motion.Y > 0))
            {
                pos.Motion.Y = 0;
            }

            if ((nextPosition.Z < outposition.Z && pos.Motion.Z < 0) || (nextPosition.Z > outposition.Z && pos.Motion.Z > 0))
            {
                pos.Motion.Z = 0;
            }

       

            Block block = entity.World.BlockAccessor.GetBlock(pos.AsBlockPos);
            entity.FeetInLiquid = block.MatterState == EnumMatterState.Liquid;

            block = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)(pos.Y + entity.SwimmingOffsetY), (int)pos.Z);
            entity.Swimming = block.IsLiquid();



            if (!onGroundBefore && entity.OnGround)
            {
                entity.OnFallToGround(motionBefore.Y);
            }
            if ((!entity.Swimming && !feetInLiquidBefore && entity.FeetInLiquid) || (!entity.FeetInLiquid && !swimmingBefore && entity.Swimming))
            {
                entity.OnCollideWithLiquid();
            }
            if (!falling || entity.OnGround)
            {
                entity.PositionBeforeFalling.Set(outposition);
            }

            if (GlobalConstants.OutsideWorld(pos.X, pos.Y, pos.Z, entity.World.BlockAccessor))
            {
                entity.DespawnReason = new EntityDespawnReason() { reason = EnumDespawnReason.Death, damageSourceForDeath = new DamageSource() { source = EnumDamageSource.Fall } };
                return;
            }

            Cuboidd entityBox = collisionTester.entityBox;
            for (int y = (int)entityBox.Y1; y <= (int)entityBox.Y2; y++)
            {
                for (int x = (int)entityBox.X1; x <= (int)entityBox.X2; x++)
                {
                    for (int z = (int)entityBox.Z1; z <= (int)entityBox.Z2; z++)
                    {
                        collisionTester.tmpPos.Set(x, y, z);
                        collisionTester.tempCuboid.Set(x, y, z, x + 1, y + 1, z + 1);
                        if (collisionTester.tempCuboid.IntersectsOrTouches(entityBox))
                            entity.World.BlockAccessor.GetBlock(x, y, z).OnEntityInside(entity.World, entity, collisionTester.tmpPos);
                    }
                }
            }
        }


        public override string PropertyName()
        {
            return "passiveentityphysics";
        }
    }
}
