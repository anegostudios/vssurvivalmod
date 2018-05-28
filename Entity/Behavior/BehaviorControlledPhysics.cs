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
    public class EntityBehaviorControlledPhysics : EntityBehavior
    {
        protected float accumulator = 0;
        protected Vec3d outposition = new Vec3d(); // Temporary field
        protected CollisionTester collisionTester = new CollisionTester();

        internal List<EntityLocomotion> Locomotors = new List<EntityLocomotion>();

        protected float stepHeight = 0.6f;

        Cuboidf smallerCollisionBox = new Cuboidf();


        public EntityBehaviorControlledPhysics(EntityAgent entity) : base(entity)
        {
            Locomotors.Add(new EntityOnGround());
            Locomotors.Add(new EntityInWater());
            Locomotors.Add(new EntityInAir());
            Locomotors.Add(new EntityApplyGravity());
            Locomotors.Add(new EntityMotionDrag());
        }


        public override void Initialize(EntityType config, JsonObject typeAttributes)
        {
            stepHeight = typeAttributes["stepHeight"].AsFloat(0.6f);

            for (int i = 0; i < Locomotors.Count; i++)
            {
                Locomotors[i].Initialize(config);
            }

            smallerCollisionBox = entity.CollisionBox.Clone().OmniNotDownGrowBy(-0.05f);
        }


        public override void OnGameTick(float deltaTime)
        {
            if (entity.State == EnumEntityState.Inactive) return;

            accumulator += deltaTime;

            if (accumulator > 1)
            {
                accumulator = 1;
            }
            
            while (accumulator >= GlobalConstants.PhysicsFrameTime)
            {
                GameTick(entity, GlobalConstants.PhysicsFrameTime);
                accumulator -= GlobalConstants.PhysicsFrameTime;
            }
        }


        public virtual void GameTick(Entity entity, float dt) {
            EntityControls controls = ((EntityAgent)entity).Controls;
            TickEntityPhysics(entity.ServerPos, controls, dt);
            if (entity.World is IServerWorldAccessor)
            {
                entity.Pos.SetFrom(entity.ServerPos);
            }
        }


       

        public void TickEntityPhysics(EntityPos pos, EntityControls controls, float dt)
        {
            IBlockAccessor blockAccessor = entity.World.BlockAccessor;

            foreach (EntityLocomotion locomotor in Locomotors)
            {
                if (locomotor.Applicable(entity, pos, controls))
                {
                    locomotor.Apply(dt, entity, pos, controls);
                }
            }

            EntityAgent agent = entity as EntityAgent;
            if (agent?.MountedOn != null)
            {
                pos.SetFrom(agent.MountedOn.MountPosition);
                pos.Motion.X = 0;
                pos.Motion.Y = 0;
                pos.Motion.Z = 0;
                return;
            }


            pos.Motion.X = GameMath.Clamp(pos.Motion.X, -10, 10);
            pos.Motion.Y = GameMath.Clamp(pos.Motion.Y, -10, 10);
            pos.Motion.Z = GameMath.Clamp(pos.Motion.Z, -10, 10);

            if (!controls.NoClip)
            {
                DisplaceWithBlockCollision(pos, controls, dt);
            } else
            {
                pos.X += pos.Motion.X;
                pos.Y += pos.Motion.Y;
                pos.Z += pos.Motion.Z;

                entity.Swimming = false;
                entity.FeetInLiquid = false;
                entity.OnGround = false;
            }

            
            

            // Shake the player violently when falling at high speeds
            /*if (movedy < -50)
            {
                pos.X += (rand.NextDouble() - 0.5) / 5 * (-movedy / 50f);
                pos.Z += (rand.NextDouble() - 0.5) / 5 * (-movedy / 50f);
            }
            */

            //return result;
        }



        Vec3d nextPosition = new Vec3d();
        BlockPos tmpPos = new BlockPos();
        Cuboidd entityBox = new Cuboidd();

        public void DisplaceWithBlockCollision(EntityPos pos, EntityControls controls, float dt)
        {
            IBlockAccessor blockAccess = entity.World.BlockAccessor;

            nextPosition.Set(pos.X + pos.Motion.X, pos.Y + pos.Motion.Y, pos.Z + pos.Motion.Z);
            bool falling = pos.Motion.Y < 0;
            bool feetInLiquidBefore = entity.FeetInLiquid;
            bool onGroundBefore = entity.OnGround;
            bool swimmingBefore = entity.Swimming;
            

            double prevYMotion = pos.Motion.Y;

            controls.IsClimbing = false;

            if (!onGroundBefore && entity.Type.CanClimb == true)
            {
                int height = (int)Math.Ceiling(entity.CollisionBox.Y2);

                entityBox.Set(entity.CollisionBox).Add(pos.X, pos.Y, pos.Z);

                for (int dy = 0; dy < height; dy++)
                {
                    tmpPos.Set((int)pos.X, (int)pos.Y + dy, (int)pos.Z);
                    Block nblock = blockAccess.GetBlock(tmpPos);
                    if (!nblock.Climbable && !entity.Type.CanClimbAnywhere) continue;

                    Cuboidf[] collBoxes = nblock.GetCollisionBoxes(blockAccess, tmpPos);
                    if (collBoxes == null) continue;

                    for (int i = 0; i < collBoxes.Length; i++)
                    {
                        double dist = entityBox.ShortestDistanceFrom(collBoxes[i], tmpPos);
                        controls.IsClimbing |= dist < entity.Type.ClimbTouchDistance;

                        if (controls.IsClimbing)
                        {
                            entity.ClimbingOnFace = null;
                            break;
                        }
                    }
                }

                for (int i = 0; !controls.IsClimbing && i < BlockFacing.HORIZONTALS.Length; i++)
                {
                    BlockFacing facing = BlockFacing.HORIZONTALS[i];
                    for (int dy = 0; dy < height; dy++)
                    {
                        tmpPos.Set((int)pos.X + facing.Normali.X, (int)pos.Y + dy, (int)pos.Z + facing.Normali.Z);
                        Block nblock = blockAccess.GetBlock(tmpPos);
                        if (!nblock.Climbable && !entity.Type.CanClimbAnywhere) continue;

                        Cuboidf[] collBoxes = nblock.GetCollisionBoxes(blockAccess, tmpPos);
                        if (collBoxes == null) continue;

                        for (int j = 0; j < collBoxes.Length; j++)
                        {
                            double dist = entityBox.ShortestDistanceFrom(collBoxes[j], tmpPos);
                            controls.IsClimbing |= dist < entity.Type.ClimbTouchDistance;

                            if (controls.IsClimbing)
                            {
                                entity.ClimbingOnFace = facing;
                                break;
                            }
                        }
                    }
                }
            }

            
            if (controls.IsClimbing)
            {
                if (controls.WalkVector.Y == 0)
                {
                    pos.Motion.Y = controls.Sneak ? Math.Max(-0.07, pos.Motion.Y - 0.07) : pos.Motion.Y;
                    if (controls.Jump) pos.Motion.Y = 0.04;
                }

                nextPosition.Set(pos.X + pos.Motion.X, pos.Y + pos.Motion.Y, pos.Z + pos.Motion.Z);
            }

            collisionTester.ApplyTerrainCollision(entity, pos, ref outposition, !(entity is EntityPlayer));

            bool isStepping = HandleSteppingOnBlocks(pos, controls);

            HandleSneaking(pos, controls, dt);



            if (blockAccess.IsNotTraversable((int)(pos.X + pos.Motion.X), (int)pos.Y, (int)pos.Z))
            {
                outposition.X = pos.X;
            }
            if (blockAccess.IsNotTraversable((int)pos.X, (int)(pos.Y + pos.Motion.Y), (int)pos.Z))
            {
                outposition.Y = pos.Y;
            }
            if (blockAccess.IsNotTraversable((int)pos.X, (int)pos.Y, (int)(pos.Z + pos.Motion.Z)))
            {
                outposition.Z = pos.Z;
            }

            pos.SetPos(outposition);

            
            // Set the players motion to zero if he collided.

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



            Block block = blockAccess.GetBlock((int)pos.X, (int)(pos.Y), (int)pos.Z);
            Block aboveblock = blockAccess.GetBlock((int)pos.X, (int)(pos.Y + 1), (int)pos.Z);
            Block middleBlock = blockAccess.GetBlock((int)pos.X, (int)(pos.Y + entity.CollisionBox.Y1 + entity.CollisionBox.Y2 * 0.66f), (int)pos.Z);


            entity.OnGround = (entity.CollidedVertically && falling && !controls.IsClimbing) || isStepping;
            entity.FeetInLiquid = block.IsLiquid() && ((block.LiquidLevel + (aboveblock.LiquidLevel > 0 ? 1 : 0)) / 8f >= pos.Y - (int)pos.Y);
            entity.Swimming = middleBlock.IsLiquid();
        //    Console.WriteLine(entity.World.Side + ": "+ entity.OnGround + " / " + pos.Y);

            if (!onGroundBefore && entity.OnGround)
            {
                entity.OnFallToGround(prevYMotion);
            }

            if ((!entity.Swimming && !feetInLiquidBefore && entity.FeetInLiquid) || (!entity.FeetInLiquid && !swimmingBefore && entity.Swimming))
            {
                entity.OnCollideWithLiquid();
            }

            if ((swimmingBefore && !entity.Swimming && !entity.FeetInLiquid) || (feetInLiquidBefore && !entity.FeetInLiquid && !entity.Swimming))
            {
                entity.OnExitedLiquid();
            }

            if (!falling || entity.OnGround || controls.IsClimbing)
            {
                entity.PositionBeforeFalling.Set(outposition);
            }

            Cuboidd testedEntityBox = collisionTester.entityBox;

            for (int y = (int) testedEntityBox.Y1; y <= (int) testedEntityBox.Y2; y++)
            {
                for (int x = (int) testedEntityBox.X1; x <= (int) testedEntityBox.X2; x++)
                {
                    for (int z = (int) testedEntityBox.Z1; z <= (int) testedEntityBox.Z2; z++)
                    {
                        collisionTester.tmpPos.Set(x, y, z);
                        collisionTester.tempCuboid.Set(x, y, z, x + 1, y + 1, z + 1);

                        if (collisionTester.tempCuboid.IntersectsOrTouches(testedEntityBox))
                        {
                            // Saves us a few cpu cycles
                            if (x == (int)pos.X && y == (int)pos.Y && z == (int)pos.Z)
                            {
                                block.OnEntityInside(entity.World, entity, collisionTester.tmpPos);
                                continue;
                            }

                            blockAccess.GetBlock(x, y, z).OnEntityInside(entity.World, entity, collisionTester.tmpPos);
                        }
                    }
                }
            }
        }



        

        private void HandleSneaking(EntityPos pos, EntityControls controls, float dt)
        {
            // Sneak to prevent falling off blocks
            if (controls.Sneak && entity.OnGround && pos.Motion.Y <= 0)
            {
                Vec3d testPosition = new Vec3d();
                testPosition.Set(pos.X, pos.Y - GlobalConstants.GravityPerSecond * dt, pos.Z);

                // Only apply this if he was on the ground in the first place
                if (!collisionTester.IsColliding(entity.World.BlockAccessor, smallerCollisionBox, testPosition))
                {
                    return;
                }
                
                testPosition.Set(outposition.X, outposition.Y - GlobalConstants.GravityPerSecond * dt, pos.Z);

                Block belowBlock = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)pos.Y - 1, (int)pos.Z);

                // Test for X
                if (!collisionTester.IsColliding(entity.World.BlockAccessor, smallerCollisionBox, testPosition))
                {
                    // Weird hack you can climb down ladders more easily
                    if (belowBlock.Climbable)
                    {
                        outposition.X += (pos.X - outposition.X) / 10;
                    }
                    else
                    {
                        outposition.X = pos.X;
                    }

                }

                // Test for Z
                testPosition.Set(pos.X, outposition.Y - GlobalConstants.GravityPerSecond * dt, outposition.Z);

                // Test for X
                if (!collisionTester.IsColliding(entity.World.BlockAccessor, smallerCollisionBox, testPosition))
                {
                    // Weird hack you can climb down ladders more easily
                    if (belowBlock.Climbable)
                    {
                        outposition.Z += (pos.Z - outposition.Z) / 10;
                    }
                    else
                    {
                        outposition.Z = pos.Z;
                    }
                }
            }
        }

        private bool HandleSteppingOnBlocks(EntityPos pos, EntityControls controls)
        {
            if (!controls.TriesToMove || (!entity.OnGround && !entity.Swimming)) return false;

            Cuboidd entityCollisionBox = entity.CollisionBox.ToDouble();
            entityCollisionBox.Add(pos.X, pos.Y, pos.Z);

            Vec3d walkVec = controls.WalkVector;
            Vec3d testVec = new Vec3d();
            Vec3d testMotion = new Vec3d();

            Cuboidd stepableBox = findSteppableCollisionbox(entityCollisionBox, pos.Motion.Y, walkVec);
            
            // Must have walked into a slab
            if (stepableBox != null)
            {
                return
                    tryStep(pos, testMotion.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z), stepableBox, entityCollisionBox) ||
                    tryStep(pos, testMotion.Set(pos.Motion.X, pos.Motion.Y, 0), findSteppableCollisionbox(entityCollisionBox, pos.Motion.Y, testVec.Set(walkVec.X, walkVec.Y, 0)), entityCollisionBox) ||
                    tryStep(pos, testMotion.Set(0, pos.Motion.Y, pos.Motion.Z), findSteppableCollisionbox(entityCollisionBox, pos.Motion.Y, testVec.Set(0, walkVec.Y, walkVec.Z)), entityCollisionBox)
                ;
            }

            return false;
        }

        private bool tryStep(EntityPos pos, Vec3d motion, Cuboidd stepableBox, Cuboidd entityCollisionBox)
        {
            if (stepableBox == null) return false;

            double heightDiff = stepableBox.Y2 - entityCollisionBox.Y1 + 0.01;
            Vec3d steppos = outposition.OffsetCopy(motion.X, heightDiff, motion.Z);
            bool canStep = !collisionTester.IsColliding(entity.World.BlockAccessor, entity.CollisionBox, steppos, false);

            if (canStep)
            {
                pos.Y += 0.07;
                //pos.Motion.Y = 0.001;
                collisionTester.ApplyTerrainCollision(entity, pos, ref outposition, !(entity is EntityPlayer));
                return true;
            }

            return false;
        }

        private Cuboidd findSteppableCollisionbox(Cuboidd entityCollisionBox, double motionY, Vec3d walkVector)
        {
            Cuboidd stepableBox = null;

            for (int i = 0; i < collisionTester.CollisionBoxList.Count; i++)
            {
                Cuboidd collisionbox = collisionTester.CollisionBoxList.cuboids[i];

                EnumIntersect intersect = CollisionTester.AabbIntersect(collisionbox, entityCollisionBox, walkVector);
                if (intersect == EnumIntersect.NoIntersect) continue;

                // Already stuck somewhere? Can't step stairs
                // Would get stuck vertically if I go up? Can't step up either
                if (intersect == EnumIntersect.Stuck || (intersect == EnumIntersect.IntersectY && motionY > 0))
                {
                    return null;
                }

                double heightDiff = collisionbox.Y2 - entityCollisionBox.Y1;

                if (heightDiff <= 0) continue;
                if (heightDiff <= stepHeight)
                {
                    stepableBox = collisionbox;
                }
            }

            return stepableBox;
        }



        public override string PropertyName()
        {
            return "controlledentityphysics";
        }
    }
}
