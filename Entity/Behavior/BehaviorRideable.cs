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

namespace Vintagestory.GameContent
{
    public delegate bool CanRideDelegate(IMountableSeat seat, out string? errorMessage);

    public enum EnumControlScheme
    {
        Hold,
        Press
    }

    public class EntityBehaviorRideable(Entity entity) : EntityBehaviorSeatable(entity), IMountable, IRenderer, IMountableListener
    {
        // List of gaits in order of keypress controls for the rideable entity. In practice, this is usually the same as ordering by increasing speed, but doesn't have to be.
        public FastSmallDictionary<EnumHabitat, List<GaitMeta>> RideableGaitOrder = new(1);
        public Vec3f MountAngle { get; set; } = new Vec3f();
        public EntityPos SeatPosition => entity.Pos;
        public double RenderOrder => 1;
        public int RenderRange => 100;
        public virtual float SpeedMultiplier => 1f;
        public Entity Mount => entity;

        public bool IsInMidJump;
        public event CanRideDelegate? CanRide;
        public event CanRideDelegate? CanTurn;

        // Time the player can walk off an edge before gravity applies.
        protected float coyoteTimer;
        // Time the player last jumped.
        protected long lastJumpMs;
        protected bool jumpNow;
        protected EntityAgent eagent = (EntityAgent)entity;

        protected FastSmallDictionary<string, ControlMeta> Controls = new(1);

        // List of gaits in order of increasing speed for the rideable entity, by environment
        // Set from json, then used and cleared to null during AfterInitialized
        protected FastSmallDictionary<EnumHabitat, string?[]>? GaitOrderCodes;
        protected ICoreClientAPI? capi;
        protected EntityBehaviorGait ebg = null!;
        protected int minGeneration = 0; // Minimum generation for the animal to be rideable
        protected GaitMeta? saddleBreakGait;
        protected string? saddleBreakGaitCode;

        protected ControlMeta? curControlMeta = null;
        public ControlMeta? CurrentControlMeta => curControlMeta;

        protected EnumControlScheme scheme;

        #region Semitamed animals

        protected float saddleBreakDayInterval;
        protected string? tamedEntityCode;

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

            capi = entity.Api as ICoreClientAPI;

            if (attributes["saddleBreaksRequired"].Exists)
            {
                if (!entity.WatchedAttributes.HasAttribute("remainingSaddleBreaksRequired") && entity.Api.Side == EnumAppSide.Server)
                {
                    RemainingSaddleBreaks = GameMath.RoundRandom(entity.Api.World.Rand, attributes["saddleBreaksRequired"].AsObject<NatFloat>()!.nextFloat(1, entity.Api.World.Rand));
                }

                saddleBreakDayInterval = attributes["saddleBreakDayInterval"].AsFloat();
                tamedEntityCode = attributes["tamedEntityCode"].AsString();
                saddleBreakGaitCode = attributes["saddleBreakGait"].AsString();
            }

            Controls = attributes["controls"].AsObject<FastSmallDictionary<string, ControlMeta>>(Controls);
            foreach (var control in Controls)
            {
                if (control.Value.PassengerAnim == null && !attributes["controls"][control.Key]["passengerAnim"].Exists)
                {
                    control.Value.PassengerAnim = control.Value.RiderAnim;
                }
            }

            minGeneration = attributes["minGeneration"].AsInt(0);
            GaitOrderCodes = attributes["rideableGaitOrder"].AsObject<FastSmallDictionary<EnumHabitat, string?[]>>();
            string?[]? oldFormatLandGaits = attributes["rideableGaitOrder"].AsArray<string>();
            if (GaitOrderCodes == null && oldFormatLandGaits != null)
            {
                GaitOrderCodes = new(1);
                GaitOrderCodes[EnumHabitat.Land] = oldFormatLandGaits;
                entity.Api.Logger.Warning("Outdated json format for rideableGaitOrder on entity " + entity.Code + ", should list gaits by environment (e.g. Land, Sea) explicitly");
            }

            foreach (var val in Controls.Values)
            {
                val.RiderAnim?.Init();
                val.PassengerAnim?.Init();
            }
            curControlMeta = Controls["idle"];

            capi?.Event.RegisterRenderer(this, EnumRenderStage.Before, "rideablesim");
            if (entity.Api.Event is IServerEventAPI serverEvents)
            {
                serverEvents.MountGaitReceived += ReceiveGaitFromClient;
            }
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

            // No action needed if the server already has this gait
            if (gaitCode == ebg.CurrentGait.Code) return;

            // Gait may not be in the dictionary if the client sent a bad packet
            ebg.Gaits.TryGetValue(gaitCode, out GaitMeta gait);
            if (gait != null)
            {
                ebg.CurrentGait = gait;
            }
        }

        public override void AfterInitialized(bool onFirstSpawn)
        {
            base.AfterInitialized(onFirstSpawn);

            ebg = eagent.GetBehavior<EntityBehaviorGait>()!;

            // Gaits are required for rideable entities
            if (ebg is null)
            {
                throw new Exception("EntityBehaviorGait not found on rideable entity. Ensure the entity's json file correctly provides one.");
            }

            ArgumentNullException.ThrowIfNull(GaitOrderCodes);
            foreach (var entry in GaitOrderCodes)
            {
                List<GaitMeta> order = RideableGaitOrder.TryGetValue(entry.Key);
                if (order == null)
                {
                    order = new();
                    RideableGaitOrder[entry.Key] = order;
                }
                foreach (var str in entry.Value)
                {
                    if (str == null) throw new Exception("Invalid gait code found in rideable behavior");
                    GaitMeta? gait = ebg?.Gaits[str];
                    if (gait == null)
                    {
                        entity.Api.Logger.Warning($"Unable to find definition of gait {str} from entity {entity.Code}");
                        continue;
                    }
                    if (gait.Environment != entry.Key)
                    {
                        entity.Api.Logger.Warning($"Wrong environment list for gait {str} from entity {entity.Code}: entered as part of {entry.Key} but gait has environment {gait.Environment}");
                    }

                    order.Add(gait);
                }
            }
            GaitOrderCodes = null;

            saddleBreakGait = ebg!.Gaits.FirstOrDefault(g => g.Value.Code == saddleBreakGaitCode).Value;
        }

        // toggleFastest lets you cycle between the fastest two gaits by calling this repeatedly - e.g. between canter and gallop, by pressing the same button again
        public void SpeedUp(bool toggleFastest) => ebg.CurrentGait = GetNextGait(true, toggleFastest);
        public void SlowDown() => ebg.CurrentGait = GetNextGait(false, false);

        public virtual GaitMeta GetNextGait(bool forward, bool toggleFastest, GaitMeta? currentGait = null)
        {
            currentGait ??= ebg.CurrentGait;
            ArgumentNullException.ThrowIfNull(currentGait);

            List<GaitMeta>? order = RideableGaitOrder.TryGetValue(currentGait.Environment);
            if (order == null ) return currentGait;

            if (this.IsBeingControlled())
            {
                int currentIndex = order.FindIndex(g => g.Code == currentGait.Code);
                // If the current gait is not part of the rideable order, try to find the next fastest
                if (currentIndex < 0)
                {
                    return order.FirstOrDefault(
                        g => forward? g.MoveSpeed > currentGait.MoveSpeed : g.MoveSpeed < currentGait.MoveSpeed,
                        order[order.Count-1]
                    );
                }
                int nextIndex = forward ? currentIndex + 1 : currentIndex - 1;

                // Boundary behavior
                if (nextIndex < 0) nextIndex = 0;
                // Don't try toggling if there isn't a second gait to toggle between
                toggleFastest = toggleFastest && currentIndex > 0 && order[currentIndex - 1].HasForwardMotion;
                if (nextIndex >= order.Count) nextIndex = toggleFastest ? currentIndex - 1 : order.Count - 1;

                return order[nextIndex];
            }
            else
            {
                return ebg.IdleGaits.TryGetValue(currentGait.Environment) ?? ebg.IdleGaits[EnumHabitat.Land];
            }
        }

        // Returns true if on a hold control scheme, you need to be holding down the sprint key to stay at this gait
        protected virtual bool IsSprinting()
        {
            var order = RideableGaitOrder.TryGetValue(ebg.CurrentGait.Environment);
            if (order == null || order.Count < 2) return false;

            // Current gait needs to be last in the list, this is the main thing
            if (order[order.Count - 1].Code != ebg.CurrentGait.Code) return false;

            // And there has to be at least one forward but not sprinting gait, too
            for (int i = order.Count - 2; i >= 0; --i)
            {
                if (order[i].HasForwardMotion) return true;
            }

            return false;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            capi?.Event.UnregisterRenderer(this, EnumRenderStage.Before);

            if (entity.Api.Event is IServerEventAPI serverEvents)
            {
                serverEvents.MountGaitReceived -= ReceiveGaitFromClient;
            }

            if (entity.Api.Side == EnumAppSide.Server)
            {
                EntityBehaviorTaskAI? taskAI = entity.GetBehavior<EntityBehaviorTaskAI>();
                if (taskAI != null) taskAI.TaskManager.OnShouldExecuteTask -= TaskManager_OnShouldExecuteTask;

                var attachableBehavior = entity.GetBehavior<EntityBehaviorAttachable>();
                if (attachableBehavior != null) attachableBehavior.Inventory.SlotModified -= Inventory_SlotModified;
            }
            else
            {
                entity.WatchedAttributes.UnregisterListener(UpdateControlScheme);
                ebg.GaitChangedForEnvironmentDelegate -= BehaviorGait_OnGaitChangedForEnvironment;
            }
        }


        public virtual void UnmountPassengers()
        {
            foreach (var seat in Seats)
            {
                (seat.Passenger as EntityAgent)?.TryUnmount();
            }
        }

        public override void OnEntityLoaded()
        {
            SetupTaskBlocker();
        }

        public override void OnEntitySpawn()
        {
            SetupTaskBlocker();
        }

        protected virtual void SetupTaskBlocker()
        {
            var attachableBehavior = entity.GetBehavior<EntityBehaviorAttachable>();

            if (entity.Api.Side == EnumAppSide.Server)
            {
                EntityBehaviorTaskAI? taskAI = entity.GetBehavior<EntityBehaviorTaskAI>();
                if (taskAI != null)
                {
                    taskAI.TaskManager.OnShouldExecuteTask += TaskManager_OnShouldExecuteTask;
                }
                if (attachableBehavior != null)
                {
                    attachableBehavior.Inventory.SlotModified += Inventory_SlotModified;
                }
            } else
            {
                if (attachableBehavior != null)
                {
                    entity.WatchedAttributes.RegisterModifiedListener(attachableBehavior.InventoryClassName, UpdateControlScheme);
                }
                ebg.GaitChangedForEnvironmentDelegate += BehaviorGait_OnGaitChangedForEnvironment;
            }
        }

        protected virtual void Inventory_SlotModified(int obj)
        {
            UpdateControlScheme();
            ebg?.SetIdle();
        }

        protected virtual void UpdateControlScheme()
        {
            var attachableBehavior = entity.GetBehavior<EntityBehaviorAttachable>();
            if (attachableBehavior != null)
            {
                scheme = EnumControlScheme.Hold;
                foreach (var slot in attachableBehavior.Inventory)
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

        protected virtual void BehaviorGait_OnGaitChangedForEnvironment()
        {
            List<GaitMeta>? order = RideableGaitOrder.TryGetValue(ebg.CurrentGait.Environment);
            if (order == null ) return;

            if (!order.Any(g => g.Code == ebg.CurrentGait.Code))
            {
                // Prevents elk from walking slowly instead of trotting when exiting the water.
                // Perhaps there is a better, more general solution than this.
                SpeedUp(false);
            }

            // When exiting the water while holding the sprint key, should get straight back to running
            if (prevSprintKey && scheme == EnumControlScheme.Hold) SpeedUp(false);
        }

        protected virtual bool TaskManager_OnShouldExecuteTask(IAiTask task)
        {
            if (task is AiTaskWander && entity.Api.World.Calendar.TotalHours - LastDismountTotalHours < 24) return false;

            return !Seats.Any(seat => seat.Passenger != null);
        }


        public virtual void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (capi!.IsGamePaused) return;

            UpdateAngleAndMotion(dt);
        }

        protected virtual void UpdateAngleAndMotion(float dt)
        {
            // Ignore lag spikes
            dt = Math.Min(0.5f, dt);

            float step = GlobalConstants.PhysicsFrameTime;
            double angularMotion = SeatsToMotion(step);

            if (jumpNow) UpdateRidingState();

            ebg.AngularVelocity = angularMotion;

            entity.Pos.Yaw += (float)angularMotion * dt * 30f;
            entity.Pos.Yaw = entity.Pos.Yaw % GameMath.TWOPI;

            if (entity.World.ElapsedMilliseconds - lastJumpMs < 2000 && entity.World.ElapsedMilliseconds - lastJumpMs > 200 && entity.OnGround)
            {
                eagent.StopAnimation("jump");
                eagent.AnimManager.AnimationsDirty = true;
            }
        }

        protected bool prevForwardKey, prevBackwardKey, prevSprintKey;
        // For unknown reasons, holding down the key can still have a frame client-side where the control is false. So we check two frames back, as well.
        protected bool prevPrevForwardKey, prevPrevBackwardKey, prevPrevSprintKey;

        // Returns angular motion
        public virtual double SeatsToMotion(float dt)
        {
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

                var controls = seat.Controls;
                bool canride = true;
                bool canturn = true;

                if (RemainingSaddleBreaks > 0)
                {
                    if (entity.Api.World.Rand.NextDouble() < 0.05) angularMotionWild = ((float)entity.Api.World.Rand.NextDouble() * 2 - 1) / 10f;
                    angularMotion = angularMotionWild;
                    canturn = false;
                }

                if (CanRide != null && (controls.Jump || controls.TriesToMove))
                {
                    foreach (CanRideDelegate dele in CanRide.GetInvocationList())
                    {
                        if (!dele(seat, out string? errMsg))
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
                        if (!dele(seat, out string? errMsg))
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
                if (controls.Jump && entity.World.ElapsedMilliseconds - lastJumpMs > 1500 && entity.Alive && (entity.OnGround || coyoteTimer > 0 || (entity.Api.Side == EnumAppSide.Client && entity.EntityId != Controller.EntityId)))
                {
                    lastJumpMs = entity.World.ElapsedMilliseconds;
                    jumpNow = true;
                }

                // We only let a keypress change the gait and animations if we are a client: this is the client-authoritative system  (prevents de-sync as server and client may see keypresses in different ticks or not at all)
                if (entity.Api.Side != EnumAppSide.Server)
                {
                    bool wasIdle = ebg.IsIdle;

                    if (controls.Forward && !prevForwardKey && !prevPrevForwardKey)
                    {
                        SpeedUp(false);
                    }

                    if (controls.Backward && !prevBackwardKey && !prevPrevBackwardKey)
                    {
                        SlowDown();
                    }

                    // Go faster (forwards or backwards) with sprint key, if it is a fresh press or was already held down while idle
                    if (controls.Sprint && (wasIdle || (!prevSprintKey && !prevPrevSprintKey)))
                    {
                        if (ebg.CurrentGait.HasForwardMotion) SpeedUp(true);
                        else if (ebg.CurrentGait.HasBackwardMotion) SlowDown();
                    }

                    if (scheme == EnumControlScheme.Hold)
                    {
                        if (IsSprinting() && !controls.Sprint)
                        {
                            SlowDown();
                        }
                        if ((!controls.Forward && !controls.Backward && !ebg.IsIdle)
                            || (!controls.Forward && ebg.CurrentGait.HasForwardMotion)
                            || (!controls.Backward && ebg.CurrentGait.HasBackwardMotion)
                            || (controls.Forward && controls.Backward))
                        {
                            ebg.SetIdle();
                        }
                    }

                    prevPrevForwardKey = prevForwardKey;
                    prevPrevBackwardKey = prevBackwardKey;
                    prevPrevSprintKey = prevSprintKey;

                    prevForwardKey = controls.Forward;
                    prevBackwardKey = controls.Backward;
                    prevSprintKey = controls.Sprint;
                }

                #region Motion update
                if (canturn && (controls.Left || controls.Right))
                {
                    float dir = controls.Left ? 1 : -1;
                    angularMotion += ebg.GetYawMultiplier() * dir * dt;
                }
                #endregion
            }

            return angularMotion;
        }

        float angularMotionWild = 1/10f;
        protected virtual void UpdateRidingState()
        {
            if (!AnyMounted()) return;

            if (RemainingSaddleBreaks > 0)
            {
                if (entity.Api.World.Rand.NextDouble() < 0.05) jumpNow = true;

                if (saddleBreakGait != null) ebg.CurrentGait = saddleBreakGait;

                if (entity.Api.World.ElapsedMilliseconds - mountedTotalMs > 4000)
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
                    AnimationMetaData? anim = meta.GetSeatAnimation(seat);
                    if (anim != null) seat.Passenger?.AnimManager?.StopAnimation(anim.Code);
                }
            }

            ControlMeta nowControlMeta = Controls.FirstOrDefault(c => c.Key == ebg.CurrentGait.Code).Value;
            nowControlMeta ??= Controls["idle"];

            eagent.Controls.Jump = jumpNow;

            if (jumpNow)
            {
                IsInMidJump = true;
                jumpNow = false;
                if (eagent.Properties.Client.Renderer is EntityShapeRenderer esr)
                    esr.LastJumpMs = capi!.InWorldEllapsedMilliseconds;

                nowControlMeta = Controls["jump"];

                entity.AnimManager.StartAnimation(ebg.Gaits["jump"]);
                eagent.AnimManager.AnimationsDirty = true;

                EntityPlayer? entityPlayer = entity as EntityPlayer;
                IPlayer? player = entityPlayer?.World.PlayerByUid(entityPlayer!.PlayerUID);
                entity.PlayEntitySound("jump", player);
            }
            curControlMeta = nowControlMeta;
        }

        protected virtual void DoSaddleBreak()
        {
            foreach (var seat in Seats)
            {
                if (seat?.Passenger == null) continue;
                var eagent = seat.Passenger as EntityAgent;

                if (entity.Api.World.Rand.NextDouble() < 0.5) eagent?.ReceiveDamage(new DamageSource()
                {
                    CauseEntity = entity,
                    DamageTier = 1,
                    Source = EnumDamageSource.Entity,
                    SourcePos = this.Position.XYZ,
                    Type = EnumDamageType.BluntAttack
                }, 1 + entity.Api.World.Rand.Next(8) / 4f);

                eagent?.TryUnmount();
            }

            jumpNow = false;
            ebg.SetIdle();

            if (entity.Api.World.Calendar.TotalDays - LastSaddleBreakTotalDays > saddleBreakDayInterval)
            {
                RemainingSaddleBreaks--;
                LastSaddleBreakTotalDays = entity.Api.World.Calendar.TotalDays;
                if (RemainingSaddleBreaks <= 0)
                {
                    ConvertToTamedAnimal();
                    return;
                }
            }
        }

        protected virtual void ConvertToTamedAnimal()
        {
            var api = entity.World.Api;

            if (api.Side == EnumAppSide.Client) return;
            var etype = api.World.GetEntityType(AssetLocation.Create(tamedEntityCode, entity.Code.Domain));
            if (etype == null) return;

            var entitytamed = api.World.ClassRegistry.CreateEntity(etype);
            entitytamed.Pos.SetFrom(entity.Pos);
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

        public override void OnGameTick(float dt)
        {
            if (entity.Api.Side == EnumAppSide.Server)
            {
                UpdateAngleAndMotion(dt);
            }

            UpdateRidingState();

            if (!AnyMounted() && eagent.Controls.TriesToMove && eagent?.MountedOn != null)
            {
                eagent.TryUnmount();
            }
        }

        public override string PropertyName() => "rideable";
        public void Dispose() { }

        public virtual void DidUnmount(EntityAgent entityAgent)
        {
            ebg.SetIdle();
            curControlMeta = null;

            LastDismountTotalHours = entity.World.Calendar.TotalHours;
            if (capi != null)
            {
                var iMountable = entity.GetInterface<IMountable>();
                // if we are mounted on this mount update the interpolation position to its current position since the entity was controlled by us and the position is up to date
                // while mounted we won't receive positions updates for our own mount , see ServerUdpNetwork.HandleMountPosition
                if (iMountable?.Controller != null && iMountable.Controller.EntityId == capi.World.Player.Entity.EntityId)
                {
                    var ebInterpolate = entity.GetBehavior<EntityBehaviorInterpolatePosition>();
                    if (ebInterpolate != null)
                    {
                        var handling = EnumHandling.Handled;
                        ebInterpolate.OnReceivedServerPos(true, ref handling);
                    }
                }
            }
        }

        long mountedTotalMs;
        public virtual void DidMount(EntityAgent entityAgent)
        {
            UpdateControlScheme();
            mountedTotalMs = entity.Api.World.ElapsedMilliseconds;
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
    }

    public class ControlMeta
    {
        [Obsolete("Does nothing. Use the gait's movement speed instead.")]
        public float MoveSpeedMultiplier;
        [Obsolete("Does nothing. Use the gait's movement speed instead.")]
        public float MoveSpeed;
        public AnimationMetaData? RiderAnim;
        public AnimationMetaData? PassengerAnim;


        /// <summary>
        /// If we are a passenger try to get the PassengerAnim if it exists else default to RiderAnim
        /// </summary>
        /// <returns></returns>
        public AnimationMetaData? GetSeatAnimation(IMountableSeat seat)
        {
            return !seat.CanControl ? PassengerAnim : RiderAnim;
        }
    }
}
