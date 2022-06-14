using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorWrenchOrientable : BlockBehavior
    {
        public static Dictionary<string, List<AssetLocation>> VariantsByType = new Dictionary<string, List<AssetLocation>>();

        public string BaseCode;

        public BlockBehaviorWrenchOrientable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            BaseCode = properties["baseCode"].AsString();
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            List<AssetLocation> vars;
            if (!VariantsByType.TryGetValue(BaseCode, out vars)) VariantsByType[BaseCode] = vars = new List<AssetLocation>();
            vars.Add(block.Code);
        }
    }
}
