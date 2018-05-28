using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorTiredness : EntityBehavior
    {
        public Random Rand;

        double hoursTotal;

        long listenerId;


        /// <summary>
        /// Tiredness in hours
        /// </summary>
        internal float Tiredness
        {
            get { return entity.WatchedAttributes.GetTreeAttribute("tiredness").GetFloat("tiredness"); }
            set { entity.WatchedAttributes.GetTreeAttribute("tiredness").SetFloat("tiredness", value); entity.WatchedAttributes.MarkPathDirty("tiredness"); }
        }

        internal bool IsSleeping
        {
            get { return entity.WatchedAttributes.GetTreeAttribute("tiredness").GetInt("isSleeping") > 0; }
            set { entity.WatchedAttributes.GetTreeAttribute("tiredness").SetInt("isSleeping", value ? 1 : 0); entity.WatchedAttributes.MarkPathDirty("tiredness"); }
        }


        public EntityBehaviorTiredness(Entity entity) : base(entity)
        {
            EntityAgent agent = entity as EntityAgent;
        }


        public override void Initialize(EntityType config, JsonObject typeAttributes)
        {
            ITreeAttribute tiredTree = entity.WatchedAttributes.GetTreeAttribute("tiredness");

            if (tiredTree == null)
            {
                entity.WatchedAttributes.SetAttribute("tiredness", tiredTree = new TreeAttribute());

                Tiredness = typeAttributes["currenttiredness"].AsFloat(0);
            }

            listenerId = entity.World.RegisterGameTickListener(SlowTick, 3000);

            hoursTotal = entity.World.Calendar.TotalHours;
        }

        private void SlowTick(float dt)
        {
            bool sleeping = IsSleeping;
            if (sleeping && (entity as EntityAgent)?.MountedOn == null) IsSleeping = sleeping = false;

            if (sleeping || entity.World.Side == EnumAppSide.Client) return;

            float hoursPassed = (float)(entity.World.Calendar.TotalHours - hoursTotal);
            Tiredness = Math.Min(Tiredness + hoursPassed * 0.75f, entity.World.Calendar.HoursPerDay / 2);
            hoursTotal = entity.World.Calendar.TotalHours;
        }


        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);

            entity.World.UnregisterGameTickListener(listenerId);
        }
        
        


        public override string PropertyName()
        {
            return "tiredness";
        }
    }

    
}
