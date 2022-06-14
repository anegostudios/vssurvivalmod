using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ClutterTypeProps : IShapeTypeProps
    {
        public string Code { get; set; }
        public Vec3f Rotation { get; set; } = new Vec3f();
        public Cuboidf[] ColSelBoxes { get; set; }
        public ModelTransform GuiTf { get; set; }
        public ModelTransform FpTf { get; set; }
        public ModelTransform TpTf { get; set; }
        public ModelTransform GroundTf { get; set; }
        public string RotInterval { get; set; } = "22.5deg";
        public string firstTexture { get; set; }
        public TextureAtlasPosition texPos { get; set; }
        public Dictionary<int, Cuboidf[]> ColSelBoxesByDeg { get; set; } = new Dictionary<int, Cuboidf[]>();

        public AssetLocation ShapePath { get; set; }
        public Shape ShapeResolved { get; set; }

        public string HashKey => Code;
    }

    public class BlockClutter : BlockShapeFromAttributes
    {
        public override string ClassType => "clutter";

        public Dictionary<string, ClutterTypeProps> clutterByCode = new Dictionary<string, ClutterTypeProps>();
        public override IEnumerable<IShapeTypeProps> AllTypes => clutterByCode.Values;
        string basePath;

        public override void LoadTypes()
        {
            var cluttertypes = Attributes["types"].AsObject<ClutterTypeProps[]>();
            basePath = Attributes["shapeBasePath"].AsString();

            List<JsonItemStack> stacks = new List<JsonItemStack>();

            foreach (var cluttertype in cluttertypes)
            {
                clutterByCode[cluttertype.Code] = cluttertype;
                cluttertype.ShapePath = AssetLocation.Create("shapes/" + basePath + "/" + cluttertype.Code + ".json", Code.Domain);

                var jstack = new JsonItemStack()
                {
                    Code = this.Code,
                    Type = EnumItemClass.Block,
                    Attributes = new JsonObject(JToken.Parse("{ \"type\": \"" + cluttertype.Code + "\" }"))
                };

                jstack.Resolve(api.World, ClassType + " type");
                stacks.Add(jstack);
            }

            this.CreativeInventoryStacks = new CreativeTabAndStackList[]
            {
                new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = new string[]{ "general", "decorative" } }
            };
        }

        public override IShapeTypeProps GetTypeProps(string code, ItemStack stack, BlockEntityShapeFromAttributes be)
        {
            if (code == null) return null;
            clutterByCode.TryGetValue(code, out var cprops);
            return cprops;
        }
    }

}
