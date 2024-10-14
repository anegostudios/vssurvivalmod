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
            var cclass = new string[] { "item", "block" };
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Target", CairoFont.WhiteDetailText(), b)
                .AddDropDown(vals, vals, vals.IndexOf(this.Target), null, b.BelowCopy(0, -5), "target")

                .AddStaticText("Class", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 25))
                .AddDropDown(cclass, cclass, vals.IndexOf(this.Target), null, b.BelowCopy(0, -5), "cclass")

                .AddStaticText("Block/Item code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 25).WithFixedWidth(300))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "code")

                .AddStaticText("Attributes", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 25).WithFixedWidth(300))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "attr")
            ;

            JsonItemStack jstack = JsonItemStack.FromString(Value);
            singleComposer.GetDropDown("cclass").SetSelectedIndex(cclass.IndexOf(jstack.Type.ToString().ToLowerInvariant()));
            singleComposer.GetTextInput("code").SetValue(jstack.Code.ToShortString());
            singleComposer.GetTextInput("attr").SetValue(jstack.Attributes?.ToString() ?? "");
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            string type = singleComposer.GetDropDown("cclass").SelectedValue;
            string code = singleComposer.GetTextInput("code").GetText();
            string attr = singleComposer.GetTextInput("attr").GetText();

            if (attr.Length > 0)
            {
                Value = string.Format("{{ type: \"{0}\", code: \"{1}\", attributes: {2} }}", type, code, attr);
            } else
            {
                Value = string.Format("{{ type: \"{0}\", code: \"{1}\" }}", type, code);
            }
            
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
