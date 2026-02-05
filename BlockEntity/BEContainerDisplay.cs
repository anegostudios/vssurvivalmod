using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface IContainedInteractable
    {
        WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel);
        bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel);
        bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel);
        void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel);
    }

    public interface IContainedCustomName
    {
        string GetContainedInfo(ItemSlot inSlot);

        /// <summary>
        /// Return null to use default title
        /// </summary>
        /// <param name="inSlot"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        string GetContainedName(ItemSlot inSlot, int quantity);
    }


    public interface IContainedMeshSource
    {
        MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos);

        /// <summary>
        /// Needs to return a unique identifier for this itemstack
        /// </summary>
        /// <param name="itemstack"></param>
        /// <returns></returns>
        string GetMeshCacheKey(ItemSlot slot);
    }

    public abstract class BlockEntityDisplay : BlockEntityContainer, ITexPositionSource
    {
        protected CollectibleObject nowTesselatingObj;
        protected Shape nowTesselatingShape;
        protected ICoreClientAPI capi;
        
        /// <summary>
        /// Return a unique code for this type of block. Used as part of the cache key. E.g. for the display case the class code is "displaycase", for the shelf its "openshelf"
        /// </summary>
        public virtual string ClassCode => InventoryClassName;

        public virtual int DisplayedItems => Inventory.Count;

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public virtual string AttributeTransformCode => "onDisplayTransform";

        public virtual TextureAtlasPosition this[string textureCode]
        {
            get
            {
                IDictionary<string, CompositeTexture> textures = nowTesselatingObj is Item item ? item.Textures : (nowTesselatingObj as Block).Textures;
                AssetLocation texturePath = null;

                // Prio 1: Get from collectible textures
                if (textures.TryGetValue(textureCode, out CompositeTexture tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 2: Get from collectible textures, use "all" code
                if (texturePath == null && textures.TryGetValue("all", out tex))
                {
                    texturePath = tex.Baked.BakedName;
                }

                // Prio 3: Get from currently tesselating shape
                if (texturePath == null)
                {
                    nowTesselatingShape?.Textures.TryGetValue(textureCode, out texturePath);
                }

                // Prio 4: The code is the path
                if (texturePath == null)
                {
                    texturePath = new AssetLocation(textureCode);
                }

                return getOrCreateTexPos(texturePath);
            }
        }

        /// <summary>
        /// Slot type for contained item meshing
        /// </summary>
        protected virtual string slotType => "default";

        protected TextureAtlasPosition getOrCreateTexPos(AssetLocation texturePath)
        {
            TextureAtlasPosition texpos = capi.BlockTextureAtlas[texturePath];

            if (texpos == null)
            {
                bool ok = capi.BlockTextureAtlas.GetOrInsertTexture(texturePath, out _, out texpos, null);

                if (!ok)
                {
                    capi.World.Logger.Warning("For render in block " + Block.Code + ", item {0} defined texture {1}, no such texture found.", nowTesselatingObj.Code, texturePath);
                    return capi.BlockTextureAtlas.UnknownTexturePosition;
                }
            }

            return texpos;
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            
            capi = api as ICoreClientAPI;
            if (capi != null)
            {
                MarkMeshesDirty();
                api.Event.RegisterEventBusListener(OnEventBusEvent);
            }
        }
        

        private void OnEventBusEvent(string eventname, ref EnumHandling handling, IAttribute data)
        {
            if (eventname != "genjsontransform" && eventname != "oncloseedittransforms" &&
                eventname != "onapplytransforms") return;
            if (capi == null || Inventory.Empty) return;

            // This is only used for doing .tfedit on nearby stuff, it creates a lot of lag if we remesh the entire loaded chunk area
            var PlayerPos = capi.World.Player.Entity.Pos;
            if (Pos.DistanceTo(PlayerPos.X, PlayerPos.Y, PlayerPos.Z) > 20) return;

            int DisplayedItems = this.DisplayedItems;
            for (var i = 0; i < DisplayedItems; i++)
            {
                ItemStack stack = Inventory[i].Itemstack;
                if (stack == null) continue;
                var key = getMeshCacheKey(Inventory[i]);
                MeshCache.Remove(key);
            }

            MarkMeshesDirty();
            Api.World.BlockAccessor.MarkBlockDirty(Pos);   // always redraw on client after updating meshes
        }


        /// <summary>
        /// Methods implementing this class need to call this at the conclusion of their FromTreeAttributes implementation.  See BEGroundStorage for an example!
        /// </summary>
        protected virtual void RedrawAfterReceivingTreeAttributes(IWorldAccessor worldForResolving)
        {
            if (worldForResolving.Side == EnumAppSide.Client && Api != null)
            {
                MarkMeshesDirty();
                Api.World.BlockAccessor.MarkBlockDirty(Pos);   // always redraw on client after updating meshes
            }
        }

        /// <summary>
        /// Low cost call; will update the meshes client-side on next tesselation of this chunk.  Pair with a MarkDirty() or MarkBlockDirty() to ensure the chunk does get retesselated
        /// <br/>No effect server-side
        /// </summary>
        public virtual void MarkMeshesDirty()
        {
            tfMatrices = null;
        }

        public virtual void updateMeshes()
        {
            if (Api == null || Api.Side == EnumAppSide.Server) return;
            int DisplayedItems = this.DisplayedItems;
            if (DisplayedItems == 0) return;

            for (int i = 0; i < DisplayedItems; i++)
            {
                updateMesh(i);
            }

            tfMatrices = genTransformationMatrices();
        }

        protected virtual void updateMesh(int index)
        {
            if (Api == null || Api.Side == EnumAppSide.Server) return;
            var stack = Inventory[index].Itemstack;
            if (stack == null || stack.Collectible?.Code == null)
            {
                return;
            }

            getOrCreateMesh(Inventory[index], index);
        }


        protected virtual string getMeshCacheKey(ItemSlot slot)
        {
            IContainedMeshSource meshSource = slot.Itemstack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();
            if (meshSource != null)
            {
                return meshSource.GetMeshCacheKey(slot);
            }

            return slot.Itemstack.Collectible.Code.ToString();
        }

        protected Dictionary<string, MeshData> MeshCache => ObjectCacheUtil.GetOrCreate(Api, "meshesDisplay-" + ClassCode, () => new Dictionary<string, MeshData>());

        protected MeshData getMesh(ItemSlot slot)
        {
            string key = getMeshCacheKey(slot);
            MeshCache.TryGetValue(key, out var meshdata);
            return meshdata;
        }

        protected virtual MeshData getOrCreateMesh(ItemSlot slot, int index)
        {
            MeshData mesh = getMesh(slot);
            if (mesh != null) return mesh;

            var stack = slot.Itemstack;
            CompositeShape customShape = stack.Collectible.Attributes?["displayedShape"].AsObject<CompositeShape>(null, stack.Collectible.Code.Domain);
            if (customShape != null)
            {
                string customkey = "displayedShape-" + customShape.ToString();
                mesh = ObjectCacheUtil.GetOrCreate(capi, customkey, () =>
                    capi.TesselatorManager.CreateMesh(
                        "displayed item shape",
                        customShape,
                        (shape, name) => new ContainedTextureSource(capi, capi.BlockTextureAtlas, shape.Textures, string.Format("For displayed item {0}", stack.Collectible.Code)),
                        null
                ));
            }
            else
            {
                IContainedMeshSource meshSource = stack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();

                if (meshSource != null)
                {
                    mesh = meshSource.GenMesh(slot, capi.BlockTextureAtlas, Pos);
                }
            }

            if (mesh == null)
            {
                mesh = getDefaultMesh(stack);
            }

            applyDefaultTranforms(stack, mesh);

            string key = getMeshCacheKey(slot);
            MeshCache[key] = mesh;

            return mesh;
        }

        protected void applyDefaultTranforms(ItemStack stack, MeshData mesh)
        {
            ModelTransform transform = stack.Collectible.Attributes?[AttributeTransformCode].AsObject<ModelTransform>();
            if (AttributeTransformCode == "onshelfTransform") // special logic because shelves a little more complicated
            {
                transform = stack.Collectible.GetCollectibleInterface<IShelvable>()?.GetOnShelfTransform(stack) ?? transform;
                transform ??= stack.Collectible.Attributes?["onDisplayTransform"].AsObject<ModelTransform>();
            }
            if (transform != null)
            {
                transform.EnsureDefaultValues();
                mesh.ModelTransform(transform);
            }

            if (stack.Class == EnumItemClass.Item && (stack.Item.Shape == null || stack.Item.Shape.VoxelizeTexture))
            {
                mesh.Rotate(GameMath.PIHALF, 0, 0);
                mesh.Scale(0.33f, 0.33f, 0.33f);
                mesh.Translate(0, -7.5f / 16f, 0f);
            }
        }

        protected MeshData getDefaultMesh(ItemStack stack)
        {
            MeshData mesh;
            ICoreClientAPI capi = Api as ICoreClientAPI;
            if (stack.Class == EnumItemClass.Block)
            {
                mesh = capi.TesselatorManager.GetDefaultBlockMesh(stack.Block).Clone();
            }
            else
            {
                nowTesselatingObj = stack.Collectible;
                nowTesselatingShape = null;
                if (stack.Item.Shape?.Base != null)
                {
                    nowTesselatingShape = capi.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
                }
                capi.Tesselator.TesselateItem(stack.Item, out mesh, this);

                mesh.RenderPassesAndExtraBits.Fill((short)EnumChunkRenderPass.BlendNoCull);
            }

            return mesh;
        }

        protected float[][] tfMatrices;
        protected abstract float[][] genTransformationMatrices();


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (tfMatrices == null)
            {
                updateMeshes();
            }

            for (int index = 0; index < DisplayedItems; index++)
            {
                ItemSlot slot = Inventory[index];
                if (slot.Empty || tfMatrices == null || slot.Itemstack.Collectible?.Code == null)
                {
                    continue;
                }
                mesher.AddMeshData(getMesh(slot), tfMatrices[index]);
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

    }

}
