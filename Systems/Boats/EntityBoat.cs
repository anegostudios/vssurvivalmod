using System;
using System.Collections.Generic;
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
        float accum;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            capi.Event.LevelFinalize += Event_LevelFinalize;
            capi.Event.RegisterGameTickListener(onTick, 0, 123);
        }

        private void onTick(float dt)
        {
            var eplr = capi.World.Player.Entity;

            if (eplr.MountedOn is EntityBoatSeat eboatseat)
            {
                NowInMotion((float)eboatseat.Entity.Pos.Motion.Length(), dt); ;
            } else
            {
                NotMounted();
            }
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

        public void NowInMotion(float velocity, float dt)
        {
            accum += dt;
            if (accum < 0.2) return;
            accum = 0;

            if (!soundsActive)
            {
                idleSound.Start();
                soundsActive = true;
            }

            if (velocity > 0.01)
            {
                if (!travelSound.IsPlaying)
                {
                    travelSound.Start();
                }

                var volume = GameMath.Clamp((velocity - 0.025f) * 7, 0, 1);
                
                travelSound.FadeTo(volume, 0.5f, null);
            }
            else
            {
                if (travelSound.IsPlaying)
                {
                    travelSound.FadeTo(0, 0.5f, (s) => travelSound.Stop());
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

    public interface ISeatInstSupplier
    {
        IMountableSeat CreateSeat(IMountable mountable, string seatId, SeatConfig config = null);
    }

    public class EntityBoat : Entity, IRenderer, ISeatInstSupplier, IMountableListener
    {
        public override double FrustumSphereRadius => base.FrustumSphereRadius * 2;
        public override bool IsCreature => true; // For RepulseAgents behavior to work

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

        double swimmingOffsetY;
        public override double SwimmingOffsetY
        {
            get { return swimmingOffsetY; }
        }

        /// <summary>
        /// The speed this boat can reach at full power
        /// </summary>
        public virtual float SpeedMultiplier => 1f;

        public double RenderOrder => 0;
        public int RenderRange => 999;

        

        
        public Dictionary<string, string> MountAnimations = new Dictionary<string, string>();
        bool requiresPaddlingTool;
        bool unfurlSails;

        ICoreClientAPI capi;

        public EntityBoat() { }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            swimmingOffsetY = properties.Attributes["swimmingOffsetY"].AsDouble();
            MountAnimations = properties.Attributes["mountAnimations"].AsObject<Dictionary<string, string>>();


            base.Initialize(properties, api, InChunkIndex3d);


            requiresPaddlingTool = properties.Attributes["requiresPaddlingTool"].AsBool(false);
            unfurlSails = properties.Attributes["unfurlSails"].AsBool(false);

            capi = api as ICoreClientAPI;
            if (capi != null)
            {
                capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "boatsim");
                modsysSounds = api.ModLoader.GetModSystem<ModSystemBoatingSound>();
            }
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            var shape = entityShape;

            if (unfurlSails)
            {
                var mountable = GetInterface<IMountable>();
                if (shape == entityShape) entityShape = entityShape.Clone();

                if (mountable != null && mountable.AnyMounted())
                {
                    entityShape.RemoveElementByName("SailFurled");
                }
                else
                {
                    entityShape.RemoveElementByName("SailUnfurled");
                }
            }

            base.OnTesselation(ref entityShape, shapePathForLogging);

        }


        float curRotMountAngleZ=0f;
        public Vec3f mountAngle = new Vec3f();

        public virtual void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            // Client side we update every frame for smoother turning
            if (capi.IsGamePaused) return;

            updateBoatAngleAndMotion(dt);

            long ellapseMs = capi.InWorldEllapsedMilliseconds;
            float forwardpitch = 0;
            if (Swimming)
            {
                float intensity = 0.15f + GlobalConstants.CurrentWindSpeedClient.X * 0.9f;
                float diff = GameMath.DEG2RAD / 2f * intensity;
                mountAngle.X = GameMath.Sin((float)(ellapseMs / 1000.0 * 2)) * 8 * diff;
                mountAngle.Y = GameMath.Cos((float)(ellapseMs / 2000.0 * 2)) * 3 * diff;
                mountAngle.Z = -GameMath.Sin((float)(ellapseMs / 3000.0 * 2)) * 8 * diff;

                curRotMountAngleZ += ((float)AngularVelocity * 5 * Math.Sign(ForwardSpeed) - curRotMountAngleZ) * dt*5;
                forwardpitch = -(float)ForwardSpeed * 1.3f;
            }

            var esr = Properties.Client.Renderer as EntityShapeRenderer;
            if (esr == null) return;

            esr.xangle = mountAngle.X + curRotMountAngleZ;
            esr.yangle = mountAngle.Y;
            esr.zangle = mountAngle.Z + forwardpitch; // Weird. Pitch ought to be xangle. 
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

                    double yaw = Pos.Yaw + GameMath.PIHALF + Math.Atan2(dx, dz);
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
            // Ignore lag spikes
            dt = Math.Min(0.5f, dt);

            float step = GlobalConstants.PhysicsFrameTime;
            var motion = SeatsToMotion(step);

            if (!Swimming) return;

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

            var bh = GetBehavior<EntityBehaviorPassivePhysicsMultiBox>();
            bool canTurn = true;

            if (AngularVelocity != 0.0)
            {
                float yawDelta = (float)AngularVelocity * dt * 30f;

                if (bh.AdjustCollisionBoxesToYaw(dt, true, SidedPos.Yaw + yawDelta))
                {
                    pos.Yaw += yawDelta;
                }
                else canTurn = false;
            } else
            {
                canTurn = bh.AdjustCollisionBoxesToYaw(dt, true, SidedPos.Yaw);
            }

            if (!canTurn)
            {
                if (bh.AdjustCollisionBoxesToYaw(dt, true, SidedPos.Yaw - 0.1f))
                {
                    pos.Yaw -= 0.0002f;
                }
                else if (bh.AdjustCollisionBoxesToYaw(dt, true, SidedPos.Yaw + 0.1f))
                {
                    pos.Yaw += 0.0002f;
                }
            }

            pos.Roll = 0;
        }

        protected virtual bool HasPaddle(Entity entity)
        {
            if (!requiresPaddlingTool) return true;

            EntityAgent agent = entity as EntityAgent;
            if (agent == null) return false;

            if (agent.RightHandItemSlot == null || agent.RightHandItemSlot.Empty) return false;
            return agent.RightHandItemSlot.Itemstack.Collectible.Attributes?.IsTrue("paddlingTool") == true;
        }

        public virtual Vec2d SeatsToMotion(float dt)
        {
            int seatsRowing = 0;

            double linearMotion = 0;
            double angularMotion = 0;

            var bh = GetBehavior<EntityBehaviorSeatable>();
            bh.Controller = null;

            foreach (var sseat in bh.Seats)
            {
                var seat = sseat as EntityBoatSeat;
                if (seat.Passenger == null) continue;

                if (!(seat.Passenger is EntityPlayer))
                {
                    seat.Passenger.SidedPos.Yaw = SidedPos.Yaw;
                }
                if (seat.Config.BodyYawLimit != null && seat.Passenger is EntityPlayer eplr)
                {
                    eplr.BodyYawLimits = new AngleConstraint(Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, 0.2f);
                    eplr.HeadYawLimits = new AngleConstraint(Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                }

                if (!seat.Config.Controllable || bh.Controller != null) continue;
                var controls = seat.controls;

                bh.Controller = seat.Passenger;

                if (!HasPaddle(seat.Passenger))
                {
                    seat.Passenger.AnimManager?.StopAnimation(MountAnimations["ready"]);
                    seat.actionAnim = null;
                    continue;
                }

                if (controls.Left == controls.Right)
                {
                    StopAnimation("turnLeft");
                    StopAnimation("turnRight");
                }
                if (controls.Left && !controls.Right)
                {
                    StartAnimation("turnLeft");
                    StopAnimation("turnRight");
                }
                if (controls.Right && !controls.Left)
                {
                    StopAnimation("turnLeft");
                    StartAnimation("turnRight");
                }



                if (!controls.TriesToMove)
                {
                    seat.actionAnim = null;
                    seat.Passenger.AnimManager?.StartAnimation(MountAnimations["ready"]);
                    continue;
                } else
                {
                    if (controls.Right && !controls.Backward && !controls.Forward)
                    {
                        seat.actionAnim = MountAnimations["backwards"];
                    }
                    else
                    {
                        seat.actionAnim = MountAnimations[controls.Backward ? "backwards" : "forwards"];
                    }

                    seat.Passenger.AnimManager?.StopAnimation(MountAnimations["ready"]);
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
            }

            return new Vec2d(linearMotion, angularMotion);
        }


        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (mode == EnumInteractMode.Interact && AllowPickup() && IsEmpty())
            {
                if (tryPickup(byEntity, mode)) return;
            }
            

            EnumHandling handled = EnumHandling.PassThrough;

            foreach (EntityBehavior behavior in SidedProperties.Behaviors)
            {
                behavior.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
                if (handled == EnumHandling.PreventSubsequent) break;
            }
        }

        private bool AllowPickup()
        {
            return Properties.Attributes?["rightClickPickup"].AsBool(false) == true;
        }

        private bool IsEmpty()
        {
            var bhs = GetBehavior<EntityBehaviorSeatable>();
            var bhr = GetBehavior<EntityBehaviorRideableAccessories>();
            return !bhs.AnyMounted() && (bhr == null || bhr.Inventory.Empty);
        }

        private bool tryPickup(EntityAgent byEntity, EnumInteractMode mode)
        {
            // sneak + click to remove boat
            if (byEntity.Controls.Sneak)
            {
                ItemStack stack = new ItemStack(World.GetItem(Code));
                if (!byEntity.TryGiveItemStack(stack))
                {
                    World.SpawnItemEntity(stack, ServerPos.XYZ);
                }

                Die();
                return true;
            }

            return false;
        }

        public override bool CanCollect(Entity byEntity)
        {
            return false;
        }


        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return base.GetInteractionHelp(world, es, player);
        }


        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);
        }


        public void Dispose()
        {

        }

        public IMountableSeat CreateSeat(IMountable mountable, string seatId, SeatConfig config)
        {
            return new EntityBoatSeat(mountable, seatId, config);
        }

        public void DidUnnmount(EntityAgent entityAgent)
        {
            MarkShapeModified();
        }

        public void DidMount(EntityAgent entityAgent)
        {
            MarkShapeModified();
        }
    }
}
