using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DressedCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        string Code;
        [JsonProperty]
        string Slot;


        protected EntityActivitySystem vas;
        public DressedCondition() { }
        public DressedCondition(EntityActivitySystem vas, string code, string slot, bool invert = false)
        {
            this.vas = vas;
            this.Code = code;
            this.Slot = slot;
            this.Invert = invert;
        }

        public string Type => "dressed";


        public virtual bool ConditionSatisfied(Entity e)
        {
            var edh = vas.Entity as EntityDressedHumanoid;
            if (edh == null) return false;

            var index = edh.OutfitSlots.IndexOf(Slot);
            if (index < 0) return false;

            return edh.OutfitCodes[index] == Code;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 250, 25);
            singleComposer
                .AddStaticText("Slot", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "slot")

                .AddStaticText("Accessory Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "code")
            ;

            singleComposer.GetTextInput("code").SetValue(Code);
            singleComposer.GetTextInput("slot").SetValue(Slot);
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Code = singleComposer.GetTextInput("code").GetText();
            Slot = singleComposer.GetTextInput("slot").GetText();
        }
        public IActionCondition Clone()
        {
            return new DressedCondition(vas, Code, Slot, Invert);
        }

        public override string ToString()
        {
            return string.Format(Invert ? "When not {0} dressed in slot {1}" : "When {0} dressed in slot {1}", Code, Slot);
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
