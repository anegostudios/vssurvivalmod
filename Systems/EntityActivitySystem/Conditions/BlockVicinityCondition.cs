using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BlockVicinityCondition : IActionCondition
    {
        [JsonProperty]
        public bool Invert { get; set; }
        [JsonProperty]
        AssetLocation blockCode;
        [JsonProperty]
        float searchRange;

        protected EntityActivitySystem vas;
        public BlockVicinityCondition() { }
        public BlockVicinityCondition(EntityActivitySystem vas, AssetLocation blockCode, float searchRange, bool invert = false)
        {
            this.vas = vas;
            this.blockCode = blockCode;
            this.searchRange = searchRange;
            this.Invert = invert;
        }

        public string Type => "blockvicinity";

        long lastSearchTotalMs;
        bool conditionsatisfied;

        public virtual bool ConditionSatisfied(Entity e)
        {
            var ela = vas.Entity.Api.World.ElapsedMilliseconds;

            if (ela - lastSearchTotalMs > 1500)
            {
                lastSearchTotalMs = ela;
                conditionsatisfied = getTarget() != null;
            }

            return conditionsatisfied;
        }

        private BlockPos getTarget()
        {
            var range = GameMath.Clamp(searchRange, -10, 10);
            var api = vas.Entity.Api;
            var minPos = vas.Entity.ServerPos.XYZ.Add(-range, -1, -range).AsBlockPos;
            var maxPos = vas.Entity.ServerPos.XYZ.Add(range, 1, range).AsBlockPos;

            BlockPos targetPos = null;
            api.World.BlockAccessor.WalkBlocks(minPos, maxPos, (block, x, y, z) =>
            {
                if (blockCode == null) return;

                if (block.WildCardMatch(blockCode))
                {
                    targetPos = new BlockPos(x, y, z);
                }
            }, true);
            return targetPos;
        }


        public void LoadState(ITreeAttribute tree) { }
        public void StoreState(ITreeAttribute tree) { }


        public void AddGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            var b = ElementBounds.Fixed(0, 0, 250, 25);
            singleComposer
                .AddStaticText("Search Range (capped 10 blocks)", CairoFont.WhiteDetailText(), b)
                .AddNumberInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "searchRange")

                .AddStaticText("Block Code", CairoFont.WhiteDetailText(), b = b.BelowCopy(0, 15))
                .AddTextInput(b = b.BelowCopy(0, -5), null, CairoFont.WhiteDetailText(), "blockCode")
            ;

            singleComposer.GetNumberInput("searchRange").SetValue(searchRange);
            singleComposer.GetTextInput("blockCode").SetValue(blockCode?.ToShortString());
        }

        public void StoreGuiEditFields(ICoreClientAPI capi, GuiComposer singleComposer)
        {
            blockCode = new AssetLocation(singleComposer.GetTextInput("blockCode").GetText());
            searchRange = singleComposer.GetNumberInput("searchRange").GetValue();
        }
        public IActionCondition Clone()
        {
            return new BlockVicinityCondition(vas, blockCode, searchRange, Invert);
        }

        public override string ToString()
        {
            return (Invert ? "When not near block" : "When near block") + blockCode;
        }

        public void OnLoaded(EntityActivitySystem vas)
        {
            this.vas = vas;
        }
    }
}
