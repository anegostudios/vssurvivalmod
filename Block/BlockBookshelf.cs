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
    public class BookShelfTypeProps : IShapeTypeProps
    {
        public BookShelfVariantGroup group;
        static Random rnd = new Random();

        public string Code { get; set; }

        public string Type1 { get; set; }
        public string Type2 { get; set; }

        public string Variant { get; set; }

        public Vec3f Rotation => group.Rotation;
        public Cuboidf[] ColSelBoxes { get { return group.ColSelBoxes; } set { group.ColSelBoxes = value; } }
        public ModelTransform GuiTf { get { return group.GuiTf; } set { group.GuiTf = value; } }
        public ModelTransform FpTf { get { return group.FpTf; } set { group.FpTf = value; } }
        public ModelTransform TpTf { get { return group.TpTf; } set { group.TpTf = value; } }
        public ModelTransform GroundTf { get { return group.GroundTf; } set { group.GroundTf = value; } }
        public string RotInterval { get { return group.RotInterval; } set { group.RotInterval = value; } }

        public string firstTexture { get; set; }
        public TextureAtlasPosition texPos { get; set; }
        public Dictionary<int, Cuboidf[]> ColSelBoxesByDeg { get { return group.ColSelBoxesByDeg; } set { group.ColSelBoxesByDeg = value; } }

        public AssetLocation ShapePath
        {
            get
            {
                if (Variant.Contains("doublesided"))
                {
                    if (Type1 == null)
                    {
                        int rndindex = rnd.Next(group.typesByCode.Count);
                        Type1 = group.typesByCode.GetKeyAtIndex(rndindex);
                    }
                    return AssetLocation.Create("shapes/" + group.block.basePath + "/" + Type1 + ".json", group.block.Code.Domain);
                }

                return AssetLocation.Create("shapes/" + group.block.basePath + "/" + Code + ".json", group.block.Code.Domain);
            }
        }
        public AssetLocation ShapePath2 { 
            get
            {
                if (Variant.Contains("doublesided"))
                {
                    if (Type2 == null)
                    {
                        int rndindex = rnd.Next(group.typesByCode.Count);
                        Type2 = group.typesByCode.GetKeyAtIndex(rndindex);
                    }
                    return AssetLocation.Create("shapes/" + group.block.basePath + "/" + Type2 + ".json", group.block.Code.Domain);
                }

                return ShapePath;
            }
        }
        public Shape ShapeResolved { get; set; }
        public Shape ShapeResolved2 { get; set; }

        public string HashKey => Code + "-" + Type1 + "-" + Type2 + "-" + Variant;
    }

    public class BookShelfVariantGroup
    {
        public bool DoubleSided;
        public BookShelfTypeProps[] types;

        public TextureAtlasPosition texPos { get; set; }

        public OrderedDictionary<string, BookShelfTypeProps> typesByCode = new OrderedDictionary<string, BookShelfTypeProps>();
        public BlockBookShelf block;

        public Vec3f Rotation { get; set; } = new Vec3f();
        public Cuboidf[] ColSelBoxes { get; set; }
        public ModelTransform GuiTf { get; set; } = ModelTransform.BlockDefaultGui().EnsureDefaultValues().WithRotation(new Vec3f(-22.6f, -45 - 0.3f - 90, 0));
        public ModelTransform FpTf { get; set; }
        public ModelTransform TpTf { get; set; }
        public ModelTransform GroundTf { get; set; }
        public string RotInterval { get; set; } = "22.5deg";

        public Dictionary<int, Cuboidf[]> ColSelBoxesByDeg { get; set; } = new Dictionary<int, Cuboidf[]>();
    }

    public class BlockBookShelf : BlockShapeFromAttributes
    {
        public override string ClassType => "bookshelf";

        public OrderedDictionary<string, BookShelfVariantGroup> variantGroupsByCode = new OrderedDictionary<string, BookShelfVariantGroup>();
        public override IEnumerable<IShapeTypeProps> AllTypes
        {
            get
            {
                var result = new List<IShapeTypeProps>();
                foreach (var variant in variantGroupsByCode.Values)
                {
                    result.AddRange(variant.typesByCode.Values);
                }
                return result;
            }
        }
        public string basePath;

        AssetLocation woodbackPanelShapePath;

        public override void LoadTypes()
        {
            variantGroupsByCode = Attributes["variantGroups"].AsObject<OrderedDictionary<string, BookShelfVariantGroup>>();
            basePath = Attributes["shapeBasePath"].AsString();

            List<JsonItemStack> stacks = new List<JsonItemStack>();

            woodbackPanelShapePath = AssetLocation.Create("shapes/" + basePath + "/" + Attributes["woodbackPanelShapePath"].AsString() + ".json", Code.Domain);

            foreach (var variant in variantGroupsByCode)
            {
                variant.Value.block = this;
                
                if (variant.Value.DoubleSided)
                {
                    var jstackd = new JsonItemStack()
                    {
                        Code = this.Code,
                        Type = EnumItemClass.Block,
                        Attributes = new JsonObject(JToken.Parse("{ \"variant\": \""+variant.Key+"\" }"))
                    };

                    jstackd.Resolve(api.World, ClassType + " type");
                    stacks.Add(jstackd);

                    foreach (var btype in variant.Value.types)
                    {
                        variant.Value.typesByCode[btype.Code] = btype;
                        btype.Variant = variant.Key;
                        btype.group = variant.Value;
                    }

                    variant.Value.types = null;

                    continue;
                }

                foreach (var btype in variant.Value.types)
                {
                    variant.Value.typesByCode[btype.Code] = btype;
                    btype.Variant = variant.Key;
                    btype.group = variant.Value;

                    var jstack = new JsonItemStack()
                    {
                        Code = this.Code,
                        Type = EnumItemClass.Block,
                        Attributes = new JsonObject(JToken.Parse("{ \"type\": \"" + btype.Code + "\", \"variant\": \""+btype.Variant+"\" }"))
                    };

                    jstack.Resolve(api.World, ClassType + " type");
                    stacks.Add(jstack);
                }

                variant.Value.types = null;
            }

            this.CreativeInventoryStacks = new CreativeTabAndStackList[]
            {
                new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = new string[]{ "general", "decorative" } }
            };
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            var bec = blockAccessor.GetBlockEntity(pos) as BlockEntityBookshelf;

            if (bec != null)
            {
                variantGroupsByCode.TryGetValue(bec.Variant, out var grp);
                if (grp?.DoubleSided == true)
                {
                    int angle = (int)(bec.MeshAngleRad * GameMath.RAD2DEG);
                    if (angle < 0) angle += 360;

                    if (angle == 0 || angle == 180)
                    {
                        return blockFace.IsAxisWE;
                    }
                    if (angle == 90 || angle == 270)
                    {
                        return blockFace.IsAxisNS;
                    }
                }
            }

            return base.CanAttachBlockAt(blockAccessor, block, pos, blockFace, attachmentArea);
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);

            var bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBookshelf;
            if (bec != null) {

                stack.Attributes.SetString("type", bec.Type);
                stack.Attributes.SetString("variant", bec.Variant);
            }

            return stack;
        }

        public override IShapeTypeProps GetTypeProps(string code, ItemStack stack, BlockEntityShapeFromAttributes be)
        {
            if (code == null) return null;

            string variant = stack == null ? (be as BlockEntityBookshelf).Variant : stack.Attributes.GetString("variant");
            if (variant == null) return null;

            if (variantGroupsByCode.TryGetValue(variant, out var vgroup))
            {
                if (vgroup.DoubleSided)
                {
                    string type1;
                    string type2;

                    if (be != null)
                    {
                        type1 = (be as BlockEntityBookshelf).Type;
                        type2 = (be as BlockEntityBookshelf).Type2;
                    } else
                    {
                        if (!stack.Attributes.HasAttribute("type1"))
                        {
                            stack.Attributes.SetString("type1", RandomType(variant));
                            stack.Attributes.SetString("type2", RandomType(variant));
                        }
                        type1 = stack.Attributes.GetString("type1");
                        type2 = stack.Attributes.GetString("type2");
                    }

                    var t1 = vgroup.typesByCode[type1];
                    var t2 = vgroup.typesByCode[type1];

                    return new BookShelfTypeProps()
                    {
                        group = vgroup,
                        Code = variant,
                        Type1 = type1,
                        Type2 = type2,
                        ShapeResolved = t1.ShapeResolved,
                        ShapeResolved2 = t2.ShapeResolved,
                        Variant = variant,
                        texPos = vgroup.texPos
                    };
                }

                vgroup.typesByCode.TryGetValue(code, out var bprops);
                return bprops;
            }

            return null;
        }

        public override MeshData GenMesh(IShapeTypeProps cprops)
        {
            var cMeshes = ObjectCacheUtil.GetOrCreate(api, ClassType + "Meshes", () => new Dictionary<string, MeshData>());
            ICoreClientAPI capi = api as ICoreClientAPI;

            var bprops = cprops as BookShelfTypeProps;
            
            if (cMeshes.TryGetValue(bprops.HashKey, out var mesh))
            {
                return mesh;
            }
            
            mesh = new MeshData(4, 3);
            var shape = cprops.ShapeResolved;
            var texSource = new ShapeTextureSource(capi, shape);

            if (shape == null) return mesh;

            capi.Tesselator.TesselateShape(ClassType + "block", shape, out mesh, texSource);

            if (bprops.Variant == "full" || bprops.group.DoubleSided)
            {
                mesh.Translate(0, 0, 0.5f);

                shape = bprops.Variant == "full" ? capi.Assets.TryGet(woodbackPanelShapePath)?.ToObject<Shape>() : bprops.ShapeResolved2;
                texSource = new ShapeTextureSource(capi, shape);
                capi.Tesselator.TesselateShape(ClassType + "block", shape, out var mesh2, texSource);

                mesh2.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, GameMath.PI, 0).Translate(0, 0, -0.5f);

                mesh.AddMeshData(mesh2);
            }

            if (cprops.texPos == null)
            {
                api.Logger.Warning("No texture previously loaded for bookshelf block " + cprops.Code);
                cprops.texPos = texSource.firstTexPos;
                cprops.texPos.RndColors = new int[TextureAtlasPosition.RndColorsLength];
            }
            if (bprops.group.texPos == null) bprops.group.texPos = cprops.texPos;

            cMeshes[bprops.HashKey] = mesh;
            
            return mesh;
        }

        public string RandomType(string variant)
        {
            var vgroup = variantGroupsByCode[variant];

            int rndindex = api.World.Rand.Next(vgroup.typesByCode.Count);
            return vgroup.typesByCode.GetKeyAtIndex(rndindex);
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes.GetString("type", "");
            string variant = itemStack.Attributes.GetString("variant", "");
            return Lang.GetMatching(Code.Domain + ":" + (type.Length == 0 ? "bookshelf-" + variant : type.Replace("/", "-")));
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityShapeFromAttributes bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;
            return Lang.GetMatching(Code.Domain + ":" + (bec?.Type?.Replace("/", "-") ?? "unknown"));
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockEntityShapeFromAttributes bec = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityShapeFromAttributes;
            return Lang.GetMatching(Code.Domain + ":" + (bec?.Type?.Replace("/", "-") ?? "unknown"));
        }
    }
}
