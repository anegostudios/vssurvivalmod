using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public delegate bool CanRideDelegate(IMountableSeat seat, out string errorMessage);

    public enum EnumControlScheme
    {
        Hold,
        Press
    }

    public class EntityBehaviorRideable : EntityBehaviorSeatable, IMountable, IRenderer, IMountableListener
    {
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
        public bool ShouldSprint;

        public bool IsInMidJump;
        public event CanRideDelegate CanRide;
        public event CanRideDelegate CanTurn;

        protected ICoreAPI api;
        // Time the player can walk off an edge before gravity applies.
        protected float coyoteTimer;
        // Time the player last jumped.
        protected long lastJumpMs;
        protected bool jumpNow;
        protected EntityAgent eagent;
        protected RideableConfig rideableconfig;
        protected ILoadedSound trotSound;
        protected ILoadedSound gallopSound;
        protected ICoreClientAPI capi;


        


        ControlMeta curControlMeta = null;
        bool shouldMove = false;
        public AnimationMetaData curAnim;

        string curTurnAnim = null;
        EnumControlScheme scheme;

        public double lastDismountTotalHours { 
            get
            {
                return entity.WatchedAttributes.GetDouble("lastDismountTotalHours");
            }
            set
            {
                entity.WatchedAttributes.SetDouble("lastDismountTotalHours", value);
            }
        }

        public EntityBehaviorRideable(Entity entity) : base(entity)
        {
            eagent = entity as EntityAgent;
        }

        protected override IMountableSeat CreateSeat(string seatId, SeatConfig config)
        {
            return new EntityRideableSeat(this, seatId, config);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            rideableconfig = attributes.AsObject<RideableConfig>();
            foreach (var val in rideableconfig.Controls.Values) { val.RiderAnim?.Init(); }

            api = entity.Api;
            capi = api as ICoreClientAPI;
            curAnim = rideableconfig.Controls["idle"].RiderAnim;            

            if (capi != null)
            {
                capi.Event.RegisterRenderer(this, EnumRenderStage.Before, "rideablesim");
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
            if (task is AiTaskWander && api.World.Calendar.TotalHours - lastDismountTotalHours < 24) return false;

            return !Seats.Any(seat => seat.Passenger != null);
        }

        bool wasPaused;

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (!wasPaused && capi.IsGamePaused)
            {
                trotSound?.Pause();
                gallopSound?.Pause();
            }
            if (wasPaused && !capi.IsGamePaused)
            {
                if (trotSound?.IsPaused == true) trotSound?.Start();
                if (gallopSound?.IsPaused == true) gallopSound?.Start();
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

            if (jumpNow)
            {
                updateRidingState();
            }

            ForwardSpeed = Math.Sign(motion.X);

            AngularVelocity = motion.Y;
            if (!eagent.Controls.Sprint) AngularVelocity *= 2;

            entity.SidedPos.Yaw += (float)motion.Y * dt * 30f;
            entity.SidedPos.Yaw = entity.SidedPos.Yaw % GameMath.TWOPI;

            if (entity.World.ElapsedMilliseconds - lastJumpMs < 2000 && entity.World.ElapsedMilliseconds - lastJumpMs > 200 && entity.OnGround)
            {
                eagent.StopAnimation("jump");
            }
        }

        bool prevForwardKey, prevBackwardKey, prevSprintKey;

        bool forward, backward, sprint;
        public virtual Vec2d SeatsToMotion(float dt)
        {
            int seatsRowing = 0;

            double linearMotion = 0;
            double angularMotion = 0;

            jumpNow = false;
            coyoteTimer -= dt;

            bool shouldSprint = false;
            Controller = null;

            foreach (var seat in Seats)
            {
                if (entity.OnGround) coyoteTimer = 0.15f;

                if (seat.Passenger == null || !seat.Config.Controllable) continue;

                var eplr = seat.Passenger as EntityPlayer;

                if (eplr != null)
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

                if (scheme == EnumControlScheme.Hold && !controls.TriesToMove)
                {
                    continue;
                }

                float str = ++seatsRowing == 1 ? 1 : 0.5f;

                

                if (scheme == EnumControlScheme.Hold)
                {
                    forward = controls.Forward;
                    backward = controls.Backward;
                    shouldSprint |= controls.Sprint && !entity.Swimming;
                } else
                {
                    bool nowForwards = controls.Forward;
                    bool nowBackwards = controls.Backward;
                    bool nowSprint = controls.Sprint;

                    if (!forward && !backward && nowForwards && !prevForwardKey) { forward = true;  }
                    else if (forward && nowBackwards && !prevBackwardKey) { forward = false; sprint = false; }
                    else if (!backward && nowBackwards && !prevBackwardKey) { backward = true; sprint = false; }
                    else if (backward && nowForwards && !prevForwardKey) {  backward = false; }

                    if (nowSprint && !prevSprintKey && !sprint) sprint = true;
                    else if (nowSprint && !prevSprintKey && sprint) sprint = false;

                    prevForwardKey = nowForwards;
                    prevBackwardKey = nowBackwards;
                    prevSprintKey = nowSprint;
                    shouldSprint = sprint && !entity.Swimming;
                }

                if (canturn && (controls.Left || controls.Right))
                {
                    float dir = controls.Left ? 1 : -1;
                    angularMotion += str * dir * dt;
                }
                if (forward || backward)
                {
                    float dir = forward ? 1 : -1;
                    linearMotion += str * dir * dt * 2f;
                }
            }

            this.ShouldSprint = shouldSprint;

            return new Vec2d(linearMotion, angularMotion);
        }


        protected void updateRidingState()
        {
            if (!AnyMounted()) return;

            bool wasMidJump = IsInMidJump;
            IsInMidJump &= (entity.World.ElapsedMilliseconds - lastJumpMs < 500 || !entity.OnGround) && !entity.Swimming;

            if (wasMidJump && !IsInMidJump)
            {
                var meta = rideableconfig.Controls["jump"];
                foreach (var seat in Seats) seat.Passenger?.AnimManager?.StopAnimation(meta.RiderAnim.Animation);
                eagent.AnimManager.StopAnimation(meta.Animation);
            }

            eagent.Controls.Backward = ForwardSpeed < 0;
            eagent.Controls.Forward = ForwardSpeed >= 0;
            eagent.Controls.Sprint = ShouldSprint && ForwardSpeed > 0;

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
            if (nowTurnAnim != curTurnAnim)
            {
                if (curTurnAnim != null) eagent.StopAnimation(curTurnAnim);
                eagent.StartAnimation((ForwardSpeed == 0 ? "idle-" : "") + (curTurnAnim = nowTurnAnim));
            }

            ControlMeta nowControlMeta;

            shouldMove = ForwardSpeed != 0;
            if (!shouldMove && !jumpNow)
            {
                if (curControlMeta != null) Stop();
                curAnim = rideableconfig.Controls[eagent.Swimming ? "swim" : "idle"].RiderAnim;

                if (eagent.Swimming) nowControlMeta = rideableconfig.Controls["swim"];
                else nowControlMeta = null;
            }
            else
            {

                string controlCode = eagent.Controls.Backward ? "walkback" : "walk";
                if (eagent.Controls.Sprint) controlCode = "sprint";
                if (eagent.Swimming) controlCode = "swim";

                nowControlMeta = rideableconfig.Controls[controlCode];

                eagent.Controls.Jump = jumpNow;

                if (jumpNow)
                {
                    IsInMidJump = true;
                    jumpNow = false;
                    var esr = eagent.Properties.Client.Renderer as EntityShapeRenderer;
                    if (esr != null) esr.LastJumpMs = capi.InWorldEllapsedMilliseconds;

                    nowControlMeta = rideableconfig.Controls["jump"];

                    nowControlMeta.EaseOutSpeed = (ForwardSpeed != 0) ? 30 : 40;

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

        public void Stop()
        {
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
                move(dt, eagent.Controls, curControlMeta.MoveSpeed);
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

            bool nowtrot = shouldMove && !eagent.Controls.Sprint && notOnGroundAccum < 0.2;
            bool nowgallop = shouldMove && eagent.Controls.Sprint && notOnGroundAccum < 0.2;

            bool wastrot = trotSound != null && trotSound.IsPlaying;
            bool wasgallop = gallopSound != null && gallopSound.IsPlaying;

            trotSound?.SetPosition((float)entity.Pos.X, (float)entity.Pos.Y, (float)entity.Pos.Z);
            gallopSound?.SetPosition((float)entity.Pos.X, (float)entity.Pos.Y, (float)entity.Pos.Z);

            if (nowtrot != wastrot)
            {
                if (nowtrot)
                {
                    if (trotSound == null)
                    {
                        trotSound = capi.World.LoadSound(new SoundParams()
                        {
                            Location = new AssetLocation("sounds/creature/hooved/trot"),
                            DisposeOnFinish = false,
                            Position = entity.Pos.XYZ.ToVec3f(),
                            ShouldLoop = true,
                        });
                    }

                    trotSound.Start();
                    
                } else
                {
                    trotSound.Stop();
                }
            }

            if (nowgallop != wasgallop)
            {
                if (nowgallop)
                {
                    if (gallopSound == null)
                    {
                        gallopSound = capi.World.LoadSound(new SoundParams()
                        {
                            Location = new AssetLocation("sounds/creature/hooved/gallop"),
                            DisposeOnFinish = false,
                            Position = entity.Pos.XYZ.ToVec3f(),
                            ShouldLoop = true,
                        });
                    }
                    gallopSound.Start();
                } else
                {
                    gallopSound.Stop();
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

        public void DidUnnmount(EntityAgent entityAgent)
        {
            Stop();

            lastDismountTotalHours = entity.World.Calendar.TotalHours;
            foreach (var meta in rideableconfig.Controls.Values)
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

        public void DidMount(EntityAgent entityAgent)
        {
            updateControlScheme();
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
        public float MoveSpeed;
        public AnimationMetaData RiderAnim;
    }

    public class RideableConfig
    {
        public int MinGeneration;
        public Dictionary<string, ControlMeta> Controls;
    }



}
