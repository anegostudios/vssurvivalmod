using Newtonsoft.Json;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable
using static Vintagestory.API.Common.EntityAgent;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TalkAction : EntityActionBase
    {
        public override string Type => "talk";

        [JsonProperty]
        string talkType;

        public TalkAction() { }

        public TalkAction(EntityActivitySystem vas, string talkType)
        {
            this.vas = vas;
            this.talkType = talkType;
        }


        public override void Start(EntityActivity act)
        {
            var vals = Enum.GetNames(typeof(EnumTalkType));
            int index = vals.IndexOf(talkType);
            (vas.Entity.Api as ICoreServerAPI).Network.BroadcastEntityPacket(vas.Entity.EntityId, (int)EntityServerPacketId.Talk, SerializerUtil.Serialize(index));
        }


        public override string ToString()
        {
            return "Talk utterance: " + talkType;
        }

        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            var vals = Enum.GetNames(typeof(EnumTalkType));
            
            singleComposer
                .AddStaticText("Utterance", CairoFont.WhiteDetailText(), b)
                .AddDropDown(vals, vals, vals.IndexOf(talkType), null, b.BelowCopy(0, -5), "talkType")
            ;
        }


        public override IEntityAction Clone()
        {
            return new TalkAction(vas, talkType);
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            talkType = singleComposer.GetDropDown("talkType").SelectedValue;
            return true;
        }

    }
}
