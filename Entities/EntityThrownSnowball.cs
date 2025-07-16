using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityThrownSnowball : Entity, IProjectile
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

        public bool NonCollectible;
        public float collidedAccum;

        public float VerticalImpactBreakChance = 1f;
        public float HorizontalImpactBreakChance = 1f;

        public float ImpactParticleSize = 1.5f;
        public int ImpactParticleCount = 10;

        public override bool IsInteractable
        {
            get { return false; }
        }

        #region IProjectile
        Entity IProjectile.FiredBy { get => FiredBy; set => FiredBy = value; }
        float IProjectile.Damage { get => Damage; set => Damage = value; }
        int IProjectile.DamageTier { get => DamageTier; set => DamageTier = value; }
        EnumDamageType IProjectile.DamageType { get; set; }
        bool IProjectile.IgnoreInvFrames { get; set; }
        ItemStack IProjectile.ProjectileStack { get => ProjectileStack; set => ProjectileStack = value; }
        ItemStack IProjectile.WeaponStack { get; set; }
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
                        Type = Damage > 0.01 ? EnumDamageType.BluntAttack : EnumDamageType.Frost,
                        DamageTier = DamageTier,
                        YDirKnockbackDiv = 3
                    }, Damage);

                    World.PlaySoundAt(new AssetLocation("sounds/block/snow"), this, null, false, 32);
                    World.SpawnCubeParticles(SidedPos.XYZ.AddCopy(SidedPos.Motion.X, SidedPos.Motion.Y, SidedPos.Motion.Z), ProjectileStack, 0.2f, 12, 1.2f);

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
                        World.SpawnCubeParticles(SidedPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.5f, ImpactParticleCount, ImpactParticleSize, null, new Vec3f((float)motionBeforeCollide.X * 8, (float)-motionBeforeCollide.Y * 6, (float)motionBeforeCollide.Z * 8));
                        Die();
                    }
                }

                World.PlaySoundAt(new AssetLocation("sounds/block/snow"), this, null, false, 32, strength);
                World.SpawnCubeParticles(SidedPos.XYZ.OffsetCopy(0, 0.25, 0), ProjectileStack, 0.5f, ImpactParticleCount, ImpactParticleSize, null, new Vec3f((float)motionBeforeCollide.X * 8, (float)-motionBeforeCollide.Y * 6, (float)motionBeforeCollide.Z * 8));

                // Resend position to client
                WatchedAttributes.MarkAllDirty();
            }

            beforeCollided = true;
            return;
        }


        public override bool CanCollect(Entity byEntity)
        {
            return false;
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

            ProjectileStack = World == null ? new ItemStack(reader) : new ItemStack(reader, World);
        }
    }
}
