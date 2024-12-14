using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class EquipAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        public string Type => "equip";
        public bool ExecutionHasFailed { get; set; }

        [JsonProperty]
        string Target;
        [JsonProperty]
        string Value;

        public EquipAction() { }

        public EquipAction(EntityActivitySystem vas, string target, string value)
        {
            this.vas = vas;
            this.Target = target;
            this.Value = value;
        }

        public bool IsFinished()
        {
            return true;
        }

        public void Start(EntityActivity act)
        {
            switch (Target)
            {
                case "righthand": 
                case "lefthand":
                    JsonItemStack jstack = JsonItemStack.FromString(Value);
                    if (!jstack.Resolve(vas.Entity.World, vas.Entity.Code + " entity activity system, equip action - could not resolve " + Value + ". Will ignore.", true))
                    {
                        return;
                    }

                    var targetslot = Target == "righthand" ? vas.Entity.RightHandItemSlot : vas.Entity.LeftHandItemSlot;
                    targetslot.Itemstack = jstack.ResolvedItemstack;
                    targetslot.MarkDirty();
                    vas.Entity.GetBehavior<EntityBehaviorContainer>().storeInv();
                    break;
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
            var vals = new string[] { "lefthand", "righthand" };
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Target", CairoFont.WhiteDetailText(), b)
                .AddDropDown(vals, vals, vals.IndexOf(this.Target), null, b.BelowCopy(0, -5), "target")

                .AddStaticText("Value (outfit code or item stack in json format)", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 25).WithFixedWidth(300))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "value")
            ;

            singleComposer.GetTextInput("value").SetValue(Value);
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Value = singleComposer.GetTextInput("value").GetText();
            Target = singleComposer.GetDropDown("target").SelectedValue;

            try
            {
                JsonItemStack jstack = JsonItemStack.FromString(Value);
                if (!jstack.Resolve(capi.World, "Entity activity system, equip action - could not resolve " + Value + ". Will ignore.", true))
                {
                    capi.TriggerIngameError(this, "cantresolve", "Can't save. Unable to resolve json stack " + Value + ".");
                    return false;
                }
            } catch
            {
                capi.TriggerIngameError(this, "cantresolve", "Can't save. Not valid json stack " + Value + " - an exception was thrown.");
                return false;
            }

            return true;
        }

        public IEntityAction Clone()
        {
            return new EquipAction(vas, Target, Value);
        }

        public override string ToString()
        {
            return "Grab " + Value + " in " + Target;
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
