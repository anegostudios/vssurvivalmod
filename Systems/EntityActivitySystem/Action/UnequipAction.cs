using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class UnequipAction : EntityActionBase
    {
        public override string Type => "unequip";
        
        [JsonProperty]
        string Target;

        public UnequipAction() { }

        public UnequipAction(EntityActivitySystem vas, string target)
        {
            this.vas = vas;
            this.Target = target;
        }


        public override void Start(EntityActivity act)
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



        public override void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var vals = new string[] { "lefthand", "righthand" };
            var b = ElementBounds.Fixed(0, 0, 300, 25);
            singleComposer
                .AddStaticText("Target", CairoFont.WhiteDetailText(), b)
                .AddDropDown(vals, vals, vals.IndexOf(this.Target), null, b.BelowCopy(0, -5), "target")
            ;
        }

        public override bool StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Target = singleComposer.GetDropDown("target").SelectedValue;
            return true;
        }

        public override IEntityAction Clone()
        {
            return new UnequipAction(vas, Target);
        }

        public override string ToString()
        {
            return "Remove item from " + Target;
        }
    }
}
