using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityBehaviorIdleAnimations : EntityBehavior
    {
        float secondsIdleAccum;
        string[] randomIdleAnimations;

        EntityAgent eagent;
        EntityBehaviorTiredness bhtiredness;

        public EntityBehaviorIdleAnimations(Entity entity) : base(entity)
        {
            eagent = entity as EntityAgent;
        }
        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            randomIdleAnimations = properties.Attributes["randomIdleAnimations"].AsArray<string>(null);

            bhtiredness = eagent.GetBehavior<EntityBehaviorTiredness>();
        }


        public override void OnGameTick(float dt)
        {
            if (!entity.Alive) return;

            bool triesToMove = eagent.ServerControls.TriesToMove;
            if (eagent.MountedOn != null) triesToMove |= eagent.MountedOn.MountSupplier.Position.Motion.LengthSq() > 0.0001f;
            if (!triesToMove && !eagent.Controls.IsFlying && !eagent.Controls.Gliding && eagent.RightHandItemSlot?.Empty == true && !eagent.Swimming && bhtiredness?.IsSleeping != true)
            {
                secondsIdleAccum += dt;
                if (secondsIdleAccum > 20 && eagent.World.Rand.NextDouble() < 0.004)
                {
                    eagent.StartAnimation(randomIdleAnimations[eagent.World.Rand.Next(randomIdleAnimations.Length)]);
                    secondsIdleAccum = 0;
                }
            }
            else secondsIdleAccum = 0;
        }

        public override string PropertyName()
        {
            return "idleanimations";
        }


    }
}
