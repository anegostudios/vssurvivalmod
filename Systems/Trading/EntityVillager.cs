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
            //{ "formal", new TraderPersonality(1 * 1.5f, 1, 0.9f) },
            { "balanced", new TraderPersonality(1.2f * 1.5f, 0.9f, 1.1f) },
            //{ "lazy", new TraderPersonality(1.65f * 1.5f, 0.7f, 0.9f) },
            //{ "rowdy", new TraderPersonality(0.75f * 1.5f, 1f, 1.8f) },
        };

        public EntityVillager()
        {
            AnimManager = new PersonalizedAnimationManager();
        }

        public string Personality
        {
            get { return WatchedAttributes.GetString("personality", "formal"); }
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
