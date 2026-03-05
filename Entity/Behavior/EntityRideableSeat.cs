using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{

    public class EntityRideableSeat : EntitySeat
    {
        public override EnumMountAngleMode AngleMode => EnumMountAngleMode.FixateYaw;
        public override AnimationMetaData SuggestedAnimation => (mountedEntity as EntityBehaviorRideable)?.CurrentControlMeta?.GetSeatAnimation(this);

        protected EntityPos seatPos = new EntityPos();
        protected Matrixf modelmat = new Matrixf();
        protected string RideableClassName = "rideableanimal";

        public override Vec3f LocalEyePos
        {
            get
            {
                modelmat.Identity();
                AttachmentPointAndPose? apap = Entity.AnimManager?.Animator?.GetAttachmentPointPose(config.APName);
                if (apap != null)
                {
                    modelmat.RotateY(GameMath.PIHALF + Entity.Pos.Yaw);
                    modelmat.RotateX((Entity.Properties.Client.Renderer as EntityShapeRenderer)?.nowSwivelRad ?? 0);
                    modelmat.Translate(config.EyeOffsetX, config.EyeHeight, 0);
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
                // Backwards compatibility
                if (config.MountOffset != null) return oldSeatPosition;

                modelmat.Identity();
                float pitchApplied = 0.55f;
                AttachmentPointAndPose? apap = Entity.AnimManager?.Animator?.GetAttachmentPointPose(config.APName);
                if (apap != null)
                {
                    var esr = Entity.Properties.Client.Renderer as EntityShapeRenderer;

                    // For whatever reason an entity will face south when Yaw=0, even though the shapes are modelled facing west
                    // So we need to temporarily correct for that to make the offsets match what's shown in the model editor, not be rotated
                    modelmat.RotateY(Entity.Pos.Yaw + GameMath.PIHALF);

                    // Doing the offset now means the hitbox is offset to match, and that the rotation point is not offset.
                    // This is deliberate because elk riders need their rotation origin to be well inside of the player hitbox
                    if (config.RiderOffset != null) modelmat.Translate(config.RiderOffset);

                    // Matches EntityShapeRenderer.loadModelMatrix(), which translates up to the entity's middle before applying step pitch
                    modelmat.Translate(0, Entity.SelectionBox.Y2 / 2, 0);
                    // The pitch returned in the entity position will also affect the translation, so we do it only partway here.
                    // Note this is not mathematically correct - sin (x/2) + sin(x/2) != sin(x) - but works as a good enough approximation
                    modelmat.RotateZ((float)mountedEntity.StepPitch * (1 - pitchApplied));

                    if (esr != null)
                    {
                        modelmat.RotateX(esr.nowSwivelRad + esr.xangle);
                        modelmat.RotateY(esr.yangle);
                        modelmat.RotateZ(esr.zangle);
                    }
                    modelmat.Translate(0, -Entity.SelectionBox.Y2 / 2, 0);

                    float scale = Entity.Properties.Client.Size;
                    modelmat.Scale(scale, scale, scale);

                    // Shapes are modelled with a center at 0.5, 0, 0.5, so move from that to the origin
                    modelmat.Translate(-0.5f, 0, -0.5f);

                    apap.MulUncentered(modelmat);

                    // Rotate back to undo the correction
                    modelmat.RotateY(-GameMath.PIHALF);
                }

                var translation = modelmat.TransformVector(new Vec4f(0, 0f, 0, 1));
                seatPos.SetFrom(mountedEntity.Position).Add(translation.X, translation.Y, translation.Z);

                seatPos.Roll = 0;
                seatPos.Yaw = Entity.Pos.Yaw;
                seatPos.Pitch = (float)mountedEntity.StepPitch * pitchApplied;
                // Deliberately leaves out the rotation from animating the attachment point, because we don't actually want to exactly match the elk's motions
                if (apap != null)
                {
                    seatPos.Roll += (float)apap.AttachPoint.RotationX;
                    seatPos.Yaw += (float)apap.AttachPoint.RotationY;
                    seatPos.Pitch += (float)apap.AttachPoint.RotationZ;
                }

                return seatPos;
            }
        }

        public override Matrixf RenderTransform
        {
            get
            {
                // Backwards compatibility
                if (config.MountOffset != null) return oldRenderTransform;

                modelmat.Identity();
                modelmat.RotateX(config.MountRotation.X * GameMath.DEG2RAD);
                // MountRotation.Y is already accounted for as part of the rider's yaw, so skip that
                modelmat.RotateZ(config.MountRotation.Z * GameMath.DEG2RAD);
                return modelmat;
            }
        }

        // Kept for backwards compatibility with Vintage Story 1.21 and 1.20
        private EntityPos oldSeatPosition
        {
            get
            {
                loadAttachPointTransform();
                var rotvec = modelmat.TransformVector(new Vec4f(0, 0f, 0, 1));
                seatPos.SetFrom(mountedEntity.Position).Add(rotvec.X, rotvec.Y, rotvec.Z);

                //seatPos.Yaw = Entity.Pos.Yaw;       // radfast 10.8.25: this was never being applied (because it was previously the line before .SetFrom() and .SetFrom() resets the Yaw).  Left here in case we actually want to apply it?

                seatPos.Pitch = (float)mountedEntity.StepPitch * 0.55f;    // If the elk is pitching forward or back due to stepping down/up, apply some of that pitch to the rider position also

                return seatPos;
            }
        }

        // Kept for backwards compatibility with Vintage Story 1.21 and 1.20
        private Matrixf oldRenderTransform
        {
            get
            {
                loadAttachPointTransform();
                var rotvec = modelmat.TransformVector(new Vec4f(0, 0, 0, 1));
                return
                    new Matrixf()
                    .Translate(-rotvec.X, -rotvec.Y, -rotvec.Z) // Relative to SeatPosition, so let's substract that offset
                    .Mul(modelmat)
                    .RotateDeg(config.MountRotation)
                ;
            }
        }

        // Kept for backwards compatibility with Vintage Story 1.21 and 1.20
        private void loadAttachPointTransform()
        {
            modelmat.Identity();
            AttachmentPointAndPose? apap = Entity.AnimManager?.Animator?.GetAttachmentPointPose(config.APName);
            if (apap != null)
            {
                var esr = Entity.Properties.Client.Renderer as EntityShapeRenderer;

                modelmat.RotateY(GameMath.PIHALF + Entity.Pos.Yaw);
                modelmat.Translate(0, 0.6, 0);
                if (esr != null)
                {
                    modelmat.RotateX(esr.nowSwivelRad + esr.xangle);
                    modelmat.RotateY(esr.yangle);
                    modelmat.RotateZ(esr.zangle);
                }
                modelmat.Translate(0, -0.6, 0); // This probably needs to be the height above ground level right after applying apap transform

                modelmat.Translate(-0.5, 0.5, -0.5);    // These values found empirically, to prevent weird motion within the seat by the pillion passenger, because its attachment point also has rotation angles: essentially we are moving the mounted player to a more sensible origin for the rotations
                apap.Mul(modelmat);
                if (config.MountOffset != null) modelmat.Translate(config.MountOffset);
                modelmat.Translate(-1.0, -0.5, -1.0);

                modelmat.RotateY(GameMath.PIHALF - Entity.Pos.Yaw);
            }
        }




        public override float FpHandPitchFollow => 0.2f;

        public EntityRideableSeat(IMountable mountablesupplier, string seatId, SeatConfig config) : base(mountablesupplier, seatId, config)
        {
        }

        public override bool CanMount(EntityAgent entityAgent)
        {
            if (entityAgent is not EntityPlayer player) return false;

            var rideable = Entity.GetBehavior<EntityBehaviorRideable>();
            if (Entity.WatchedAttributes.GetInt("generation") < rideable.MinGeneration)
            {
                (player.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "toowildtoride", Lang.Get("mount-interact-toowildtoride"));
                return false;
            }

            var ebho = Entity.GetBehavior<EntityBehaviorOwnable>();
            if (ebho != null && !ebho.IsOwner(player) && config.Controllable)
            {
                (player.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "requiersownership", Lang.Get("mount-interact-requiresownership"));
                return false;
            }
            return true;
        }

        public static IMountableSeat? GetMountable(IWorldAccessor world, TreeAttribute tree)
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

            ArgumentNullException.ThrowIfNull(Entity);
            Entity.Api.Event.TriggerEntityMounted(entityAgent, this);
        }

        public override void DidUnmount(EntityAgent entityAgent)
        {
            if (entityAgent.World.Side == EnumAppSide.Server && DoTeleportOnUnmount)
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
            ebh?.DidUnmount(entityAgent);

            ebh = Entity as IMountableListener;
            ebh?.DidUnmount(entityAgent);

            Entity.Api.Event.TriggerEntityUnmounted(entityAgent, this);
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

            var block = ba.GetBlockRaw((int)rightPos.X, (int)(rightPos.Y - 0.1), (int)rightPos.Z, BlockLayersAccess.MostSolid);
            if (block.SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(ba, Passenger.CollisionBox, rightPos, false))
            {
                Passenger.TeleportTo(rightPos);
                return;
            }

            block = ba.GetBlockRaw((int)leftPos.X, (int)(leftPos.Y - 0.1), (int)leftPos.Z, BlockLayersAccess.MostSolid);
            if (block.SideSolid[BlockFacing.UP.Index] && !world.CollisionTester.IsColliding(ba, Passenger.CollisionBox, leftPos, false))
            {
                Passenger.TeleportTo(leftPos);
                return;
            }
        }
    }
}
