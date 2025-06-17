using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{

    [JsonObject(MemberSerialization.OptIn)]
    public class HeldCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        AssetLocation Code;


        protected EntityActivitySystem vas;
        public HeldCondition() { }
        public HeldCondition(EntityActivitySystem vas, string Code, bool invert = false)
        {
            this.vas = vas;
            this.Code = Code;
            this.Invert = invert;
        }

        public string Type => "held";


        public virtual bool ConditionSatisfied(Entity e)
        {
            var eagent = e as EntityAgent;

            var leftStack = eagent.LeftHandItemSlot.Itemstack;
            if (leftStack != null && WildcardUtil.Match(Code, leftStack.Collectible.Code)) return true;

            var rightStack = eagent.RightHandItemSlot.Itemstack;
            if (rightStack != null && WildcardUtil.Match(Code, rightStack.Collectible.Code)) return true;

            return false;
        }
        

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 250, 25);
            singleComposer
                .AddStaticText("Block or Item Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "code")
            ;

            singleComposer.GetTextInput("code").SetValue(Code?.ToShortString());
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            Code = new AssetLocation(singleComposer.GetTextInput("code").GetText());
        }
        public IActionCondition Clone()
        {
            return new HeldCondition(vas, Code, Invert);
        }

        public override string ToString()
        {
            return string.Format(Invert ? "When not holding {0} in hands" : "When holding {0} in hands", Code);
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
