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
using Vintagestory.API.Server;

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
        /// <summary>
        /// This is the current animation for the rider
        /// </summary>
        public AnimationMetaData curAnim;
        /// <summary>
        /// This is the current animation for any passenger who is not the rider, for example a pillion passenger
        /// </summary>
        public AnimationMetaData curAnimPassanger;

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
        protected bool onlyTwoGaits = false;
        protected bool prevPrevForwardsKey = false;  // Used to more reliably filter out long presses of Forwards sometimes detected as two presses (because there is a frame where the control is false, for unknown reasons)
        protected bool prevPrevBackwardsKey = false;  // Used to more reliably filter out long presses of Backwards sometimes being detected as two presses (because there is a frame where the control is false, for unknown reasons)
        protected GaitMeta CurrentGait;

        ControlMeta curControlMeta = null;
        bool shouldMove = false;

        internal string prevSoundCode;
        internal string curSoundCode = null;

        string curTurnAnim = null;
        string curMoveAnim = null;
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

            if (attributes["saddleBreaksRequired"].Exists)
            {
                if (!entity.WatchedAttributes.HasAttribute("remainingSaddleBreaksRequired") && api.Side == EnumAppSide.Server)
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

            foreach (var val in Controls.Values)
            {
                val.RiderAnim?.Init();
                val.PassengerAnim?.Init();
            }
            SetPassengerAnimationsToIdle();

            capi?.Event.RegisterRenderer(this, EnumRenderStage.Before, "rideablesim");
            if (api.Event is IServerEventAPI serverEvents)
            {
                serverEvents.MountGaitReceived += ReceiveGaitFromClient;
            }
        }

        private void SetPassengerAnimationsToIdle()
        {
            var idleControl = Controls["idle"];
            curAnim = idleControl.RiderAnim;
            curAnimPassanger = idleControl.GetPassengerAnim();
        }

        /// <summary>
        /// Server-side method called when the server has received the gait from the client-authoritative client (i.e. the rider's client)
        /// (Helps fix de-sync issues especially for multiplayer observers)
        /// </summary>
        /// <param name="mountEntity"></param>
        /// <param name="gaitCode"></param>
        private void ReceiveGaitFromClient(Entity mountEntity, string gaitCode)
        {
            if (mountEntity != entity || gaitCode == null) return;

            foreach (var gait in RideableGaitOrder)
            {
                if (gait.Code == gaitCode)
                {
                    if (gait == CurrentGait) return;     // No action needed if the server already has this gait

                    if (gait == ebg.IdleGait)
                    {
                        Stop();
                    }
                    else
                    {
                        SetGait(gait);
                    }

                    break;
                }
            }
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

            onlyTwoGaits = RideableGaitOrder.Count(g => g.MoveSpeed > 0 && g.Backwards == false) == 2;
            saddleBreakGait = ebg.Gaits.FirstOrDefault(g => g.Value.Code == saddleBreakGaitCode).Value;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);

            if (api.Event is IServerEventAPI serverEvents)
            {
                serverEvents.MountGaitReceived -= ReceiveGaitFromClient;
            }
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
            CurrentGait = ebg.IdleGait;
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

            CurrentGait ??= ebg.CurrentGait;   // Initialize CurrentGait if necessary
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

            float yawMultiplier = ebg.GetYawMultiplier();
            AngularVelocity = motion.Y * yawMultiplier;

            entity.SidedPos.Yaw += (float)motion.Y * dt * 30f;
            entity.SidedPos.Yaw = entity.SidedPos.Yaw % GameMath.TWOPI;

            if (entity.World.ElapsedMilliseconds - lastJumpMs < 2000 && entity.World.ElapsedMilliseconds - lastJumpMs > 200 && entity.OnGround)
            {
                eagent.StopAnimation("jump");
                eagent.AnimManager.AnimationsDirty = true;
            }
        }

        bool prevForwardKey, prevBackwardKey, prevSprintKey;
        public void SpeedUp() => SetNextGait(true);
        public void SlowDown() => SetNextGait(false);

        public virtual GaitMeta GetNextGait(bool forward, GaitMeta currentGait = null)
        {
            currentGait ??= CurrentGait;

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

        public void SetGait(GaitMeta nextGait)
        {
            CurrentGait = nextGait;
            ebg.CurrentGait = nextGait;
            ForwardSpeed = nextGait.Backwards  ? - 1 : (nextGait == ebg.IdleGait ? 0 : 1);
        }

        public void SetNextGait(bool forward, GaitMeta nextGait = null)
        {
            nextGait ??= GetNextGait(forward);

            CurrentGait = nextGait;
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
                if (seat.Config.Controllable && seat.Passenger != null)
                {
                    Controller = seat.Passenger;
                    break;      // the controller will be the first found passenger who is in a controllable seat; in principle a rideable could have more than one controllable seat
                }
            }

            foreach (var seat in Seats)
            {
                if (entity.OnGround) coyoteTimer = 0.15f;

                if (seat.Passenger == null) continue;

                if (seat.Passenger is EntityPlayer eplr)
                {
                    eplr.Controls.LeftMouseDown = seat.Controls.LeftMouseDown;
                    if (eplr.HeadYawLimits == null)
                    {
                        eplr.BodyYawLimits = new AngleConstraint(entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, seat.Config.BodyYawLimit ?? GameMath.PIHALF);
                        eplr.HeadYawLimits = new AngleConstraint(entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD, GameMath.PIHALF);
                    }
                    else
                    {
                        eplr.BodyYawLimits.X = entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD;
                        eplr.BodyYawLimits.Y = seat.Config.BodyYawLimit ?? GameMath.PIHALF;
                        eplr.HeadYawLimits.X = entity.Pos.Yaw + seat.Config.MountRotation.Y * GameMath.DEG2RAD;
                        eplr.HeadYawLimits.Y = GameMath.PIHALF;
                    }

                }

                if (Controller != seat.Passenger) continue;
                if (capi != null && seat.Passenger != capi.World.Player.Entity) CurrentGait = ebg.CurrentGait;   // sync from server to us (through WatchedAttributes system)  but don't let the server override if we are the controlling rider client


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


                // Only able to jump every 1500ms. Only works while on the ground. (But for clients on the pillion we omit the ground check, because the elk already left the ground before we receive the Jump control) 
                if (controls.Jump && entity.World.ElapsedMilliseconds - lastJumpMs > 1500 && entity.Alive && (entity.OnGround || coyoteTimer > 0 || (api.Side == EnumAppSide.Client && entity.EntityId != Controller.EntityId)))
                {
                    lastJumpMs = entity.World.ElapsedMilliseconds;
                    jumpNow = true;
                }

                if (scheme == EnumControlScheme.Hold && !controls.TriesToMove) continue;

                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                // We only let a keypress change the gait and animations if we are a client: this is the client-authoritative system  (prevents de-sync as server and client may see keypresses in different ticks or not at all)
                if (api.Side != EnumAppSide.Server)
                {
                    // Detect if button currently being pressed
                    bool nowForwards = controls.Forward;
                    bool nowBackwards = controls.Backward;
                    bool nowSprint = controls.Sprint;
                    bool sprintPressed = nowSprint && !prevSprintKey;
                    prevSprintKey = nowSprint;

                    // Toggling this control off for curb bridle so that the next press of the sprint key will be a fresh press
                    // Need this to allow cycling up with sprint rather than just treating it as a boolean
                    // Only applies if there are more than two gaits specified for this mount
                    if (scheme == EnumControlScheme.Press && !onlyTwoGaits)
                    {
                        prevSprintKey = false;
                    }

                    // Detect if current press is a fresh press
                    bool backwardPressed = nowBackwards && !prevBackwardKey && !prevPrevBackwardsKey;
                    long nowMs = entity.World.ElapsedMilliseconds;

                    // This ensures we start moving without sprint key
                    if (nowForwards && !prevForwardKey)
                    {
                        if (ebg.IsIdleGait(CurrentGait) && !prevPrevForwardsKey)
                        {
                            SpeedUp();
                            lastGaitChangeMs = nowMs;
                        }

                        // Handle backward to idle change without sprint key
                        else if (ebg.IsBackwards(CurrentGait))
                        {
                            CurrentGait = ebg.IdleGait;
                            lastGaitChangeMs = nowMs;
                        }
                    }

                    if (scheme == EnumControlScheme.Hold)
                    {
                        if ((!nowForwards && ebg.IsForwards(CurrentGait)) || (!nowBackwards && ebg.IsBackwards(CurrentGait)))
                        {
                            // This only reached if Left or Right is being pressed, otherwise skipped entirely by the Controls.TriesToMove check earlier
                            CurrentGait = ebg.IdleGait;
                            lastGaitChangeMs = nowMs;
                        }
                    }

                    // Cycle up with sprint
                    if (sprintPressed && ebg.IsForwards(CurrentGait) && nowMs - lastGaitChangeMs > 300)
                    {
                        SpeedUp();
                        lastGaitChangeMs = nowMs;
                    }

                    // Cycle down with back or when letting go of sprint when there are only two gaits
                    bool cycleDown = backwardPressed || (!nowSprint && CurrentGait.IsSprint && scheme == EnumControlScheme.Hold);
                    if (cycleDown && nowMs - lastGaitChangeMs > 300)
                    {
                        controls.Sprint = false;
                        SlowDown();
                        lastGaitChangeMs = nowMs;
                    }

                    prevPrevForwardsKey = prevForwardKey;  //   Used to "de-bounce", as sometimes a long-ish press of the Forwards key has a frame client-side when nowForwards is false
                    prevPrevBackwardsKey = prevBackwardKey;  //   Used to "de-bounce", as sometimes a long-ish press of the Backwards key has a frame client-side when nowForwards is false
                    prevForwardKey = (scheme == EnumControlScheme.Press && nowForwards);
                    prevBackwardKey = scheme == EnumControlScheme.Press && nowBackwards;
                }

                #region Motion update
                if (canturn && (controls.Left || controls.Right))
                {
                    float dir = controls.Left ? 1 : -1;
                    angularMotion += ebg.GetYawMultiplier() * dir * dt;
                }

                if (ebg.IsForwards(CurrentGait) || ebg.IsBackwards(CurrentGait))
                {
                    float dir = ebg.IsForwards(CurrentGait) ? 1 : -1;
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
                if (api.World.Rand.NextDouble() < 0.05) jumpNow = true;
                SetGait(saddleBreakGait);

                if (api.World.ElapsedMilliseconds - mountedTotalMs > 4000)
                {
                    DoSaddleBreak();
                    return;
                }
            }

            bool wasMidJump = IsInMidJump;
            IsInMidJump &= (entity.World.ElapsedMilliseconds - lastJumpMs < 500 || !entity.OnGround) && !entity.Swimming;

            if (wasMidJump && !IsInMidJump)
            {
                var meta = Controls["jump"];
                foreach (var seat in Seats)
                {
                    var anim = meta.GetSeatAnimation(seat);
                    seat.Passenger?.AnimManager?.StopAnimation(anim.Code);
                }

                eagent.AnimManager.StopAnimation(meta.Code);
            }

            // Handle transition from swimming to walking
            if (eagent.Swimming)
            {
                if (ForwardSpeed > 0) CurrentGait = ebg.Gaits["swim"];
                else if (ForwardSpeed < 0) CurrentGait = ebg.Gaits["swimback"];
            }
            else if (wasSwimming)
            {
                if (ForwardSpeed > 0) CurrentGait = ebg.Gaits["walk"];
                else if (ForwardSpeed < 0) CurrentGait = ebg.Gaits["walkback"];
            }

            wasSwimming = eagent.Swimming;

            // radfast 27.9.25 - we should not be setting to the Controls, this needs future review
            eagent.Controls.Backward = ForwardSpeed < 0;
            eagent.Controls.Forward = ForwardSpeed >= 0;
            eagent.Controls.Sprint = CurrentGait.IsSprint && ForwardSpeed > 0;

            string nowTurnAnim =null;
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
                if (curTurnAnim != null)
                {
                    eagent.StopAnimation(curTurnAnim);
                    eagent.AnimManager.AnimationsDirty = true;
                }
                if (nowTurnAnim == null)
                {
                    curTurnAnim = null;
                }
                else
                {
                    var anim = (ForwardSpeed == 0 ? "idle-" : "") + nowTurnAnim;
                    curTurnAnim = anim;
                    eagent.StartAnimation(anim);
                    curMoveAnim = null;
                }
            }
            else if (curMoveAnim == null && curControlMeta != null)
            {
                // Restart normal motion animation after a turn
                eagent.AnimManager.StartAnimation(curControlMeta);
                curMoveAnim = curControlMeta.Animation;
            }

            ControlMeta nowControlMeta;

            bool doRiders = false;
            shouldMove = ForwardSpeed != 0;
            if (!shouldMove && !jumpNow)
            {
                if (curControlMeta != null) Stop();
                curAnim = Controls[eagent.Swimming ? "swim" : "idle"].RiderAnim;
                curAnimPassanger = Controls[eagent.Swimming ? "swim" : "idle"].GetPassengerAnim();

                if (eagent.Swimming)
                    nowControlMeta = Controls["swim"];
                else nowControlMeta = null;
            }
            else
            {
                nowControlMeta = Controls.FirstOrDefault(c => c.Key == CurrentGait.Code).Value;

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

                    foreach (var seat in Seats)
                    {
                        if (seat.Passenger != Controller) continue;   // Pillion passenger should not use jump animation
                        var anim = nowControlMeta.GetSeatAnimation(seat);
                        seat.Passenger?.AnimManager?.StartAnimation(anim);
                    }

                    EntityPlayer entityPlayer = entity as EntityPlayer;
                    IPlayer player = entityPlayer?.World.PlayerByUid(entityPlayer.PlayerUID);
                    entity.PlayEntitySound("jump", player, false);
                }
                else
                {
                    doRiders = true;
                }
            }

            StartAnimationsForControl(nowControlMeta, doRiders);

            if (api.Side == EnumAppSide.Server)
            {
                eagent.Controls.Sprint = false; // Uh, why does the elk speed up 2x with this on?
            }
        }

        private void StartAnimationsForControl(ControlMeta nowControlMeta, bool doRiders)
        {
            if (nowControlMeta != curControlMeta)
            {
                if (curControlMeta == null)
                {
                    if (nowControlMeta.Code != "jump")
                    {
                        eagent.StopAnimation(Controls["idle"].Code);
                        Controller?.StopAnimation(Controls["idle"].RiderAnim.Code);
                        eagent.AnimManager.AnimationsDirty = true;
                    }
                }
                else if (curControlMeta.Code != "jump")
                {
                    eagent.StopAnimation(curControlMeta.Code);
                    eagent.AnimManager.AnimationsDirty = true;
                }

                curControlMeta = nowControlMeta;
                if (api.Side == EnumAppSide.Client) nowControlMeta.ClientSide = true;
                eagent.AnimManager.StartAnimation(nowControlMeta);
                curMoveAnim = nowControlMeta.Animation;
            }

            if (doRiders)
            {
                curAnim = nowControlMeta.RiderAnim;
                curAnimPassanger = nowControlMeta.GetPassengerAnim();
            }
        }

        protected void DoSaddleBreak()
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
            CurrentGait = ebg.IdleGait;
            ForwardSpeed = 0;
            eagent.Controls.StopAllMovement();
            eagent.Controls.WalkVector.Set(0, 0, 0);
            eagent.Controls.FlyVector.Set(0,0,0);
            if (curTurnAnim != null)
            {
                eagent.StopAnimation(curTurnAnim);
                eagent.AnimManager.AnimationsDirty = true;
                curTurnAnim = null;
            }
            shouldMove = false;
            if (curControlMeta != null && curControlMeta.Code != "jump")
            {
                eagent.StopAnimation(curControlMeta.Code);
                eagent.AnimManager.AnimationsDirty = true;
            }
            curControlMeta = null;
            curMoveAnim = null;
            eagent.StartAnimation("idle");
        }



        public override void OnGameTick(float dt)
        {
            CurrentGait ??= ebg.CurrentGait;   // Initialize CurrentGait if necessary

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
                    : CurrentGait.MoveSpeed * curControlMeta.MoveSpeedMultiplier;

                move(dt, eagent.Controls, curMoveSpeed);
            } else
            {
                if (entity.Swimming) eagent.Controls.FlyVector.Y = 0.2;
            }

            updateSoundState(dt);

            if (api.Side == EnumAppSide.Server) ebg.CurrentGait = CurrentGait;
        }

        float notOnGroundAccum;
        private void updateSoundState(float dt)
        {
            if (capi == null) return;

            if (eagent.OnGround) notOnGroundAccum = 0;
            else notOnGroundAccum += dt;

            gaitSound?.SetPosition((float)entity.Pos.X, (float)entity.Pos.Y, (float)entity.Pos.Z);

            if (Controls.TryGetValue(CurrentGait.Code, out ControlMeta controlMeta))
            {
                var gaitMeta = CurrentGait;

                curSoundCode = eagent.Swimming || notOnGroundAccum > 0.2 ? null : gaitMeta.Sound;

                bool nowChange = curSoundCode != prevSoundCode;

                if (nowChange)
                {
                    gaitSound?.Stop();
                    gaitSound?.Dispose();
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
                Block inblock = entity.World.BlockAccessor.GetBlockRaw((int)pos.X, (int)(pos.Y), (int)pos.Z, BlockLayersAccess.Fluid);
                Block aboveblock = entity.World.BlockAccessor.GetBlockRaw((int)pos.X, (int)(pos.Y + 1), (int)pos.Z, BlockLayersAccess.Fluid);
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
                    eagent.AnimManager.AnimationsDirty = true;
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
            AnimationsDirty = true;
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
        public AnimationMetaData PassengerAnim;


        /// <summary>
        /// If we are a passenger try to get the PassengerAnim if it exists else default to RiderAnim
        /// </summary>
        /// <returns></returns>
        public AnimationMetaData GetSeatAnimation(IMountableSeat seat)
        {
            return !seat.CanControl && PassengerAnim != null ? PassengerAnim : RiderAnim;
        }

        /// <summary>
        /// Return a PassengerAnim if it exists else return the RiderAnim
        /// </summary>
        /// <returns></returns>
        public AnimationMetaData GetPassengerAnim()
        {
            return PassengerAnim != null ? PassengerAnim : RiderAnim;
        }
    }
}
