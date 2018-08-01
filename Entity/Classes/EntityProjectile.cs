using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class EntityProjectile : Entity
    {
        bool beforeCollided;
        bool stuck;

        long msLaunch;
        long msCollide;

        Vec3d motionBeforeCollide = new Vec3d();

        CollisionTester collTester = new CollisionTester();

        public IEntity FiredBy;
        public float Weight = 0.1f;
        public float Damage;
        public ItemStack ProjectileStack;
        public bool DropOnImpact = false;

        Cuboidf collisionTestBox;



        public override bool ApplyGravity
        {
            get { return !stuck; }
        }

        public override bool IsInteractable
        {
            get { return false; }
        }

        public override void Initialize(ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(api, InChunkIndex3d);

            msLaunch = World.ElapsedMilliseconds;

            collisionTestBox = CollisionBox.Clone().OmniGrowBy(0.05f);
        }


        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (ShouldDespawn) return;

            EntityPos pos = LocalPos;

            stuck = Collided || collTester.IsColliding(World.BlockAccessor, collisionTestBox, pos.XYZ);

            double impactSpeed = Math.Max(motionBeforeCollide.Length(), pos.Motion.Length());

            if (stuck)
            {
                IsColliding(pos, impactSpeed);
                return;
            }

            if (TryAttackEntity(impactSpeed))
            {
                return;
            }

            beforeCollided = false;
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);
            SetRotation();
        }


        private void IsColliding(EntityPos pos, double impactSpeed)
        {
            pos.Motion.Set(0, 0, 0);

            if (!beforeCollided && World is IServerWorldAccessor && World.ElapsedMilliseconds > msCollide + 250)
            {
                if (impactSpeed >= 0.07)
                {
                    World.PlaySoundAt(new AssetLocation("sounds/arrow-impact"), this, null, false, 32);

                    // Slighty randomize orientation to make it a bit more realistic
                    //pos.Yaw += (float)(World.Rand.NextDouble() * 0.05 - 0.025);
                    //pos.Roll += (float)(World.Rand.NextDouble() * 0.05 - 0.025);

                    // Resend position to client
                    WatchedAttributes.MarkAllDirty();

                    int leftDurability = ProjectileStack == null ? 1 : ProjectileStack.Attributes.GetInt("durability", 1);
                    if (leftDurability <= 0)
                    {
                        Die();
                    }
                }

                TryAttackEntity(impactSpeed);

                msCollide = World.ElapsedMilliseconds;
            }

            beforeCollided = true;
        }


        bool TryAttackEntity(double impactSpeed)
        {
            if (World is IClientWorldAccessor || World.ElapsedMilliseconds <= msCollide + 250) return false;
            if (impactSpeed <= 0.01) return false;

            EntityPos pos = LocalPos;

            Cuboidd projectileBox = CollisionBox.ToDouble().Translate(ServerPos.X, ServerPos.Y, ServerPos.Z);
            if (ServerPos.Motion.X < 0) projectileBox.X1 += ServerPos.Motion.X;
            else projectileBox.X2 += ServerPos.Motion.X;
            if (ServerPos.Motion.Y < 0) projectileBox.Y1 += ServerPos.Motion.Y;
            else projectileBox.Y2 += ServerPos.Motion.Y;
            if (ServerPos.Motion.Z < 0) projectileBox.Z1 += ServerPos.Motion.Z;
            else projectileBox.Z2 += ServerPos.Motion.Z;


            IEntity entity = World.GetNearestEntity(ServerPos.XYZ, 5f, 5f, (e) => {
                if (e.EntityId == this.EntityId || !e.IsInteractable) return false;

                if (FiredBy != null && e.EntityId == FiredBy.EntityId && World.ElapsedMilliseconds - msLaunch < 500)
                {
                    return false;
                }

                Cuboidd eBox = e.CollisionBox.ToDouble().Translate(e.ServerPos.X, e.ServerPos.Y, e.ServerPos.Z);

                return eBox.IntersectsOrTouches(projectileBox);
            });

            if (entity != null)
            {
                msCollide = World.ElapsedMilliseconds;

                entity.ReceiveDamage(new DamageSource() { source = EnumDamageSource.Entity, sourceEntity = this, type = EnumDamageType.PiercingAttack }, Damage);

                float kbresist = entity.Type.KnockbackResistance;
                entity.LocalPos.Motion.Add(kbresist * pos.Motion.X * Weight, kbresist * pos.Motion.Y * Weight, kbresist * pos.Motion.Z * Weight);

                World.PlaySoundAt(new AssetLocation("sounds/arrow-impact"), this, null, false, 32);

                int leftDurability = ProjectileStack == null ? 1 : ProjectileStack.Attributes.GetInt("durability", 1);

                if (DropOnImpact && leftDurability > 0)
                {
                    pos.Motion.Set(0, 0, 0);
                }
                else
                {
                    Die();
                }
                return true;
            }
            

            return false;
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
            return Alive && World.ElapsedMilliseconds - msLaunch > 1000 && ServerPos.Motion.Length() < 0.01;
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
    }
}
