using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TemporalStormCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        EntityActivitySystem vas;

        public TemporalStormCondition() { }

        public TemporalStormCondition(EntityActivitySystem vas, bool invert = false)
        {
            this.vas = vas;
            this.Invert = invert;
        }

        public string Type => "temporalstorm";

        public bool ConditionSatisfied(Entity e)
        {
            return e.Api.ModLoader.GetModSystem<SystemTemporalStability>().StormStrength > 0;
        }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            
        }

        public IActionCondition Clone()
        {
            return new TemporalStormCondition(vas, Invert);
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            
        }

        public override string ToString()
        {
            return Invert ? "Outisde a temporal storm" : "During a temporal storm";
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
