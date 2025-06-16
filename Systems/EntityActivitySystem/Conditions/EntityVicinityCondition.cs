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
    public class EntityVicinityCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        float range;
        [JsonProperty]
        AssetLocation entityCode;

        EntityActivitySystem vas;
        EntityPartitioning ep;

        public EntityVicinityCondition() { }

        public EntityVicinityCondition(EntityActivitySystem vas, float range, AssetLocation entityCode, bool invert = false)
        {
            this.vas = vas;
            this.range = range;
            this.Invert = invert;

            ep = vas.Entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
        }

        public virtual string Type => "entityvicinity";

        public bool ConditionSatisfied(Entity e)
        {
            return ep.GetNearestEntity(vas.Entity.Pos.XYZ, range, (e) => e.WildCardMatch(entityCode), EnumEntitySearchType.Creatures) != null;
        }

        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 200, 25);
            singleComposer
                .AddStaticText("Range", CairoFont.WhiteDetailText(), b)
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "range")

                .AddStaticText("EntityCode", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 10))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "entityCode")
            ;

            singleComposer.GetTextInput("range").SetValue(range + "");
            singleComposer.GetTextInput("entityCode").SetValue(entityCode + "");
        }

        public IActionCondition Clone()
        {
            return new EntityVicinityCondition(vas, range, entityCode, Invert);
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            range = singleComposer.GetTextInput("range").GetText().ToFloat();
            entityCode = new AssetLocation(singleComposer.GetTextInput("entityCode").GetText());
        }

        public override string ToString()
        {
            return string.Format("When {2} in {0} blocks range of {1}", range, entityCode, Invert ? "NOT":"");
        }
        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
            ep = vas?.Entity.Api.ModLoader.GetModSystem<EntityPartitioning>();
        }
    }
}
