using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    // When player detected at a range of "searchRange" then
    // Always keep at a range of "targetRange" blocks if possible
    // This means we need some semi intelligent position selection where the bowtorn should stand because
    // 1. Needs to be within a min/max range
    // 2. Needs to have line of sight to shoot at
    // 3. Needs to not be in water or fall down a cliff in the process
    // 4. Prefer highground
    // 5. Sort&Find the most optimal location from all of these conditions


    // I think a simple steering system would be just fine for now
    // Plot a line straight towards/away from player and walk along that line for as long as there is nothing blocking it
    // If something is blocking it, turn left or right
    public class AiTaskStayInRange : AiTaskBaseTargetable
    {
        protected Vec3d targetPos;
        private readonly Vec3d ownPos = new Vec3d();

        protected float moveSpeed = 0.02f;
        protected float searchRange = 25f;
        protected float targetRange = 15f;
        protected float rangeTolerance = 2f;
        protected bool stopNow = false;
        protected bool active = false;
        protected float currentFollowTime = 0;

        protected long finishedMs;

        protected long lastSearchTotalMs;
        protected long lastHurtByTargetTotalMs;

        protected bool lastPathfindOk;

        /// <summary>
        /// Amount of ms to wait between searches. This value exists so as to not overload the server with searches.
        /// </summary>
        protected int searchWaitMs = 4000;


        protected bool RecentlyHurt => entity.World.ElapsedMilliseconds - lastHurtByTargetTotalMs < 10000;

        protected Vec3d lastGoalReachedPos;
        protected Dictionary<long, int> futilityCounters;
        float executionChance;

        public AiTaskStayInRange(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            searchRange = taskConfig["searchRange"].AsFloat(25);
            targetRange = taskConfig["targetRange"].AsFloat(15);
            rangeTolerance = taskConfig["targetRangeTolerance"].AsFloat(2);
            retaliateAttacks = taskConfig["retaliateAttacks"].AsBool(true);
            executionChance = taskConfig["executionChance"].AsFloat(0.1f);
            searchWaitMs = taskConfig["searchWaitMs"].AsInt(4000);
        }


        public override bool ShouldExecute()
        {
            if (noEntityCodes && (attackedByEntity == null || !retaliateAttacks)) return false;
            if (!PreconditionsSatisifed()) return false;

            if (targetEntity != null)
            {
                var sqdist = entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos);
                bool toofar = sqdist > (targetRange + rangeTolerance) * (targetRange + rangeTolerance);
                bool toonear = sqdist < (targetRange - rangeTolerance) * (targetRange - rangeTolerance);
                if (toofar || toonear) return true;
            }

            if (WhenInEmotionState == null && rand.NextDouble() > 0.5f) return false;
            if (lastSearchTotalMs + searchWaitMs > entity.World.ElapsedMilliseconds) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds && !RecentlyAttacked) return false;
            // React immediately on hurt, otherwise only 1/10 chance of execution
            if (rand.NextDouble() > executionChance && (WhenInEmotionState == null || IsInEmotionState(WhenInEmotionState) != true) && !RecentlyAttacked) return false;

            lastSearchTotalMs = entity.World.ElapsedMilliseconds;
            if (!RecentlyAttacked)
            {
                attackedByEntity = null;
            }

            if (retaliateAttacks && attackedByEntity != null && attackedByEntity.Alive && attackedByEntity.IsInteractable && IsTargetableEntity(attackedByEntity, searchRange, true) && !entity.ToleratesDamageFrom(attackedByEntity))
            {
                targetEntity = attackedByEntity;
                targetPos = targetEntity.ServerPos.XYZ;
                return true;
            }
            else
            {
                ownPos.SetWithDimension(entity.ServerPos);
                targetEntity = partitionUtil.GetNearestEntity(ownPos, searchRange, (e) => IsTargetableEntity(e, searchRange), EnumEntitySearchType.Creatures);

                if (targetEntity != null)
                {
                    targetPos = targetEntity.ServerPos.XYZ;

                    var sqdist = entity.ServerPos.SquareDistanceTo(targetPos);
                    bool toofar = sqdist > (targetRange + rangeTolerance) * (targetRange + rangeTolerance);
                    bool toonear = sqdist < (targetRange - rangeTolerance) * (targetRange - rangeTolerance);

                    return toonear || toofar;
                }
            }

            return false;
        }


        public override void StartExecute()
        {
            base.StartExecute();
            stopNow = false;
            active = true;
            currentFollowTime = 0;
        }

        public override bool CanContinueExecute()
        {
            return true;
        }

        public override bool 
            ContinueExecute(float dt)
        {
            //Check if time is still valid for task.
            if (!IsInValidDayTimeHours(false)) return false;

            if (pathTraverser.Active) return true;

            var sqdist = entity.ServerPos.SquareDistanceTo(targetEntity.ServerPos);
            bool toofar = sqdist > (targetRange + rangeTolerance) * (targetRange + rangeTolerance);
            bool toonear = sqdist < (targetRange - rangeTolerance) * (targetRange - rangeTolerance);

            bool canWalk = false;

            if (toofar)
            {
                canWalk = WalkTowards(-1);
            } else if (toonear)
            {
                canWalk = WalkTowards(1);
            }

            return canWalk && (toofar || toonear);
        }

        private bool WalkTowards(int sign)
        {
            var ba = entity.World.BlockAccessor;
            var selfpos = entity.ServerPos.XYZ;
            var dir = selfpos.SubCopy(targetEntity.ServerPos.X, selfpos.Y, targetEntity.ServerPos.Z).Normalize();
            var nextPos = selfpos + sign*dir;
            // Lets use only block center for testing
            var testPos = new Vec3d((int)nextPos.X + 0.5, (int)nextPos.Y, (int)nextPos.Z + 0.5);

            // Straight
            if (canStepTowards(nextPos))
            {
                pathTraverser.WalkTowards(nextPos, moveSpeed, 0.3f, OnGoalReached, OnStuck);
                return true;
            }

            // Randomly flip left/right direction preference
            int rnds = 1 - entity.World.Rand.Next(2) * 2;

            // Left
            var ldir = dir.RotatedCopy(rnds * GameMath.PIHALF);
            nextPos = selfpos + ldir;
            testPos = new Vec3d((int)nextPos.X + 0.5, (int)nextPos.Y, (int)nextPos.Z + 0.5);
            if (canStepTowards(testPos))
            {
                pathTraverser.WalkTowards(nextPos, moveSpeed, 0.3f, OnGoalReached, OnStuck);
                return true;
            }
            // Right
            var rdir = dir.RotatedCopy(-rnds * GameMath.PIHALF);
            nextPos = selfpos + rdir;
            testPos = new Vec3d((int)nextPos.X + 0.5, (int)nextPos.Y, (int)nextPos.Z + 0.5);
            if (canStepTowards(testPos))
            {
                pathTraverser.WalkTowards(nextPos, moveSpeed, 0.3f, OnGoalReached, OnStuck);
                return true;
            }

            return false;
        }

        private bool canStepTowards(Vec3d nextPos)
        {
            bool hereCollide = world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, nextPos, false);
            if (hereCollide)
            {
                bool oneBlockUpCollide = world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, collTmpVec.Set(nextPos).Add(0, Math.Min(1, stepHeight), 0), false);
                // Ok to step up one block
                if (!oneBlockUpCollide) return true;
            }

            // Block in front plus block in front one step up -> this is a wall
            if (hereCollide) return false;

            if (isLiquidAt(nextPos)) return false;

            // Ok to step down one block
            bool belowCollide = world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, collTmpVec.Set(nextPos).Add(0, -1.1, 0), false);
            if (belowCollide)
            {
                nextPos.Y-=1;
                return true;
            }

            if (isLiquidAt(collTmpVec)) return false;

            // Ok to step down 2 or 3 blocks if we are 1-2 block above the player
            bool below2Collide = world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, collTmpVec.Set(nextPos).Add(0, -2.1, 0), false);
            if (!belowCollide && below2Collide && entity.ServerPos.Y - TargetEntity.ServerPos.Y >= 1)
            {
                nextPos.Y-=2;
                return true;
            }

            if (isLiquidAt(collTmpVec)) return false;

            bool below3Collide = world.CollisionTester.IsColliding(world.BlockAccessor, entity.SelectionBox, collTmpVec.Set(nextPos).Add(0, -3.1, 0), false);
            if (!belowCollide && !below2Collide && below3Collide && entity.ServerPos.Y - TargetEntity.ServerPos.Y >= 2)
            {
                nextPos.Y-=3;
                return true;
            }

            return false;
        }

        protected bool isLiquidAt(Vec3d pos)
        {
            return entity.World.BlockAccessor.GetBlock((int)pos.X, (int)pos.Y, (int)pos.Z).IsLiquid();
        }

        private void WalkTowards()
        {
            var selfpos = entity.ServerPos.XYZ;
            var dir = selfpos.Sub(targetEntity.ServerPos.XYZ).Normalize();
            pathTraverser.WalkTowards(selfpos + dir, moveSpeed, 0.25f, OnGoalReached, OnStuck);            
        }

        private void OnStuck()
        {
            
        }

        private void OnGoalReached()
        {
            
        }

        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            finishedMs = entity.World.ElapsedMilliseconds;
            active = false;
        }


        
        public override void OnEntityHurt(DamageSource source, float damage)
        {
            base.OnEntityHurt(source, damage);

            if (targetEntity == source.GetCauseEntity() || !active)
            {
                lastHurtByTargetTotalMs = entity.World.ElapsedMilliseconds;
                float dist = targetEntity == null ? 0 : (float)targetEntity.ServerPos.DistanceTo(entity.ServerPos);
            }
        }


    }
}
