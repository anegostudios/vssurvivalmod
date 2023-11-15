using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockRockTyped : BlockShapeFromAttributes
    {
        public override string ClassType => "rocktyped-" + Variant["cover"];
        public Dictionary<string, ClutterTypeProps> clutterByCode = new Dictionary<string, ClutterTypeProps>();
        public override IEnumerable<IShapeTypeProps> AllTypes => clutterByCode.Values;

        

        public override IShapeTypeProps GetTypeProps(string code, ItemStack stack, BEBehaviorShapeFromAttributes be)
        {
            if (code == null) return null;
            clutterByCode.TryGetValue(code, out var cprops);
            return cprops;
        }

        public override void LoadTypes()
        {
            var cluttertypes = Attributes["types"].AsObject<ClutterTypeProps[]>();

            IAsset asset = api.Assets.Get("worldproperties/block/rock.json");
            StandardWorldProperty rocktypes = asset.ToObject<StandardWorldProperty>();
            List<JsonItemStack> stacks = new List<JsonItemStack>();

            var defaultGui = ModelTransform.BlockDefaultGui();
            var defaultFp = ModelTransform.BlockDefaultFp();
            var defaultTp = ModelTransform.BlockDefaultTp();
            var defaultGround = ModelTransform.BlockDefaultGround();

            foreach (var ct in cluttertypes)
            {
                if (ct.GuiTf != null) ct.GuiTransform = new ModelTransform(ct.GuiTf, defaultGui);
                if (ct.FpTf != null) ct.FpTtransform = new ModelTransform(ct.FpTf, defaultFp);
                if (ct.TpTf != null) ct.TpTransform = new ModelTransform(ct.TpTf, defaultTp);
                if (ct.GroundTf != null) ct.GroundTransform = new ModelTransform(ct.GroundTf, defaultGround);
                
                ct.ShapePath.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

                foreach (var rocktype in rocktypes.Variants)
                {
                    var rct = ct.Clone();
                    rct.Code += "-" + rocktype.Code.Path;
                    clutterByCode[rct.Code] = rct;

                    foreach (var btex in rct.Textures.Values)
                    {
                        btex.FillPlaceholder("{rock}", rocktype.Code.Path);
                    }

                    if (rct.Drops != null)
                    {
                        foreach (var drop in rct.Drops)
                        {
                            drop.Code.Path = drop.Code.Path.Replace("{rock}", rocktype.Code.Path);
                            drop.Resolve(api.World, "rock typed block drop", Code);
                        }
                    }

                    var jstack = new JsonItemStack()
                    {
                        Code = this.Code,
                        Type = EnumItemClass.Block,
                        Attributes = new JsonObject(JToken.Parse("{ \"type\": \"" + rct.Code + "\", \"rock\": \""+rocktype.Code.Path+"\" }"))
                    };

                    jstack.Resolve(api.World, ClassType + " type");
                    stacks.Add(jstack);
                }
            }

            if (Variant["cover"] != "snow")
            {
                this.CreativeInventoryStacks = new CreativeTabAndStackList[]
                {
                new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = new string[] { "general", "terrain" } }
                };
            }
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var bect = GetBEBehavior<BEBehaviorShapeFromAttributes>(pos);
            var cprops = GetTypeProps(bect?.Type, null, bect);

            return cprops.Drops?.Select(drop => drop.GetNextItemStack(dropQuantityMultiplier)).ToArray() ?? base.GetDrops(world, pos, byPlayer);
        }
    }

}
