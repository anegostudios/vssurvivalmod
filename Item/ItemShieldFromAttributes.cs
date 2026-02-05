using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ItemShieldFromAttributes : ItemShield, IContainedMeshSource, IAttachableToEntity, IHandbookGrouping
    {
        public const string ShieldMeshRefsKey = "shieldmeshrefs";
        private ICoreClientAPI capi;

        private Dictionary<int, MultiTextureMeshRef> meshrefs => ObjectCacheUtil.GetOrCreate(api, ShieldMeshRefsKey, () => new Dictionary<int, MultiTextureMeshRef>());
        private Dictionary<string, Dictionary<string, int>> durabilityGains = null;
        private API.Datastructures.OrderedDictionary<string, string[]> variantGroups = null;
        public string Construction => Variant["construction"];

        #region IAttachableToEntity
        public new int RequiresBehindSlots { get; set; } = 0;
        string IAttachableToEntity.GetCategoryCode(ItemStack stack) => AttachableToEntity?.GetCategoryCode(stack);
        CompositeShape IAttachableToEntity.GetAttachedShape(ItemStack stack, string slotCode) => AttachableToEntity?.GetAttachedShape(stack, slotCode);
        string[] IAttachableToEntity.GetDisableElements(ItemStack stack) => AttachableToEntity?.GetDisableElements(stack);
        string[] IAttachableToEntity.GetKeepElements(ItemStack stack) => AttachableToEntity?.GetKeepElements(stack);
        string IAttachableToEntity.GetTexturePrefixCode(ItemStack stack)
        {
            return AttachableToEntity?.GetTexturePrefixCode(stack) + "-" + string.Join("-", variantGroups.Keys.Select(code => stack.Attributes.GetString(code)).Where(code => code != null));
        }

        void IAttachableToEntity.CollectTextures(ItemStack itemstack, Shape intoShape, string texturePrefixCode, Dictionary<string, CompositeTexture> intoDict)
        {
            ContainedTextureSource cnts = genTextureSource(itemstack, null);

            if (cnts == null)
            {
                api.Logger.Warning(GetHeldItemName(itemstack) + " (" + Code.ToShortString() + "-" + string.Join("-", variantGroups.Keys.Select(code => itemstack.Attributes.GetString(code)).Where(code => code != null)) + ") could not create textures, may render with unknown textures");
                return;
            }

            foreach (var val in cnts.Textures)
            {
                intoShape.Textures[val.Key] = val.Value;
            }
        }


        public new bool IsAttachable(Entity toEntity, ItemStack itemStack) => true;
        #endregion

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            capi = api as ICoreClientAPI;

            durabilityGains = Attributes["durabilityGains"].AsObject(durabilityGains);
            variantGroups = Attributes["variantGroups"].AsObject(variantGroups);

            if (durabilityGains == null || variantGroups == null) throw new System.Exception("Round shield " + Code + " has improperly defined durabilityGains or variantGroups");

            AddAllTypesToCreativeInventory();
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (api.ObjectCache.ContainsKey(ShieldMeshRefsKey) && meshrefs.Count > 0)
            {
                foreach (var (_, meshRef) in meshrefs)
                {
                    meshRef.Dispose();
                }

                ObjectCacheUtil.Delete(api, ShieldMeshRefsKey);
            }
            base.OnUnloaded(api);
        }

        public override int GetMaxDurability(ItemStack itemstack)
        {
            int gain = 0;

            foreach (var val in durabilityGains)
            {
                if (itemstack.Attributes.GetString(val.Key) is not string material) continue;

                if (val.Value.TryGetValue(material, out var matgain)) gain += matgain;
            }

            return base.GetMaxDurability(itemstack) + gain;
        }

        private void AddAllTypesToCreativeInventory()
        {
            List<JsonItemStack> stacks = new List<JsonItemStack>();
            var allowedVariants = Attributes["allowedVariants"].AsArray<Dictionary<string, string>>();

            foreach (var entry in allowedVariants)
            {
                Dictionary<string, string[]> attributes = [];
                foreach (var key in variantGroups.Keys)
                {
                    if (entry.TryGetValue(key, out var val))
                    {
                        attributes.Add(key, val == "*" ? [.. variantGroups[key]] : (val != null ? [val] : []));
                    }
                }

                string[] variants = [""];
                foreach (var key in attributes.Keys)
                {
                    var newVariants = variants.SelectMany(variant => attributes[key].Select(val => variant + $" {key}: \"{val}\","));
                    if (newVariants.Any()) variants = [.. newVariants];
                }

                variants = [.. variants.Select(var => $"{{{var} }}")];
                stacks.AddRange(variants.Select(genJstack));
            }

            CreativeInventoryStacks = [new () { Stacks = [.. stacks], Tabs = ["general", "items", "tools"] }];
        }

        private JsonItemStack genJstack(string json)
        {
            var jstack = new JsonItemStack()
            {
                Code = this.Code,
                Type = EnumItemClass.Item,
                Attributes = new JsonObject(JToken.Parse(json))
            };

            jstack.Resolve(api.World, "shield type");

            return jstack;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            int meshrefid = itemstack.TempAttributes.GetInt("meshRefId");
            if (meshrefid == 0 || !meshrefs.TryGetValue(meshrefid, out renderinfo.ModelRef))
            {
                int id = meshrefs.Count + 1;
                var modelref = capi.Render.UploadMultiTextureMesh(GenMesh(itemstack, capi.ItemTextureAtlas));
                renderinfo.ModelRef = meshrefs[id] = modelref;

                itemstack.TempAttributes.SetInt("meshRefId", id);
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
        {
            if (genTextureSource(itemstack, targetAtlas) is not ContainedTextureSource cnts) return new MeshData();
            capi.Tesselator.TesselateItem(this, out MeshData mesh, cnts);
            return mesh;
        }

        private ContainedTextureSource genTextureSource(ItemStack itemstack, ITextureAtlasAPI targetAtlas)
        {
            var cnts = new ContainedTextureSource(api as ICoreClientAPI, targetAtlas, [], string.Format("For render in shield {0}", Code));

            cnts.Textures.Clear();

            Dictionary<string, string> attributes = getAttributesFromItemstack(itemstack);

            var shape = capi.TesselatorManager.GetCachedShape(this.Shape.Base);

            foreach (var ctex in shape.Textures)
            {
                cnts.Textures[ctex.Key] = ctex.Value;
            }

            if (attributes.Count == 0) return cnts;

            Dictionary<string, Dictionary<string, string>> textures = Attributes["textures"].AsObject<Dictionary<string, Dictionary<string, string>>>();

            string resolvedKey = "";
            Dictionary<string, string> resolvedTextures = [];

            bool match = false;
            foreach (var variant in textures)
            {
                string currentVariant = "";
                resolvedKey = variant.Key;
                resolvedTextures = variant.Value;
                foreach (var entry in attributes)
                {
                    currentVariant += (currentVariant == "" ? "" : "-") + entry.Value;
                    resolvedKey = resolvedKey.Replace($"{{{entry.Key}}}", entry.Value);
                    foreach (var texture in variant.Value.Keys)
                    {
                        resolvedTextures[texture] = resolvedTextures[texture].Replace($"{{{entry.Key}}}", entry.Value);
                    }
                }

                if (currentVariant == resolvedKey)
                {
                    match = true;
                    break;
                }
            }

            if (!match) return cnts;

            if (resolvedTextures.Values.Any(val => val.Contains('{') || val.Contains('}'))) throw new System.Exception("Round shield " + Code + " has improperly defined textures in attributes");

            foreach (var ctex in shape.Textures)
            {
                if (resolvedTextures.ContainsKey(ctex.Key)) cnts.Textures[ctex.Key] = resolvedTextures[ctex.Key];
            }

            return cnts;
        }

        public override string GetHeldItemName(ItemStack itemStack)
        {
            Dictionary<string, string> attributes = getAttributesFromItemstack(itemStack);

            if (attributes.Count == 0) return base.GetHeldItemName(itemStack);

            string variant = Code.Domain + AssetLocation.LocationSeparator + "item-" + Code.Path + "-" + string.Join("-", attributes.Values);
            return Lang.GetMatchingIfExists(variant) ?? base.GetHeldItemName(itemStack);
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            Dictionary<string, string> attributes = getAttributesFromItemstack(inSlot.Itemstack);

            if (attributes.Count == 0) return;

            var variant = Code.Domain + AssetLocation.LocationSeparator + "itemdesc-" + Code.Path;
            variant = Lang.GetMatchingIfExists(variant + "-" + string.Join("-", attributes.Values));

            if (variant != null) dsc.AppendLine(variant);
        }

        private Dictionary<string, string> getAttributesFromItemstack(ItemStack itemStack)
        {
            Dictionary<string, string> attributes = [];
            foreach (var key in variantGroups.Keys)
            {
                if (itemStack.Attributes.GetAsString(key, null) is string val)
                {
                    attributes.Add(key, val);
                }
            }

            // Default to a generic woodtype specifically only for these two item variants if none is found for the default game domain
            if (Code.Domain == GlobalConstants.DefaultDomain && Construction is "woodmetal" or "woodmetalleather" && (!attributes.TryGetValue("wood", out var wood) || wood == ""))
            {
                attributes.Add("wood", "generic");
            }

            return attributes;
        }

        public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        {
            return GenMesh(slot.Itemstack, targetAtlas);
        }

        public string GetMeshCacheKey(ItemSlot slot)
        {
            return Code.ToShortString() + "-" + string.Join("-", variantGroups.Keys.Select(code => slot.Itemstack.Attributes.GetString(code)).Where(code => code != null));
        }

        public AssetLocation GetCodeForHandbookGrouping(ItemStack stack)
        {
            Dictionary<string, string> attributes = getAttributesFromItemstack(stack);

            if (attributes.Count == 0) return Code;
            
            return AssetLocation.Create(Code.Path + "-" + string.Join("-", attributes.Values), Code.Domain);
        }

        public string GetWildcardForHandbookGrouping(string wildcard, ItemStack stack)
        {
            foreach (var variant in variantGroups)
            {
                wildcard = wildcard.Replace("{" + variant.Key + "}", stack.Attributes.GetAsString(variant.Key));
            }

            return wildcard;
        }
    }
}
