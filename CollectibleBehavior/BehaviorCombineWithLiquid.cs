using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace DanaTweaks;

public class CombineWithLiquidProps
{
    public JsonItemStack outputStack { get; set; }
    public BarrelRecipeIngredient requiredLiquidStack { get; set; }

    public bool Resolve(IWorldAccessor world)
    {
        if (outputStack == null || requiredLiquidStack == null)
        {
            return false;
        }

        return outputStack.Resolve(world, "CombineWithLiquid outputStack")
            && requiredLiquidStack.Resolve(world, "CombineWithLiquid requiredLiquidStack");
    }
}

public class CollectibleBehaviorCombineWithLiquid : CollectibleBehavior
{
    CombineWithLiquidProps props;
    WorldInteraction[] interactions;
    string actionLangCode;

    public CollectibleBehaviorCombineWithLiquid(CollectibleObject collObj) : base(collObj) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        actionLangCode = properties["actionLangCode"].AsString("heldhelp-combinewithliquid");
    }

    public override void OnLoaded(ICoreAPI api)
    {
        if (collObj.Attributes.Exists)
        {
            props = collObj.Attributes["combineWithLiquidProps"].AsObject<CombineWithLiquidProps>();
            props?.Resolve(api.World);
        }

        if (api is not ICoreClientAPI capi)
        {
            return;
        }

        interactions = ObjectCacheUtil.GetOrCreate(api, "combineWithLiquidInteractions", () =>
        {
            ItemStack[] containerStacks = Array.Empty<ItemStack>();

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj.Code == null || obj.GetCollectibleInterface<ILiquidInterface>() == null)
                {
                    continue;
                }

                List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                if (stacks != null)
                {
                    containerStacks = containerStacks.Append(stacks);
                }
            }

            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = actionLangCode,
                    HotKeyCode = "shift",
                    Itemstacks = containerStacks
                }
            };
        });
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        OnInteract(slot, byEntity, blockSel, ref handHandling, ref handling);
    }

    private void OnInteract(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (blockSel == null || props == null || props.outputStack?.ResolvedItemstack == null || props.requiredLiquidStack?.ResolvedItemstack == null)
        {
            return;
        }

        BlockPos pos = blockSel.Position;
        Block block = byEntity.World.BlockAccessor.GetBlock(pos);

        if (block == null)
        {
            return;
        }

        if (block.GetInterface<ILiquidInterface>(byEntity.World, pos) is ILiquidSource liquidSource1)
        {
            ItemStack liquidStack = liquidSource1.GetContent(pos);
            if (liquidStack == null)
            {
                return;
            }

            WaterTightContainableProps liquidProps = liquidSource1.GetContentProps(pos);

            if (!liquidStack.Equals(byEntity.World, props.requiredLiquidStack.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes))
            {
                return;
            }

            int takeLitres = (int)(liquidProps.ItemsPerLitre * props.requiredLiquidStack.Litres);
            if (liquidSource1.GetCurrentLitres(pos) < props.requiredLiquidStack.Litres)
            {
                return;
            }

            if (liquidSource1.TryTakeContent(pos, takeLitres) == null)
            {
                return;
            }

            if (byEntity.World.BlockAccessor.GetBlockEntity(pos) is BlockEntity be)
            {
                be.MarkDirty(true);
            }

            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);

            byEntity.World.PlaySoundAt(liquidProps.PourSound, byEntity, byPlayer, range: 10);
            slot.TakeOut(1);

            if (!byEntity.TryGiveItemStack(props.outputStack.ResolvedItemstack.Clone()))
            {
                byEntity.World.SpawnItemEntity(props.outputStack.ResolvedItemstack.Clone(), pos);
            }

            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
        }
        else if (byEntity.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityGroundStorage begs)
        {
            ItemSlot gsslot = begs.GetSlotAt(blockSel);
            if (gsslot?.Itemstack.Collectible.GetCollectibleInterface<ILiquidSource>() is not ILiquidSource liquidSource2)
            {
                return;
            }

            ItemStack liquidContainer = gsslot.Itemstack;
            if (liquidContainer == null)
            {
                return;
            }

            ItemStack liquidStack = liquidSource2.GetContent(liquidContainer);
            if (liquidStack == null)
            {
                return;
            }

            WaterTightContainableProps liquidProps = liquidSource2.GetContentProps(liquidContainer);

            if (!liquidStack.Equals(byEntity.World, props.requiredLiquidStack.ResolvedItemstack, GlobalConstants.IgnoredStackAttributes))
            {
                return;
            }

            int takeLitres = (int)(liquidProps.ItemsPerLitre * props.requiredLiquidStack.Litres);
            if (liquidSource2.GetCurrentLitres(liquidContainer) < props.requiredLiquidStack.Litres)
            {
                return;
            }

            if (liquidSource2.TryTakeContent(liquidContainer, takeLitres) == null)
            {
                return;
            }

            if (byEntity.World.BlockAccessor.GetBlockEntity(pos) is BlockEntity be)
            {
                be.MarkDirty(true);
            }

            IPlayer byPlayer = byEntity.World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);

            byEntity.World.PlaySoundAt(liquidProps.PourSound, byEntity, byPlayer, range: 10);
            slot.TakeOut(1);

            if (!byEntity.TryGiveItemStack(props.outputStack.ResolvedItemstack.Clone()))
            {
                byEntity.World.SpawnItemEntity(props.outputStack.ResolvedItemstack.Clone(), pos);
            }

            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        handling = EnumHandling.PassThrough;
        return interactions;
    }
}