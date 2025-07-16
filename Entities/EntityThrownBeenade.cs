using CompactExifLib;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityThrownBeenade : Entity, IProjectile
    {
        bool beforeCollided;
        bool stuck;
        long msLaunch;
        Vec3d motionBeforeCollide = new Vec3d();

        public Entity FiredBy;
        public float Damage;
        public ItemStack ProjectileStack;

        public override bool IsInteractable
        {
            get { return false; }
        }

        #region IProjectile
        Entity IProjectile.FiredBy { get => FiredBy; set => FiredBy = value; }
        float IProjectile.Damage { get => Damage; set => Damage = value; }
        int IProjectile.DamageTier { get; set; }
        EnumDamageType IProjectile.DamageType { get; set; }
        bool IProjectile.IgnoreInvFrames { get; set; }
        ItemStack IProjectile.ProjectileStack { get => ProjectileStack; set => ProjectileStack = value; }
        ItemStack IProjectile.WeaponStack { get; set; }
        float IProjectile.DropOnImpactChance { get; set; }
        bool IProjectile.DamageStackOnImpact { get; set; }
        bool IProjectile.NonCollectible { get; set; }
        bool IProjectile.EntityHit { get; }
        float IProjectile.Weight { get; set; }
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
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (ShouldDespawn) return;

            EntityPos pos = SidedPos;

            stuck = Collided;
            pos.Pitch = GameMath.PIHALF;
            pos.Roll = 0;
            pos.Yaw = GameMath.PIHALF;

            if (stuck)
            {
                if (!beforeCollided && World.Side == EnumAppSide.Server)
                {
                    float strength = GameMath.Clamp((float)motionBeforeCollide.Length() * 4, 0, 1);

                    if (CollidedHorizontally || CollidedVertically)
                    {
                        OnImpact();
                        return;
                    }

                    // Resend position to client
                    WatchedAttributes.MarkAllDirty();
                }

                beforeCollided = true;
                return;
            }


            if (Damage > 0 && World.Side == EnumAppSide.Server)
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
                    entity.ReceiveDamage(new DamageSource
                    {
                        Source = EnumDamageSource.Entity, 
                        SourceEntity = this, 
                        CauseEntity = FiredBy, 
                        Type = EnumDamageType.BluntAttack 
                    }, Damage);
                    OnImpact();
                    return;
                }
            }

            beforeCollided = false;
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);
            
        }


        public void OnImpact()
        {
            World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), this, null, false, 32);
            World.SpawnCubeParticles(SidedPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.8f, 20);
            Die();

            EntityProperties type = World.GetEntityType(new AssetLocation("beemob"));
            Entity entity = World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.ServerPos.X = SidedPos.X + 0.5f;
                entity.ServerPos.Y = SidedPos.Y + 0.5f;
                entity.ServerPos.Z = SidedPos.Z + 0.5f;
                entity.ServerPos.Yaw = (float)World.Rand.NextDouble() * 2 * GameMath.PI;
                entity.Pos.SetFrom(entity.ServerPos);

                entity.Attributes.SetString("origin", "brokenbeenade");
                World.SpawnEntity(entity);
            }
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
