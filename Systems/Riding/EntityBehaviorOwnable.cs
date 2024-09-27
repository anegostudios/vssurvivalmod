using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorOwnable : EntityBehavior
    {
        public string Group;
        public EntityBehaviorOwnable(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            Group = attributes["groupCode"].AsString();

            verifyOwnership();
        }

        private void verifyOwnership()
        {
            if (entity.World.Side != EnumAppSide.Server) return;

            // Verify, it might be outdated
            bool found = false;
            var tree = entity.WatchedAttributes.GetTreeAttribute("ownedby");
            if (tree != null)
            {
                var mseo = entity.World.Api.ModLoader.GetModSystem<ModSystemEntityOwnership>();

                if (mseo.OwnerShipsByPlayerUid.TryGetValue(tree.GetString("uid", ""), out var ownerships))
                {
                    if (ownerships.TryGetValue(Group, out var ownership))
                    {
                        found = ownership.EntityId == entity.EntityId;
                    }
                }
            }
            if (!found)
            {
                entity.WatchedAttributes.RemoveAttribute("ownedby");
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);

            var tree = entity.WatchedAttributes.GetTreeAttribute("ownedby");
            if (tree != null)
            {
                infotext.AppendLine(Lang.Get("Owned by {0}", tree.GetString("name")));

                if ((entity.World as IClientWorldAccessor).Player.WorldData.CurrentGameMode != EnumGameMode.Creative) // In creative mode health is displayed on all creatures anyway
                {
                    var ebh = entity.GetBehavior<EntityBehaviorHealth>();
                    if (ebh != null) infotext.AppendLine(Lang.Get("Health: {0:0.##}/{1}", ebh.Health, ebh.MaxHealth));
                }
            }
        }



        public override string PropertyName() => "ownable";
    }
}
