using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class PlantContainerProps {
        public CompositeShape Shape;
        public Dictionary<string, CompositeTexture> Textures;
        public ModelTransform Transform;
        public bool RandomRotate = true;
    }

    public class BlockEntityPlantContainer : BlockEntityContainer, ITexPositionSource
    {
        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "pottedplant";

        public virtual float MeshAngle { get; set; }
        public string ContainerSize => Block.Attributes?["plantContainerSize"].AsString();

        MeshData potMesh;
        MeshData contentMesh;

        bool hasSoil => !inv[0].Empty;

        public BlockEntityPlantContainer()
        {
            inv = new InventoryGeneric(1, null, null, null);
            inv.OnAcquireTransitionSpeed = slotTransitionSpeed;
        }

        private float slotTransitionSpeed(EnumTransitionType transType, ItemStack stack, float mulByConfig)
        {
            return 0;
        }

        protected override void OnTick(float dt)
        {
            // Don't tick inventory contents
        }

        PlantContainerProps PlantContProps => GetProps(inv[0].Itemstack);

        ICoreClientAPI capi;
        ITexPositionSource contentTexSource;
        PlantContainerProps curContProps;
        Dictionary<string, AssetLocation> shapeTextures;

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                AssetLocation textureLoc = null;
                if (curContProps.Textures != null)
                {
                    CompositeTexture compTex;
                    if (curContProps.Textures.TryGetValue(textureCode, out compTex))
                    {
                        textureLoc = compTex.Base;
                    }
                }

                if (textureLoc == null && shapeTextures != null)
                {
                    shapeTextures.TryGetValue(textureCode, out textureLoc);

                }

                if (textureLoc != null)
                {
                    TextureAtlasPosition texPos = capi.BlockTextureAtlas[textureLoc];
                    if (texPos == null)
                    {
                        BitmapRef bmp = capi.Assets.TryGet(textureLoc.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);
                        if (bmp != null)
                        {
                            capi.BlockTextureAtlas.InsertTextureCached(textureLoc, bmp, out _, out texPos);
                            bmp.Dispose();
                        }
                    }

                    return texPos;
                }

                ItemStack content = GetContents();
                if (content.Class == EnumItemClass.Item)
                {
                    TextureAtlasPosition texPos;
                    textureLoc = content.Item.Textures[textureCode].Base;
                    BitmapRef bmp = capi.Assets.TryGet(textureLoc.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);
                    if (bmp != null)
                    {
                        capi.BlockTextureAtlas.InsertTextureCached(textureLoc, bmp, out _, out texPos);
                        bmp.Dispose();
                        return texPos;
                    }
                }

                return contentTexSource[textureCode];
            }
        }

        public ItemStack GetContents()
        {
            return inv[0].Itemstack;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            capi = api as ICoreClientAPI;

            if (api.Side == EnumAppSide.Client && potMesh == null)
            {
                genMeshes();
                MarkDirty(true);
            }
        }

        public bool TryPutContents(ItemSlot fromSlot, IPlayer player)
        {
            if (!inv[0].Empty || fromSlot.Empty) return false;
            ItemStack stack = fromSlot.Itemstack;
            if (GetProps(stack) == null) return false;
            
            if (fromSlot.TryPutInto(Api.World, inv[0], 1) > 0)
            {
                if (Api.Side == EnumAppSide.Server)
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/plant"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
                }

                (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                fromSlot.MarkDirty();
                MarkDirty(true);
                return true;
            }

            return false;
        }

        public bool TrySetContents(ItemStack stack)
        {
            if (GetProps(stack) == null) return false;

            inv[0].Itemstack = stack;
            MarkDirty(true);
            return true;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            MeshAngle = tree.GetFloat("meshAngle", MeshAngle);

            if (capi != null)
            {
                genMeshes();
                MarkDirty(true);
            }
        }

        private void genMeshes()
        {
            if (Block.Code == null) return;

            potMesh = GenPotMesh(capi.Tesselator);
            if (potMesh != null)
            {
                potMesh = potMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0);
            }

            MeshData[] meshes = GenContentMeshes(capi.Tesselator);
            if (meshes != null && meshes.Length > 0)
            {
                contentMesh = meshes[GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, meshes.Length)];

                if (PlantContProps.RandomRotate)
                {
                    float radY = GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, 16) * 22.5f * GameMath.DEG2RAD;
                    contentMesh = contentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, radY, 0);
                } else
                {
                    contentMesh = contentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, MeshAngle, 0);
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("meshAngle", MeshAngle);
        }

        private MeshData GenPotMesh(ITesselatorAPI tesselator)
        {
            Dictionary<string, MeshData> meshes = ObjectCacheUtil.GetOrCreate(Api, "plantContainerMeshes", () =>
            {
                return new Dictionary<string, MeshData>();
            });


            MeshData mesh;
            string key = Block.Code.ToString() + (hasSoil ? "soil" : "empty");

            if (meshes.TryGetValue(key, out mesh))
            {
                return mesh;
            }

            if (hasSoil && Block.Attributes != null)
            {
                CompositeShape compshape = Block.Attributes["filledShape"].AsObject<CompositeShape>(null, Block.Code.Domain);
                Shape shape = null; 
                if (compshape != null)
                {
                    shape = Shape.TryGet(Api, compshape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                }

                if (shape != null)
                {
                    tesselator.TesselateShape(Block, shape, out mesh);
                } else
                {
                    Api.World.Logger.Error("Plant container, asset {0} not found,", compshape?.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                    return mesh;
                }
            } else
            {
                mesh = capi.TesselatorManager.GetDefaultBlockMesh(Block);
            }

            return meshes[key] = mesh;
        }

        private MeshData[] GenContentMeshes(ITesselatorAPI tesselator) 
        {
            ItemStack content = GetContents();
            if (content == null) return null;

            Dictionary<string, MeshData[]> meshes = ObjectCacheUtil.GetOrCreate(Api, "plantContainerContentMeshes", () =>
            {
                return new Dictionary<string, MeshData[]>();
            });

            float fillHeight = Block.Attributes["fillHeight"].AsFloat(0.4f);

            MeshData[] meshwithVariants;
            string containersize = this.ContainerSize;
            string key = content?.ToString() + "-" + containersize + "f" + fillHeight;

            if (meshes.TryGetValue(key, out meshwithVariants))
            {
                return meshwithVariants;
            }

            curContProps = PlantContProps;
            if (curContProps == null) return null;
            
            CompositeShape compoShape = curContProps.Shape;
            if (compoShape == null)
            {
                compoShape = content.Class == EnumItemClass.Block ? content.Block.Shape : content.Item.Shape;
            }
            ModelTransform transform = curContProps.Transform;
            if (transform == null)
            {
                transform = new ModelTransform().EnsureDefaultValues();
                transform.Translation.Y = fillHeight;
            }

            contentTexSource = content.Class == EnumItemClass.Block ? capi.Tesselator.GetTexSource(content.Block) : capi.Tesselator.GetTextureSource(content.Item);
            List<IAsset> assets;
            if (compoShape.Base.Path.EndsWith("*"))
            {
                assets = Api.Assets.GetManyInCategory("shapes", compoShape.Base.Path.Substring(0, compoShape.Base.Path.Length - 1), compoShape.Base.Domain);
            }
            else
            {
                assets = new List<IAsset>();
                assets.Add(Api.Assets.TryGet(compoShape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")));
            }

            if (assets != null && assets.Count > 0)
            {
                ShapeElement.locationForLogging = compoShape.Base;
                meshwithVariants = new MeshData[assets.Count];

                for (int i = 0; i < assets.Count; i++)
                {
                    IAsset asset = assets[i];
                    Shape shape = asset.ToObject<Shape>();
                    shapeTextures = shape.Textures;
                    MeshData mesh;

                    try
                    {
                        byte climateColorMapId = content.Block?.ClimateColorMapResolved == null ? (byte)0 : (byte)(content.Block.ClimateColorMapResolved.RectIndex + 1);
                        byte seasonColorMapId = content.Block?.SeasonColorMapResolved == null ? (byte)0 : (byte)(content.Block.SeasonColorMapResolved.RectIndex + 1);

                        tesselator.TesselateShape("plant container content shape", shape, out mesh, this, null, 0, climateColorMapId, seasonColorMapId);
                    }
                    catch (Exception e)
                    { 
                        Api.Logger.Error(e.Message + " (when tesselating " + compoShape.Base.WithPathPrefixOnce("shapes/") + ")"); meshwithVariants = null; break; 
                    }

                    mesh.ModelTransform(transform);
                    meshwithVariants[i] = mesh;
                }
            }
            else
            {
                Api.World.Logger.Error("Plant container, content asset {0} not found,", compoShape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
            }

            return meshes[key] = meshwithVariants;
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (potMesh == null) return false;

            mesher.AddMeshData(potMesh);
            
            if (contentMesh != null)
            {
                mesher.AddMeshData(contentMesh);
            }

            return true;
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            ItemStack contents = GetContents();
            if (contents != null)
            {
                dsc.Append(Lang.Get("Planted: {0}", contents.GetName()));
            }
        }

        public PlantContainerProps GetProps(ItemStack stack)
        {
            if (stack == null) return null;
            return stack.Collectible.Attributes?["plantContainable"]?[ContainerSize + "Container"]?.AsObject<PlantContainerProps>(null, stack.Collectible.Code.Domain);
        }

    }
}
