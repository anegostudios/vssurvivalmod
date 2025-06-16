using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityProjectile : Entity
    {
        protected bool beforeCollided;
        protected bool stuck;
        protected long msLaunch;
        protected long msCollide;
        protected Vec3d motionBeforeCollide = new Vec3d();
        protected CollisionTester collTester = new CollisionTester();
        protected Cuboidf collisionTestBox;
        protected EntityPartitioning ep;
        protected List<long> entitiesHit = new();
        protected long FiredByMountEntityId;

        public Entity FiredBy;
        public float Weight = 0.1f;
        public float Damage;
        public EnumDamageType DamageType = EnumDamageType.PiercingAttack;
        public int DamageTier = 0;
        public ItemStack ProjectileStack;
        public ItemStack WeaponStack;
        public float DropOnImpactChance = 0f;
        public bool DamageStackOnImpact = false;

        public bool EntityHit { get; protected set; }

        public bool NonCollectible
        {
            get { return Attributes.GetBool("nonCollectible"); }
            set { Attributes.SetBool("nonCollectible", value); }
        }

        public override bool ApplyGravity
        {
            get { return !stuck; }
        }

        public override bool IsInteractable
        {
            get { return false; }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            if (Api.Side == EnumAppSide.Server)
            {
                if (FiredBy != null)
                {
                    WatchedAttributes.SetLong("firedBy", FiredBy.EntityId);
                }
            }

            if (Api.Side == EnumAppSide.Client)
            {
                FiredBy = Api.World.GetEntityById(WatchedAttributes.GetLong("firedBy"));
            }
            msLaunch = World.ElapsedMilliseconds;

            if (FiredBy != null && FiredBy is EntityAgent firedByAgent && firedByAgent.MountedOn?.Entity != null)
            {
                FiredByMountEntityId = firedByAgent.MountedOn.Entity.EntityId;
            }

            collisionTestBox = SelectionBox.Clone().OmniGrowBy(0.05f);

            GetBehavior<EntityBehaviorPassivePhysics>().OnPhysicsTickCallback = onPhysicsTickCallback;
            ep = api.ModLoader.GetModSystem<EntityPartitioning>();

            GetBehavior<EntityBehaviorPassivePhysics>().CollisionYExtra = 0f; // Slightly cheap hax so that stones/arrows don't collide with fences
        }

        private void onPhysicsTickCallback(float dtFac)
        {
            if (ShouldDespawn || !Alive) return;
            if (World.ElapsedMilliseconds <= msCollide + 500) return;

            var pos = SidedPos;
            if (pos.Motion.LengthSq() < 0.2*0.2) return;  // Don't do damage if stuck in ground

            Cuboidd projectileBox = SelectionBox.ToDouble().Translate(pos.X, pos.Y, pos.Z);

            if (pos.Motion.X < 0) projectileBox.X1 += pos.Motion.X * dtFac;
            else projectileBox.X2 += pos.Motion.X * dtFac;
            if (pos.Motion.Y < 0) projectileBox.Y1 += pos.Motion.Y * dtFac;
            else projectileBox.Y2 += pos.Motion.Y * dtFac;
            if (pos.Motion.Z < 0) projectileBox.Z1 += pos.Motion.Z * dtFac;
            else projectileBox.Z2 += pos.Motion.Z * dtFac;

            ep.WalkEntities(pos.XYZ, 5f, (e) => {
                if (e.EntityId == this.EntityId || (FiredBy != null && e.EntityId == FiredBy.EntityId && World.ElapsedMilliseconds - msLaunch < 500) || !e.IsInteractable) return true;

                if (entitiesHit.Contains(e.EntityId)) return false;

                if (e.EntityId == FiredByMountEntityId && World.ElapsedMilliseconds - msLaunch < 500)
                {
                    return true;
                }

                Cuboidd eBox = e.SelectionBox.ToDouble().Translate(e.ServerPos.X, e.ServerPos.Y, e.ServerPos.Z);

                if (eBox.IntersectsOrTouches(projectileBox))
                {
                    impactOnEntity(e);
                    return false;
                }

                return true;
            }, EnumEntitySearchType.Creatures);
        }


        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (ShouldDespawn) return;

            EntityPos pos = SidedPos;

            stuck = Collided || collTester.IsColliding(World.BlockAccessor, collisionTestBox, pos.XYZ) || WatchedAttributes.GetBool("stuck");
            if (Api.Side == EnumAppSide.Server) WatchedAttributes.SetBool("stuck", stuck);

            double impactSpeed = Math.Max(motionBeforeCollide.Length(), pos.Motion.Length());

            if (stuck)
            {
                if (Api.Side == EnumAppSide.Client) ServerPos.SetFrom(Pos);
                IsColliding(pos, impactSpeed);
                entitiesHit.Clear(); // to enable falling projectile to hit same entities second time if it was stuck and then released
                return;
            } else
            {
                SetRotation();
            }

            if (TryAttackEntity(impactSpeed))
            {
                return;
            }

            beforeCollided = false;
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);
        }


        public override void OnCollided()
        {
            EntityPos pos = SidedPos;

            IsColliding(SidedPos, Math.Max(motionBeforeCollide.Length(), pos.Motion.Length()));
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);
        }


        protected virtual void IsColliding(EntityPos pos, double impactSpeed)
        {
            pos.Motion.Set(0, 0, 0);

            if (!beforeCollided && World is IServerWorldAccessor && World.ElapsedMilliseconds > msCollide + 500)
            {
                if (impactSpeed >= 0.07)
                {
                    World.PlaySoundAt(new AssetLocation("sounds/arrow-impact"), this, null, false, 32);

                    // Resend position to client
                    WatchedAttributes.MarkAllDirty();

                    if (DamageStackOnImpact)
                    {
                        ProjectileStack.Collectible.DamageItem(World, this, new DummySlot(ProjectileStack));
                        int leftDurability = ProjectileStack == null ? 1 : ProjectileStack.Collectible.GetRemainingDurability(ProjectileStack);
                        if (leftDurability <= 0)
                        {
                            Die();
                        }
                    }
                }

                TryAttackEntity(impactSpeed);

                msCollide = World.ElapsedMilliseconds;

                beforeCollided = true;
            }


        }


        protected virtual bool TryAttackEntity(double impactSpeed)
        {
            if (World is IClientWorldAccessor || World.ElapsedMilliseconds <= msCollide + 250) return false;
            if (impactSpeed <= 0.01) return false;

            EntityPos pos = SidedPos;

            Cuboidd projectileBox = SelectionBox.ToDouble().Translate(ServerPos.X, ServerPos.Y, ServerPos.Z);

            // We give it a bit of extra leeway of 50% because physics ticks can run twice or 3 times in one game tick
            if (ServerPos.Motion.X < 0) projectileBox.X1 += 1.5 * ServerPos.Motion.X;
            else projectileBox.X2 += 1.5 * ServerPos.Motion.X;
            if (ServerPos.Motion.Y < 0) projectileBox.Y1 += 1.5 * ServerPos.Motion.Y;
            else projectileBox.Y2 += 1.5 * ServerPos.Motion.Y;
            if (ServerPos.Motion.Z < 0) projectileBox.Z1 += 1.5 * ServerPos.Motion.Z;
            else projectileBox.Z2 += 1.5 * ServerPos.Motion.Z;

            Entity entity = World.GetNearestEntity(ServerPos.XYZ, 5f, 5f, (e) => {
                if (e.EntityId == this.EntityId || !e.IsInteractable) return false;

                // So projectile does not damage same entity twice
                if (entitiesHit.Contains(e.EntityId)) return false;

                if (FiredBy != null && e.EntityId == FiredBy.EntityId && World.ElapsedMilliseconds - msLaunch < 500)
                {
                    return false;
                }

                if (e.EntityId == FiredByMountEntityId && World.ElapsedMilliseconds - msLaunch < 500)
                {
                    return false;
                }

                Cuboidd eBox = e.SelectionBox.ToDouble().Translate(e.ServerPos.X, e.ServerPos.Y, e.ServerPos.Z);

                return eBox.IntersectsOrTouches(projectileBox);
            });

            if (entity != null)
            {
                entitiesHit.Add(entity.EntityId);
                impactOnEntity(entity);
                return true;
            }


            return false;
        }


        protected virtual void impactOnEntity(Entity entity)
        {
            if (!Alive) return;

            EntityHit = true;

            EntityPos pos = SidedPos;

            IServerPlayer fromPlayer = null;
            if (FiredBy is EntityPlayer)
            {
                fromPlayer = (FiredBy as EntityPlayer).Player as IServerPlayer;
            }

            bool targetIsPlayer = entity is EntityPlayer;
            bool targetIsCreature = entity is EntityAgent;
            bool canDamage = true;

            ICoreServerAPI sapi = World.Api as ICoreServerAPI;
            if (fromPlayer != null)
            {
                if (targetIsPlayer && (!sapi.Server.Config.AllowPvP || !fromPlayer.HasPrivilege("attackplayers"))) canDamage = false;
                if (targetIsCreature && !fromPlayer.HasPrivilege("attackcreatures")) canDamage = false;
            }

            msCollide = World.ElapsedMilliseconds;

            if (canDamage && World.Side == EnumAppSide.Server)
            {
                World.PlaySoundAt(new AssetLocation("sounds/arrow-impact"), this, null, false, 24);

                float dmg = Damage;
                if (FiredBy != null)
                {
                    dmg *= FiredBy.Stats.GetBlended("rangedWeaponsDamage");

                    if (entity.Properties.Attributes?["isMechanical"].AsBool() == true)
                    {
                        dmg *= FiredBy.Stats.GetBlended("mechanicalsDamage");
                    }
                }

                bool didDamage = entity.ReceiveDamage(new DamageSource()
                {
                    Source = fromPlayer != null ? EnumDamageSource.Player : EnumDamageSource.Entity,
                    SourceEntity = this,
                    CauseEntity = FiredBy,
                    Type = DamageType,
                    DamageTier = DamageTier
                }, dmg);

                float kbresist = entity.Properties.KnockbackResistance;
                entity.SidedPos.Motion.Add(kbresist * pos.Motion.X * Weight, kbresist * pos.Motion.Y * Weight, kbresist * pos.Motion.Z * Weight);

                int leftDurability = 1;
                if (DamageStackOnImpact)
                {
                    ProjectileStack.Collectible.DamageItem(entity.World, entity, new DummySlot(ProjectileStack));
                    leftDurability = ProjectileStack == null ? 1 : ProjectileStack.Collectible.GetRemainingDurability(ProjectileStack);
                }

                if (World.Rand.NextDouble() < DropOnImpactChance && leftDurability > 0)
                {

                }
                else
                {
                    Die();
                }

                if (FiredBy is EntityPlayer && didDamage)
                {
                    World.PlaySoundFor(new AssetLocation("sounds/player/projectilehit"), (FiredBy as EntityPlayer).Player, false, 24);
                }
            }

            pos.Motion.Set(0, 0, 0);
        }

        public virtual void SetInitialRotation()
        {
            var pos = ServerPos;
            double speed = pos.Motion.Length();
            if (speed > 0.01)
            {
                pos.Pitch = 0;
                pos.Yaw = GameMath.PI + (float)Math.Atan2(pos.Motion.X / speed, pos.Motion.Z / speed);
                pos.Roll = -(float)Math.Asin(GameMath.Clamp(-pos.Motion.Y / speed, -1, 1));
            }
        }

        public virtual void SetRotation()
        {
            EntityPos pos = (World is IServerWorldAccessor) ? ServerPos : Pos;

            double speed = pos.Motion.Length();

            if (speed > 0.01)
            {
                pos.Pitch = 0;
                pos.Yaw =
                    GameMath.PI + (float)Math.Atan2(pos.Motion.X / speed, pos.Motion.Z / speed)
                    + GameMath.Cos((World.ElapsedMilliseconds - msLaunch) / 200f) * 0.03f
                ;
                pos.Roll =
                    -(float)Math.Asin(GameMath.Clamp(-pos.Motion.Y / speed, -1, 1))
                    + GameMath.Sin((World.ElapsedMilliseconds - msLaunch) / 200f) * 0.03f
                ;
            }
        }


        public override bool CanCollect(Entity byEntity)
        {
            return !NonCollectible && Alive && World.ElapsedMilliseconds - msLaunch > 1000 && ServerPos.Motion.Length() < 0.01;
        }

        public override ItemStack OnCollected(Entity byEntity)
        {
            ProjectileStack.ResolveBlockOrItem(World);
            return ProjectileStack;
        }


        public override void OnCollideWithLiquid()
        {
            base.OnCollideWithLiquid();
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);
            writer.Write(beforeCollided);
            ProjectileStack.ToBytes(writer);
        }

        public override void FromBytes(BinaryReader reader, bool fromServer)
        {
            base.FromBytes(reader, fromServer);
            beforeCollided = reader.ReadBoolean();
            ProjectileStack = new ItemStack(reader);
        }

        /// <summary>
        /// Common code for spawning and initiating the motion of an aimed projectile thrown overhead from the right hand
        /// </summary>
        /// <param name="entity">The projectile. Does not have to be an EntityProjectile, but if it is then we call .SetRotation()</param>
        /// <param name="byEntity">The thrower</param>
        /// <param name="accuracyFactor">A multiplier for the thrower's accuracy: usually 0.75 but a smaller number would make this projectile type more accurate than most</param>
        /// <param name="heightOffset">The height above or below eye-height of the launched projectile</param>
        /// <param name="horizontalOffset">The horizontal distance from eyes to throwing arm - used for thrown stones and snowballs. Positive for right arm, negative for left arm</param>
        /// <param name="velocityFactor">How fast the projectile should move: default 0.5</param>
        /// <param name="behindDistance">How far behind the player's eyes the projectile starts when first spawned, affects the feel of throwing/firing it: default 0.21</param>
        /// <param name="aheadDistance">How far ahead of the player's position the projectile starts: default 0  (used only by Beenade)</param>
        public static void SpawnThrownEntity(Entity entity, EntityAgent byEntity, double accuracyFactor, double heightOffset, double horizontalOffset, double velocityFactor = 0.5, double behindDistance = 0.21, double aheadDistance = 0)
        {
            float acc = Math.Max(0.001f, (1 - byEntity.Attributes.GetFloat("aimingAccuracy", 0)));
            double rndpitch = byEntity.WatchedAttributes.GetDouble("aimingRandPitch", 1) * acc * accuracyFactor;
            double rndyaw = byEntity.WatchedAttributes.GetDouble("aimingRandYaw", 1) * acc * accuracyFactor;

            Vec3d pos = byEntity.ServerPos.XYZ.Add(0, byEntity.LocalEyePos.Y + heightOffset, 0);
            if (horizontalOffset != 0)
            {
                rndyaw += Math.Atan(horizontalOffset / 4);
            }
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.ServerPos.Pitch + rndpitch, byEntity.ServerPos.Yaw + rndyaw);
            Vec3d velocity = (aheadPos - pos) * velocityFactor;

            Vec3d spawnPos = byEntity.ServerPos.BehindCopy(behindDistance).XYZ.Add(
                byEntity.LocalEyePos.X - GameMath.Cos(byEntity.ServerPos.Yaw) * horizontalOffset,
                byEntity.LocalEyePos.Y + heightOffset,
                byEntity.LocalEyePos.Z + GameMath.Sin(byEntity.ServerPos.Yaw) * horizontalOffset
            );
            if (aheadDistance != 0) spawnPos = spawnPos.Ahead(aheadDistance, 0, byEntity.ServerPos.Yaw + GameMath.PIHALF);
            entity.ServerPos.SetPosWithDimension(spawnPos);
            entity.ServerPos.Motion.Set(velocity);

            entity.Pos.SetFrom(entity.ServerPos);
            entity.World = byEntity.World;

            if (entity is EntityProjectile enpr) enpr.SetRotation();

            byEntity.World.SpawnPriorityEntity(entity);
        }
    }
}
