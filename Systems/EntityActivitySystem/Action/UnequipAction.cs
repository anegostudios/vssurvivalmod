using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UnequipAction : IEntityAction
    {
        protected EntityActivitySystem vas;
        public string Type => "unequip";
        public bool ExecutionHasFailed { get; set; }

        [JsonProperty]
        string Target;

        public UnequipAction() { }

        public UnequipAction(EntityActivitySystem vas, string target)
        {
            this.vas = vas;
            this.Target = target;
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
                    var targetslot = Target == "righthand" ? vas.Entity.RightHandItemSlot : vas.Entity.LeftHandItemSlot;
                    targetslot.Itemstack = null;
                    targetslot.MarkDirty();
                    vas.Entity.GetBehavior<EntityBehaviorContainer>().storeInv();
                    break;
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
            var vals = new string[] { "lefthand", "righthand" };
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Target", CairoFont.WhiteDetailText(), b)
                .AddDropDown(vals, vals, vals.IndexOf(this.Target), null, b.BelowCopy(0, -5), "target")
            ;
        }

        public bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Target = singleComposer.GetDropDown("target").SelectedValue;
            return true;
        }

        public IEntityAction Clone()
        {
            return new UnequipAction(vas, Target);
        }

        public override string ToString()
        {
            return "Remove item from " + Target;
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
