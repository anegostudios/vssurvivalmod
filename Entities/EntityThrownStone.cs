using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class EntityThrownStone : Entity
    {
        bool beforeCollided;
        bool stuck;

        long msLaunch;
        Vec3d motionBeforeCollide = new Vec3d();

        CollisionTester collTester = new CollisionTester();

        public IEntity FiredBy;
        internal float Damage;
        public ItemStack ProjectileStack;

        public override bool IsInteractable
        {
            get { return false; }
        }

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

            EntityPos pos = LocalPos;

            stuck = Collided;
            if (stuck)
            {
                pos.Pitch = GameMath.PIHALF;
                pos.Roll = 0;
                pos.Yaw = GameMath.PIHALF;
            } else
            {
                pos.Pitch = (World.ElapsedMilliseconds / 300f) % GameMath.TWOPI;
                pos.Roll = 0;
                pos.Yaw = (World.ElapsedMilliseconds / 400f) % GameMath.TWOPI;
            }

            if (stuck)
            {
                if (!beforeCollided && World is IServerWorldAccessor)
                {
                    float strength = GameMath.Clamp((float)motionBeforeCollide.Length() * 4, 0, 1);

                    if (CollidedHorizontally)
                    {
                        pos.Motion.X = motionBeforeCollide.X * 0.8f;
                        pos.Motion.Z = motionBeforeCollide.Z * 0.8f;

                        if (strength > 0.08f && World.Rand.NextDouble() > 0.2f)
                        {
                            World.SpawnCubeParticles(LocalPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.2f, 20);
                            Die();
                        }
                    }

                    if (CollidedVertically && motionBeforeCollide.Y <= 0)
                    {
                        pos.Motion.Y = GameMath.Clamp(motionBeforeCollide.Y * -0.4f, -0.1f, 0.1f);
                    }

                    

                    World.PlaySoundAt(new AssetLocation("sounds/thud"), this, null, false, 32, strength);

                    // Slighty randomize orientation to make it a bit more realistic
                    //pos.Yaw += (float)(World.Rand.NextDouble() * 0.05 - 0.025);
                    //pos.Roll += (float)(World.Rand.NextDouble() * 0.05 - 0.025);

                    // Resend position to client
                    WatchedAttributes.MarkAllDirty();
                }

                beforeCollided = true;
                return;
            }


            if (World is IServerWorldAccessor)
            {
                IEntity entity = World.GetNearestEntity(ServerPos.XYZ, 5f, 5f, (e) => {
                    if (e.EntityId == this.EntityId || (FiredBy != null && e.EntityId == FiredBy.EntityId && World.ElapsedMilliseconds - msLaunch < 500) || !e.IsInteractable)
                    {
                        return false;
                    }

                    double dist = e.CollisionBox.ToDouble().Translate(e.ServerPos.X, e.ServerPos.Y, e.ServerPos.Z).ShortestDistanceFrom(ServerPos.X, ServerPos.Y, ServerPos.Z);
                    return dist < 0.5f;
                });

                if (entity != null)
                {
                    entity.ReceiveDamage(new DamageSource() { Source = EnumDamageSource.Entity, SourceEntity = this, Type = EnumDamageType.BluntAttack }, Damage);
                    World.PlaySoundAt(new AssetLocation("sounds/thud"), this, null, false, 32);
                    World.SpawnCubeParticles(entity.LocalPos.XYZ.OffsetCopy(0, 0.2, 0), ProjectileStack, 0.2f, 20);
                    Die();
                    return;
                }
            }

            beforeCollided = false;
            motionBeforeCollide.Set(pos.Motion.X, pos.Motion.Y, pos.Motion.Z);

            SetRotation();
        }


        public virtual void SetRotation()
        {
            EntityPos pos = (World is IServerWorldAccessor) ? ServerPos : Pos;

            double speed = pos.Motion.Length();

            /* if (speed > 0.01)
             {

                 pos.Yaw =
                     GameMath.PI + (float)Math.Atan2(pos.Motion.X / speed, pos.Motion.Z / speed)
                     + GameMath.Cos((World.ElapsedMilliseconds - msLaunch) / 200f) * 0.03f
                 ;
                 pos.Roll = 0;
             }*/

            /*float sizex = Math.Max(0.25f, 0.8f * Math.Abs(GameMath.Sin(pos.Yaw) * GameMath.Cos(pos.Roll)));
            float sizez = Math.Max(0.25f, 0.8f * Math.Abs(GameMath.Cos(pos.Yaw) * GameMath.Cos(pos.Roll)));
            float sizey = Math.Max(0.25f, 0.8f * Math.Abs(GameMath.Sin(pos.Roll)));

            if (CollisionBox == null) return;
            CollisionBox.X1 = -sizex / 2;
            CollisionBox.X2 = sizex / 2;
            CollisionBox.Z1 = -sizez / 2;
            CollisionBox.Z2 = sizez / 2;
            CollisionBox.Y1 = -sizey / 2;
            CollisionBox.Y2 = sizey / 2;*/

            if (CollisionBox == null) return;
            CollisionBox.X1 = -0.2f;
            CollisionBox.X2 = 0.2f;
            CollisionBox.Z1 = -0.2f;
            CollisionBox.Z2 = 0.2f;
            CollisionBox.Y1 = 0f;
            CollisionBox.Y2 = 0.2f;
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
            if (motionBeforeCollide.Y <= 0)
            {
                LocalPos.Motion.Y = GameMath.Clamp(motionBeforeCollide.Y * -0.5f, -0.1f, 0.1f);
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

