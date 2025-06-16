using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemSkinnableAdditions : ModSystem
    {
        protected Dictionary<string, SkinnablePart> skinPartsByCode = new Dictionary<string, SkinnablePart>();

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);

            List<SkinnablePart> skinparts = new List<SkinnablePart>();

            var assets = api.Assets.GetMany("config/seraphskinnableparts.json");
            foreach (var asset in assets)
            {
                var parts = asset.ToObject<SkinnablePart[]>();

                foreach (var part in parts) {
                    skinPartsByCode.TryGetValue(part.Code, out var expart);
                    if (expart != null)
                    {
                        expart.Variants = expart.Variants.Append(part.Variants);
                    } else
                    {
                        skinPartsByCode[part.Code] = part;
                    }
                }
            }
        }

        public SkinnablePart[] AppendAdditions(SkinnablePart[] toParts)
        {
            foreach (var part in toParts)
            {
                skinPartsByCode.TryGetValue(part.Code, out var expart);
                if (expart != null)
                {
                    part.Variants = part.Variants.Append(expart.Variants);
                } else
                {
                    
                }
            }

            return toParts;
        }
    }

}
