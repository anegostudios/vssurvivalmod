using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;


namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorWearableBitAttachedShape : CollectibleBehavior, IWearableBitAttachedShape
    {
        public CollectibleBehaviorWearableBitAttachedShape(CollectibleObject collObj) : base(collObj)
        {
        }



        protected ICoreAPI? api;



        public virtual void AddGearToShape(ITextureAtlasAPI targetAtlas, Shape entityShape, WearableBitEntry bit, string shapePathForLogging, ref string[]? willDeleteElements, IDictionary<string, CompositeTexture> collectedTextures, string additionalPrefix = "", float carrierDamageEffect = 0)
        {
            ItemStack stack = bit.Stack;

            ILayeredAttachableToEntity? attachableInterface = IAttachableToEntity.FromCollectible(stack.Collectible) as ILayeredAttachableToEntity;

            if (attachableInterface == null || api is not ICoreClientAPI clientApi) return;

            shapePathForLogging = $"{shapePathForLogging} (when attaching: {stack.Collectible.Code})";

            processDeleteElements(out string[] delteElements, out string[] keepElements, attachableInterface, bit);

            willDeleteElements = willDeleteElements?.Append(delteElements).Except(keepElements).ToArray();

            //entityShape.RemoveElements(delteElements, shapePathForLogging, api); // why remove here is they will be removed using willDeleteElements later?

            float bitDamageEffect = 0;
            if (stack.ItemAttributes?["visibleDamageEffect"].AsBool() == true)
            {
                bitDamageEffect = Math.Max(0, 1 - (float)stack.Collectible.GetRemainingDurability(stack) / stack.Collectible.GetMaxDurability(stack) * 1.1f);
            }
            float damageEffect = Math.Clamp(carrierDamageEffect + bitDamageEffect, 0, 1);

            string texturePrefixCode = attachableInterface.GetTexturePrefixCode(stack) ?? "";

            texturePrefixCode = $"{additionalPrefix}{texturePrefixCode}-{bit.SlotCode}-{bit.LayerCode}{Shape.RemoveElementPrefixDelimeter}";

            Shape? gearShape = null;
            AssetLocation? shapePath = null;
            CompositeShape? compGearShape = null;

            IWearableShapeSupplier? wearableShapeSupplier = stack.Collectible.GetCollectibleInterface<IWearableShapeSupplier>();
            if (wearableShapeSupplier != null)
            {
                gearShape = wearableShapeSupplier.GetShape(stack, clientApi.World.Player.Entity, texturePrefixCode);
            }

            if (gearShape == null)
            {
                compGearShape = attachableInterface.GetAttachedShape(stack, clientApi.World.Player.Entity, bit.SlotCode, bit.LayerCode);

                if (compGearShape == null) return;

                shapePath = compGearShape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");

                gearShape = Shape.TryGet(clientApi, shapePath);
                if (gearShape == null)
                {
                    clientApi.World.Logger.Warning($"Entity attachable shape {compGearShape.Base} defined in {stack.Class} {stack.Collectible.Code} not found or errored, was supposed to be at {shapePath}. Shape will be invisible.");
                    return;
                }

                gearShape.SubclassForStepParenting(texturePrefixCode, damageEffect);
                gearShape.ResolveReferences(clientApi.World.Logger, shapePath);
            }

            Dictionary<string, CompositeTexture> texturesToCollect = [];
            attachableInterface.CollectTextures(stack, gearShape, texturePrefixCode, texturesToCollect);

            IDictionary<string, CompositeTexture>? attachedTextures = attachableInterface.GetAttachedTextures(stack, clientApi.World.Player.Entity, bit.SlotCode, bit.LayerCode);
            if (attachedTextures != null)
            {
                IItemTextureAtlasAPI itemAtlas = clientApi.ItemTextureAtlas;
                foreach ((_, CompositeTexture texture) in attachedTextures)
                {
                    if (texture.Baked != null) continue;
                    itemAtlas.GetOrInsertTexture(texture, out int textureSubid, out _);
                    if (texture.Baked != null)
                    {
                        texture.Baked.TextureSubId = textureSubid;
                    }
                }

                foreach ((string textureCode, CompositeTexture? compositeTexutre) in attachedTextures)
                {
                    texturesToCollect[texturePrefixCode + textureCode] = compositeTexutre;
                }
            }


            entityShape.StepParentShape(
                gearShape,
                (compGearShape?.Base.ToString() ?? "Custom texture from ItemWearableShapeSupplier") + $" defined in {stack.Class} {stack.Collectible.Code}",
                shapePathForLogging,
                clientApi.World.Logger,
                (textureCode, textureLocation) => addTexture(clientApi, textureCode, textureLocation, collectedTextures, texturePrefixCode, targetAtlas)
            );


            if (compGearShape?.Overlays != null)
            {
                foreach (CompositeShape overlay in compGearShape.Overlays)
                {
                    Shape overlayShape = Shape.TryGet(clientApi, overlay.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json"));
                    if (overlayShape == null)
                    {
                        clientApi.World.Logger.Warning("Entity attachable shape {0} overlay {4} defined in {1} {2} not found or errored, was supposed to be at {3}. Shape will be invisible.", compGearShape.Base, stack.Class, stack.Collectible.Code, shapePath, overlay.Base);
                        continue;
                    }

                    overlayShape.SubclassForStepParenting(texturePrefixCode, damageEffect);

                    attachableInterface.CollectTextures(stack, overlayShape, texturePrefixCode, texturesToCollect);

                    entityShape.StepParentShape(overlayShape, overlay.Base.ToShortString(), shapePathForLogging, clientApi.Logger, (texcode, tloc) => addTexture(clientApi, texcode, tloc, collectedTextures, texturePrefixCode, targetAtlas));
                }
            }

            foreach ((string code, CompositeTexture texture) in texturesToCollect)
            {
                CompositeTexture textureCopy = texture.Clone();
                collectedTextures[code] = textureCopy;
                targetAtlas.GetOrInsertTexture(textureCopy, out int textureSubid, out _);
                textureCopy.Baked.TextureSubId = textureSubid;
            }
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
        }


        protected virtual void addTexture(ICoreClientAPI? capi, string textureCode, AssetLocation textureLocation, IDictionary<string, CompositeTexture> textures, string texturePrefixCode, ITextureAtlasAPI targetAtlas)
        {
            if (capi == null) return;

            CompositeTexture newTexture = new(textureLocation);
            if (textures != null)
            {
                textures[texturePrefixCode + textureCode] = newTexture;
            }
            newTexture.Bake(capi.Assets);
            targetAtlas.GetOrInsertTexture(newTexture.Baked.TextureFilenames[0], out int textureSubid, out _);
            newTexture.Baked.TextureSubId = textureSubid;
        }

        protected virtual string resolveCodes(string value, string layer, string slot)
        {
            if (value.Contains("{slotcode}"))
            {
                value = value.Replace("{slotcode}", slot);
            }

            if (value.Contains("{layercode}"))
            {
                value = value.Replace("{layercode}", layer);
            }

            return value;
        }

        protected virtual string[] resolveCodes(string[] values, string layer, string slot) => values.Select(value => resolveCodes(value, layer, slot)).ToArray();

        protected virtual void processDeleteElements(out string[] delteElements, out string[] keepElements, IAttachableToEntity attachableInterface, WearableBitEntry bit)
        {
            ItemStack stack = bit.Stack;
            string slotCode = bit.SlotCode;
            string layerCode = bit.LayerCode;

            delteElements = [];
            keepElements = [];

            string[]? disableElements = attachableInterface.GetDisableElements(stack);
            if (disableElements != null)
            {
                disableElements = resolveCodes(disableElements, layerCode, slotCode);
                delteElements = delteElements.Append(disableElements);
            }

            string[]? keepElementsFromInterface = attachableInterface.GetKeepElements(stack);
            if (keepElementsFromInterface != null)
            {
                keepElementsFromInterface = resolveCodes(keepElementsFromInterface, layerCode, slotCode);
                keepElements = keepElements.Append(keepElementsFromInterface);
            }

            string[]? disabledElementsForSlot = stack.ItemAttributes?["attachableToEntity"]?["disableElementsBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsArray<string>()?.OfType<string>().ToArray();
            string[]? keepElementsForSlot = stack.ItemAttributes?["attachableToEntity"]?["keepElementsBySlotCode"]?.FirstByWildcardKey(slotCode)?.AsArray<string>()?.OfType<string>().ToArray();
            string[]? disabledElementsForLayer = stack.ItemAttributes?["attachableToEntity"]?["disableElementsByLayerCode"]?.FirstByWildcardKey(layerCode)?.AsArray<string>()?.OfType<string>().ToArray();
            string[]? keepElementsForLayer = stack.ItemAttributes?["attachableToEntity"]?["keepElementsByLayerCode"]?.FirstByWildcardKey(layerCode)?.AsArray<string>()?.OfType<string>().ToArray();
            string[]? disabledElementsForLayerAndSlot = stack.ItemAttributes?["attachableToEntity"]?["disableElementsByLayerAndSlot"]?.FirstByWildcardKey(layerCode)?.FirstByWildcardKey(slotCode)?.AsArray<string>()?.OfType<string>().ToArray();
            string[]? keepElementsForLayerAndSlot = stack.ItemAttributes?["attachableToEntity"]?["keepElementsByLayerAndSlot"]?.FirstByWildcardKey(layerCode)?.FirstByWildcardKey(slotCode)?.AsArray<string>()?.OfType<string>().ToArray();

            if (disabledElementsForSlot != null)
            {
                disabledElementsForSlot = resolveCodes(disabledElementsForSlot, layerCode, slotCode);
                delteElements = delteElements.Append(disabledElementsForSlot);
            }
            if (keepElementsForSlot != null)
            {
                keepElementsForSlot = resolveCodes(keepElementsForSlot, layerCode, slotCode);
                keepElements = keepElements.Append(keepElementsForSlot);
            }

            if (disabledElementsForLayer != null)
            {
                disabledElementsForLayer = resolveCodes(disabledElementsForLayer, layerCode, slotCode);
                delteElements = delteElements.Append(disabledElementsForLayer);
            }
            if (keepElementsForLayer != null)
            {
                keepElementsForLayer = resolveCodes(keepElementsForLayer, layerCode, slotCode);
                keepElements = keepElements.Append(keepElementsForLayer);
            }

            if (disabledElementsForLayerAndSlot != null)
            {
                disabledElementsForLayerAndSlot = resolveCodes(disabledElementsForLayerAndSlot, layerCode, slotCode);
                delteElements = delteElements.Append(disabledElementsForLayerAndSlot);
            }
            if (keepElementsForLayerAndSlot != null)
            {
                keepElementsForLayerAndSlot = resolveCodes(keepElementsForLayerAndSlot, layerCode, slotCode);
                keepElements = keepElements.Append(keepElementsForLayerAndSlot);
            }
        }
    }
}
