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
            return 0.15f;
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

                foreach (var val in tradeProps.Selling.List)
                {
                    if (val.Resolve(capi.World, "tradehandbookinfo", true))
                    {
                        var collobj = val.ResolvedItemstack.Collectible;

                        if (collobj.Attributes == null) collobj.Attributes = new JsonObject(JToken.Parse("{}"));

                        ExtraHandbookSection section;
                        var bh = collobj.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>();

                        string title = Lang.Get("Sold by");
                        section = bh.ExtraHandBookSections?.FirstOrDefault(ele => ele.Title == title);
                        if (section == null)
                        {
                            section = new ExtraHandbookSection() { Title = title, TextParts = new string[0] };
                            collobj.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>().ExtraHandBookSections = bh.ExtraHandBookSections == null ? new ExtraHandbookSection[] { section } : bh.ExtraHandBookSections.Append(section);
                        }

                        section.TextParts = section.TextParts.Append(Lang.Get(entitytype.Code.Domain + ":item-creature-" + entitytype.Code.Path));
                    }
                }

                foreach (var val in tradeProps.Buying.List)
                {
                    if (val.Resolve(capi.World, "tradehandbookinfo", true))
                    {
                        var collobj = val.ResolvedItemstack.Collectible;

                        if (collobj.Attributes == null) collobj.Attributes = new JsonObject(JToken.Parse("{}"));

                        ExtraHandbookSection section;
                        var bh = collobj.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>();

                        string title = Lang.Get("Purchased by");
                        section = bh?.ExtraHandBookSections?.FirstOrDefault(ele => ele.Title == title);

                        if (section == null)
                        {
                            section = new ExtraHandbookSection() { Title = title, TextParts = new string[0] };
                            collobj.GetBehavior<CollectibleBehaviorHandbookTextAndExtraInfo>().ExtraHandBookSections = bh.ExtraHandBookSections == null ? new ExtraHandbookSection[] { section } : bh.ExtraHandBookSections.Append(section);
                        }

                        section.TextParts = section.TextParts.Append(Lang.Get(entitytype.Code.Domain + ":item-creature-" + entitytype.Code.Path));
                    }
                }
            }
        }
    }
}
