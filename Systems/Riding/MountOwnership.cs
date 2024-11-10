using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{

    [ProtoContract]
    public class EntityOwnershipPacket
    {
        [ProtoMember(1)]
        public Dictionary<string, EntityOwnership> OwnerShipByGroup;
    }

    [ProtoContract]
    public class EntityOwnership
    {
        [ProtoMember(1)]
        public long EntityId;
        [ProtoMember(2)]
        public EntityPos Pos;
        [ProtoMember(3)]
        public string Color;
        [ProtoMember(4)]
        public string Name;
    }

    // We need:
    // - A way to claim ownership to an elk
    // - Only one elk can be owned
    // - The owned elk is globally tracked - you can see it on the map
    // - The owned elk can be called with the bone flute
    public class ModSystemEntityOwnership : ModSystem
    {
        public Dictionary<string, Dictionary<string, EntityOwnership>> OwnerShipsByPlayerUid;
        public Dictionary<string, EntityOwnership> SelfOwnerShips { get; set; } = new Dictionary<string, EntityOwnership>();
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        ICoreServerAPI sapi;
        ICoreClientAPI capi;

        public override void Start(ICoreAPI api)
        {
            api.Network
                .RegisterChannel("entityownership")
                .RegisterMessageType<EntityOwnershipPacket>()
            ;

            api.ModLoader.GetModSystem<WorldMapManager>().RegisterMapLayer<OwnedEntityMapLayer>("ownedcreatures", 2);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.PlayerJoin += Event_PlayerJoin;
            api.Event.OnEntityDeath += Event_EntityDeath;

            AiTaskRegistry.Register<AiTaskComeToOwner>("cometoowner");
        }

        private void Event_PlayerJoin(IServerPlayer player)
        {
            sendOwnerShips(player);
        }

        private void sendOwnerShips(IServerPlayer player)
        {
            if (OwnerShipsByPlayerUid.TryGetValue(player.PlayerUID, out var playerShipsByPlayerUid))
            {
                sapi.Network.GetChannel("entityownership").SendPacket(new EntityOwnershipPacket { OwnerShipByGroup = playerShipsByPlayerUid }, player);
            }
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("entityownership", OwnerShipsByPlayerUid);
        }

        private void Event_SaveGameLoaded()
        {
            OwnerShipsByPlayerUid = sapi.WorldManager.SaveGame.GetData("entityownership", new Dictionary<string, Dictionary<string, EntityOwnership>>());
        }

        private void Event_EntityDeath(Entity entity, DamageSource damageSource)
        {
            RemoveOwnership(entity);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Network.GetChannel("entityownership").SetMessageHandler<EntityOwnershipPacket>(onPacket);
        }

        private void onPacket(EntityOwnershipPacket packet)
        {
            SelfOwnerShips = packet.OwnerShipByGroup ?? new Dictionary<string, EntityOwnership>();

            var wm = capi.ModLoader.GetModSystem<WorldMapManager>();
            if (wm != null && wm.worldMapDlg != null && wm.worldMapDlg.IsOpened())
            {
                (wm.worldMapDlg.MapLayers.FirstOrDefault(ml => ml is OwnedEntityMapLayer) as OwnedEntityMapLayer)?.Reload();
            }

        }

        public void ClaimOwnership(Entity toEntity, EntityAgent byEntity)
        {
            if (sapi == null) return;
            string group = toEntity.GetBehavior<EntityBehaviorOwnable>()?.Group;
            if (group == null) return;

            var player = (byEntity as EntityPlayer).Player as IServerPlayer;

            Dictionary<string, EntityOwnership> playerShipsByPlayerUid;

            OwnerShipsByPlayerUid.TryGetValue(player.PlayerUID, out playerShipsByPlayerUid);
            if (playerShipsByPlayerUid == null)
            {
                OwnerShipsByPlayerUid[player.PlayerUID] = playerShipsByPlayerUid = new Dictionary<string, EntityOwnership>();
            }

            if (playerShipsByPlayerUid.TryGetValue(group, out var eo))
            {
                var prevOwnedEntity = sapi.World.GetEntityById(eo.EntityId);
                prevOwnedEntity?.WatchedAttributes.RemoveAttribute("ownedby");
            }

            playerShipsByPlayerUid[group] = new EntityOwnership() { 
                EntityId = toEntity.EntityId, 
                Pos = toEntity.ServerPos,
                Name = toEntity.GetName(),
                Color = "#0e9d51"
            };

            var tree = new TreeAttribute();
            tree.SetString("uid", player.PlayerUID);
            tree.SetString("name", player.PlayerName);
            toEntity.WatchedAttributes["ownedby"] = tree;
            toEntity.WatchedAttributes.MarkPathDirty("ownedby");

            sendOwnerShips(player);
        }

        public void RemoveOwnership(Entity fromEntity)
        {
            var tree = fromEntity.WatchedAttributes.GetTreeAttribute("ownedby");
            if (tree == null) return;

            string uid = tree.GetString("uid");
            string groupecode = fromEntity.GetBehavior<EntityBehaviorOwnable>().Group;
            if (OwnerShipsByPlayerUid.TryGetValue(uid, out var ownerships))
            {
                if (ownerships?.TryGetValue(groupecode, out var ownership) == true)
                {
                    if (ownership?.EntityId == fromEntity.EntityId)
                    {
                        ownerships.Remove(groupecode);
                        var player = sapi.World.PlayerByUid(uid);
                        if (player != null) sendOwnerShips(player as IServerPlayer);

                        fromEntity.WatchedAttributes.RemoveAttribute("ownedby");
                    }
                }
            }
        }
    }
}
