using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class CrateTypeProperties
    {
        public int QuantitySlots;
        public CompositeShape Shape;
        public string RotatatableInterval;
        public EnumItemStorageFlags StorageType;
        public bool RetrieveOnly;
    }

    public class LabelProps
    {
        public string Texture;
        public CompositeShape Shape;
        public CompositeShape EditableShape;
    }

    public class CrateProperties
    {
        public Dictionary<string, CrateTypeProperties> Properties;
        public string[] Types;
        public Dictionary<string, LabelProps> Labels; 
        public string DefaultType = "wood-aged";
        public string VariantByGroup;
        public string VariantByGroupInventory;
        public string InventoryClassName = "crate";

        public CrateTypeProperties this[string type]
        {
            get
            {
                if (!Properties.TryGetValue(type, out var props))
                {
                    return Properties["*"];
                }

                return props;
            }
        }
    }

    public class ItemStackRenderCacheItem
    {
        public int TextureSubId;
        public HashSet<int> UsedCounter;
    }

    public class BlockCrate : BlockContainer, ITexPositionSource
    {
        public Size2i AtlasSize { get { return tmpTextureSource.AtlasSize; } }

        string curType;
        LabelProps nowTeselatingLabel;
        ITexPositionSource tmpTextureSource;

        TextureAtlasPosition labelTexturePos;
        
        public CrateProperties Props;

        public string Subtype => Props.VariantByGroup == null ? "" : Variant[Props.VariantByGroup];
        public string SubtypeInventory => Props?.VariantByGroupInventory == null ? "" : Variant[Props.VariantByGroupInventory];


        public Dictionary<int, ItemStackRenderCacheItem> itemStackRenders;
        private Vec3f origin = new Vec3f(0.5f, 0.5f, 0.5f);

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (nowTeselatingLabel != null) return labelTexturePos;

                TextureAtlasPosition pos = tmpTextureSource[curType + "-" + textureCode];
                if (pos == null) pos = tmpTextureSource[textureCode];
                if (pos == null)
                {
                    pos = (api as ICoreClientAPI).BlockTextureAtlas.UnknownTexturePosition;
                }
                return pos;
            }
        }


        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityCrate be = blockAccessor.GetBlockEntity(pos) as BlockEntityCrate;
            if (be != null) return be.GetSelectionBoxes();
            

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        Cuboidf[] closedCollBoxes = new Cuboidf[] { new Cuboidf(0.0625f, 0, 0.0625f, 0.9375f, 0.9375f, 0.9375f) };
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityCrate be = blockAccessor.GetBlockEntity(pos) as BlockEntityCrate;
            if (be != null && be.LidState == "closed") {
                return closedCollBoxes;
            }

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client)
            {
                itemStackRenders = ObjectCacheUtil.GetOrCreate(api as ICoreClientAPI, "itemStackBlockAtlasRenders", () => new Dictionary<int, ItemStackRenderCacheItem>());
            }

            Props = Attributes.AsObject<CrateProperties>(null, Code.Domain);

            PlacedPriorityInteract = true;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BlockEntityCrate bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCrate;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);


                    string type = bect.type;
                    string rotatatableInterval = Props[type].RotatatableInterval;

                    if (rotatatableInterval == "22.5degnot45deg")
                    {
                        float rounded90degRad = ((int)Math.Round(angleHor / GameMath.PIHALF)) * GameMath.PIHALF;
                        float deg45rad = GameMath.PIHALF / 4;


                        if (Math.Abs(angleHor - rounded90degRad) >= deg45rad)
                        {
                            bect.MeshAngle = rounded90degRad + 22.5f * GameMath.DEG2RAD * Math.Sign(angleHor - rounded90degRad);
                        }
                        else
                        {
                            bect.MeshAngle = rounded90degRad;
                        }
                    }
                    if (rotatatableInterval == "22.5deg")
                    {
                        float deg22dot5rad = GameMath.PIHALF / 4;
                        float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                        bect.MeshAngle = roundRad;
                    }
                }
            }

            return val;
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            string cacheKey = "crateMeshRefs" + FirstCodePart() + SubtypeInventory;
            var meshrefs = ObjectCacheUtil.GetOrCreate(capi, cacheKey, () => new Dictionary<string, MultiTextureMeshRef>());

            string type = itemstack.Attributes.GetString("type", Props.DefaultType);
            string label = itemstack.Attributes.GetString("label");
            string lidState = itemstack.Attributes.GetString("lidState", "closed");

            string key = type + "-" + label + "-" + lidState;

            if (!meshrefs.TryGetValue(key, out renderinfo.ModelRef))
            {
                CompositeShape cshape = Props[type].Shape;
                var rot = ShapeInventory == null ? null : new Vec3f(ShapeInventory.rotateX, ShapeInventory.rotateY, ShapeInventory.rotateZ);

                var contentStacks = GetNonEmptyContents(capi.World, itemstack);
                var contentStack = contentStacks == null || contentStacks.Length == 0 ? null : contentStacks[0];

                var mesh = GenMesh(capi, contentStack, type, label, lidState, cshape, rot);
                meshrefs[key] = renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(mesh);
            }
        }




        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            string key = "crateMeshRefs" + FirstCodePart() + SubtypeInventory;
            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, key);

            if (meshrefs != null)
            {
                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(key);
            }
        }



        public Shape GetShape(ICoreClientAPI capi, string type, CompositeShape cshape)
        {
            if (cshape?.Base == null) return null;
            var tesselator = capi.Tesselator;

            tmpTextureSource = tesselator.GetTextureSource(this, 0, true);

            AssetLocation shapeloc = cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape shape = API.Common.Shape.TryGet(capi, shapeloc);
            curType = type;
            return shape;
        }


        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, string type, string label, string lidState, CompositeShape cshape, Vec3f rotation = null)
        {
            if (lidState == "opened")
            {
                cshape = cshape.Clone();
                cshape.Base.Path = cshape.Base.Path.Replace("closed", "opened");
            }

            Shape shape = GetShape(capi, type, cshape);
            var tesselator = capi.Tesselator;
            if (shape == null) return new MeshData();

            curType = type;
            MeshData mesh;
            tesselator.TesselateShape("crate", shape, out mesh, this, rotation == null ? new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ) : rotation);

            if (label != null && Props.Labels.TryGetValue(label, out var labelProps))
            {
                var meshLabel = GenLabelMesh(capi, label, tmpTextureSource[labelProps.Texture], false, rotation);
                mesh.AddMeshData(meshLabel);
            }

            if (contentStack != null && lidState != "closed")
            {
                var contentMesh = genContentMesh(capi, contentStack, rotation);
                if (contentMesh != null) mesh.AddMeshData(contentMesh);
            }


            return mesh;
        }

        public MeshData GenLabelMesh(ICoreClientAPI capi, string label, TextureAtlasPosition texPos, bool editableVariant, Vec3f rotation = null)
        {
            Props.Labels.TryGetValue(label, out var labelProps);
            if (Props == null) throw new ArgumentException("No label props found for this label");

            AssetLocation shapeloc = (editableVariant ? labelProps.EditableShape : labelProps.Shape).Base.Clone().WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
            Shape shape = API.Common.Shape.TryGet(capi, shapeloc);

            var rot = rotation == null ? new Vec3f(labelProps.Shape.rotateX, labelProps.Shape.rotateY, labelProps.Shape.rotateZ) : rotation;

            nowTeselatingLabel = labelProps;
            labelTexturePos = texPos;
            tmpTextureSource = capi.Tesselator.GetTextureSource(this, 0, true);

            capi.Tesselator.TesselateShape("cratelabel", shape, out var meshLabel, this, rot);
            nowTeselatingLabel = null;

            return meshLabel;
        }


        protected MeshData genContentMesh(ICoreClientAPI capi, ItemStack contentStack, Vec3f rotation = null)
        {
            float fillHeight;

            var contentSource = BlockBarrel.getContentTexture(capi, contentStack, out fillHeight);

            if (contentSource != null)
            {
                Shape shape = API.Common.Shape.TryGet(api, "shapes/block/wood/crate/contents.json");
                MeshData contentMesh;
                capi.Tesselator.TesselateShape("cratecontents", shape, out contentMesh, contentSource, rotation);
                contentMesh.Translate(0, fillHeight * 1.1f, 0);
                return contentMesh;
            }

            return null;
        }

        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BlockEntityCrate be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrate;
            if (be != null)
            {

                decalModelData.Rotate(origin, 0, be.MeshAngle, 0);
                decalModelData.Scale(origin, 15f / 16, 1f, 15f / 16);
                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }




        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = new ItemStack(this);

            BlockEntityCrate be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCrate;
            if (be != null)
            {
                stack.Attributes.SetString("type", be.type);
                if (be.label != null && be.label.Length > 0)
                {
                    stack.Attributes.SetString("label", be.label);
                }
                stack.Attributes.SetString("lidState", be.preferredLidState);
            }
            else
            {
                stack.Attributes.SetString("type", Props.DefaultType);
            }

            return stack;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { OnPickBlock(world, pos) };
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityCrate be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCrate;
            if (be != null) return be.OnBlockInteractStart(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes.GetString("type", Props.DefaultType);
            string lidState = itemStack.Attributes.GetString("lidState", "closed");
            if (lidState.Length == 0) lidState = "closed";

            return Lang.GetMatching(Code?.Domain + AssetLocation.LocationSeparator + "block-" + type + "-" + Code?.Path, Lang.Get("cratelidstate-" + lidState, "closed"));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string type = inSlot.Itemstack.Attributes.GetString("type", Props.DefaultType);

            if (type != null)
            {
                int qslots = Props[type].QuantitySlots;
                dsc.AppendLine("\n" + Lang.Get("Storage Slots: {0}", qslots));
            }
        }


        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BlockEntityGenericTypedContainer be = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                CompositeTexture tex = null;
                if (!Textures.TryGetValue(be.type + "-lid", out tex))
                {
                    Textures.TryGetValue(be.type + "-top", out tex);
                }
                return capi.BlockTextureAtlas.GetRandomColor(tex?.Baked == null ? 0 : tex.Baked.TextureSubId, rndIndex);
            }

            return base.GetRandomColor(capi, pos, facing, rndIndex);


        }

        public string GetType(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityGenericTypedContainer be = blockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                return be.type;
            }

            return Props.DefaultType;
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer).Append(new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-crate-add",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "shift"
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-crate-addall",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCodes = new string[] { "shift", "ctrl" }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-crate-remove",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = null
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-crate-removeall",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "ctrl"
                }
            });
        }

    }

}
