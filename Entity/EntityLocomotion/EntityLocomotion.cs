using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Vintagestory.GameContent
{
    public abstract class EntityLocomotion
    {
        internal int appliedMilliseconds;

        internal virtual void Initialize(EntityType config)
        {

        }

        public abstract bool Applicable(Entity entity, EntityPos pos, EntityControls controls);

        public abstract void DoApply(float dt, Entity entity, EntityPos pos, EntityControls controls);

        public void Apply(float dt, Entity entity, EntityPos pos, EntityControls controls)
        {
            DoApply(dt, entity, pos, controls);

            appliedMilliseconds += (int)(dt * 1000);
        }

        public void Reset()
        {
            appliedMilliseconds = 0;
        }

    }
}
