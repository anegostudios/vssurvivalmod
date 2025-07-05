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

    public bool IsAttachable(Entity toEntity, ItemStack itemStack)
    {
        return true;
    }

    public void CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
    {
    }

    public string GetCategoryCode(ItemStack stack)
    {
        return Attributes["attachableToEntity"]["categoryCode"].AsString();
    }

    public CompositeShape GetAttachedShape(ItemStack stack, string slotCode)
    {
        var material = stack.Attributes.GetString("material");
        var shape = Shape.Clone();
        shape.Base.Path = shape.Base.Path.Replace("{material}", material);
        return shape;
    }

    public string[] GetDisableElements(ItemStack stack)
    {
        return Array.Empty<string>();
    }

    public string[] GetKeepElements(ItemStack stack)
    {
        return Array.Empty<string>();
    }

    public string GetTexturePrefixCode(ItemStack stack)
    {
        return string.Empty;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        string wood = inSlot.Itemstack.Attributes.GetString("material", "oak");
        dsc.AppendLine(Lang.Get("Material: {0}", Lang.Get("material-" + wood)));
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        var material = itemStack.Attributes.GetString("material");
        return Lang.GetMatching($"block-{Code.Path}-{material}");
    }

    public void Rotate(EntityAgent byEntity, BlockSelection blockSel, int dir)
    {
        var bect = GetBlockEntity<BlockEntityGeneric>(blockSel.Position)?.GetBehavior<BEBehaviorMaterialFromAttributes>();
        bect?.Rotate(byEntity, blockSel, dir);
    }
}