using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class EntityBluntProjectile : EntityProjectileBase
    {
        public float collidedAccum;
        public float VerticalImpactBreakChance = 0f;
        public float ImpactParticleSize = 1f;
        public int ImpactParticleCount = 20;


        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (ShouldDespawn) return;

            EntityPos pos = Pos;

            // This seems to work properly.
            // Does not work: Testing for Collided (does not trigger always)
            // Does not work: Testing for motion only (can be 0 in some cases, even while mid-air)
            Stuck = Api.Side == EnumAppSide.Server
                && (pos.Motion.LengthSq() < 0.01 * 0.01)
                && World.CollisionTester.IsColliding(World.BlockAccessor, CollisionBox, Pos.XYZ)
            ;

            if (Stuck)
            {
                pos.Pitch = 0;
                pos.Roll = 0;
                pos.Motion.X = 0;
                pos.Motion.Z = 0;

                collidedAccum += dt;
                if (!Collectible && collidedAccum > 1) Die();
            }
            else
            {
                pos.Pitch = (World.ElapsedMilliseconds / 300f) % GameMath.TWOPI;
                pos.Roll = 0;
                pos.Yaw = (World.ElapsedMilliseconds / 400f) % GameMath.TWOPI;
            }

            if (World is IServerWorldAccessor && (!Stuck || pos.Motion.Length() * 4 > 0.1f))
            {
                Entity? entity = World.GetNearestEntity(Pos.XYZ, 5f, 5f, (e) =>
                {
                    if (e.EntityId == this.EntityId || (FiredBy != null && e.EntityId == FiredBy.EntityId && World.ElapsedMilliseconds - msLaunch < 500) || !e.IsInteractable)
                    {
                        return false;
                    }

                    double dist = e.SelectionBox.ToDouble().Translate(e.Pos.X, e.Pos.Y, e.Pos.Z).ShortestDistanceFrom(Pos.X, Pos.Y, Pos.Z);
                    return dist < 0.4f;
                });

                if (entity != null)
                {
                    var damageSource = new DamageSource()
                    {
                        Source = FiredBy is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
                        SourceEntity = this,
                        CauseEntity = FiredBy,
                        Type = DamageType,
                        DamageTier = DamageTier,
                        IgnoreInvFrames = IgnoreInvFrames,
                        YDirKnockbackDiv = 3
                    };
                    bool didDamage = false;
                    if (entity.ShouldReceiveDamage(damageSource, Damage))
                    {
                        didDamage = entity.ReceiveDamage(damageSource, Damage);
                    }

                    World.PlaySoundAt(new AssetLocation("sounds/thud"), this, null, false);
                    World.SpawnCubeParticles(entity.Pos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.2f, ImpactParticleCount, ImpactParticleSize);

                    if (FiredBy is EntityPlayer entityPlayer && didDamage)
                    {
                        World.PlaySoundFor(new AssetLocation("sounds/player/projectilehit"), entityPlayer.Player, false, 24);
                    }

                    if (World.Rand.NextDouble() > DropOnImpactChance)
                    {
                        Die();
                    }
                    return;
                }
            }

            beforeCollided = false;
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);
        }


        public override void OnCollided()
        {
            EntityPos pos = Pos;
            double currentMotionAmount = pos.Motion.Length();
            if (currentMotionAmount == 0) return;

            if (!beforeCollided && World is IServerWorldAccessor)
            {
                double motionStrength = motionBeforeCollide.Length();
                if (motionStrength == 0) motionStrength = currentMotionAmount;
                float strength = GameMath.Clamp((float)motionStrength * 4, 0, 1);

                xdir = 0;
                zdir = 0;

                if (CollidedHorizontally)
                {
                    xdir = pos.Motion.X == 0 ? -1 : 1;
                    zdir = pos.Motion.Z == 0 ? -1 : 1;

                    pos.Motion.X = xdir * motionBeforeCollide.X * 0.4f;
                    pos.Motion.Z = zdir * motionBeforeCollide.Z * 0.4f;

                    if (strength > 0.1f && World.Rand.NextDouble() > DropOnImpactChance)
                    {
                        Die();
                    }
                }

                if (CollidedVertically)
                {
                    bool bounceOrStop = false;
                    if (motionBeforeCollide.Y <= -0.01)   // Only bounce if we had a reasonable amount of down motion
                    {
                        pos.Motion.Y = GameMath.Clamp(motionBeforeCollide.Y * -0.3f, -0.1f, 0.1f);
                        bounceOrStop = true;
                    }

                    if (!bounceOrStop && pos.Motion.Y >= 0 && pos.Motion.Y <= 0.0000001)   // Stop immediately on non-bouncing impacts
                    {
                        pos.Motion.X = 0;
                        pos.Motion.Z = 0;
                        bounceOrStop = true;
                    }

                    if (bounceOrStop && strength > 0.1f && World.Rand.NextDouble() > 1 - VerticalImpactBreakChance)
                    {
                        Die();
                    }
                }

                if (strength > 0) World.PlaySoundAt(new AssetLocation("sounds/thud"), this, null, false, 32, strength);

                // Resend position to client
                WatchedAttributes.MarkAllDirty();
            }

            beforeCollided = true;
        }


        float xdir = 0, zdir = 0;
        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource? damageSourceForDeath = null)
        {
            if (reason == EnumDespawnReason.Death && World.Side == EnumAppSide.Server && ProjectileStack != null)
            {
                ProjectileStack.ResolveBlockOrItem(World);
                World.SpawnCubeParticles(Pos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.5f, ImpactParticleCount, ImpactParticleSize, null, new Vec3f(xdir * (float)motionBeforeCollide.X * 8, 0, zdir * (float)motionBeforeCollide.Z * 8));
            }

            base.Die(reason, damageSourceForDeath);
        }

    }
}

