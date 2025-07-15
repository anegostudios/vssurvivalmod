using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable


namespace Vintagestory.GameContent
{

    public class CollectibleBehaviorBoatableGenericTypedContainer : CollectibleBehaviorHeldBag
    {
        public CollectibleBehaviorBoatableGenericTypedContainer(CollectibleObject collObj) : base(collObj)
        {
        }

        public override int GetQuantitySlots(ItemStack bagstack)
        {
            if (bagstack.Attributes?.HasAttribute("animalSerialized") == true) return 0;
            string type = bagstack.Attributes.GetString("type");
            if (type == null)
            {
                type = bagstack.Block.Attributes["defaultType"].AsString();
            }
            return bagstack.ItemAttributes?["quantitySlots"]?[type]?.AsInt(0) ?? 0;
        }
    }

    public class BlockGenericTypedContainerTrunk : BlockGenericTypedContainer, IMultiBlockColSelBoxes
    {
        Cuboidf[] mirroredColBox;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            mirroredColBox = new Cuboidf[] { CollisionBoxes[0].RotatedCopy(0, 180, 0, new Vec3d(0.5, 0.5, 0.5)) };
        }

        public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return mirroredColBox;
        }

        public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return mirroredColBox;
        }

        public override bool IsAttachable(Entity toEntity, ItemStack itemStack)
        {
            return false;
        }
    }

    public class GenericContainerTextureSource : ITexPositionSource
    {
        public Size2i AtlasSize { get { return blockTextureSource.AtlasSize; } }
        public ITexPositionSource blockTextureSource;
        public string curType;

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                return blockTextureSource[curType + "-" + textureCode];
            }
        }
    }

    public class BlockGenericTypedContainer : Block, IAttachableToEntity, IWearableShapeSupplier
    {
        string defaultType;
        string variantByGroup;
        string variantByGroupInventory;

        #region IAttachableToEntity
        public int RequiresBehindSlots { get; set; } = 0;
        Shape IWearableShapeSupplier.GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
        {
            string type = stack.Attributes.GetString("type", defaultType);
            string shapename = Attributes["shape"][type].AsString();
            var shape = GetShape(forEntity.World.Api, shapename);

            shape.SubclassForStepParenting(texturePrefixCode, 0);


            return shape;
        }

        public string GetCategoryCode(ItemStack stack)
        {
            string type = stack.Attributes?.GetString("type", defaultType);
            return Attributes["attachableCategoryCode"][type].AsString("chest");
        }
        public CompositeShape GetAttachedShape(ItemStack stack, string slotCode) => null;

        public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict) {

            string type = stack.Attributes.GetString("type", defaultType);
            foreach (var key in shape.Textures.Keys)
            {
                intoDict[texturePrefixCode + key] = this.Textures[type + "-" + key];
            }
        }

        public string[] GetDisableElements(ItemStack stack) => null;
        public string[] GetKeepElements(ItemStack stack) => null;

        public string GetTexturePrefixCode(ItemStack stack) => Code.ToShortString() + "-" + stack.Attributes.GetString("type", defaultType) + "-";
        #endregion

        public string Subtype
        {
            get
            {
                return variantByGroup == null ? "" : Variant[variantByGroup];
            }
        }

        public string SubtypeInventory
        {
            get
            {
                return variantByGroupInventory == null ? "" : Variant[variantByGroupInventory];
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            defaultType = Attributes["defaultType"].AsString("normal-generic");
            variantByGroup = Attributes["variantByGroup"].AsString(null);
            variantByGroupInventory = Attributes["variantByGroupInventory"].AsString(null);
        }

        
        public string GetType(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityGenericTypedContainer be = blockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                return be.type;
            }

            return defaultType;
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityGenericTypedContainer bect = blockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (bect?.collisionSelectionBoxes != null) return bect.collisionSelectionBoxes;

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntityGenericTypedContainer bect = blockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (bect?.collisionSelectionBoxes != null) return bect.collisionSelectionBoxes;

            return base.GetSelectionBoxes(blockAccessor, pos);
        }


        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val) {
                BlockEntityGenericTypedContainer bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGenericTypedContainer;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);


                    string type = bect.type;
                    string rotatatableInterval = Attributes?["rotatatableInterval"][type]?.AsString("22.5deg") ?? "22.5deg";

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
            Dictionary<string, MultiTextureMeshRef> meshrefs = new Dictionary<string, MultiTextureMeshRef>();
            string key = "genericTypedContainerMeshRefs" + FirstCodePart() + SubtypeInventory;

            
            meshrefs = ObjectCacheUtil.GetOrCreate(capi, key, () =>
            {
                Dictionary<string, MeshData> meshes = GenGuiMeshes(capi);

                foreach (var val in meshes)
                {
                    meshrefs[val.Key] = capi.Render.UploadMultiTextureMesh(val.Value);
                }

                return meshrefs;
            });
            

            string type = itemstack.Attributes.GetString("type", defaultType);

            if (!meshrefs.TryGetValue(type, out renderinfo.ModelRef))
            {
                MeshData mesh = GenGuiMesh(capi, type);
                meshrefs[type] = renderinfo.ModelRef = capi.Render.UploadMultiTextureMesh(mesh);
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            string key = "genericTypedContainerMeshRefs" + FirstCodePart() + SubtypeInventory;
            Dictionary<string, MultiTextureMeshRef> meshrefs = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, key);
            
            if (meshrefs != null) {
                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(key);
            }
        }


        MeshData GenGuiMesh(ICoreClientAPI capi, string type)
        {
            string shapename = this.Attributes["shape"][type].AsString();
            return GenMesh(capi, type, shapename);
        }

        public Dictionary<string, MeshData> GenGuiMeshes(ICoreClientAPI capi)
        {
            string[] types = this.Attributes["types"].AsArray<string>();            

            Dictionary<string, MeshData> meshes = new Dictionary<string, MeshData>();

            foreach (string type in types)
            {
                string shapename = Attributes["shape"][type].AsString();
                meshes[type] = GenMesh(capi, type, shapename, null, ShapeInventory == null ? null : new Vec3f(ShapeInventory.rotateX, ShapeInventory.rotateY, ShapeInventory.rotateZ));
            }

            return meshes;
        }


        public Shape GetShape(ICoreAPI capi, string shapename)
        {
            if (shapename == null) return null;
            
            AssetLocation shapeloc = AssetLocation.Create(shapename, Code.Domain).WithPathPrefixOnce("shapes/");
            Shape shape = API.Common.Shape.TryGet(capi, shapeloc + ".json");
            if (shape == null)
            {
                shape = API.Common.Shape.TryGet(capi, shapeloc + "1.json");
            }

            return shape;
        }


        public MeshData GenMesh(ICoreClientAPI capi, string type, string shapename, ITesselatorAPI tesselator = null, Vec3f rotation = null, int altTexNumber = 0)
        {
            Shape shape = GetShape(capi, shapename);
            if (tesselator == null) tesselator = capi.Tesselator;
            if (shape == null)
            {
                capi.Logger.Warning("Container block {0}, type: {1}: Shape file {2} not found!", Code, type, shapename);
                return new MeshData();
            }


            var texSource = new GenericContainerTextureSource()
            {
                blockTextureSource = tesselator.GetTextureSource(this, altTexNumber),
                curType = type
            };

            TesselationMetaData meta = new TesselationMetaData()
            {
                TexSource = texSource,
                WithJointIds = true,
                WithDamageEffect = true,
                TypeForLogging = "typedcontainer",
                Rotation = rotation == null ? new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ) : rotation
            };

            tesselator.TesselateShape(meta, shape, out MeshData mesh);
            return mesh;
        }
        
        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                ICoreClientAPI capi = api as ICoreClientAPI;
                string shapename = Attributes["shape"][be.type].AsString();
                if (shapename == null)
                {
                    base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
                    return;
                }

                blockModelData = GenMesh(capi, be.type, shapename);

                AssetLocation shapeloc = AssetLocation.Create(shapename, Code.Domain).WithPathPrefixOnce("shapes/");
                Shape shape = API.Common.Shape.TryGet(capi, shapeloc + ".json");
                if (shape == null)
                {
                    shape = API.Common.Shape.TryGet(capi, shapeloc + "1.json");
                }

                var texSource = new GenericContainerTextureSource
                {
                    blockTextureSource = decalTexSource,
                    curType = be.type
                };
                capi.Tesselator.TesselateShape("typedcontainer-decal", shape, out MeshData md, texSource);
                decalModelData = md;

                decalModelData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, be.MeshAngle, 0);

                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            ItemStack stack = new ItemStack(world.GetBlock(CodeWithVariant("side", "east")));

            BlockEntityGenericTypedContainer be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                stack.Attributes.SetString("type", be.type);
            }
            else
            {
                stack.Attributes.SetString("type", defaultType);
            }

            return stack;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            bool preventDefault = false;
            foreach (BlockBehavior behavior in BlockBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnBlockBroken(world, pos, byPlayer, ref handled);
                if (handled == EnumHandling.PreventDefault) preventDefault = true;
                if (handled == EnumHandling.PreventSubsequent) return;
            }

            if (preventDefault) return;


            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { OnPickBlock(world, pos) };

                if (this.Attributes["drop"]?[GetType(world.BlockAccessor, pos)]?.AsBool() == true && drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        world.SpawnItemEntity(drops[i], pos, null);
                    }
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos, -0.5, byPlayer);
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken(byPlayer);
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }




        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            string type = handbookStack.Attributes?.GetString("type");

            if (type == null)
            {
                api.World.Logger.Warning("BlockGenericTypedContainer.GetDropsForHandbook(): type not set for block " + handbookStack.Collectible?.Code);
                return Array.Empty<BlockDropItemStack>();
            }

            if (Attributes?["drop"]?[type]?.AsBool() == false)
            {
                return Array.Empty<BlockDropItemStack>(); 
            } else
            {
                return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
            }
        }

        

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            return new ItemStack[] { new ItemStack(world.GetBlock(CodeWithVariant("side", "east"))) };
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            string type = itemStack.Attributes.GetString("type", defaultType);
            return Lang.GetMatching(Code?.Domain + AssetLocation.LocationSeparator + "block-" + type + "-" + Code?.Path);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string type = inSlot.Itemstack.Attributes.GetString("type");

            if (type != null)
            {
                int? qslots = inSlot.Itemstack.ItemAttributes?["quantitySlots"]?[type]?.AsInt(0);
                dsc.AppendLine("\n" + Lang.Get("Storage Slots: {0}", qslots));
            }
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BlockEntityGenericTypedContainer be = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGenericTypedContainer;
            if (be != null)
            {
                if (!Textures.TryGetValue(be.type + "-lid", out CompositeTexture tex))
                {
                    Textures.TryGetValue(be.type + "-top", out tex);
                }
                return capi.BlockTextureAtlas.GetRandomColor(tex?.Baked == null ? 0 : tex.Baked.TextureSubId, rndIndex);
            }

            return base.GetRandomColor(capi, pos, facing, rndIndex);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-chest-open",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

        public virtual bool IsAttachable(Entity toEntity, ItemStack itemStack)
        {
            if (toEntity is EntityPlayer) return false;
            if (itemStack.Attributes?.HasAttribute("animalSerialized") == true) return false;
            return true;
        }
    }
}
