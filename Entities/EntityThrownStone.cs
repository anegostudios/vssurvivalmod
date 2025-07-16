using CompactExifLib;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityThrownStone : Entity, IProjectile
    {
        protected bool beforeCollided;
        protected bool stuck;

        protected long msLaunch;
        protected Vec3d motionBeforeCollide = new Vec3d();

        protected CollisionTester collTester = new CollisionTester();

        public Entity FiredBy;
        public float Damage;
        public int DamageTier = 0;
        public ItemStack ProjectileStack;

        public float collidedAccum;

        public float VerticalImpactBreakChance = 0f;
        public float HorizontalImpactBreakChance = 0.8f;

        public float ImpactParticleSize = 1f;
        public int ImpactParticleCount = 20;

        public bool NonCollectible
        {
            get { return Attributes.GetBool("nonCollectible"); }
            set { Attributes.SetBool("nonCollectible", value); }
        }

        public override bool IsInteractable
        {
            get { return false; }
        }

        #region IProjectile
        Entity? IProjectile.FiredBy { get => FiredBy; set => FiredBy = value; }
        float IProjectile.Damage { get => Damage; set => Damage = value; }
        int IProjectile.DamageTier { get => DamageTier; set => DamageTier = value; }
        EnumDamageType IProjectile.DamageType { get; set; }
        bool IProjectile.IgnoreInvFrames { get; set; }
        ItemStack? IProjectile.ProjectileStack { get => ProjectileStack; set => ProjectileStack = value; }
        ItemStack? IProjectile.WeaponStack { get; set; }
        float IProjectile.DropOnImpactChance { get; set; }
        bool IProjectile.DamageStackOnImpact { get; set; }
        bool IProjectile.NonCollectible { get => NonCollectible; set => NonCollectible = value; }
        bool IProjectile.EntityHit { get; }
        float IProjectile.Weight { get => Properties.Weight; set => Properties.Weight = value; }
        bool IProjectile.Stuck { get => stuck; set => stuck = value; }

        void IProjectile.PreInitialize() { }
        #endregion

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

            if (ProjectileStack?.Collectible != null)
            {
                ProjectileStack.ResolveBlockOrItem(World);
            }

            GetBehavior<EntityBehaviorPassivePhysics>().CollisionYExtra = 0f; // Slightly cheap hax so that stones/arrows don't collid with fences
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (ShouldDespawn) return;

            EntityPos pos = SidedPos;

            stuck = Collided;
            if (stuck)
            {
                pos.Pitch = 0;
                pos.Roll = 0;
                pos.Yaw = GameMath.PIHALF;

                collidedAccum += dt;
                if (NonCollectible && collidedAccum > 1) Die();

            } else
            {
                pos.Pitch = (World.ElapsedMilliseconds / 300f) % GameMath.TWOPI;
                pos.Roll = 0;
                pos.Yaw = (World.ElapsedMilliseconds / 400f) % GameMath.TWOPI;
            }


            if (World is IServerWorldAccessor)
            {
                Entity entity = World.GetNearestEntity(ServerPos.XYZ, 5f, 5f, (e) => {
                    if (e.EntityId == this.EntityId || (FiredBy != null && e.EntityId == FiredBy.EntityId && World.ElapsedMilliseconds - msLaunch < 500) || !e.IsInteractable)
                    {
                        return false;
                    }

                    double dist = e.SelectionBox.ToDouble().Translate(e.ServerPos.X, e.ServerPos.Y, e.ServerPos.Z).ShortestDistanceFrom(ServerPos.X, ServerPos.Y, ServerPos.Z);
                    return dist < 0.5f;
                });

                if (entity != null)
                {
                    bool didDamage = entity.ReceiveDamage(new DamageSource() {
                        Source = FiredBy is EntityPlayer ? EnumDamageSource.Player : EnumDamageSource.Entity,
                        SourceEntity = this,
                        CauseEntity = FiredBy,
                        Type = EnumDamageType.BluntAttack,
                        DamageTier = DamageTier,
                        YDirKnockbackDiv = 3
                    }, Damage);
                    World.PlaySoundAt(new AssetLocation("sounds/thud"), this, null, false, 32);
                    World.SpawnCubeParticles(entity.SidedPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.2f, ImpactParticleCount, ImpactParticleSize);

                    if (FiredBy is EntityPlayer && didDamage)
                    {
                        World.PlaySoundFor(new AssetLocation("sounds/player/projectilehit"), (FiredBy as EntityPlayer).Player, false, 24);
                    }

                    Die();
                    return;
                }
            }

            beforeCollided = false;
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);
        }


        public override void OnCollided()
        {
            EntityPos pos = SidedPos;

            if (!beforeCollided && World is IServerWorldAccessor)
            {
                float strength = GameMath.Clamp((float)motionBeforeCollide.Length() * 4, 0, 1);

                if (CollidedHorizontally)
                {
                    float xdir = pos.Motion.X == 0 ? -1 : 1;
                    float zdir = pos.Motion.Z == 0 ? -1 : 1;

                    pos.Motion.X = xdir * motionBeforeCollide.X * 0.4f;
                    pos.Motion.Z = zdir * motionBeforeCollide.Z * 0.4f;

                    if (strength > 0.1f && World.Rand.NextDouble() > 1 - HorizontalImpactBreakChance)
                    {
                        World.SpawnCubeParticles(SidedPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.5f, ImpactParticleCount, ImpactParticleSize, null, new Vec3f(xdir * (float)motionBeforeCollide.X * 8, 0, zdir * (float)motionBeforeCollide.Z * 8));
                        Die();
                    }
                }

                if (CollidedVertically && motionBeforeCollide.Y <= 0)
                {
                    pos.Motion.Y = GameMath.Clamp(motionBeforeCollide.Y * -0.3f, -0.1f, 0.1f);

                    if (strength > 0.1f && World.Rand.NextDouble() > 1 - VerticalImpactBreakChance)
                    {
                        World.SpawnCubeParticles(SidedPos.XYZ.OffsetCopy(0, 0.25, 0), ProjectileStack, 0.5f, ImpactParticleCount, ImpactParticleSize, null, new Vec3f((float)motionBeforeCollide.X * 8, (float)-motionBeforeCollide.Y * 6, (float)motionBeforeCollide.Z * 8));
                        Die();
                    }
                }

                World.PlaySoundAt(new AssetLocation("sounds/thud"), this, null, false, 32, strength);

                // Resend position to client
                WatchedAttributes.MarkAllDirty();
            }

            beforeCollided = true;
            return;
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
            if (motionBeforeCollide.Y <= 0)
            {
                SidedPos.Motion.Y = GameMath.Clamp(motionBeforeCollide.Y * -0.5f, -0.1f, 0.1f);
                PositionBeforeFalling.Y = Pos.Y + 1;
            }

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

            ProjectileStack = World == null ? new ItemStack(reader) : new ItemStack(reader, World);
        }
    }
}

