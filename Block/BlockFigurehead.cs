using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Vintagestory.GameContent;

public class BlockFigurehead : BlockMaterialFromAttributes, IAttachableToEntity, IWrenchOrientable
{
    public override string MeshKey => "Figurehead";

    public int RequiresBehindSlots { get; set; }

    public bool IsAttachable(Entity toEntity, ItemStack itemStack) => true;

    public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
    {
        var material = stack.Attributes.GetString("material", "oak");
        foreach (var key in shape.Textures.Keys)
        {
            var ctex = TexturesBMFA[key].Clone();
            ctex.Base.Path = ctex.Base.Path.Replace("{material}", material);
            ctex.Bake(api.Assets);
            intoDict[key] = ctex;
        }
    }

    public string GetCategoryCode(ItemStack stack) => Attributes["attachableToEntity"]["categoryCode"].AsString();
    public CompositeShape GetAttachedShape(ItemStack stack, string slotCode) => Shape;
    public string[] GetDisableElements(ItemStack stack) => [];
    public string[] GetKeepElements(ItemStack stack) => [];
    public string GetTexturePrefixCode(ItemStack stack) => string.Empty;

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        string wood = inSlot.Itemstack.Attributes.GetString("material", "oak");
        dsc.AppendLine(Lang.Get("Material: {0}", Lang.Get("material-" + wood)));
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        var material = itemStack.Attributes.GetString("material", "oak");
        return Lang.GetMatching($"block-{Code.Path}-{material}", Lang.Get("material-" + material));
    }

    public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
    {
        var bect = GetBlockEntity<BlockEntityGeneric>(blockSel.Position)?.GetBehavior<BEBehaviorMaterialFromAttributes>();
        bect?.Rotate(byEntity, blockSel, dir);
    }
}