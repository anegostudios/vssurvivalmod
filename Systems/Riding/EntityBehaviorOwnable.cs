using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#nullable disable

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
                    if (ownerships != null && ownerships.TryGetValue(Group, out var ownership))
                    {
                        // this will keep the marker position updated server side when the savegame is saved
                        if (entity.World.Side == EnumAppSide.Server)
                        {
                            ownership.Pos = entity.ServerPos;
                        }
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
                    if (ebh != null) infotext.AppendLine(Lang.Get("ownableentity-health", ebh.Health, ebh.MaxHealth));
                }
            }
        }

        public override string PropertyName() => "ownable";

        public bool IsOwner(EntityAgent byEntity)
        {
            if (byEntity is EntityPlayer byPlayer)
            {
                var tree = entity.WatchedAttributes.GetTreeAttribute("ownedby");
                if (tree == null) return true;
                string uid = tree.GetString("uid");

                // only owner can remove medallion if present
                if (uid != null && byPlayer.PlayerUID == uid)
                {
                    return true;
                }
            }

            return false;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (entity.World.Api is ICoreClientAPI capi)
            {
                var tree = entity.WatchedAttributes.GetTreeAttribute("ownedby");
                if (tree != null)
                {
                    if (capi.World.Player.PlayerUID == tree.GetString("uid", ""))
                    {
                        // update our own ownables with the last known position on the client side
                        var mseo = entity.World.Api.ModLoader.GetModSystem<ModSystemEntityOwnership>();
                        if(mseo.SelfOwnerShips.TryGetValue(Group, out var ownership))
                        {
                            ownership.Pos = entity.Pos.Copy();
                        }
                    }
                }
            }
        }

        public override bool ToleratesDamageFrom(Entity eOther, ref EnumHandling handling)
        {
            if (eOther is EntityAgent eagent && IsOwner(eagent))
            {
                handling = EnumHandling.PreventDefault;
                return true;
            }
            return false;
        }
    }
}
