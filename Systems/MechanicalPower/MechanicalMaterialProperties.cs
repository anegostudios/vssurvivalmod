using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent.Mechanics
{
    public class MechanicalMaterialProperties
    {
        readonly CollectibleObject collectible;
        readonly IDictionary<string, CompositeTexture> textures;
        readonly MechanicalMaterialVariants materialVariants;
        readonly Dictionary<string, string[]> materialStates = [];
        readonly Dictionary<string, string> materialTextureGroups = [];
        readonly Dictionary<string, MechanicalMaterialPalette> materialPalettes = [];

        public IEnumerable<string> MaterialTextureCodes => materialTextureGroups.Keys;

        public static MechanicalMaterialProperties? Load(ICoreAPI api, CollectibleObject collectible, IDictionary<string, CompositeTexture> textures)
        {
            var variants = collectible.Attributes?["materialVariants"].AsObject<MechanicalMaterialVariants>(null, collectible.Code.Domain);
            return variants == null ? null : new MechanicalMaterialProperties(api, collectible, textures, variants);
        }

        MechanicalMaterialProperties(ICoreAPI api, CollectibleObject collectible, IDictionary<string, CompositeTexture> textures, MechanicalMaterialVariants materialVariants)
        {
            this.collectible = collectible;
            this.textures = textures;
            this.materialVariants = materialVariants;
            RegistryObjectVariantGroup[] groups = materialVariants.VariantGroups;
            if (groups.Length == 0) throw new InvalidOperationException($"Mechanical collectible {collectible.Code} declares materialVariants without any variantGroups");

            foreach (RegistryObjectVariantGroup group in groups)
            {
                string[] states = loadMaterialStates(api, group);
                if (states.Length == 0) throw new InvalidOperationException($"Mechanical collectible {collectible.Code} declares material group {group.Code} without any states");
                materialStates[group.Code] = states;
            }

            foreach ((string textureCode, CompositeTexture texture) in materialVariants.Textures)
            {
                if (!textures.ContainsKey(textureCode)) throw new InvalidOperationException($"Mechanical collectible {collectible.Code} gives a material texture for undefined texture {textureCode}");

                string[] textureGroups = groups.Select(group => group.Code).Where(group => texture.Base.Path.Contains($"{{{group}}}")).ToArray();
                if (textureGroups.Length != 1) throw new InvalidOperationException($"Mechanical collectible {collectible.Code} material texture {textureCode} must use exactly one material placeholder");
                materialTextureGroups[textureCode] = textureGroups[0];
            }
        }

        public string? GetMaterialKey(ItemStack? stack)
        {
            if (stack == null) return null;
            if (materialVariants.VariantGroups.Length == 1)
            {
                string? value = stack.Attributes.GetString(materialVariants.VariantGroups[0].Code);
                return string.IsNullOrEmpty(value) ? null : value;
            }

            var material = GetMaterialValues(stack);
            return material == null ? null : string.Join("-", material.Values);
        }

        public Dictionary<string, CompositeTexture>? GetMaterialTextures(ItemStack? stack)
        {
            var material = GetMaterialValues(stack);
            if (material == null) return null;

            Dictionary<string, CompositeTexture> resolved = [];
            foreach ((string textureCode, CompositeTexture texture) in textures)
            {
                resolved[textureCode] = materialTextureGroups.TryGetValue(textureCode, out string? group) ? resolveMaterialTexture(textureCode, material[group]) : texture;
            }
            return resolved;
        }

        // Keeps the faces drawn with one material texture, or with none of them when textureCode is null.
        public Shape GetMaterialRenderShape(Shape shape, string? textureCode)
        {
            Shape renderShape = shape.Clone();
            foreach (ShapeElement root in renderShape.Elements) keepMaterialFaces(root, textureCode);
            return renderShape;
        }

        void keepMaterialFaces(ShapeElement element, string? textureCode)
        {
            for (int i = 0; i < element.FacesResolved.Length; i++)
            {
                string? faceTexture = element.FacesResolved[i]?.Texture;
                if (faceTexture == null) continue;
                if (textureCode == null ? materialTextureGroups.ContainsKey(faceTexture) : faceTexture != textureCode) element.FacesResolved[i] = null;
            }

            if (element.Children == null) return;
            foreach (ShapeElement child in element.Children) keepMaterialFaces(child, textureCode);
        }

        public ITexPositionSource GetMaterialRenderTextureSource(ICoreClientAPI capi, ITexPositionSource baseSource, string textureCode)
        {
            return new MechanicalMaterialTextureSource(baseSource, textureCode, getMaterialPalette(capi, textureCode).ReferencePosition);
        }

        public void GetMaterialUvOffset(ICoreClientAPI capi, ItemStack? stack, string textureCode, Vec2f offset)
        {
            string? value = materialTextureGroups.TryGetValue(textureCode, out string? group) && hasMaterialValues(stack) ? stack!.Attributes.GetString(group) : null;
            GetMaterialUvOffset(capi, textureCode, value, offset);
        }

        public void GetMaterialUvOffset(ICoreClientAPI capi, string textureCode, string? materialValue, Vec2f offset)
        {
            offset.X = 0;
            offset.Y = 0;
            if (materialValue == null || !materialTextureGroups.ContainsKey(textureCode)) return;

            // Each region resolves only the material group it declares.
            MechanicalMaterialPalette palette = getMaterialPalette(capi, textureCode);
            if (!palette.Positions.TryGetValue(materialValue, out TextureAtlasPosition? pos)) return;

            offset.X = pos.x1 - palette.ReferencePosition.x1;
            offset.Y = pos.y1 - palette.ReferencePosition.y1;
        }

        MechanicalMaterialPalette getMaterialPalette(ICoreClientAPI capi, string textureCode)
        {
            if (materialPalettes.TryGetValue(textureCode, out MechanicalMaterialPalette? palette)) return palette;

            List<KeyValuePair<string, CompositeTexture>> textures = [];
            StringBuilder signature = new(getTextureSignature(this.textures[textureCode]));
            foreach (string state in materialStates[materialTextureGroups[textureCode]])
            {
                CompositeTexture texture = resolveMaterialTexture(textureCode, state);
                textures.Add(new(state, texture));
                signature.Append($"|{state}={getTextureSignature(texture)}");
            }

            palette = capi.ModLoader.GetModSystem<MechanicalPowerMod>().GetOrCreateMaterialPalette(signature.ToString(), this.textures[textureCode], textures);
            materialPalettes[textureCode] = palette;
            return palette;
        }

        string[] loadMaterialStates(ICoreAPI api, RegistryObjectVariantGroup group)
        {
            List<string> states = [];
            if (group.LoadFromProperties != null)
            {
                AssetLocation propertyLoc = group.LoadFromProperties.Clone().WithPathPrefixOnce("worldproperties/").WithPathAppendixOnce(".json");
                StandardWorldProperty property = api.Assets.TryGet(propertyLoc)?.ToObject<StandardWorldProperty>()
                                                 ?? throw new InvalidOperationException($"Mechanical collectible {collectible.Code} could not load material states from {propertyLoc}");
                states.AddRange(property.Variants.Select(variant => variant.Code.Path));
            }

            return group.States != null ? states.Union(group.States).ToArray() : states.ToArray();
        }

        static string getTextureSignature(CompositeTexture texture)
        {
            return JsonConvert.SerializeObject(texture.Clone());
        }

        CompositeTexture resolveMaterialTexture(string textureCode, string value)
        {
            CompositeTexture texture = materialVariants.Textures[textureCode].Clone();
            texture.Base.Path = RegistryObject.FillPlaceHolder(texture.Base.Path, materialTextureGroups[textureCode], value);
            return texture;
        }

        bool hasMaterialValues(ItemStack? stack)
        {
            if (stack == null) return false;

            foreach (RegistryObjectVariantGroup group in materialVariants.VariantGroups)
            {
                if (string.IsNullOrEmpty(stack.Attributes.GetString(group.Code))) return false;
            }
            return true;
        }

        // Every group has to be set, otherwise the stack keeps the collectible's normal textures.
        public API.Datastructures.OrderedDictionary<string, string>? GetMaterialValues(ItemStack? stack)
        {
            if (stack == null) return null;

            API.Datastructures.OrderedDictionary<string, string> material = new();
            foreach (RegistryObjectVariantGroup group in materialVariants.VariantGroups)
            {
                string? value = stack.Attributes.GetString(group.Code);
                if (string.IsNullOrEmpty(value)) return null;
                material.Add(group.Code, value);
            }
            return material;
        }
    }
}
