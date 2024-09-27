using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using System.IO;

namespace Vintagestory.GameContent
{
    public class EntityVillager : EntityDressedHumanoid
    {
        public static OrderedDictionary<string, TraderPersonality> Personalities = new OrderedDictionary<string, TraderPersonality>()
        {
            { "balanced", new TraderPersonality(1.2f * 1.5f, 0.9f, 1.1f) },
            { "elderbalanced", new TraderPersonality(1.2f * 1.5f, 0.9f, 1.1f) },
        };

        public EntityVillager()
        {
            AnimManager = new PersonalizedAnimationManager();
        }

        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "balanced"); }
            set
            {
                WatchedAttributes.SetString("personality", value);
            }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            this.Personality = "balanced";

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            (AnimManager as PersonalizedAnimationManager).All = true;
        }


        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (World.Api.Side == EnumAppSide.Server)
            {
                Personality = Personalities.GetKeyAtIndex(World.Rand.Next(Personalities.Count));
                (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
            }
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            (AnimManager as PersonalizedAnimationManager).Personality = this.Personality;
        }

    }

}
