using Newtonsoft.Json.Linq;
using System;
 using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class TradeHandbookInfo : ModSystem
    {
        ICoreClientAPI capi;

        public override double ExecuteOrder()
        {
            return 0.15;
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            api.Event.LevelFinalize += Event_LevelFinalize;
        }

        private void Event_LevelFinalize()
        {
            foreach (var entitytype in capi.World.EntityTypes)
            {
                TradeProperties tradeProps = null;

                if (entitytype.Attributes?["tradeProps"].Exists == true)
                {
                    try
                    {
                        tradeProps = entitytype.Attributes["tradeProps"].AsObject<TradeProperties>();
                    }
                    catch (Exception e)
                    {
                        capi.World.Logger.Error("Failed deserializing tradeProps attribute for entitiy {0}, exception logged to verbose debug", entitytype.Code);
                        capi.World.Logger.VerboseDebug("Failed deserializing TradeProperties: {0}", e);
                        capi.World.Logger.VerboseDebug("=================");
                        capi.World.Logger.VerboseDebug("Tradeprops json:");
                        capi.World.Logger.VerboseDebug("{0}", entitytype.Server?.Attributes["tradeProps"].ToJsonToken());
                    }
                }
                if (tradeProps == null) continue;

                string traderName = Lang.Get(entitytype.Code.Domain + ":item-creature-" + entitytype.Code.Path);
                string handbookTitle = Lang.Get("Sold by");
                foreach (var val in tradeProps.Selling.List)
                {
                    AddTraderHandbookInfo(val, traderName, handbookTitle);
                }

                handbookTitle = Lang.Get("Purchased by");
                foreach (var val in tradeProps.Buying.List)
                {
                    AddTraderHandbookInfo(val, traderName, handbookTitle);
                }
            }
            capi.Logger.VerboseDebug("Done traders handbook stuff");
        }

        private void AddTraderHandbookInfo(TradeItem val, string traderName, string title)
        {
            if (val.Resolve(capi.World, "tradehandbookinfo", true))
            {
                var collobj = val.ResolvedItemstack.Collectible;

                if (collobj.Attributes == null) collobj.Attributes = new JsonObject(JToken.Parse("{}"));

                var bh = collobj.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>();

                ExtraHandbookSection section = bh.ExtraHandBookSections?.FirstOrDefault(ele => ele.Title == title);
                if (section == null)
                {
                    section = new ExtraHandbookSection() { Title = title, TextParts = new string[0] };
                    if (bh.ExtraHandBookSections != null) bh.ExtraHandBookSections.Append(section);
                    else bh.ExtraHandBookSections = new ExtraHandbookSection[] { section };
                }

                section.TextParts = section.TextParts.Append(traderName);
            }
        }
    }
}
