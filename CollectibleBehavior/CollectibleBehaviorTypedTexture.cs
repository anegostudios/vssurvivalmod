using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent;

public class CollectibleBehaviorTypedTexture : CollectibleBehavior
{
    protected string[] materialTypes;
    protected string[] defaults;
    protected string[] translationEntries;
    public CompositeShape CompositeShape;
    public Dictionary<string, string> TextureMapping;


    public CollectibleBehaviorTypedTexture(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        materialTypes = collObj.Attributes?["materialTypes"].AsArray<string>();
        if (materialTypes == null || materialTypes.Length == 0)
        {
            throw new InvalidOperationException("materialTypes must be set and non-empty");
        }

        defaults = collObj.Attributes?["defaults"].AsArray<string>();
        if (defaults == null || defaults.Length != materialTypes.Length)
        {
            throw new InvalidOperationException("defaults must be set and the same length as materialTypes");
        }

        translationEntries = collObj.Attributes?["translationEntries"].AsArray<string>();

        if (translationEntries == null || translationEntries.Length != defaults.Length)
        {
            throw new InvalidOperationException("translationEntries must be set and the same length as materialTypes");
        }

        TextureMapping = collObj.Attributes?["textureMapping"].AsObject<Dictionary<string, string>>();
        var defaultcshape = collObj.ItemClass == EnumItemClass.Item ? (collObj as Item).Shape : (collObj as Block).Shape;
        CompositeShape = collObj.Attributes?["shape"].AsObject<CompositeShape>(defaultcshape);
    }

    protected string getDictKey(ItemStack stack)
    {
        var dictKey = "";
        if (materialTypes != null)
        {
            for (int i = 0; i < materialTypes.Length; i++)
            {
                dictKey += "-" + materialTypes[i] + "" + stack.Attributes.GetString(materialTypes[i]);
            }
        }

        return dictKey;
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
    {
        var meshes = ObjectCacheUtil.GetOrCreate<Dictionary<string, MultiTextureMeshRef>>(capi, "typedtextureMeshRefs" + collObj.Code, () => []);
        var dictKey = getDictKey(itemstack);

        if (!meshes.TryGetValue(dictKey, out renderinfo.ModelRef))
        {
            var shape = capi.TesselatorManager.GetCachedShape(CompositeShape.Base);
            Dictionary<string, string> materials = GetMaterials(itemstack);

            var typedTextureSource = new TypedTextureSource(capi, capi.BlockTextureAtlas, collObj.Code.Domain, TextureMapping, materials);
            capi.Tesselator.TesselateShape(new TesselationMetaData()
            {
                TexSource = typedTextureSource,
                TypeForLogging = "CollectibleBehaviorTypedTextureMesh",
                SelectiveElements = CompositeShape.SelectiveElements,
                IgnoreElements = CompositeShape.IgnoreElements
            }, shape, out var meshdata);

            renderinfo.ModelRef = meshes[dictKey] = capi.Render.UploadMultiTextureMesh(meshdata);
        }
    }

    public Dictionary<string, string> GetMaterials(ItemStack itemstack)
    {
        Dictionary<string, string> materials = new Dictionary<string, string>();
        for (int i = 0; i < materialTypes.Length; i++)
        {
            materials[materialTypes[i]] = itemstack.Attributes.GetString(materialTypes[i], defaults[i]);
        }

        return materials;
    }


}
