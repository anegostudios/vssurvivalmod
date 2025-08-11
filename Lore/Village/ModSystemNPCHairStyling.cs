using ProtoBuf;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class PacketHairStyle
    {
        [ProtoMember(1)]
        public long HairstylingNpcEntityId;
        [ProtoMember(2)]
        public Dictionary<string, string> Hairstyle;
    }

    public class ModSystemNPCHairStyling : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void Start(ICoreAPI api)
        {
            this.Api = api;
            api.Network
                .RegisterChannel("hairstyling")
                .RegisterMessageType<PacketHairStyle>()
            ;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Network.GetChannel("hairstyling").SetMessageHandler<PacketHairStyle>(onPacketHairstyle);
        }

        private void onPacketHairstyle(IServerPlayer fromPlayer, PacketHairStyle packet)
        {
            var hairstylingNpc = Api.World.GetEntityById(packet.HairstylingNpcEntityId) as EntityTradingHumanoid;
            if (hairstylingNpc == null || !hairstylingNpc.interactingWithPlayer.Contains(fromPlayer.Entity))
            {
                return;
            }

            int costs = getCost(fromPlayer, packet.Hairstyle, GetPricesByCode(hairstylingNpc));
            int money = InventoryTrader.GetPlayerAssets(fromPlayer.Entity);

            if (money >= costs)
            {
                InventoryTrader.DeductFromEntity(Api, fromPlayer.Entity, costs);

                var skinMod = fromPlayer.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();
                foreach (var val in packet.Hairstyle)
                {
                    skinMod.selectSkinPart(val.Key, val.Value, false, false);
                }

                fromPlayer.Entity.WatchedAttributes.MarkPathDirty("skinConfig");
            } else
            {
                fromPlayer.SendIngameError("notenoughmoney", Lang.GetL(fromPlayer.LanguageCode, "Not enough money"));
            }            
        }

        public int getCost(IServerPlayer player, Dictionary<string, string> hairstyle, Dictionary<string, int> costs)
        {
            int cost = 0;

            var skinMod = player.Entity.GetBehavior<EntityBehaviorExtraSkinnable>();

            foreach (var val in hairstyle)
            {
                AppliedSkinnablePartVariant appliedVar = skinMod.AppliedSkinParts.FirstOrDefault(sp => sp.PartCode == val.Key);

                if (hairstyle[val.Key] != appliedVar.Code)
                {
                    cost += costs[val.Key];
                }
            }

            return cost;
        }

        GuiDialogHairStyling hairStylingDialog;
        ICoreAPI Api;

        public void handleHairstyling(EntityTradingHumanoid hairstylingNpc, EntityAgent triggeringEntity, string[] hairStylingCategories)
        {
            var eplr = triggeringEntity as EntityPlayer;
            if (hairstylingNpc.Alive && triggeringEntity.Pos.SquareDistanceTo(hairstylingNpc.Pos) <= 7 && !hairstylingNpc.interactingWithPlayer.Contains(eplr))
            {
                hairstylingNpc.interactingWithPlayer.Add(triggeringEntity as EntityPlayer);

                if (Api is ICoreClientAPI capi)
                {
                    hairStylingDialog = new GuiDialogHairStyling(capi, hairstylingNpc.EntityId, hairStylingCategories, GetPricesByCode(triggeringEntity));
                    hairStylingDialog.TryOpen();
                    hairStylingDialog.OnClosed += () =>
                    {
                        hairstylingNpc.interactingWithPlayer.Remove(eplr);
                        capi.Network.SendEntityPacket(hairstylingNpc.EntityId, EntityTradingHumanoid.PlayerStoppedInteracting);
                    };
                }
            }
            else
            {
                hairStylingDialog?.TryClose();
                hairstylingNpc.interactingWithPlayer.Remove(eplr);
                if (Api is ICoreServerAPI sapi)
                {
                    sapi.Network.SendEntityPacket(eplr.Player as IServerPlayer, hairstylingNpc.EntityId, EntityTradingHumanoid.PlayerStoppedInteracting);
                }
            }
        }

        public Dictionary<string, int> GetPricesByCode(Entity hairstylingNpc)
        {
            Dictionary<string, int> defaultValue = new Dictionary<string, int>()
            {
                { "hairbase", 2 },
                { "hairextra", 2 },
                { "mustache", 2 },
                { "beard", 2 }
            };

            return hairstylingNpc?.Properties?.Attributes["hairstylingCosts"].AsObject(defaultValue) ?? defaultValue;
        }
    }
}
