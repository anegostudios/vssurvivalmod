using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{

    public class EntityRideableSeat : EntitySeat
    {
        public override EnumMountAngleMode AngleMode => EnumMountAngleMode.FixateYaw;
        public override AnimationMetaData SuggestedAnimation => (mountedEntity as EntityBehaviorRideable).curAnim;

        protected EntityPos seatPos = new EntityPos();
        protected Matrixf modelmat = new Matrixf();
        protected string RideableClassName = "rideableanimal";

        public override Vec3f LocalEyePos
        {
            get
            {
                modelmat.Identity();
                AttachmentPointAndPose apap = Entity.AnimManager?.Animator?.GetAttachmentPointPose(config.APName);
                if (apap != null)
                {
                    modelmat.RotateY(GameMath.PIHALF + Entity.Pos.Yaw);
                    modelmat.RotateX((Entity.Properties.Client.Renderer as EntityShapeRenderer)?.nowSwivelRad ?? 0);
                    modelmat.Translate(0f, config.EyeHeight, 0);
                    modelmat.RotateY(-GameMath.PIHALF - Entity.Pos.Yaw);
                }

                var rotvec = modelmat.TransformVector(new Vec4f(0, 0f, 0, 1));
                return rotvec.XYZ;
            }
        }

        public override EntityPos SeatPosition
        {
            get
            {
                loadAttachPointTransform();
                var rotvec = modelmat.TransformVector(new Vec4f(0, 0f, 0, 1));
                seatPos.Yaw = Entity.Pos.Yaw;
                return seatPos.SetFrom(mountedEntity.Position).Add(rotvec.X, rotvec.Y, rotvec.Z);
            }
        }

        public override Matrixf RenderTransform
        {
            get
            {
                loadAttachPointTransform();
                var rotvec = modelmat.TransformVector(new Vec4f(0, 0, 0, 1));
                return
                    new Matrixf()
                    .RotateDeg(config.MountRotation)
                    .Translate(-rotvec.X, -rotvec.Y, -rotvec.Z) // Relative to SeatPosition, so let's substract that offset
                    .Mul(modelmat)
                ;
            }
        }

        public override float FpHandPitchFollow => 0.2f;

        private void loadAttachPointTransform()
        {
            modelmat.Identity();
            AttachmentPointAndPose apap = Entity.AnimManager?.Animator?.GetAttachmentPointPose(config.APName);
            if (apap != null)
            {
                var esr = Entity.Properties.Client.Renderer as EntityShapeRenderer;

                modelmat.RotateY(-GameMath.PIHALF + Entity.Pos.Yaw + GameMath.PI);
                modelmat.Translate(0, 0.6, 0);
                if (esr != null)
                {
                    modelmat.RotateX(esr.nowSwivelRad + esr.xangle);
                    modelmat.RotateY(esr.yangle);
                    modelmat.RotateZ(esr.zangle);
                }
                modelmat.Translate(0, -0.6, 0); // This probably needs to be the height above ground level right after applying apap transform

                apap.Mul(modelmat);
                if (config.MountOffset != null) modelmat.Translate(config.MountOffset);

                modelmat.Translate(-0.5, 0, -0.5);
                modelmat.RotateY(GameMath.PIHALF - Entity.Pos.Yaw);
            }
        }

        public EntityRideableSeat(IMountable mountablesupplier, string seatId, SeatConfig config) : base(mountablesupplier, seatId, config)
        {
        }

        public static IMountableSeat GetMountable(IWorldAccessor world, TreeAttribute tree)
        {
            Entity entityAnimal = world.GetEntityById(tree.GetLong("entityIdMount"));
            var bh = entityAnimal?.GetBehavior<EntityBehaviorSeatable>();
            return bh?.Seats.FirstOrDefault(seat => seat.SeatId == tree.GetString("seatId"));
        }

        public override void MountableToTreeAttributes(TreeAttribute tree)
        {
            base.MountableToTreeAttributes(tree);
            tree.SetLong("entityIdMount", Entity.EntityId);
            tree.SetString("className", RideableClassName);
        }

        public override void DidMount(EntityAgent entityAgent)
        {
            base.DidMount(entityAgent);

            if (Entity != null)
            {
                Entity.GetBehavior<EntityBehaviorTaskAI>()?.TaskManager.StopTasks();
                Entity.StartAnimation("idle");

                var capi = entityAgent.Api as ICoreClientAPI;
                if (capi != null && capi.World.Player.Entity.EntityId == entityAgent.EntityId) // Isself
                {
                    capi.Input.MouseYaw = Entity.Pos.Yaw;
                }
            }

            var ebh = mountedEntity as IMountableListener;
            ebh?.DidMount(entityAgent);

            ebh = Entity as IMountableListener;
            ebh?.DidMount(entityAgent);
        }

        public override void DidUnmount(EntityAgent entityAgent)
        {
            if (entityAgent.World.Side == EnumAppSide.Server)
            {
                tryTeleportToFreeLocation();
            }
            if (entityAgent is EntityPlayer eplr)
            {
                eplr.BodyYawLimits = null;
                eplr.HeadYawLimits = null;
            }
            
            base.DidUnmount(entityAgent);

            var ebh = mountedEntity as IMountableListener;
            ebh?.DidUnnmount(entityAgent);

            ebh = Entity as IMountableListener;
            ebh?.DidUnnmount(entityAgent);
        }

        protected virtual void tryTeleportToFreeLocation()
        {
            var world = Passenger.World;
            var ba = Passenger.World.BlockAccessor;

            var rightPos = Entity.Pos.XYZ.Add(EntityPos.GetViewVector(0, Entity.Pos.Yaw + GameMath.PIHALF)).Add(0, 0.01, 0);
            var leftPos = Entity.Pos.XYZ.Add(EntityPos.GetViewVector(0, Entity.Pos.Yaw - GameMath.PIHALF)).Add(0, 0.01, 0);


            // Prefer the unmount side in which the player is looking at
            var dist = GameMath.AngleRadDistance(Passenger.Pos.Yaw, Entity.Pos.Yaw + GameMath.PIHALF);
            if (dist < GameMath.PIHALF)
            {
                var tmp = leftPos;
                leftPos = rightPos;
                rightPos = tmp;
            }

            var block = ba.GetMostSolidBlock((int)rightPos.X, (int)(rightPos.Y - 0.1), (int)rightPos.Z);
            if (block.SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(ba, Passenger.CollisionBox, rightPos, false))
            {
                Passenger.TeleportTo(rightPos);
                return;
            }

            block = ba.GetMostSolidBlock((int)leftPos.X, (int)(leftPos.Y - 0.1), (int)leftPos.Z);
            if (block.SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(ba, Passenger.CollisionBox, leftPos, false))
            {
                Passenger.TeleportTo(leftPos);
                return;
            }
        }
    }
}
