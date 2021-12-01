using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api as ICoreClientAPI;

            api.Event.BlockTexturesLoaded += Event_BlockTexturesLoaded;
        }

        private void Event_BlockTexturesLoaded()
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

                        string title = Lang.Get("Sold by");
                        section = collobj.ExtraHandBookSections?.FirstOrDefault(ele => ele.Title == title);
                        if (section == null)
                        {
                            section = new ExtraHandbookSection() { Title = title, TextParts = new string[0] };
                            collobj.ExtraHandBookSections = collobj.ExtraHandBookSections == null ? new ExtraHandbookSection[] { section } : collobj.ExtraHandBookSections.Append(section);
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

                        string title = Lang.Get("Purchased by");
                        section = collobj.ExtraHandBookSections?.FirstOrDefault(ele => ele.Title == title);
                        if (section == null)
                        {
                            section = new ExtraHandbookSection() { Title = title, TextParts = new string[0] };
                            collobj.ExtraHandBookSections = collobj.ExtraHandBookSections == null ? new ExtraHandbookSection[] { section } : collobj.ExtraHandBookSections.Append(section);
                        }

                        section.TextParts = section.TextParts.Append(Lang.Get(entitytype.Code.Domain + ":item-creature-" + entitytype.Code.Path));
                    }
                }
            }
        }
    }
}
