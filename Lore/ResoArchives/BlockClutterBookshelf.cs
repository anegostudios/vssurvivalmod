using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{

    public class BlockClutterBookshelf : BlockShapeFromAttributes
    {
        string classtype;
        public override string ClassType => classtype;

        public API.Datastructures.OrderedDictionary<string, BookShelfVariantGroup> variantGroupsByCode = new ();
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
            variantGroupsByCode = Attributes["variantGroups"].AsObject<API.Datastructures.OrderedDictionary<string, BookShelfVariantGroup>>();
            basePath = Attributes["shapeBasePath"].AsString();
            classtype = Attributes["classtype"].AsString("bookshelf");

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
                new CreativeTabAndStackList() { Stacks = stacks.ToArray(), Tabs = new string[]{ "general", "clutter" } }
            };
        }





        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            var bec = GetBEBehavior<BEBehaviorClutterBookshelf>(pos);

            if (bec?.Variant != null)
            {
                variantGroupsByCode.TryGetValue(bec.Variant, out var grp);
                if (grp?.DoubleSided == true)
                {
                    int angle = (int)(bec.rotateY * GameMath.RAD2DEG);
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

            var bec = GetBEBehavior<BEBehaviorClutterBookshelf>(pos);
            if (bec != null) {

                stack.Attributes.SetString("type", bec.Type);
                stack.Attributes.SetString("variant", bec.Variant);
            }

            return stack;
        }

        public override IShapeTypeProps GetTypeProps(string code, ItemStack stack, BEBehaviorShapeFromAttributes be)
        {
            if (code == null) return null;

            string variant = stack == null ? (be as BEBehaviorClutterBookshelf)?.Variant : stack.Attributes.GetString("variant");
            if (variant == null) return null;

            if (variantGroupsByCode.TryGetValue(variant, out var vgroup))
            {
                if (vgroup.DoubleSided)
                {
                    string type1;
                    string type2;

                    if (be != null)
                    {
                        type1 = (be as BEBehaviorClutterBookshelf).Type;
                        type2 = (be as BEBehaviorClutterBookshelf).Type2;
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

                    if (!vgroup.typesByCode.TryGetValue(type1, out var t1)) t1 = vgroup.typesByCode.First((ele)=>true).Value;
                    if (!vgroup.typesByCode.TryGetValue(type2, out var t2)) t2 = t1;

                    return new BookShelfTypeProps()
                    {
                        group = vgroup,
                        Code = variant + "-" + type1 + "-" + type2,
                        Type1 = type1,
                        Type2 = type2,
                        ShapeResolved = t1.ShapeResolved,
                        ShapeResolved2 = t2.ShapeResolved,
                        Variant = variant,
                        TexPos = vgroup.texPos
                    };
                }

                vgroup.typesByCode.TryGetValue(code, out var bprops);
                return bprops;
            }

            return null;
        }

        public override MeshData GetOrCreateMesh(IShapeTypeProps cprops, ITexPositionSource overrideTexturesource = null, string overrideTextureCode = null, bool forLOD2 = false)
        {
            var cMeshes = forLOD2 ? meshDictionaryLOD2 : meshDictionary;
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (cprops.ShapeLOD2Resolved == null) forLOD2 = false;

            var bprops = cprops as BookShelfTypeProps;

            if (overrideTexturesource == null && cMeshes.TryGetValue(bprops.HashKey, out var mesh))
            {
                return mesh;
            }

            mesh = new MeshData(4, 3);
            Shape shape = forLOD2 ? cprops.ShapeLOD2Resolved?.Clone() : cprops.ShapeResolved;
            if (shape == null) return mesh;

            var texSource = overrideTexturesource;
            ShapeTextureSource stexSource=null;
            if (forLOD2 || texSource == null)
            {
                // Prio 0: Shape textures
                string shapePath = cprops.ShapePath.ToString();
                stexSource = new ShapeTextureSource(capi, shape, forLOD2 ? Lod2Shape.Base.Path : shapePath);
                texSource = stexSource;

                // Prio 1: Block wide custom textures
                if (blockTextures != null && !forLOD2)
                {
                    foreach (var val in blockTextures)
                    {
                        if (val.Value.Baked == null) val.Value.Bake(capi.Assets);
                        stexSource.textures[val.Key] = val.Value;
                    }
                }

                // Prio 2: LOD2
                if (forLOD2)
                {
                    AssetLocation sideTex = null;
                    SetLOD2Texture(capi, stexSource, shape, "front", new AssetLocation("game", "block/lod2/clutter/" + cprops.Code), null);

                    ShapeElementFace face = FindElement(cprops.ShapeResolved.Elements, "e")?.FacesResolved?[1];
                    string inshapeTextureName = face?.Texture;
                    if (inshapeTextureName == null || !cprops.ShapeResolved.Textures.TryGetValue(inshapeTextureName, out sideTex))
                    {
                        // In various bookshelf models these are the texture names for the case sides, not sure why it's called "bookshelf"/"shelf" etc
                        if (cprops.ShapeResolved.Textures.TryGetValue("shelf-ruined", out sideTex) || cprops.ShapeResolved.Textures.TryGetValue("bookshelf", out sideTex))
                        { /* intentionally blank*/ }
                    }
                    SetLOD2Texture(capi, stexSource, shape, "bookshelf", sideTex, face);

                    face = FindElement(cprops.ShapeResolved.Elements, "n")?.FacesResolved?[0];
                    inshapeTextureName = face?.Texture;
                    if (inshapeTextureName == null || !cprops.ShapeResolved.Textures.TryGetValue(inshapeTextureName, out sideTex))
                    {
                        // In various bookshelf models these are the texture names for the case back, not sure why it's called "sides" etc
                        if (cprops.ShapeResolved.Textures.TryGetValue("sides-ruined", out sideTex) || cprops.ShapeResolved.Textures.TryGetValue("sides", out sideTex))
                        { /* intentionally blank*/ }
                    }
                    SetLOD2Texture(capi, stexSource, shape, "back", sideTex, face);
                }
            }

            capi.Tesselator.TesselateShape(blockForLogging, shape, out mesh, texSource);

            if (bprops.Variant == "full" || bprops.group.DoubleSided)
            {
                mesh.Translate(0, 0, 0.5f);

                shape = bprops.Variant == "full" ? capi.Assets.TryGet(woodbackPanelShapePath)?.ToObject<Shape>() : bprops.ShapeResolved2;
                texSource = new ShapeTextureSource(capi, shape, (bprops.Variant == "full" ? woodbackPanelShapePath : bprops.ShapePath2).ToString());
                // Prio 1: Block wide custom textures
                if (blockTextures != null && stexSource != null)
                {
                    foreach (var val in blockTextures)
                    {
                        if (val.Value.Baked == null) val.Value.Bake(capi.Assets);
                        stexSource.textures[val.Key] = val.Value;
                    }
                }

                capi.Tesselator.TesselateShape(blockForLogging, shape, out var mesh2, texSource);

                mesh2.Rotate(0, GameMath.PI, 0).Translate(0, 0, -0.5f);
                mesh.AddMeshData(mesh2);
            }

            if (cprops.TexPos == null)
            {
                cprops.TexPos = (texSource as ShapeTextureSource)?.firstTexPos;
                cprops.TexPos.RndColors = new int[TextureAtlasPosition.RndColorsLength];
            }
            if (bprops.group.texPos == null) bprops.group.texPos = cprops.TexPos;

            if (overrideTexturesource == null)
            {
                cMeshes[bprops.HashKey] = mesh;
            }

            return mesh;
        }

        private void SetLOD2Texture(ICoreClientAPI capi, ShapeTextureSource stexSource, Shape shape, string texName, AssetLocation sideTex, ShapeElementFace face)
        {
            if (sideTex != null)
            {
                // In our models these are the texture names for the case sides, not sure why it's called "bookshelf"
                var texture = new CompositeTexture(sideTex);
                texture.Bake(capi.Assets);
                stexSource.textures[texName] = texture;

                if (face != null && shape != null)
                {
                    var faces = FindElement(shape.Elements, "bottom")?.FacesResolved;
                    if (faces != null)
                    {
                        foreach (var targetFace in faces)
                        {
                            if (targetFace.Texture == texName)
                            {
                                targetFace.Uv = face.Uv;
                            }
                        }
                    }
                }
            }
        }

        private ShapeElement FindElement(ShapeElement[] baseElements, string name)
        {
            if (baseElements == null || baseElements.Length == 0) return null;
            ShapeElement baseElement = baseElements[0];
            if (baseElement.Name == name) return baseElement;
            if (baseElement.Children == null) return null;
            foreach (var childElement in baseElement.Children)
            {
                if (childElement == null) continue;
                if (childElement.Name == name) return childElement;
                if (childElement.Children == null) continue;
                foreach (var grandchild in childElement.Children)
                {
                    if (grandchild.Name == name) return grandchild;
                }
            }
            return null;
        }

        public string RandomType(string variant)
        {
            if (variantGroupsByCode == null) return null;
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

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            var bec = GetBEBehavior<BEBehaviorClutterBookshelf>(pos);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Lang.GetMatching(Code.Domain + ":" + (bec?.Type?.Replace("/", "-") ?? "unknown")));
            bec?.GetBlockInfo(forPlayer, sb);
            sb.AppendLine();
            foreach (BlockBehavior bh in BlockBehaviors)
            {
                sb.Append(bh.GetPlacedBlockInfo(world, pos, forPlayer));
            }
            return sb.ToString();
        }

        //Suppress "bookshelf-" at start of localized name key; it will therefore normally start with "bookshelves-"
        public override string BaseCodeForName()
        {
            return Code.Domain + ":";
        }
    }
}
