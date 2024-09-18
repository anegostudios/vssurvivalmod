using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UndressAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        public string Type => "undress";
        public bool ExecutionHasFailed { get; set; }

        [JsonProperty]
        string Slot;

        public UndressAction() { }

        public UndressAction(EntityActivitySystem vas, string slot)
        {
            this.vas = vas;
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
            if (index >= 0)
            {
                edh.OutfitCodes = edh.OutfitCodes.RemoveAt(index);
                edh.OutfitSlots = edh.OutfitSlots.RemoveAt(index);
            }
        }


        public void OnTick(float dt) { }
        public void Cancel()
        {
            // re-equip here
        }
        public void Finish() { }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("From Slot", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "slot")
            ;

            singleComposer.GetTextInput("slot").SetValue(Slot);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Slot = singleComposer.GetTextInput("slot").GetText();
            return true;
        }

        public IEntityAction Clone()
        {
            return new UndressAction(vas, Slot);
        }

        public override string ToString()
        {
            return "Remove outfit in slot " + Slot;
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
