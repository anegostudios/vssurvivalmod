using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public abstract class EntitySeat : IMountableSeat
    {
        public EntityControls controls = new EntityControls();
        public Entity Passenger { get; set; }

        public long PassengerEntityIdForInit { get; set; }
        private string SeatIdForInit;
        public bool DoTeleportOnUnmount { get; set; } = true;
        public string SeatId
        {
            get { return config?.SeatId ?? SeatIdForInit; }
            set
            {
                if (config != null) config.SeatId= value;
                SeatIdForInit = value;
            }
        }
        public virtual SeatConfig Config
        {
            get { return config; }
            set
            {
                config = value;
                if (config != null) SeatIdForInit = config.SeatId;
            }
        }

        protected IMountable mountedEntity;
        protected Vec3f eyePos = new Vec3f(0, 1, 0);
        protected SeatConfig config;

        public EntitySeat(IMountable mountedEntity, string seatId, SeatConfig config)
        {
            controls.OnAction = this.onControls;
            this.mountedEntity = mountedEntity;
            this.config = config;
            this.SeatId = seatId;
        }

        public abstract AnimationMetaData SuggestedAnimation { get; }
        public EntityControls Controls => controls;
        public IMountable MountSupplier => mountedEntity;
        public virtual EnumMountAngleMode AngleMode => EnumMountAngleMode.Push;
        public virtual Vec3f LocalEyePos => eyePos;
        public bool CanControl => Config?.Controllable ?? false;
        public virtual Entity Entity => (mountedEntity as EntityBehaviorSeatable).entity;
        public abstract EntityPos SeatPosition { get; }
        public abstract Matrixf RenderTransform { get; }
        public bool SkipIdleAnimation => true;
        public abstract float FpHandPitchFollow { get; }

        public virtual bool CanMount(EntityAgent entityAgent) => true;
        public virtual bool CanUnmount(EntityAgent entityAgent) => true;

        public virtual void DidMount(EntityAgent entityAgent)
        {
            if (this.Passenger != null && this.Passenger != entityAgent)
            {
                (Passenger as EntityAgent)?.TryUnmount();
                return;
            }

            this.Passenger = entityAgent;
        }

        public virtual void DidUnmount(EntityAgent entityAgent)
        {
            if (Passenger != null)
            {
                var pesr = Passenger.Properties?.Client.Renderer as EntityShapeRenderer;
                if (pesr != null)
                {
                    pesr.xangle = 0;
                    pesr.yangle = 0;
                    pesr.zangle = 0;
                }

                Passenger.Pos.Roll = 0;
            }
            Passenger = null;
        }


        public virtual void MountableToTreeAttributes(TreeAttribute tree)
        {
            tree.SetString("seatId", SeatId);
        }

        internal void onControls(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (action == EnumEntityAction.Sneak && on)
            {
                (Passenger as EntityAgent)?.TryUnmount();
                controls.StopAllMovement();
            }
        }
    }
}
