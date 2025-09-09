using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#nullable disable

namespace Vintagestory.GameContent
{

    public class EntityBehaviorVillagerInv : EntityBehaviorContainer
    {
        InventoryGeneric inv;

        public EntityBehaviorVillagerInv(Entity entity) : base(entity)
        {
            inv = new InventoryGeneric(6, null, null);
        }

        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "villagerinv";

        public override string PropertyName() => "villagerinv";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            Api = entity.World.Api;
            inv.LateInitialize("villagerinv-" + entity.EntityId, Api);
            loadInv();

            base.Initialize(properties, attributes);
        }
    }

    public class EntityDressedHumanoid : EntityHumanoid
    {
        EntityBehaviorVillagerInv ebhv;
        private HumanoidOutfits humanoidOutfits;

        public override ItemSlot RightHandItemSlot => ebhv?.Inventory[0];
        public override ItemSlot LeftHandItemSlot => ebhv?.Inventory[1];
        public string OutfitConfigFileName => this.Properties.Attributes["outfitConfigFileName"].AsString("traderaccessories");


        public Dictionary<string, WeightedCode[]> partialRandomOutfitsOverride = null;

        public string[] OutfitSlots
        {
            get { return (WatchedAttributes["outfitslots"] as StringArrayAttribute)?.value; }
            set
            {
                if (value == null) WatchedAttributes.RemoveAttribute("outfitslots");
                else
                {
                    WatchedAttributes["outfitslots"] = new StringArrayAttribute(value);
                }

                WatchedAttributes.MarkPathDirty("outfitslots");
            }
        }
        public string[] OutfitCodes
        {
            get { return (WatchedAttributes["outfitcodes"] as StringArrayAttribute)?.value; }
            set
            {
                if (value == null) WatchedAttributes.RemoveAttribute("outfitcodes");
                else
                {
                    for (int i = 0; i < value.Length; i++) if (value[i] == null) value[i] = ""; // Null not supported right now
                    WatchedAttributes["outfitcodes"] = new StringArrayAttribute(value);
                }
                
                WatchedAttributes.MarkPathDirty("outfitcodes");
            }
        }

        public void LoadOutfitCodes()
        {
            if (Api.Side != EnumAppSide.Server) return;

            var houtfit = Properties.Attributes["outfit"].AsObject<Dictionary<string, string>>();
            if (houtfit != null)
            {
                OutfitCodes = houtfit.Values.ToArray();
                OutfitSlots = houtfit.Keys.ToArray();
            }
            else
            {
                if (partialRandomOutfitsOverride == null) partialRandomOutfitsOverride = Properties.Attributes["partialRandomOutfits"].AsObject<Dictionary<string, WeightedCode[]>>();
                
                var outfit = humanoidOutfits.GetRandomOutfit(OutfitConfigFileName, partialRandomOutfitsOverride);
                OutfitSlots = outfit.Keys.ToArray();
                OutfitCodes = outfit.Values.ToArray();
            }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            humanoidOutfits = Api.ModLoader.GetModSystem<HumanoidOutfits>();

            if (api.Side == EnumAppSide.Server)
            {
                if (OutfitCodes == null)
                {
                    LoadOutfitCodes();
                }
            } else
            {
                WatchedAttributes.RegisterModifiedListener("outfitcodes", onOutfitsChanged);
            }

            ebhv = GetBehavior<EntityBehaviorVillagerInv>();
        }

        private void onOutfitsChanged()
        {
            MarkShapeModified();
        }

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            var capi = Api as ICoreClientAPI;

            // Reset textures to default    
            var textDict = new FastSmallDictionary<string, CompositeTexture>(0);
            Properties.Client.Textures = textDict;
            foreach (var val in Api.World.GetEntityType(this.Code).Client.Textures)
            {
                textDict[val.Key] = val.Value;
                val.Value.Bake(capi.Assets);
            }

            // Make a copy so we don't mess up the original
            Shape newShape = entityShape.Clone();
            entityShape = newShape;

            string[] outfitCodes = OutfitCodes;
            TexturedWeightedCompositeShape[] cshapes = humanoidOutfits.Outfit2Shapes(OutfitConfigFileName, OutfitCodes);

            var slots = OutfitSlots;

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (i >= cshapes.Length) break;
                    var twcshape = cshapes[i];
                    if (twcshape == null || twcshape.Base == null) continue;

                    addGearToShape(slots[i], twcshape, newShape, shapePathForLogging, null, twcshape.Textures);
                }

                foreach (var val in entityShape.Textures)
                {
                    if (!textDict.ContainsKey(val.Key))
                    {
                        var texture = new CompositeTexture(val.Value);
                        texture.Bake(capi.Assets);
                        textDict[val.Key] = texture;
                    }
                }
            }

            for (int i = 0; i < outfitCodes.Length; i++)
            {
                var twcshape = cshapes[i];
                if (twcshape == null) continue;

                if (twcshape.DisableElements != null)
                {
                    entityShape.RemoveElements(twcshape.DisableElements);
                }

                if (twcshape.OverrideTextures != null)
                {
                    foreach (var val in twcshape.OverrideTextures)
                    {
                        var loc = val.Value;
                        entityShape.Textures[val.Key] = loc;

                        textDict[val.Key] = CreateCompositeTexture(loc, capi, new SourceStringComponents("Outfit config file {0}, Outfit slot {1}, Outfit type {2}, Override Texture {3}", OutfitConfigFileName, OutfitSlots[i], OutfitCodes[i], val.Key));
                    }
                }
            }

            //entityShape.InitForAnimations(Api.Logger, shapePathForLogging, "head");   // unnecessary to InitForAnimations here as animations will be initialized, for "head", in the base.OnTesselation() call below, as cloned is true

            bool cloned = true;
            base.OnTesselation(ref entityShape, shapePathForLogging, ref cloned);
        }


        /// <summary>
        /// Costly: baking a composite texture and fetching its existing subTextureId from the texture atlas is slightly costly, for a new texture reading it from disk and inserting in the texture atlas during runtime is very costly (and will likely require re-mipmapping of the texture atlas in a subsequent frame, creating a GPU-side lagspike)
        /// </summary>
        /// <param name="loc"></param>
        /// <param name="capi"></param>
        /// <param name="sourceForLogging"></param>
        /// <returns></returns>
        private CompositeTexture CreateCompositeTexture(AssetLocation loc, ICoreClientAPI capi, SourceStringComponents sourceForLogging)
        {
            var cmpt = new CompositeTexture(loc);
            cmpt.Bake(capi.Assets);
            capi.EntityTextureAtlas.GetOrInsertTexture(new AssetLocationAndSource(cmpt.Baked.TextureFilenames[0], sourceForLogging), out int textureSubid, out _);
            cmpt.Baked.TextureSubId = textureSubid;
            return cmpt;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefixcode">Any unique Identifier</param>
        /// <param name="cshape"></param>
        /// <param name="entityShape"></param>
        /// <param name="shapePathForLogging"></param>
        /// <param name="disableElements"></param>
        protected void addGearToShape(string prefixcode, CompositeShape cshape, Shape entityShape, string shapePathForLogging, string[] disableElements = null, Dictionary<string, AssetLocation> textureOverrides = null)
        {
            if (disableElements != null) entityShape.RemoveElements(disableElements);

            AssetLocation shapePath = cshape.Base.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
            Shape gearshape = Shape.TryGet(Api, shapePath);
            if (gearshape == null)
            {
                Api.World.Logger.Warning("Compositshape {0} (code: {2}) defined but not found or errored, was supposed to be at {1}. Part will be invisible.", cshape.Base, shapePath, prefixcode);
                return;
            }

            if (prefixcode != null && prefixcode.Length > 0) prefixcode += "-";

            if (textureOverrides != null)
            {
                foreach (var val in textureOverrides)
                {
                    gearshape.Textures[prefixcode + val.Key] = val.Value;
                }
            }

            foreach (var val in gearshape.Textures)
            {
                entityShape.TextureSizes[prefixcode + val.Key] = new int[] { gearshape.TextureWidth, gearshape.TextureHeight };
            }

            var capi = Api as ICoreClientAPI;
            var clientTextures = Properties.Client.Textures;

            gearshape.SubclassForStepParenting(prefixcode, 0);
            gearshape.ResolveReferences(Api.Logger, shapePath);
            entityShape.StepParentShape(gearshape, shapePath.ToShortString(), shapePathForLogging, Api.Logger, (texcode, loc) =>
            {
                string texName = prefixcode + texcode;
                if (!clientTextures.ContainsKey(texName))
                {
                    clientTextures[texName] = CreateCompositeTexture(loc, capi, new SourceStringComponents("Humanoid outfit", shapePath));
                }
            });
        }

    }

}
