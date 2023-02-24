using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BookShelfVariantGroup
    {
        public bool DoubleSided;
        public BookShelfTypeProps[] types;

        public TextureAtlasPosition texPos { get; set; }

        public OrderedDictionary<string, BookShelfTypeProps> typesByCode = new OrderedDictionary<string, BookShelfTypeProps>();
        public BlockClutterBookshelf block;

        public Vec3f Rotation { get; set; } = new Vec3f();
        public Cuboidf[] ColSelBoxes { get; set; }
        public ModelTransform GuiTf { get; set; } = ModelTransform.BlockDefaultGui().EnsureDefaultValues().WithRotation(new Vec3f(-22.6f, -45 - 0.3f - 90, 0));
        public ModelTransform FpTf { get; set; }
        public ModelTransform TpTf { get; set; }
        public ModelTransform GroundTf { get; set; }
        public string RotInterval { get; set; } = "22.5deg";

        public Dictionary<int, Cuboidf[]> ColSelBoxesByDeg { get; set; } = new Dictionary<int, Cuboidf[]>();
    }
}
