using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBoatSeat : EntityRideableSeat
    {
        public override EnumMountAngleMode AngleMode => config.AngleMode;
        protected override bool ApplyEntityRenderAnglesToSeatPosition => false;
        Dictionary<string, string> animations => (Entity as EntityBoat).MountAnimations;
        public string actionAnim;

        public override EntityPos SeatPosition
        {
            get
            {
                EntityPos pos = base.SeatPosition;
                ((EntityBoat)Entity).ApplySwayToPosition(pos);
                return pos;
            }
        }

        public override AnimationMetaData SuggestedAnimation
        {
            get
            {
                if (actionAnim == null) return null;

                if (Passenger?.Properties?.Client.AnimationsByMetaCode?.TryGetValue(actionAnim, out var ameta) == true)
                {
                    return ameta;
                }

                return null;
            }
        }


        #region Rider hunger

        public float RiderSatietyLossPerSteeringSecond;
        public float RiderSatietyLossPerPaddlingSecond;

        #endregion

        public EntityBoatSeat(IMountable mountablesupplier, string seatId, SeatConfig config) : base(mountablesupplier, seatId, config)
        {
            RideableClassName = "boat";

            var hungerProps = config?.Attributes?["riderSatLoss"];
            if (hungerProps?.Exists != true) return;

            RiderSatietyLossPerSteeringSecond = hungerProps["perSteerSec"].AsFloat();
            RiderSatietyLossPerPaddlingSecond = hungerProps["perPaddleSec"].AsFloat();
        }

        public override bool CanMount(EntityAgent entityAgent)
        {
            if (config.Attributes?["ropeTieablesOnly"].AsBool(false)==true)
            {
                return entityAgent.HasBehavior<EntityBehaviorRopeTieable>();
            }

            return base.CanMount(entityAgent);
        }

        public override void DidMount(EntityAgent entityAgent)
        {
            base.DidMount(entityAgent);

            entityAgent.AnimManager.StartAnimation(config.Animation ?? animations["idle"]);
        }

        public override bool CanUnmount(EntityAgent entityAgent)
        {
            return !DoTeleportOnUnmount || !entityAgent.Alive || TryGetExitPosition(entityAgent, out _);
        }

        /// <summary>
        /// Uses the seat facing to find standing room, regardless of where the passenger looks.
        /// Seat attributes.unmountForwardOffset sets the distance in blocks (default 0.9).
        /// </summary>
        public bool TryGetExitPosition(EntityAgent entityAgent, out EntityPos exitPos)
        {
            var boat = (EntityBoat)Entity;
            var transform = boat.GetKinematicColliderTransform();
            exitPos = SeatPosition.Copy();
            double localYaw = exitPos.Yaw - boat.Pos.Yaw + config.MountRotation.Y * GameMath.DEG2RAD;
            double distance = config.Attributes?["unmountForwardOffset"].AsDouble(0.9) ?? 0.9;
            // Boat forward motion uses the opposite of the entity view vector.
            transform.TransformDirection(Math.Sin(localYaw) * distance, 0, Math.Cos(localYaw) * distance, out double dx, out double dy, out double dz);
            exitPos.Add(dx, dy, dz);

            var world = entityAgent.World;
            var playerBox = entityAgent.OriginCollisionBox;
            var colliderBehavior = boat.GetBehavior<EntityBehaviorKinematicCollider>();
            if (world.CollisionScene.TryGetCollider(colliderBehavior?.ColliderId ?? 0, out var collider))
            {
                collider = collider.WithTransform(boat.Pos.Dimension, transform);
                if (!collider.CanCarryEntities || !new CollisionQueryFilter(CollisionLayer.Entity).Matches(collider)) return false;

                // Search below the seat so overhead colliders cannot become the exit floor... Like the spars.
                double startY = exitPos.Y + 0.01 - playerBox.Y1;
                var box = playerBox.ToDouble().Translate(exitPos.X, startY, exitPos.Z);
                double drop = playerBox.Height;
                if (!collider.TrySweepAABB(box, 0, -drop, 0, out double fraction)) return false;
                exitPos.Y = startY - drop * fraction + 0.001;
                box.SetAndTranslate(playerBox, exitPos.X, exitPos.Y, exitPos.Z);
                if (collider.Intersects(box)) return false;
            }

            if (world.CollisionTester.IsColliding(world.BlockAccessor, playerBox, exitPos.XYZ, exitPos.Dimension, false)) return false;
            return !world.CollisionScene.IntersectsAny(playerBox.ToDouble().Translate(exitPos.X, exitPos.Y, exitPos.Z), exitPos.Dimension, false, new CollisionQueryFilter(CollisionLayer.Entity, excludedOwnerEntityId: boat.EntityId));
        }

        public override void DidUnmount(EntityAgent entityAgent)
        {
            EntityPos exitPos = null;
            bool standUp = DoTeleportOnUnmount && TryGetExitPosition(entityAgent, out exitPos);
            if (standUp)
            {
                entityAgent.Pos.SetPos(exitPos);
                entityAgent.Pos.Motion.Set(0, 0, 0);
            }

            if (Passenger != null)
            {
                Passenger.AnimManager?.StopAnimation(animations["ready"]);
                Passenger.AnimManager?.StopAnimation(animations["forwards"]);
                Passenger.AnimManager?.StopAnimation(animations["backwards"]);
                Passenger.AnimManager?.StopAnimation(animations["idle"]);
                Passenger.AnimManager?.StopAnimation(config.Animation);
                Passenger.Pos.Roll = 0;
            }

            base.DidUnmount(entityAgent);

            if (standUp)
            {
                var colliderBehavior = Entity.GetBehavior<EntityBehaviorKinematicCollider>();
                bool carried = entityAgent.GetBehavior<EntityBehaviorControlledPhysics>()?.TryStandOnCollider(colliderBehavior?.ColliderId ?? 0, ((EntityBoat)Entity).GetKinematicColliderTransform(), resetMountedCollisionBox: true) == true;
                entityAgent.OnGround = carried;
                entityAgent.Swimming = false;
                entityAgent.FeetInLiquid = false;
                if (!carried) entityAgent.Pos.Motion.Set(Entity.Pos.Motion);
                entityAgent.PhysicsUpdateWatcher?.Invoke(0, entityAgent.Pos.XYZ);
            }
        }

        protected override void tryTeleportToFreeLocation()
        {
            // Might not be needed anymore, needs more testing.
        }
    }


}
