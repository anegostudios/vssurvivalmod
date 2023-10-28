using System;
using System.IO;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ModSystemBoatingSound : ModSystem
    {
        public ILoadedSound travelSound;
        public ILoadedSound idleSound;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        ICoreClientAPI capi;
        bool soundsActive;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.Event.LevelFinalize += Event_LevelFinalize;
        }

        private void Event_LevelFinalize()
        {
            travelSound = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/raft-moving.ogg"),
                ShouldLoop = true,
                RelativePosition = false,
                DisposeOnFinish = false,
                Volume = 0
            });

            idleSound = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/raft-idle.ogg"),
                ShouldLoop = true,
                RelativePosition = false,
                DisposeOnFinish = false,
                Volume = 0.35f
            });
        }

        public void NowInMotion(float velocity)
        {
            if (!soundsActive)
            {
                idleSound.Start();
                soundsActive = true;
            }

            if (velocity > 0)
            {
                if (!travelSound.IsPlaying) travelSound.Start();

                var volume = GameMath.Clamp((velocity - 0.025f) * 7, 0, 1);
                travelSound.FadeTo(volume, 0.5f, null);
            }
            else
            {
                if (travelSound.IsPlaying)
                {
                    travelSound.Stop();
                }
            }
        }

        public override void Dispose()
        {
            travelSound?.Dispose();
            idleSound?.Dispose();
        }

        public void NotMounted()
        {
            if (soundsActive)
            {
                idleSound.Stop();
                travelSound.SetVolume(0);
                travelSound.Stop();
            }
            soundsActive = false;
        }
    }

    public class EntityBoat : Entity, IRenderer, IMountableSupplier
    {
        public EntityBoatSeat[] Seats;

        // current forward speed
        public double ForwardSpeed = 0.0;

        // current turning speed (rad/tick)
        public double AngularVelocity = 0.0;

        ModSystemBoatingSound modsysSounds;

        public override bool ApplyGravity
        {
            get { return true; }
        }

        public override bool IsInteractable
        {
            get { return true; }
        }


        public override float MaterialDensity
        {
            get { return 100f; }
        }

        public override double SwimmingOffsetY
        {
            get { return 0.45; }
        }

        /// <summary>
        /// The speed this boat can reach at full power
        /// </summary>
        public virtual float SpeedMultiplier => 1f;

        public double RenderOrder => 0;
        public int RenderRange => 999;

        public IMountable[] MountPoints => Seats;

        public Vec3f[] MountOffsets = new Vec3f[] { new Vec3f(-0.6f, 0.2f, 0), new Vec3f(0.7f, 0.2f, 0) };

        ICoreClientAPI capi;

        public EntityBoat()
        {
            Seats = new EntityBoatSeat[2];
            for (int i = 0; i < Seats.Length; i++) Seats[i] = new EntityBoatSeat(this, i, MountOffsets[i]);
            Seats[0].controllable = true;
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            capi = api as ICoreClientAPI;
            if (capi != null)
            {
                capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "boatsim");
                modsysSounds = api.ModLoader.GetModSystem<ModSystemBoatingSound>();
            }

            // The mounted entity will try to mount as well, but at that time, the boat might not have been loaded, so we'll try mounting on both ends. 
            foreach (var seat in Seats)
            {
                if (seat.PassengerEntityIdForInit != 0 && seat.Passenger == null)
                {
                    var entity = api.World.GetEntityById(seat.PassengerEntityIdForInit) as EntityAgent;
                    if (entity != null)
                    {
                        entity.TryMount(seat);
                    }
                }
            }
        }


        public float xangle = 0, yangle = 0, zangle = 0;

        public virtual void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            // Client side we update every frame for smoother turning
            if (capi.IsGamePaused) return;

            updateBoatAngleAndMotion(dt);

            long ellapseMs = capi.InWorldEllapsedMilliseconds;
            
            if (Swimming)
            {
                float intensity = 0.15f + GlobalConstants.CurrentWindSpeedClient.X * 0.9f;
                float diff = GameMath.DEG2RAD / 2f * intensity;
                xangle = GameMath.Sin((float)(ellapseMs / 1000.0 * 2)) * 8 * diff;
                yangle = GameMath.Cos((float)(ellapseMs / 2000.0 * 2)) * 3 * diff;
                zangle = -GameMath.Sin((float)(ellapseMs / 3000.0 * 2)) * 8 * diff - (float)AngularVelocity*5 * Math.Sign(ForwardSpeed);

                SidedPos.Pitch = (float)ForwardSpeed * 1.3f;
            }

            var esr = Properties.Client.Renderer as EntityShapeRenderer;
            if (esr == null) return;

            esr.xangle = xangle;
            esr.yangle = yangle;
            esr.zangle = zangle;

            bool selfSitting = false;

            foreach (var seat in Seats)
            {
                selfSitting |= seat.Passenger == capi.World.Player.Entity;
                var pesr = seat.Passenger?.Properties?.Client.Renderer as EntityShapeRenderer;
                if (pesr != null)
                {
                    pesr.xangle = xangle;
                    pesr.yangle = yangle;
                    pesr.zangle = zangle;
                }
            }

            if (selfSitting)
            {
                modsysSounds.NowInMotion((float)Pos.Motion.Length());
            } else 
            {
                modsysSounds.NotMounted();
            }
        }


        public override void OnGameTick(float dt)
        {
            if (World.Side == EnumAppSide.Server)
            {
                updateBoatAngleAndMotion(dt);
            }
            
            base.OnGameTick(dt);
        }

        public override void OnAsyncParticleTick(float dt, IAsyncParticleManager manager)
        {
            base.OnAsyncParticleTick(dt, manager);

            double disturbance = Math.Abs(ForwardSpeed) + Math.Abs(AngularVelocity);
            if (disturbance > 0.01)
            {
                float minx = -3f;
                float addx = 6f;
                float minz = -0.75f;
                float addz = 1.5f;

                EntityPos herepos = Pos;
                var rnd = Api.World.Rand;
                SplashParticleProps.AddVelocity.Set((float)herepos.Motion.X * 20, (float)herepos.Motion.Y, (float)herepos.Motion.Z * 20);
                SplashParticleProps.AddPos.Set(0.1f, 0, 0.1f);
                SplashParticleProps.QuantityMul = 0.5f * (float)disturbance;

                double y = herepos.Y - 0.15;

                for (int i = 0; i < 10; i++)
                {
                    float dx = minx + (float)rnd.NextDouble() * addx;
                    float dz = minz + (float)rnd.NextDouble() * addz;

                    double yaw = Pos.Yaw + Math.Atan2(dx, dz);
                    double dist = Math.Sqrt(dx * dx + dz * dz);

                    SplashParticleProps.BasePos.Set(
                        herepos.X + Math.Sin(yaw) * dist,
                        y,
                        herepos.Z + Math.Cos(yaw) * dist
                    );

                    manager.Spawn(SplashParticleProps);
                }
            }

        }

        protected virtual void updateBoatAngleAndMotion(float dt)
        {
            if (!Swimming) return;

            // Ignore lag spikes
            dt = Math.Min(0.5f, dt);

            float step = GlobalConstants.PhysicsFrameTime;
            var motion = SeatsToMotion(step);

            // Add some easing to it
            ForwardSpeed += (motion.X * SpeedMultiplier - ForwardSpeed) * dt;
            AngularVelocity += (motion.Y * SpeedMultiplier - AngularVelocity) * dt;

            var pos = SidedPos;

            if (ForwardSpeed != 0.0)
            {
                var targetmotion = pos.GetViewVector().Mul((float)-ForwardSpeed).ToVec3d();
                pos.Motion.X = targetmotion.X;
                pos.Motion.Z = targetmotion.Z;
            }

            if (AngularVelocity != 0.0)
            {
                pos.Yaw += (float)AngularVelocity * dt * 30f;
            }
        }

        protected virtual bool HasPaddle(EntityAgent agent)
        {
            if (agent.RightHandItemSlot == null || agent.RightHandItemSlot.Empty) return false;
            return agent.RightHandItemSlot.Itemstack.Collectible.Attributes?.IsTrue("paddlingTool") == true;
        }

        public virtual Vec2d SeatsToMotion(float dt)
        {
            int seatsRowing = 0;

            double linearMotion = 0;
            double angularMotion = 0;

            foreach (var seat in Seats)
            {
                if (seat.Passenger == null || !seat.controllable) continue;

                var controls = seat.controls;

                if (!HasPaddle(seat.Passenger))
                {
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarBackward");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarForward");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarReady");
                    continue;
                }

                if (!controls.TriesToMove)
                {
                    seat.Passenger.AnimManager?.StartAnimation("crudeOarReady");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarBackward");
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarForward");
                    continue;
                } else
                {
                    if (controls.Right && !controls.Backward && !controls.Forward)
                    {
                        seat.Passenger.AnimManager?.StartAnimation("crudeOarBackward");
                        seat.Passenger.AnimManager?.StopAnimation("crudeOarForward");
                    }
                    else
                    {
                        seat.Passenger.AnimManager?.StartAnimation(controls.Backward ? "crudeOarBackward" : "crudeOarForward");
                        seat.Passenger.AnimManager?.StopAnimation(!controls.Backward ? "crudeOarBackward" : "crudeOarForward");
                    }
                    seat.Passenger.AnimManager?.StopAnimation("crudeOarReady");
                }

                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                if (controls.Left || controls.Right)
                {
                    float dir = controls.Left ? 1 : -1;
                    angularMotion += str * dir * dt;
                }

                if (controls.Forward || controls.Backward)
                {
                    float dir = controls.Forward ? 1 : -1;
                    
                    var yawdist = Math.Abs(GameMath.AngleRadDistance(SidedPos.Yaw, seat.Passenger.SidedPos.Yaw));
                    bool isLookingBackwards = yawdist > GameMath.PIHALF;

                    if (isLookingBackwards) dir *= -1;

                    linearMotion += str * dir * dt * 2f;
                }

                // Only the first player can control the boat
                // Reason: Very difficult to properly smoothly synchronize that over the network
                break;
            }

            return new Vec2d(linearMotion, angularMotion);
        }


        public virtual bool IsMountedBy(Entity entity)
        {
            foreach (var seat in Seats)
            {
                if (seat.Passenger == entity) return true;
            }
            return false;
        }

        public virtual Vec3f GetMountOffset(Entity entity)
        {
            foreach (var seat in Seats)
            {
                if (seat.Passenger == entity)
                {
                    return seat.MountOffset;
                }
            }
            return null;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode != EnumInteractMode.Interact)
            {
                return;
            }

            // sneak + click to remove boat
            if (byEntity.Controls.Sneak && IsEmpty())
            {
                foreach (var seat in Seats)
                {
                    seat.Passenger?.TryUnmount();
                }

                ItemStack stack = new ItemStack(World.GetItem(Code));
                if (!byEntity.TryGiveItemStack(stack))
                {
                    World.SpawnItemEntity(stack, ServerPos.XYZ);
                }
                Die();
                return;
            }

            if (World.Side == EnumAppSide.Server)
            {
                foreach (var seat in Seats)
                {
                    if (byEntity.MountedOn == null && seat.Passenger == null)
                    {
                        byEntity.TryMount(seat);
                    }
                }

                /*Vec3d boatDirection = Vec3dFromYaw(ServerPos.Yaw);
                Vec3d hitDirection = hitPosition.Normalize();
                double hitDotProd = hitDirection.X * boatDirection.X + hitDirection.Z * boatDirection.Z;
                int seatNumber = hitDotProd > 0.0 ? 1 : 0;
                if (byEntity.MountedOn == null && Seats[seatNumber].Passenger == null)
                {
                    byEntity.TryMount(Seats[seatNumber]);
                }*/

            }
        }


        public static Vec3d Vec3dFromYaw(float yawRad)
        {
            return new Vec3d(Math.Cos(yawRad), 0.0, -Math.Sin(yawRad));
        }

        public override bool CanCollect(Entity byEntity)
        {
            return false;
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            writer.Write(Seats.Length);
            foreach (var seat in Seats)
            {
                writer.Write(seat.Passenger?.EntityId ?? (long)0);
            }
        }

        public override void FromBytes(BinaryReader reader, bool fromServer)
        {
            base.FromBytes(reader, fromServer);

            int numseats = reader.ReadInt32();
            for (int i = 0; i < numseats; i++)
            {
                long entityId = reader.ReadInt64();
                Seats[i].PassengerEntityIdForInit = entityId;
            }
        }

        public virtual bool IsEmpty()
        {
            return !Seats.Any(seat => seat.Passenger != null);
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return base.GetInteractionHelp(world, es, player);
        }

        public void Dispose()
        {
            
        }
    }
}