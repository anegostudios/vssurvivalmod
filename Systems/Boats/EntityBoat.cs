using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemBoatingSoundAndRatlineStamina : ModSystem
    {
        public ILoadedSound travelSound;
        public ILoadedSound idleSound;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        ICoreAPI api;
        ICoreClientAPI capi;
        bool soundsActive;
        float accum;

        ModSystemProgressBar mspb;
        IProgressBar progressBar;

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.api = api;
            capi = api;
            capi.Event.LevelFinalize += Event_LevelFinalize;
            capi.Event.RegisterGameTickListener(onTick, 0, 123);

            capi.Event.EntityMounted += Event_EntityMounted;
            capi.Event.EntityUnmounted += Event_EntityUnmounted;

            mspb = capi.ModLoader.GetModSystem<ModSystemProgressBar>();
        }


        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            api.Event.RegisterGameTickListener(onTickServer, 200);
            api.Event.EntityMounted += Event_EntityMounted;
        }

        Dictionary<string, EntityPlayer> playersOnRatlines = new();


        private void Event_EntityUnmounted(EntityAgent mountingEntity, IMountableSeat mountedSeat)
        {
            mspb.RemoveProgressbar(progressBar);
            progressBar = null;
        }

        private void Event_EntityMounted(EntityAgent mountingEntity, IMountableSeat mountedSeat)
        {
            bool willTire = false;

            if (mountingEntity is EntityPlayer eplr)
            {
                if (mountedSeat.Config.Attributes?.IsTrue("tireWhenMounted") == true)
                {
                    willTire = true;
                    playersOnRatlines[eplr.PlayerUID] = eplr;
                    if (!eplr.WatchedAttributes.HasAttribute("remainingMountedStrengthHours"))
                    {
                        eplr.WatchedAttributes.SetFloat("remainingMountedStrengthHours", 2);
                    }
                }
            }

            if (api.Side == EnumAppSide.Client && progressBar == null && willTire)
            {
                progressBar = mspb.AddProgressbar();
            }

        }


        double lastUpdateTotalHours = 0;
        private void onTickServer(float dt)
        {
            var hoursPassed = (float)(api.World.Calendar.TotalHours - lastUpdateTotalHours);
            if (hoursPassed < 0.1) return;

            List<string> playersToRemove = new List<string>();

            foreach (var eplr in playersOnRatlines.Values)
            {
                bool isOnRatlines = eplr.MountedOn != null && eplr.MountedOn.Config.Attributes?.IsTrue("tireWhenMounted") == true;

                var remainStrengthHours = eplr.WatchedAttributes.GetFloat("remainingMountedStrengthHours", 0);
                remainStrengthHours -= hoursPassed;
                eplr.WatchedAttributes.SetFloat("remainingMountedStrengthHours", remainStrengthHours);

                if (isOnRatlines)
                {
                    if (remainStrengthHours < 0)
                    {
                        eplr.TryUnmount();
                    }
                    // Reduce strength
                } else
                {
                    // Increase strength
                    if (remainStrengthHours < -1)
                    {
                        eplr.WatchedAttributes.RemoveAttribute("remainingMountedStrengthHours");
                        playersToRemove.Add(eplr.PlayerUID);
                    }
                }
            }

            foreach (var val in playersToRemove) playersOnRatlines.Remove(val);

            lastUpdateTotalHours = api.World.Calendar.TotalHours;
        }

        private void onTick(float dt)
        {
            var eplr = capi.World.Player.Entity;

            if (progressBar != null && eplr.WatchedAttributes.HasAttribute("remainingMountedStrengthHours")) {
                progressBar.Progress = eplr.WatchedAttributes.GetFloat("remainingMountedStrengthHours", 0) / 2f;
            }

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
        public override double FrustumSphereRadius => base.FrustumSphereRadius * 2.5;
        public override bool IsCreature => true; // For RepulseAgents behavior to work

        // current forward speed
        public double ForwardSpeed = 0.0;

        // current turning speed (rad/tick)
        public double AngularVelocity = 0.0;

        ModSystemBoatingSoundAndRatlineStamina modsysSounds;

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
        public virtual float SpeedMultiplier { get; set; } = 1f;

        public double RenderOrder => 0;
        public int RenderRange => 999;


        public string CreatedByPlayername => WatchedAttributes.GetString("createdByPlayername");
        public string CreatedByPlayerUID => WatchedAttributes.GetString("createdByPlayerUID");



        public Dictionary<string, string> MountAnimations = new Dictionary<string, string>();
        bool requiresPaddlingTool;
        bool unfurlSails;
        string weatherVaneAnimCode;

        protected int sailPosition
        {
            get { return WatchedAttributes.GetInt("sailPosition", 0); } // 0 = furled, 1 = half, 2 = unfurled
            set { WatchedAttributes.SetInt("sailPosition", value); }
        }

        long CurrentlyControllingEntityId = 0;

        ICoreClientAPI capi;

        public EntityBoat() { }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            swimmingOffsetY = properties.Attributes["swimmingOffsetY"].AsDouble();
            SpeedMultiplier = properties.Attributes["speedMultiplier"].AsFloat(1f);
            MountAnimations = properties.Attributes["mountAnimations"].AsObject<Dictionary<string, string>>();
            weatherVaneAnimCode = properties.Attributes["weatherVaneAnimCode"].AsString(null);

            base.Initialize(properties, api, InChunkIndex3d);

            WatchedAttributes.RegisterModifiedListener("sailPosition", MarkShapeModified);

            requiresPaddlingTool = properties.Attributes["requiresPaddlingTool"].AsBool(false);
            unfurlSails = properties.Attributes["unfurlSails"].AsBool(false);

            capi = api as ICoreClientAPI;
            if (capi != null)
            {
                capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "boatsim");
                modsysSounds = api.ModLoader.GetModSystem<ModSystemBoatingSoundAndRatlineStamina>();
            }
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            var shape = entityShape;

            if (unfurlSails)
            {
                if (shape == entityShape) entityShape = entityShape.Clone();

                switch (sailPosition)
                {
                    case 0: // Furled
                        entityShape.RemoveElementByName("SailUnfurled");
                        entityShape.RemoveElementByName("SailHalf");
                        break;
                    case 1: // Half
                        entityShape.RemoveElementByName("SailFurled");
                        entityShape.RemoveElementByName("SailUnfurled");
                        break;
                    case 2: // Unfurled
                        entityShape.RemoveElementByName("SailFurled");
                        entityShape.RemoveElementByName("SailHalf");
                        break;
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
                double gamespeed = capi.World.Calendar.SpeedOfTime / 60f;
                float intensity = (0.15f + GlobalConstants.CurrentWindSpeedClient.X * 0.9f) * (unfurlSails ? 0.7f : 1f);
                float diff = GameMath.DEG2RAD / 2f * intensity;

                mountAngle.X = GameMath.Sin((float)(ellapseMs / 1000.0 * 2 * gamespeed)) * 8 * diff;
                mountAngle.Y = GameMath.Cos((float)(ellapseMs / 2000.0 * 2 * gamespeed)) * 3 * diff;
                mountAngle.Z = -GameMath.Sin((float)(ellapseMs / 3000.0 * 2 * gamespeed)) * 8 * diff;

                curRotMountAngleZ += ((float)AngularVelocity * 5 * Math.Sign(ForwardSpeed) - curRotMountAngleZ) * dt*5;
                forwardpitch = -(float)ForwardSpeed * 1.3f * (unfurlSails ? 0.5f : 1f);
            }

            var esr = Properties.Client.Renderer as EntityShapeRenderer;
            if (esr == null) return;

            esr.xangle = mountAngle.X + curRotMountAngleZ;
            esr.yangle = mountAngle.Y;
            esr.zangle = mountAngle.Z + forwardpitch; // Weird. Pitch ought to be xangle.

            if (AnimManager.Animator != null)
            {
                if (weatherVaneAnimCode != null && !AnimManager.IsAnimationActive(weatherVaneAnimCode))
                {
                    AnimManager.StartAnimation(weatherVaneAnimCode);
                }

                float targetWindDir = GameMath.Mod((float)Math.Atan2(GlobalConstants.CurrentWindSpeedClient.X, GlobalConstants.CurrentWindSpeedClient.Z) + GameMath.TWOPI - Pos.Yaw, GameMath.TWOPI);
                var anim = AnimManager.GetAnimationState(weatherVaneAnimCode);
                if (anim != null)
                {
                    anim.CurrentFrame = targetWindDir * GameMath.RAD2DEG / 10;
                    anim.BlendedWeight = 1f;
                    anim.EasingFactor = 1f;
                }
            }
        }


        public override void OnGameTick(float dt)
        {
            if (World.Side == EnumAppSide.Server)
            {
                var ela = World.ElapsedMilliseconds;
                if (IsOnFire && (World.ElapsedMilliseconds - OnFireBeginTotalMs > 10000))
                {
                    Die();
                }

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

            if (!Swimming)   // If not swimming then the boat is on land, or partly touching land or in water that is too shallow
            {
                if (!unfurlSails || sailPosition > 0) return;    // For rafts or sailboat with unfurled sails, it cannot move at all

                // For furled sails, we allow the player to attempt to move it slowly using the forwards or backwards keys (hopefully to get to deeper water)
                bool nudgeForwards = false;
                var bhs = GetBehavior<EntityBehaviorSeatable>();
                foreach (var sseat in bhs.Seats)
                {
                    var seat = sseat as EntityBoatSeat;
                    if (!seat.Config.Controllable) continue;
                    if (seat.Passenger is EntityPlayer pl)
                    {
                        if (seat.Passenger.EntityId != CurrentlyControllingEntityId) continue;
                        var controls = seat.controls;
                        if (controls.Forward || controls.Backward)
                        {
                            nudgeForwards = true;
                            break;
                        }
                    }
                }

                if (!nudgeForwards) return;

                motion *= 0.5;   // Move it slowly
            }

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
                    if (eplr.BodyYawLimits == null)
                    {
                        eplr.BodyYawLimits = new AngleConstraint(Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, (float)seat.Config.BodyYawLimit);
                        eplr.HeadYawLimits = new AngleConstraint(Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                    }
                    else
                    {
                        eplr.BodyYawLimits.X = Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD;
                        eplr.BodyYawLimits.Y = (float)seat.Config.BodyYawLimit;
                        eplr.HeadYawLimits.X = Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD;
                        eplr.HeadYawLimits.Y = GameMath.PIHALF;
                    }
                }

                if (!seat.Config.Controllable || bh.Controller != null)
                {
                    continue;
                }
                var controls = seat.controls;

                if (seat.Passenger.EntityId != CurrentlyControllingEntityId) continue;

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

                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                if (unfurlSails && sailPosition > 0)
                {
                    linearMotion += str * dt * sailPosition * 1.5f;
                }

                if (!controls.TriesToMove)
                {
                    seat.actionAnim = null;
                    if (seat.Passenger.AnimManager != null && !seat.Passenger.AnimManager.IsAnimationActive(MountAnimations["ready"]))
                    {
                        seat.Passenger.AnimManager.StartAnimation(MountAnimations["ready"]);
                    }
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
                    if (isLookingBackwards && requiresPaddlingTool) dir *= -1;

                    float ctrlstr = 2f;
                    if (unfurlSails) ctrlstr = sailPosition == 0 ? 0.4f : 0f;

                    linearMotion += str * dir * dt * ctrlstr;
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

            if (GetBehavior<EntityBehaviorSelectionBoxes>()?.IsAPCode((byEntity as EntityPlayer).EntitySelection, "LowerMastAP") == true)
            {
                sailPosition = (ushort)((sailPosition + 1) % 3);
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
            // shift + click to remove boat
            if (byEntity.Controls.ShiftKey)
            {
                ItemStack stack = new ItemStack(World.GetItem(Code));
                if (!byEntity.TryGiveItemStack(stack))
                {
                    World.SpawnItemEntity(stack, ServerPos.XYZ);
                }

                Api.World.Logger.Audit("{0} Picked up 1x{1} at {2}.",
                    byEntity.GetName(),
                    stack.Collectible.Code,
                    Pos
                );

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
            var wis = base.GetInteractionHelp(world, es, player);

            if (GetBehavior<EntityBehaviorSelectionBoxes>()?.IsAPCode(es, "LowerMastAP") == true)
            {
                wis = wis.Append(new WorldInteraction()
                {
                    ActionLangCode = "sailboat-unfurlsails",
                    MouseButton = EnumMouseButton.Right
                });
            }

            return wis;
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

        public void DidUnmount(EntityAgent entityAgent)
        {
            if (CurrentlyControllingEntityId == entityAgent.EntityId)
            {
                var bh = GetBehavior<EntityBehaviorSeatable>();

                CurrentlyControllingEntityId = bh.Seats.FirstOrDefault(seat => seat.CanControl && seat.Passenger != null)?.Passenger.EntityId ?? 0;
            }

            MarkShapeModified();
        }

        public void DidMount(EntityAgent entityAgent)
        {
            if (entityAgent.MountedOn.CanControl && CurrentlyControllingEntityId <= 0)
            {
                CurrentlyControllingEntityId = entityAgent.EntityId;
            }

            MarkShapeModified();
        }

        public override string GetInfoText()
        {
            string text = base.GetInfoText();
            if (CreatedByPlayername != null)
            {
                text += "\n" + Lang.Get("entity-createdbyplayer", CreatedByPlayername);
            }
            return text;
        }



    }
}
