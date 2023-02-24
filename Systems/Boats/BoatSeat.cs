using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBoatSeat : IMountable
    {
        public EntityBoat EntityBoat;
        public int SeatNumber;
        public EntityControls controls = new EntityControls();
        public EntityAgent Passenger = null;
        public long PassengerEntityIdForInit;
        public bool controllable;

        protected Vec3f eyePos = new Vec3f(0, 1, 0);
        protected Vec3f mountOffset;

        public EntityBoatSeat(EntityBoat entityBoat, int seatNumber, Vec3f mountOffset)
        {
            controls.OnAction = this.onControls;
            this.EntityBoat = entityBoat;
            this.SeatNumber = seatNumber;
            this.mountOffset = mountOffset;
        }

        public static IMountable GetMountable(IWorldAccessor world, TreeAttribute tree)
        {
            Entity entityBoat = world.GetEntityById(tree.GetLong("entityIdBoat"));
            if (entityBoat is EntityBoat eBoat)
            {
                return eBoat.Seats[tree.GetInt("seatNumber")];
            }

            return null;
        }

        Vec4f tmp = new Vec4f();
        Vec3f transformedMountOffset = new Vec3f();
        public Vec3f MountOffset
        {
            get
            {
                var pos = EntityBoat.SidedPos;
                modelmat.Identity();
                
                modelmat.Rotate(EntityBoat.xangle, EntityBoat.yangle + pos.Yaw, EntityBoat.zangle);
                
                var rotvec = modelmat.TransformVector(tmp.Set(mountOffset.X, mountOffset.Y, mountOffset.Z, 0));
                return transformedMountOffset.Set(rotvec.X, rotvec.Y, rotvec.Z);
            }
        }

        EntityPos mountPos = new EntityPos();
        Matrixf modelmat = new Matrixf();
        public EntityPos MountPosition
        {
            get
            {
                var pos = EntityBoat.SidedPos;
                var moffset = MountOffset;

                mountPos.SetPos(pos.X + moffset.X, pos.Y + moffset.Y, pos.Z + moffset.Z);

                mountPos.SetAngles(
                    pos.Roll + EntityBoat.xangle,
                    pos.Yaw + EntityBoat.yangle,
                    pos.Pitch + EntityBoat.zangle
                );

                return mountPos;
            }
        }

        public string SuggestedAnimation
        {
            get { return "sitflooridle"; }
        }

        public EntityControls Controls
        {
            get {
                return this.controls; 
            }
        }

        public IMountableSupplier MountSupplier => EntityBoat;
        public EnumMountAngleMode AngleMode => EnumMountAngleMode.Push;
        public Vec3f LocalEyePos => eyePos;
        public Entity MountedBy => Passenger;
        public bool CanControl => controllable;

        public void DidUnmount(EntityAgent entityAgent)
        {
            if (entityAgent.World.Side == EnumAppSide.Server)
            {
                tryTeleportPassengerToShore();
            }

            var pesr = Passenger?.Properties?.Client.Renderer as EntityShapeRenderer;
            if (pesr != null)
            {
                pesr.xangle = 0;
                pesr.yangle = 0;
                pesr.zangle = 0;
            }

            Passenger?.AnimManager?.StopAnimation("crudeOarReady");
            Passenger?.AnimManager?.StopAnimation("crudeOarBackward");
            Passenger?.AnimManager?.StopAnimation("crudeOarForward");

            this.Passenger.Pos.Roll = 0;
            this.Passenger = null;
        }

        private void tryTeleportPassengerToShore()
        {
            var world = Passenger.World;
            var ba = Passenger.World.BlockAccessor;
            bool found = false;

            for (int dx = -1; !found && dx <= 1; dx++)
            {
                for (int dz = -1; !found && dz <= 1; dz++)
                {
                    var targetPos = Passenger.ServerPos.XYZ.AsBlockPos.ToVec3d().Add(dx + 0.5, 1.1, dz + 0.5);
                    var block = ba.GetMostSolidBlock((int)targetPos.X, (int)(targetPos.Y - 0.15), (int)targetPos.Z);
                    if (block.SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(ba, Passenger.CollisionBox, targetPos, false))
                    {
                        this.Passenger.TeleportTo(targetPos);
                        found = true;
                        break;
                    }
                }
            }

            for (int dx = -2; !found && dx <= 2; dx++)
            {
                for (int dz = -2; !found && dz <= 2; dz++)
                {
                    if (Math.Abs(dx) != 2 && Math.Abs(dz) != 2) continue;

                    var targetPos = Passenger.ServerPos.XYZ.AsBlockPos.ToVec3d().Add(dx + 0.5, 1.1, dz + 0.5);
                    var block = ba.GetMostSolidBlock((int)targetPos.X, (int)(targetPos.Y - 0.15), (int)targetPos.Z);
                    if (block.SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(ba, Passenger.CollisionBox, targetPos, false))
                    {
                        this.Passenger.TeleportTo(targetPos);
                        found = true;
                        break;
                    }
                }
            }

            for (int dx = -1; !found && dx <= 1; dx++)
            {
                for (int dz = -1; !found && dz <= 1; dz++)
                {
                    var targetPos = Passenger.ServerPos.XYZ.AsBlockPos.ToVec3d().Add(dx + 0.5, 1.1, dz + 0.5);
                    if (!world.CollisionTester.IsColliding(ba, Passenger.CollisionBox, targetPos, false))
                    {
                        this.Passenger.TeleportTo(targetPos);
                        found = true;
                        break;
                    }
                }
            }
        }

        public void DidMount(EntityAgent entityAgent)
        {
            if (this.Passenger != null && this.Passenger != entityAgent)
            {
                this.Passenger.TryUnmount();
                return;
            }

            this.Passenger = entityAgent;
        }

        public void MountableToTreeAttributes(TreeAttribute tree)
        {
            tree.SetString("className", "boat");
            tree.SetLong("entityIdBoat", this.EntityBoat.EntityId);
            tree.SetInt("seatNumber", SeatNumber);
        }

        internal void onControls(EnumEntityAction action, bool on, ref EnumHandling handled)
        {
            if (action == EnumEntityAction.Sneak && on)
            {
                Passenger?.TryUnmount();
                controls.StopAllMovement();
            }
        }

    }



}