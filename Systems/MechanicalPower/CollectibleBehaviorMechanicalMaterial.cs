using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    // Typed mechanical blocks get the same from BlockMPBase!
    public class CollectibleBehaviorMechanicalMaterial(CollectibleObject collObj) : CollectibleBehavior(collObj), IContainedMeshSource
    {
        readonly Dictionary<string, MultiTextureMeshRef> materialMeshes = [];
        ICoreAPI api = null!;
        Item item = null!;

        public MechanicalMaterialProperties MaterialProperties { get; private set; } = null!;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            this.api = api;
            item = collObj as Item ?? throw new InvalidOperationException($"{collObj.Code} is not an item, so it cannot use the MechanicalMaterial behavior.");
            MaterialProperties = MechanicalMaterialProperties.Load(api, item, item.Textures)
                                 ?? throw new InvalidOperationException($"{collObj.Code} uses the MechanicalMaterial behavior without materialVariants.");
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            foreach (MultiTextureMeshRef mesh in materialMeshes.Values) mesh.Dispose();
            materialMeshes.Clear();
            base.OnUnloaded(api);
        }

        public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack)
        {
            var material = MaterialProperties.GetMaterialValues(itemStack);
            if (material == null) return;

            object[] materialNames = material.ValuesOrdered.Select(value => (object)Lang.Get("material-" + value)).ToArray();
            string? name = Lang.GetMatchingIfExists($"{collObj.Code.Domain}:item-{collObj.Code.Path}-{MaterialProperties.GetMaterialKey(itemStack)}", materialNames);
            if (name == null) return;

            sb.Clear();
            sb.Append(name);
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            string? materialKey = MaterialProperties.GetMaterialKey(itemstack);
            if (materialKey == null) return;

            if (!materialMeshes.TryGetValue(materialKey, out MultiTextureMeshRef? mesh))
            {
                mesh = capi.Render.UploadMultiTextureMesh(GenMesh(new DummySlot(itemstack), capi.ItemTextureAtlas, null));
                materialMeshes[materialKey] = mesh;
            }
            renderinfo.ModelRef = mesh;
        }

        public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos? atBlockPos)
        {
            var capi = (ICoreClientAPI)api;
            Shape shape = capi.TesselatorManager.GetCachedShape(item.Shape.Base);
            capi.Tesselator.TesselateItem(item, out MeshData mesh, GetMaterialTextureSource(shape, slot.Itemstack, targetAtlas));
            return mesh;
        }

        public string GetMeshCacheKey(ItemSlot slot)
        {
            return collObj.Code.ToShortString() + "-" + MaterialProperties.GetMaterialKey(slot.Itemstack);
        }

        public ITexPositionSource GetMaterialTextureSource(Shape shape, ItemStack? stack, ITextureAtlasAPI targetAtlas)
        {
            Dictionary<string, CompositeTexture> textures = [];
            foreach (var entry in shape.Textures) textures[entry.Key] = new CompositeTexture { Base = entry.Value };
            foreach (var entry in MaterialProperties.GetMaterialTextures(stack) ?? item.Textures) textures[entry.Key] = entry.Value;

            FastSmallDictionary<string, int> mapping = new(textures.Count);
            foreach (var entry in textures)
            {
                if (!targetAtlas.GetOrInsertTexture(entry.Value.Clone(), out int textureId, out _))
                {
                    throw new InvalidOperationException($"Could not load material texture {entry.Value.Base} for {collObj.Code}.");
                }
                mapping[entry.Key] = textureId;
            }
            return new DictionaryTextureSource { Atlas = targetAtlas, Mapping = mapping };
        }
    }
}
