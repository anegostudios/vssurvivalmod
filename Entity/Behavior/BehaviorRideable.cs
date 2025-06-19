using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public delegate bool CanRideDelegate(IMountableSeat seat, out string errorMessage);

    public enum EnumControlScheme
    {
        Hold,
        Press
    }

    public class EntityBehaviorRideable(Entity entity) : EntityBehaviorSeatable(entity), IMountable, IRenderer, IMountableListener
    {
        public List<GaitMeta> RideableGaitOrder = new(); // List of gaits in order of increasing speed for the rideable entity
        public Vec3f MountAngle { get; set; } = new Vec3f();
        public EntityPos SeatPosition => entity.SidedPos;
        public double RenderOrder => 1;
        public int RenderRange => 100;
        public virtual float SpeedMultiplier => 1f;
        public Entity Mount => entity;
        // current forward speed
        public double ForwardSpeed = 0.0;
        // current turning speed (rad/tick)
        public double AngularVelocity = 0.0;

        public bool IsInMidJump;
        public event CanRideDelegate CanRide;
        public event CanRideDelegate CanTurn;
        public AnimationMetaData curAnim;

        protected ICoreAPI api;
        // Time the player can walk off an edge before gravity applies.
        protected float coyoteTimer;
        // Time the player last jumped.
        protected long lastJumpMs;
        protected bool jumpNow;
        protected EntityAgent eagent = entity as EntityAgent;
        protected long lastGaitChangeMs = 0;
        protected float timeSinceLastGaitCheck = 0;
        protected float timeSinceLastGaitFatigue = 0;
        protected ILoadedSound gaitSound;

        protected FastSmallDictionary<string, ControlMeta> Controls;
        protected string[] GaitOrderCodes; // List of gaits in order of increasing speed for the rideable entity
        protected ICoreClientAPI capi;
        protected EntityBehaviorGait ebg;
        protected int minGeneration = 0; // Minimum generation for the animal to be rideable
        protected GaitMeta saddleBreakGait;
        protected string saddleBreakGaitCode;

        ControlMeta curControlMeta = null;
        bool shouldMove = false;

        internal string prevSoundCode;
        internal string curSoundCode = null;

        string curTurnAnim = null;
        EnumControlScheme scheme;

        #region Semitamed animals

        protected float saddleBreakDayInterval;
        protected string tamedEntityCode;

        public int RemainingSaddleBreaks
        {
            get
            {
                return entity.WatchedAttributes.GetInt("remainingSaddleBreaksRequired");
            }
            set
            {
                entity.WatchedAttributes.SetInt("remainingSaddleBreaksRequired", value);
            }
        }

        public double LastSaddleBreakTotalDays
        {
            get
            {
                return entity.WatchedAttributes.GetDouble("lastSaddlebreakTotalDays");
            }
            set
            {
                entity.WatchedAttributes.SetDouble("lastSaddlebreakTotalDays", value);
            }
        }
        #endregion

        public double LastDismountTotalHours { 
            get
            {
                return entity.WatchedAttributes.GetDouble("lastDismountTotalHours");
            }
            set
            {
                entity.WatchedAttributes.SetDouble("lastDismountTotalHours", value);
            }
        }

        protected override IMountableSeat CreateSeat(string seatId, SeatConfig config)
        {
            return new EntityRideableSeat(this, seatId, config);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            api = entity.Api;
            capi = api as ICoreClientAPI;

            // /entity spawn semitameddeer-elk-male-adult 1
            if (attributes["saddleBreaksRequired"].Exists)
            {
                if (!entity.WatchedAttributes.HasAttribute("requiredSaddleBreaks") && api.Side == EnumAppSide.Server)
                {
                    RemainingSaddleBreaks = GameMath.RoundRandom(api.World.Rand, attributes["saddleBreaksRequired"].AsObject<NatFloat>().nextFloat(1, api.World.Rand));
                }

                saddleBreakDayInterval = attributes["saddleBreakDayInterval"].AsFloat();
                tamedEntityCode = attributes["tamedEntityCode"].AsString();
                saddleBreakGaitCode = attributes["saddleBreakGait"].AsString();
            }

            Controls = attributes["controls"].AsObject<FastSmallDictionary<string, ControlMeta>>();
            minGeneration = attributes["minGeneration"].AsInt(0);
            GaitOrderCodes = attributes["rideableGaitOrder"].AsArray<string>();

            foreach (var val in Controls.Values) val.RiderAnim?.Init();
            curAnim = Controls["idle"].RiderAnim;

            capi?.Event.RegisterRenderer(this, EnumRenderStage.Before, "rideablesim");
            
        }
        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            ebg = eagent.GetBehavior<EntityBehaviorGait>();

            // Gaits are required for rideable entities
            if (ebg is null)
            {
                throw new Exception("EntityBehaviorGait not found on rideable entity. Ensure it is properly registered in the entity's properties.");
            }

            foreach (var str in GaitOrderCodes)
            {
                GaitMeta gait = ebg?.Gaits[str];
                if (gait != null) RideableGaitOrder.Add(gait);
            }
            
            saddleBreakGait = ebg.Gaits.FirstOrDefault(g => g.Value.Code == saddleBreakGaitCode).Value;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);
        }


        public void UnmnountPassengers()
        {
            foreach (var seat in Seats)
            {
                (seat.Passenger as EntityAgent)?.TryUnmount();
            }
        }

        public override void OnEntityLoaded()
        {
            setupTaskBlocker();
        }

        public override void OnEntitySpawn()
        {
            setupTaskBlocker();
        }

        void setupTaskBlocker()
        {
            var ebc = entity.GetBehavior<EntityBehaviorAttachable>();

            if (api.Side == EnumAppSide.Server)
            {
                EntityBehaviorTaskAI taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
                taskAi.TaskManager.OnShouldExecuteTask += TaskManager_OnShouldExecuteTask;
                if (ebc != null)
                {
                    ebc.Inventory.SlotModified += Inventory_SlotModified;
                }
            } else
            {
                if (ebc != null)
                {
                    entity.WatchedAttributes.RegisterModifiedListener(ebc.InventoryClassName, updateControlScheme);
                }
            }
        }

        private void Inventory_SlotModified(int obj)
        {
            updateControlScheme();
            ebg?.SetIdle();
        }

        private void updateControlScheme()
        {
            var ebc = entity.GetBehavior<EntityBehaviorAttachable>();
            if (ebc != null)
            {
                scheme = EnumControlScheme.Hold;
                foreach (var slot in ebc.Inventory)
                {
                    if (slot.Empty) continue;
                    var sch = slot.Itemstack.ItemAttributes?["controlScheme"].AsString(null);
                    if (sch != null)
                    {
                        if (!Enum.TryParse<EnumControlScheme>(sch, out scheme)) scheme = EnumControlScheme.Hold;
                        else break;
                    }
                }
            }
        }

        private bool TaskManager_OnShouldExecuteTask(IAiTask task)
        {
            if (task is AiTaskWander && api.World.Calendar.TotalHours - LastDismountTotalHours < 24) return false;

            return !Seats.Any(seat => seat.Passenger != null);
        }

        bool wasPaused;

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (!wasPaused && capi.IsGamePaused)
            {
                gaitSound?.Pause();
            }
            if (wasPaused && !capi.IsGamePaused)
            {
                if (gaitSound?.IsPaused == true) gaitSound?.Start();
            }

            wasPaused = capi.IsGamePaused;

            if (capi.IsGamePaused) return;

            updateAngleAndMotion(dt);
        }


        protected virtual void updateAngleAndMotion(float dt)
        {
            // Ignore lag spikes
            dt = Math.Min(0.5f, dt);

            float step = GlobalConstants.PhysicsFrameTime;
            var motion = SeatsToMotion(step);

            if (jumpNow) updateRidingState();

            ForwardSpeed = Math.Sign(motion.X);

            float yawMultiplier = ebg?.GetTurnRadius() ?? 3.5f;

            AngularVelocity = motion.Y * yawMultiplier;

            entity.SidedPos.Yaw += (float)motion.Y * dt * 30f;
            entity.SidedPos.Yaw = entity.SidedPos.Yaw % GameMath.TWOPI;

            if (entity.World.ElapsedMilliseconds - lastJumpMs < 2000 && entity.World.ElapsedMilliseconds - lastJumpMs > 200 && entity.OnGround)
            {
                eagent.StopAnimation("jump");
            }
        }

        bool prevForwardKey, prevBackwardKey, prevSprintKey;
        public void SpeedUp() => SetNextGait(true);
        public void SlowDown() => SetNextGait(false);

        public GaitMeta GetNextGait(bool forward, GaitMeta currentGait = null)
        {
            currentGait ??= ebg.CurrentGait;

            if (eagent.Swimming) return forward ? ebg.Gaits["swim"] : ebg.Gaits["swimback"];

            if (RideableGaitOrder is not null && RideableGaitOrder.Count > 0 && this.IsBeingControlled())
            {
                int currentIndex = RideableGaitOrder.IndexOf(currentGait);
                int nextIndex = forward ? currentIndex + 1 : currentIndex - 1;

                // Boundary behavior
                if (nextIndex < 0) nextIndex = 0;
                if (nextIndex >= RideableGaitOrder.Count) nextIndex = currentIndex - 1;

                return RideableGaitOrder[nextIndex];
            }
            else
            {
                return ebg.IdleGait;
            }
        }

        public void SetNextGait(bool forward, GaitMeta nextGait = null)
        {
            if (api.Side != EnumAppSide.Server) return;

            nextGait ??= GetNextGait(forward);

            ebg.CurrentGait = nextGait;
        }

        public GaitMeta GetFirstForwardGait()
        {
            if (RideableGaitOrder == null || RideableGaitOrder.Count == 0)
                return ebg.IdleGait;

            // Find the first forward gait
            return RideableGaitOrder.FirstOrDefault(g => !g.Backwards && g.MoveSpeed > 0) ?? ebg.IdleGait;
        }

        public virtual Vec2d SeatsToMotion(float dt)
        {
            int seatsRowing = 0;

            double linearMotion = 0;
            double angularMotion = 0;

            jumpNow = false;
            coyoteTimer -= dt;

            Controller = null;

            foreach (var seat in Seats)
            {
                if (entity.OnGround) coyoteTimer = 0.15f;

                if (seat.Passenger == null || !seat.Config.Controllable) continue;

                if (seat.Passenger is EntityPlayer eplr)
                {
                    eplr.Controls.LeftMouseDown = seat.Controls.LeftMouseDown;
                    eplr.HeadYawLimits = new AngleConstraint(entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                    eplr.BodyYawLimits = new AngleConstraint(entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                }

                if (Controller != null) continue;
                Controller = seat.Passenger;

                var controls = seat.Controls;
                bool canride = true;
                bool canturn = true;

                if (RemainingSaddleBreaks > 0)
                {
                    if (api.World.Rand.NextDouble() < 0.05) angularMotionWild = ((float)api.World.Rand.NextDouble() * 2 - 1) / 10f;
                    angularMotion = angularMotionWild;
                    canturn = false;
                }

                if (CanRide != null && (controls.Jump || controls.TriesToMove))
                {
                    foreach (CanRideDelegate dele in CanRide.GetInvocationList())
                    {
                        if (!dele(seat, out string errMsg))
                        {
                            if (capi != null && seat.Passenger == capi.World.Player.Entity)
                            {
                                capi.TriggerIngameError(this, "cantride", Lang.Get("cantride-" + errMsg));
                            }
                            canride = false;
                            break;
                        }
                    }
                }

                if (CanTurn != null && (controls.Left || controls.Right))
                {
                    foreach (CanRideDelegate dele in CanTurn.GetInvocationList())
                    {
                        if (!dele(seat, out string errMsg))
                        {
                            if (capi != null && seat.Passenger == capi.World.Player.Entity)
                            {
                                capi.TriggerIngameError(this, "cantride", Lang.Get("cantride-" + errMsg));
                            }
                            canturn = false;
                            break;
                        }
                    }
                }

                if (!canride) continue;


                // Only able to jump every 1500ms. Only works while on the ground.
                if (controls.Jump && entity.World.ElapsedMilliseconds - lastJumpMs > 1500 && entity.Alive && (entity.OnGround || coyoteTimer > 0))
                {
                    lastJumpMs = entity.World.ElapsedMilliseconds;
                    jumpNow = true;
                }

                if (scheme == EnumControlScheme.Hold && !controls.TriesToMove) continue;
                
                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                // Detect if button currently being pressed
                bool nowForwards = controls.Forward;
                bool nowBackwards = controls.Backward;
                bool nowSprint = controls.CtrlKey;

                // Detect if current press is a fresh press
                bool forwardPressed = nowForwards && !prevForwardKey;
                bool backwardPressed = nowBackwards && !prevBackwardKey;
                bool sprintPressed = nowSprint && !prevSprintKey;

                long nowMs = entity.World.ElapsedMilliseconds;

                // This ensures we start moving without sprint key
                if (forwardPressed && ebg.IsIdle) SpeedUp();

                // Handle backward to idle change without sprint key
                if (forwardPressed && ebg.IsBackward) ebg.SetIdle();

                // Cycle up with sprint
                if (ebg.IsForward && sprintPressed && nowMs - lastGaitChangeMs > 300)
                {
                    SpeedUp();

                    lastGaitChangeMs = nowMs;
                }

                // Cycle down with back
                if (backwardPressed && nowMs - lastGaitChangeMs > 300)
                {
                    SlowDown();

                    lastGaitChangeMs = nowMs;
                }

                prevSprintKey = nowSprint;
                prevForwardKey = scheme == EnumControlScheme.Press && nowForwards;
                prevBackwardKey = scheme == EnumControlScheme.Press && nowBackwards;

                #region Motion update
                if (canturn && (controls.Left || controls.Right))
                {
                    float dir = controls.Left ? 1 : -1;
                    angularMotion += str * dir * dt;
                }
                if (ebg.IsForward || ebg.IsBackward)
                {
                    float dir = ebg.IsForward ? 1 : -1;
                    linearMotion += str * dir * dt * 2f;
                }
                #endregion
            }

            return new Vec2d(linearMotion, angularMotion);
        }

        float angularMotionWild = 1/10f;
        bool wasSwimming = false;
        protected void updateRidingState()
        {
            if (!AnyMounted()) return;

            if (RemainingSaddleBreaks > 0)
            {
                ForwardSpeed = 1;
                if (api.World.Rand.NextDouble() < 0.05) jumpNow = true;
                ebg.CurrentGait = saddleBreakGait;

                if (api.World.ElapsedMilliseconds - mountedTotalMs > 4000)
                {
                    foreach (var seat in Seats)
                    {
                        if (seat?.Passenger == null) continue;
                        var eagent = seat.Passenger as EntityAgent;

                        if (api.World.Rand.NextDouble() < 0.5) eagent.ReceiveDamage(new DamageSource()
                        {
                            CauseEntity = entity,
                            DamageTier = 1,
                            Source = EnumDamageSource.Entity,
                            SourcePos = this.Position.XYZ,
                            Type = EnumDamageType.BluntAttack
                        }, 1 + api.World.Rand.Next(8) / 4f);

                        eagent.TryUnmount();
                    }

                    jumpNow = false;
                    ForwardSpeed = 0;
                    Stop();

                    if (api.World.Calendar.TotalDays - LastSaddleBreakTotalDays > saddleBreakDayInterval)
                    {
                        RemainingSaddleBreaks--;
                        LastSaddleBreakTotalDays = api.World.Calendar.TotalDays;
                        if (RemainingSaddleBreaks <= 0)
                        {
                            ConvertToTamedAnimal();
                            return;
                        }
                    }

                    return;
                }
            }

            bool wasMidJump = IsInMidJump;
            IsInMidJump &= (entity.World.ElapsedMilliseconds - lastJumpMs < 500 || !entity.OnGround) && !entity.Swimming;

            if (wasMidJump && !IsInMidJump)
            {
                var meta = Controls["jump"];
                foreach (var seat in Seats) seat.Passenger?.AnimManager?.StopAnimation(meta.RiderAnim.Animation);
                eagent.AnimManager.StopAnimation(meta.Animation);
            }

            // Handle transition from swimming to walking
            if (eagent.Swimming)
            {
                ebg.CurrentGait = ForwardSpeed > 0 ? ebg.Gaits["swim"] : ebg.Gaits["swimback"];
            }
            else if (!eagent.Swimming && wasSwimming)
            {
                ebg.CurrentGait = ebg.Gaits["walk"];
            }

            wasSwimming = eagent.Swimming;

            eagent.Controls.Backward = ForwardSpeed < 0;
            eagent.Controls.Forward = ForwardSpeed >= 0;
            eagent.Controls.Sprint = ebg.CurrentGait.IsSprint && ForwardSpeed > 0;

            string nowTurnAnim=null;
            if (ForwardSpeed >= 0)
            {
                if (AngularVelocity > 0.001)
                {
                    nowTurnAnim = "turn-left";
                }
                else if (AngularVelocity < -0.001)
                {
                    nowTurnAnim = "turn-right";
                }
            }
            // This update fixes idle turn animation not stopping when entity has separate idle turn animations
            if (nowTurnAnim != curTurnAnim)
            {
                if (curTurnAnim != null) eagent.StopAnimation(curTurnAnim);
                var anim = (ForwardSpeed == 0 ? "idle-" : "") + nowTurnAnim;
                curTurnAnim = anim;
                eagent.StartAnimation(anim);
            }

            ControlMeta nowControlMeta;

            shouldMove = ForwardSpeed != 0;
            if (!shouldMove && !jumpNow)
            {
                if (curControlMeta != null) Stop();
                curAnim = Controls[eagent.Swimming ? "swim" : "idle"].RiderAnim;

                if (eagent.Swimming) nowControlMeta = Controls["swim"];
                else nowControlMeta = null;
            }
            else
            {
                nowControlMeta = Controls.FirstOrDefault(c => c.Key == ebg.CurrentGait.Code).Value;

                nowControlMeta ??= Controls["idle"];
                                
                eagent.Controls.Jump = jumpNow;

                if (jumpNow)
                {
                    IsInMidJump = true;
                    jumpNow = false;
                    if (eagent.Properties.Client.Renderer is EntityShapeRenderer esr) 
                        esr.LastJumpMs = capi.InWorldEllapsedMilliseconds;

                    nowControlMeta = Controls["jump"];

                    if (ForwardSpeed != 0) nowControlMeta.EaseOutSpeed = 30;

                    foreach (var seat in Seats) seat.Passenger?.AnimManager?.StartAnimation(nowControlMeta.RiderAnim);

                    EntityPlayer entityPlayer = entity as EntityPlayer;
                    IPlayer player = entityPlayer?.World.PlayerByUid(entityPlayer.PlayerUID);
                    entity.PlayEntitySound("jump", player, false);
                }
                else
                {
                    curAnim = nowControlMeta.RiderAnim;
                }
            }

            if (nowControlMeta != curControlMeta)
            {
                if (curControlMeta != null && curControlMeta.Animation != "jump")
                {
                    eagent.StopAnimation(curControlMeta.Animation);
                }

                curControlMeta = nowControlMeta;
                eagent.AnimManager.StartAnimation(nowControlMeta);
            }

            if (api.Side == EnumAppSide.Server)
            {
                eagent.Controls.Sprint = false; // Uh, why does the elk speed up 2x with this on?
            }
        }

        private void ConvertToTamedAnimal()
        {
            var api = entity.World.Api;

            if (api.Side == EnumAppSide.Client) return;
            var etype = api.World.GetEntityType(AssetLocation.Create(tamedEntityCode, entity.Code.Domain));
            if (etype == null) return;

            var entitytamed = api.World.ClassRegistry.CreateEntity(etype);
            entitytamed.ServerPos.SetFrom(entity.Pos);
            entitytamed.WatchedAttributes = (SyncedTreeAttribute)entity.WatchedAttributes.Clone();

            entity.Die(EnumDespawnReason.Expire);
            api.World.SpawnEntity(entitytamed);
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            foreach (var seat in Seats)
            {
                (seat?.Entity as EntityAgent)?.TryUnmount();
            }

            base.OnEntityDeath(damageSourceForDeath);
        }

        public void Stop()
        {
            gaitSound?.Stop();
            ebg.SetIdle();
            eagent.Controls.StopAllMovement();
            eagent.Controls.WalkVector.Set(0, 0, 0);
            eagent.Controls.FlyVector.Set(0,0,0);
            shouldMove = false;
            if (curControlMeta != null && curControlMeta.Animation != "jump")
            {
                eagent.StopAnimation(curControlMeta.Animation);
            }
            curControlMeta = null;
            eagent.StartAnimation("idle");
        }



        public override void OnGameTick(float dt)
        {
            if (api.Side == EnumAppSide.Server)
            {
                updateAngleAndMotion(dt);
            }

            updateRidingState();

            if (!AnyMounted() && eagent.Controls.TriesToMove && eagent?.MountedOn != null)
            {
                eagent.TryUnmount();
            }

            if (shouldMove)
            {
                // Adjust move speed based based on gait and control meta
                var curMoveSpeed = curControlMeta.MoveSpeed > 0
                    ? curControlMeta.MoveSpeed
                    : ebg.CurrentGait.MoveSpeed * curControlMeta.MoveSpeedMultiplier;

                move(dt, eagent.Controls, curMoveSpeed);
            } else
            {
                if (entity.Swimming) eagent.Controls.FlyVector.Y = 0.2;
            }

            updateSoundState(dt);
        }

        float notOnGroundAccum;
        private void updateSoundState(float dt)
        {
            if (capi == null) return;

            if (eagent.OnGround) notOnGroundAccum = 0;
            else notOnGroundAccum += dt;

            gaitSound?.SetPosition((float)entity.Pos.X, (float)entity.Pos.Y, (float)entity.Pos.Z);

            if (Controls.TryGetValue(ebg.CurrentGait.Code, out ControlMeta controlMeta))
            {
                var gaitMeta = ebg.CurrentGait;

                curSoundCode = eagent.Swimming || notOnGroundAccum > 0.2 ? null : gaitMeta.Sound;

                bool nowChange = curSoundCode != prevSoundCode;

                if (nowChange)
                {
                    gaitSound?.Stop();
                    prevSoundCode = curSoundCode;

                    if (curSoundCode is null) return;

                    gaitSound = capi.World.LoadSound(new SoundParams()
                    {
                        Location = gaitMeta.Sound.Clone().WithPathPrefix("sounds/"),
                        DisposeOnFinish = false,
                        Position = entity.Pos.XYZ.ToVec3f(),
                        ShouldLoop = true
                    });

                    gaitSound?.Start();
                }
            }
        }


        private void move(float dt, EntityControls controls, float nowMoveSpeed)
        {
            double cosYaw = Math.Cos(entity.Pos.Yaw);
            double sinYaw = Math.Sin(entity.Pos.Yaw);
            controls.WalkVector.Set(sinYaw, 0, cosYaw);
            controls.WalkVector.Mul(nowMoveSpeed * GlobalConstants.OverallSpeedMultiplier * ForwardSpeed);

            // Make it walk along the wall, but not walk into the wall, which causes it to climb
            if (entity.Properties.RotateModelOnClimb && controls.IsClimbing && entity.ClimbingOnFace != null && entity.Alive)
            {
                BlockFacing facing = entity.ClimbingOnFace;
                if (Math.Sign(facing.Normali.X) == Math.Sign(controls.WalkVector.X))
                {
                    controls.WalkVector.X = 0;
                }

                if (Math.Sign(facing.Normali.Z) == Math.Sign(controls.WalkVector.Z))
                {
                    controls.WalkVector.Z = 0;
                }
            }

            if (entity.Swimming)
            {
                controls.FlyVector.Set(controls.WalkVector);

                Vec3d pos = entity.Pos.XYZ;
                Block inblock = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)(pos.Y), (int)pos.Z, BlockLayersAccess.Fluid);
                Block aboveblock = entity.World.BlockAccessor.GetBlock((int)pos.X, (int)(pos.Y + 1), (int)pos.Z, BlockLayersAccess.Fluid);
                float waterY = (int)pos.Y + inblock.LiquidLevel / 8f + (aboveblock.IsLiquid() ? 9 / 8f : 0);
                float bottomSubmergedness = waterY - (float)pos.Y;

                // 0 = at swim line
                // 1 = completely submerged
                float swimlineSubmergedness = GameMath.Clamp(bottomSubmergedness - ((float)entity.SwimmingOffsetY), 0, 1);
                swimlineSubmergedness = Math.Min(1, swimlineSubmergedness + 0.075f);
                controls.FlyVector.Y = GameMath.Clamp(controls.FlyVector.Y, 0.002f, 0.004f) * swimlineSubmergedness*3;

                if (entity.CollidedHorizontally)
                {
                    controls.FlyVector.Y = 0.05f;
                }

                eagent.Pos.Motion.Y += (swimlineSubmergedness-0.1)/300.0;
            }
        }

        public override string PropertyName() => "rideable";
        public void Dispose() { }

        public void DidUnmount(EntityAgent entityAgent)
        {
            Stop();

            LastDismountTotalHours = entity.World.Calendar.TotalHours;
            foreach (var meta in Controls.Values)
            {
                if (meta.RiderAnim?.Animation != null)
                {
                    entityAgent.StopAnimation(meta.RiderAnim.Animation);
                }
            }

            if (eagent.Swimming)
            {
                eagent.StartAnimation("swim");
            }
        }

        long mountedTotalMs;
        public void DidMount(EntityAgent entityAgent)
        {
            updateControlScheme();
            mountedTotalMs = api.World.ElapsedMilliseconds;
        }

        public override bool ToleratesDamageFrom(Entity eOther, ref EnumHandling handling)
        {
            if (eOther != null && Controller == eOther)
            {
                handling = EnumHandling.PreventDefault;
                return true;
            }
            return false;
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            if (RemainingSaddleBreaks > 0)
            {
                infotext.AppendLine(Lang.Get("{0} saddle breaks required every {1} days to fully tame.", RemainingSaddleBreaks, saddleBreakDayInterval));
            }
            base.GetInfoText(infotext);
        }
    }

    public class ElkAnimationManager : AnimationManager
    {
        public string animAppendix = "-antlers";

        public override void ResetAnimation(string animCode)
        {
            base.ResetAnimation(animCode);
            base.ResetAnimation(animCode + animAppendix);
        }

        public override void StopAnimation(string code)
        {
            base.StopAnimation(code);
            base.StopAnimation(code + animAppendix);
        }

        public override bool StartAnimation(AnimationMetaData animdata)
        {
            return base.StartAnimation(animdata);
        }
    }

    public class ControlMeta : AnimationMetaData
    {
        public float MoveSpeedMultiplier; // Multiplied by GaitMeta MoveSpeed to get rideable speed
        public float MoveSpeed; // Overrides GaitMeta MoveSpeed
        public AnimationMetaData RiderAnim;
    }
}