using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DressAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        public string Type => "dress";
        public bool ExecutionHasFailed { get; set; }

        [JsonProperty]
        string Code;
        [JsonProperty]
        string Slot;

        public DressAction() { }

        public DressAction(EntityActivitySystem vas, string code, string slot)
        {
            this.vas = vas;
            this.Code = code;
            this.Slot = slot;
        }

        public bool IsFinished()
        {
            return true;
        }

        public void Start(EntityActivity act)
        {
            var edh = vas.Entity as EntityDressedHumanoid;
            if (edh == null) return;

            var index = edh.OutfitSlots.IndexOf(Slot);
            if (index < 0)
            {
                edh.OutfitCodes = edh.OutfitCodes.Append(Code);
                edh.OutfitSlots = edh.OutfitSlots.Append(Slot);
            } else
            {
                edh.OutfitCodes[index] = Code;
                edh.WatchedAttributes.MarkPathDirty("outfitcodes");
            }
        }


        public void OnTick(float dt) { }
        public void Cancel() { 
            // unequip here
        }
        public void Finish() { }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Slot", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "slot")

                .AddStaticText("Outfit code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 25).WithFixedWidth(300))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "code")
            ;

            singleComposer.GetTextInput("slot").SetValue(Slot);
            singleComposer.GetTextInput("code").SetValue(Code);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Slot = singleComposer.GetTextInput("slot").GetText();
            Code = singleComposer.GetTextInput("code").GetText();
            return true;
        }

        public IEntityAction Clone()
        {
            return new DressAction(vas, Code, Slot);
        }

        public override string ToString()
        {
            return "Dress outfit " + Code + " in slot" + Slot;
        }

        public void OnVisualize(ActivityVisualizer visualizer)
        {
            
        }
        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
