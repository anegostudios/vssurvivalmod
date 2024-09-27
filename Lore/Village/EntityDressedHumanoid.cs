using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

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
                
                var outfit = Api.ModLoader.GetModSystem<HumanoidOutfits>().GetRandomOutfit(OutfitConfigFileName, partialRandomOutfitsOverride);
                OutfitSlots = outfit.Keys.ToArray();
                OutfitCodes = outfit.Values.ToArray();
            }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

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
            Properties.Client.Textures = new FastSmallDictionary<string, CompositeTexture>(0);
            foreach (var val in Api.World.GetEntityType(this.Code).Client.Textures)
            {
                Properties.Client.Textures[val.Key] = val.Value;
                val.Value.Bake(capi.Assets);
            }


            base.OnTesselation(ref entityShape, shapePathForLogging);

            // Make a copy so we don't mess up the original
            Shape newShape = entityShape.Clone();
            entityShape = newShape;

            string[] outfitCodes = OutfitCodes;
            TexturedWeightedCompositeShape[] cshapes = Api.ModLoader.GetModSystem<HumanoidOutfits>().Outfit2Shapes(OutfitConfigFileName, OutfitCodes);

            var slots = OutfitSlots;

            for (int i = 0; i < outfitCodes.Length; i++)
            {
                var twcshape = cshapes[i];
                if (twcshape == null) continue;

                if (twcshape?.Base == null)
                {
                    continue;
                }
                if (slots == null || slots.Length <= i) continue; 

                addGearToShape(OutfitSlots[i], twcshape, newShape, shapePathForLogging, null, twcshape.Textures);
            }

            for (int i = 0; i < outfitCodes.Length; i++)
            {
                var twcshape = cshapes[i];
                if (twcshape == null) continue;

                if (twcshape.DisableElements != null)
                {
                    entityShape.RemoveElements(twcshape.DisableElements);
                }                

                if (twcshape?.OverrideTextures != null)
                {
                    foreach (var val in twcshape.OverrideTextures)
                    {
                        var loc = val.Value;
                        entityShape.Textures[val.Key] = loc;

                        var cmpt = Properties.Client.Textures[val.Key] = new CompositeTexture(loc);
                        cmpt.Bake(capi.Assets);
                        capi.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _);
                        cmpt.Baked.TextureSubId = textureSubid;
                    }
                }
            }

            entityShape.InitForAnimations(Api.Logger, shapePathForLogging, "head");
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefixcode">Any unique Identifier</param>
        /// <param name="cshape"></param>
        /// <param name="entityShape"></param>
        /// <param name="shapePathForLogging"></param>
        /// <param name="disableElements"></param>
        /// <returns></returns>
        protected Shape addGearToShape(string prefixcode, CompositeShape cshape, Shape entityShape, string shapePathForLogging, string[] disableElements = null, Dictionary<string, AssetLocation> textureOverrides = null)
        {
            AssetLocation shapePath = cshape.Base.CopyWithPath("shapes/" + cshape.Base.Path + ".json");

            entityShape.RemoveElements(disableElements);

            Shape armorShape = Shape.TryGet(Api, shapePath);
            if (armorShape == null)
            {
                Api.World.Logger.Warning("Compositshape {0} (code: {2}) defined but not found or errored, was supposed to be at {1}. Part will be invisible.", cshape.Base, shapePath, prefixcode);
                return null;
            }

            if (prefixcode != null && prefixcode.Length > 0) prefixcode += "-";

            var capi = Api as ICoreClientAPI;

            if (textureOverrides != null)
            {
                foreach (var val in textureOverrides)
                {
                    armorShape.Textures[prefixcode + val.Key] = val.Value;
                }
            }

            foreach (var val in armorShape.Textures)
            {
                entityShape.TextureSizes[prefixcode + val.Key] = new int[] { armorShape.TextureWidth, armorShape.TextureHeight };
            }

            var textures = Properties.Client.Textures;
            entityShape.StepParentShape(armorShape, prefixcode, shapePath.ToShortString(), shapePathForLogging, Api.Logger, (texcode, loc) =>
            {
                var cmpt = textures[prefixcode + texcode] = new CompositeTexture(loc);
                cmpt.Bake(capi.Assets);
                capi.EntityTextureAtlas.GetOrInsertTexture(cmpt.Baked.TextureFilenames[0], out int textureSubid, out _);
                cmpt.Baked.TextureSubId = textureSubid;
            });

            foreach (var val in entityShape.Textures)
            {
                if (!textures.ContainsKey(val.Key))
                {
                    textures[val.Key] = new CompositeTexture(val.Value);
                    textures[val.Key].Bake(capi.Assets);
                }
            }

            return entityShape;
        }

    }

}
