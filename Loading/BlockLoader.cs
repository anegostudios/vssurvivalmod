using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace Vintagestory.ServerMods
{
    public class BlockVariant
    {
        public string Code;

        public List<string> Codes;
        public List<string> Types;
    }

    public class ResolvedBlockVariant
    {
        public Dictionary<string, string> Codes = new Dictionary<string, string>();
    }


    public class ModBlockLoader : ModSystem
    {
        // Dict Key is filename (with .json)
        Dictionary<AssetLocation, StandardWorldProperty> worldProperties;
        Dictionary<AssetLocation, BlockVariant[]> worldPropertiesVariants;

        Dictionary<AssetLocation, Shape> blockShapes;
        Dictionary<AssetLocation, BlockType> blockTypes;

        Dictionary<AssetLocation, ItemType> itemTypes;


        ICoreServerAPI api;

        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Server;
        }


        public override double ExecuteOrder()
        {
            return 0.2;
        }

        public override bool AllowRuntimeReload()
        {
            return false;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            worldProperties = new Dictionary<AssetLocation, StandardWorldProperty>();

            foreach (var entry in api.Assets.GetMany<StandardWorldProperty>(api.Server.Logger, "worldproperties/"))
            {
                AssetLocation loc = entry.Key.Clone();
                loc.Path = loc.Path.Replace("worldproperties/", "");
                loc.RemoveEnding();
                worldProperties.Add(loc, entry.Value);
            }

            blockShapes = api.Assets.GetMany<Shape>(api.Server.Logger, "shapes/block/");

            blockTypes = new Dictionary<AssetLocation, BlockType>();
            foreach (KeyValuePair<AssetLocation, JObject> entry in api.Assets.GetMany<JObject>(api.Server.Logger, "blocktypes/"))
            {
                JToken property = null;
                JObject blockTypeObject = entry.Value;

                if (blockTypeObject == null)
                {
                    api.Server.LogWarning("Unable to load block type {0}, does not seem to be a json file. Will ignore.", entry.Key);
                    continue;
                }

                AssetLocation location=null;
                try
                {
                    location = blockTypeObject.GetValue("code").ToObject<AssetLocation>();
                    location.Domain = entry.Key.Domain;
                } catch (Exception e)
                {
                    api.World.Logger.Error("Block type {0} has no valid code property. Will ignore. Exception thrown: {1}", entry.Key, e);
                    continue;
                }
                

                blockTypes.Add(entry.Key, new BlockType()
                {
                    Code = location,
                    VariantGroups = blockTypeObject.TryGetValue("variantgroups", out property) ? property.ToObject<CollectibleVariantGroup[]>() : null,
                    Enabled = blockTypeObject.TryGetValue("enabled", out property) ? property.ToObject<bool>() : true,
                    jsonObject = blockTypeObject
                });
            }

            itemTypes = new Dictionary<AssetLocation, ItemType>();
            foreach (KeyValuePair<AssetLocation, JObject> entry in api.Assets.GetMany<JObject>(api.Server.Logger, "itemtypes/"))
            {
                JToken property = null;
                JObject itemTypeObject = entry.Value;

                if (itemTypeObject == null)
                {
                    api.Server.LogWarning("Unable to load item type {0}, does not seem to be a json file. Will ignore.", entry.Key);
                    continue;
                }
                
                
                AssetLocation location = itemTypeObject.GetValue("code").ToObject<AssetLocation>();
                location.Domain = entry.Key.Domain;

                itemTypes.Add(entry.Key, new ItemType()
                {
                    Code = location,
                    VariantGroups = itemTypeObject.TryGetValue("variantgroups", out property) ? property.ToObject<CollectibleVariantGroup[]>() : null,
                    Enabled = itemTypeObject.TryGetValue("enabled", out property) ? property.ToObject<bool>() : true,
                    jsonObject = itemTypeObject
                });
            }

            worldPropertiesVariants = new Dictionary<AssetLocation, BlockVariant[]>();
            foreach (var val in worldProperties)
            {
                if (val.Value == null) continue;

                WorldPropertyVariant[] variants = val.Value.Variants;
                if (variants == null) continue;

                if (val.Value.Code == null) {
                    api.Server.LogError("Error in worldproperties {0}, not code set", val.Key);
                    return;
                }

                worldPropertiesVariants[val.Value.Code] = new BlockVariant[variants.Length];

                for (int i = 0; i < variants.Length; i++)
                {
                    worldPropertiesVariants[val.Value.Code][i] = new BlockVariant() { Code = variants[i].Code.Path };
                }
            }

            LoadItems();
            LoadBlocks();

            api.Server.LogNotification("BlockLoader: Blocks and Items loaded");
        }



        #region Items
        void LoadItems()
        {
            List<Item> items = new List<Item>();

            foreach (var val in itemTypes)
            {
                if (!val.Value.Enabled) continue;
                GatherItems(val.Key, val.Value, items);
            }

            foreach (Item item in items)
            {
                try
                {
                    api.RegisterItem(item);
                } catch (Exception e)
                {
                    api.Server.LogError("Failed registering item {0}: {1}", item.Code, e);
                }
                
            }

        }


        void GatherItems(AssetLocation location, ItemType itemType, List<Item> items)
        {
            List<ResolvedBlockVariant> variants = null;
            try
            {
                variants = GatherVariants(itemType.VariantGroups, itemType.Code);
            } catch (Exception e)
            {
                api.Server.Logger.Error("Exception thrown while trying to gather all variants of the item type with code {0}. Will ignore most itemtype completly. Exception: {1}", itemType.Code, e);
                return;
            }
            

            // Single item type
            if (variants.Count == 0)
            {
                Item item = baseItemFromItemType(itemType, itemType.Code.Clone(), new Dictionary<string, string>());
                items.Add(item);
            }
            else
            {
                // Multi item type
                foreach (ResolvedBlockVariant variant in variants)
                {
                    AssetLocation fullcode = itemType.Code.Clone();
                    foreach (string code in variant.Codes.Values)
                    {
                        fullcode.Path += "-" + code;
                    }
                    Item typedItem = baseItemFromItemType(itemType, fullcode, variant.Codes);
                    typedItem.FillPlaceHolders(variant.Codes);

                    if (itemType.SkipVariants != null)
                    {
                        bool found = false;
                        for (int i = 0; i < itemType.SkipVariants.Length; i++)
                        {
                            if (typedItem.WildCardMatch(itemType.SkipVariants[i]))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (found) continue;
                    }

                    items.Add(typedItem);
                }
            }

            itemType.jsonObject = null;
        }


        Item baseItemFromItemType(ItemType itemType, AssetLocation fullcode, Dictionary<string, string> searchReplace)
        {
            ItemType typedItemType = new ItemType()
            {
                Code = itemType.Code,
                VariantGroups = itemType.VariantGroups,
                Enabled = itemType.Enabled,
                jsonObject = itemType.jsonObject.DeepClone() as JObject
            };

            solveByType(typedItemType.jsonObject, fullcode.Path);

            try
            {
                JsonUtil.PopulateObject(typedItemType, typedItemType.jsonObject.ToString(), fullcode.Domain);
            } catch (Exception e)
            {
                api.Server.Logger.Error("Exception thrown while trying to populate/load json data of the typed item with code {0}. Will ignore most of the attributes. Exception: {1}", typedItemType.Code, e);
            }

            typedItemType.jsonObject = null;
            Item item;

            if (api.ClassRegistry.GetItemClass(typedItemType.Class) == null)
            {
                api.Server.Logger.Error("Item with code {0} has defined an item class {1}, but no such class registered. Will ignore.", typedItemType.Code, typedItemType.Class);
                item = new Item();
            } else
            {
                item = api.ClassRegistry.CreateItem(typedItemType.Class);
            }
            

            item.Code = fullcode;
            item.Textures = typedItemType.Textures;
            item.MaterialDensity = typedItemType.MaterialDensity;
            item.GuiTransform = typedItemType.GuiTransform;
            item.FpHandTransform = typedItemType.FpHandTransform;
            item.TpHandTransform = typedItemType.TpHandTransform;
            item.GroundTransform = typedItemType.GroundTransform;
            item.DamagedBy = typedItemType.DamagedBy;
            item.MaxStackSize = typedItemType.MaxStackSize;
            if (typedItemType.Attributes != null) item.Attributes = typedItemType.Attributes;
            item.CombustibleProps = typedItemType.CombustibleProps;
            item.NutritionProps = typedItemType.NutritionProps;
            item.GrindingProps = typedItemType.GrindingProps;
            item.Shape = typedItemType.Shape;
            item.Tool = typedItemType.Tool;
            item.AttackPower = typedItemType.AttackPower;
            item.LiquidSelectable = typedItemType.LiquidSelectable;
            item.MiningTier = typedItemType.MiningTier;
            item.Durability = typedItemType.Durability;
            item.MiningSpeed = typedItemType.MiningSpeed;
            item.AttackRange = typedItemType.AttackRange;
            item.StorageFlags = (EnumItemStorageFlags)typedItemType.StorageFlags;
            item.RenderAlphaTest = typedItemType.RenderAlphaTest;
            item.HeldTpHitAnimation = typedItemType.HeldTpHitAnimation;
            item.HeldTpIdleAnimation = typedItemType.HeldTpIdleAnimation;
            item.HeldTpUseAnimation = typedItemType.HeldTpUseAnimation;
            item.CreativeInventoryStacks = typedItemType.CreativeInventoryStacks == null ? null : (CreativeTabAndStackList[])typedItemType.CreativeInventoryStacks.Clone();

            typedItemType.InitItem(api.ClassRegistry, item, searchReplace);

            return item;
        }
        #endregion

        #region Blocks
        void LoadBlocks()
        {
            List<Block> blocks = new List<Block>();

            foreach (var val in blockTypes)
            {
                if (!val.Value.Enabled) continue;
                GatherBlocks(val.Value.Code, val.Value, blocks);
            }
            
            foreach (Block block in blocks)
            {
                try
                {
                    api.RegisterBlock(block);
                }
                catch (Exception e)
                {
                    api.Server.LogError("Failed registering block {0}: {1}", block.Code, e);
                }
            }
        }

        void GatherBlocks(AssetLocation location, BlockType blockType, List<Block> blocks)
        {
            List<ResolvedBlockVariant> variants = null;
            try
            {
                variants = GatherVariants(blockType.VariantGroups, location);
            }
            catch (Exception e)
            {
                api.Server.Logger.Error("Exception thrown while trying to gather all variants of the block type with code {0}. Will ignore most itemtype completly. Exception: {1}", blockType.Code, e);
                return;
            }


            // Single block type
            if (variants.Count == 0)
            {
                Block block = baseBlockFromBlockType(blockType, blockType.Code.Clone(), new Dictionary<string, string>());
                blocks.Add(block);
            }
            else
            {
                foreach (ResolvedBlockVariant variant in variants)
                {
                    AssetLocation fullcode = blockType.Code.Clone();
                    foreach (string code in variant.Codes.Values)
                    {
                        fullcode.Path += "-" + code;
                    }

                    Block block = baseBlockFromBlockType(blockType, fullcode, variant.Codes);

                    block.FillPlaceHolders(api.World.Logger, variant.Codes);

                    if (blockType.SkipVariants != null)
                    {
                        bool found = false;
                        for (int i = 0; i < blockType.SkipVariants.Length; i++)
                        {
                            if (block.WildCardMatch(blockType.SkipVariants[i]))
                            {
                                found = true;
                                break;
                            }
                        }
                        if (found)
                        {
                            continue;
                        }
                    }


                    blocks.Add(block);
                }
            }

            blockType.jsonObject = null;
        }


        Block baseBlockFromBlockType(BlockType blockType, AssetLocation fullcode, Dictionary<string, string> searchReplace)
        {
            BlockType typedBlockType = new BlockType()
            {
                Code = blockType.Code,
                VariantGroups = blockType.VariantGroups,
                Enabled = blockType.Enabled,
                jsonObject = blockType.jsonObject.DeepClone() as JObject
            };

            try
            {
                solveByType(typedBlockType.jsonObject, fullcode.Path);
            } catch (Exception e)
            {
                api.Server.Logger.Error("Exception thrown while trying to resolve *byType properties of typed block {0}. Will ignore most of the attributes. Exception thrown: {1}", typedBlockType.Code, e);
            }

            try
            {
                JsonUtil.PopulateObject(typedBlockType, typedBlockType.jsonObject.ToString(), fullcode.Domain);
            } catch (Exception e)
            {
                api.Server.Logger.Error("Exception thrown while trying to populate/load json data of the typed block {0}. Will ignore most of the attributes. Exception thrown: {1}", typedBlockType.Code, e);
            }
            
            typedBlockType.jsonObject = null;
            Block block;

            if (api.ClassRegistry.GetBlockClass(typedBlockType.Class) == null)
            {
                api.Server.Logger.Error("Block with code {0} has defined a block class {1}, no such class registered. Will ignore.", typedBlockType.Code, typedBlockType.Class);
                block = new Block();
            } else
            {
                block = api.ClassRegistry.CreateBlock(typedBlockType.Class);
            }
            
             
            if (typedBlockType.EntityClass != null)
            {
                if (api.ClassRegistry.GetBlockEntity(typedBlockType.EntityClass) != null)
                {
                    block.EntityClass = typedBlockType.EntityClass;
                } else
                {
                    api.Server.Logger.Error("Block with code {0} has defined a block entity class {1}, no such class registered. Will ignore.", typedBlockType.Code, typedBlockType.EntityClass);
                }
            }

            block.Code = fullcode;
            block.LiquidSelectable = typedBlockType.LiquidSelectable;
            block.WalkSpeedMultiplier = typedBlockType.WalkspeedMultiplier;
            block.DragMultiplier = typedBlockType.DragMultiplier;
            block.DrawType = typedBlockType.DrawType;
            block.Replaceable = typedBlockType.Replaceable;
            block.Fertility = typedBlockType.Fertility;
            block.LightAbsorption = typedBlockType.LightAbsorption;
            block.LightHsv = typedBlockType.LightHsv;
            block.VertexFlags = typedBlockType.VertexFlags?.Clone() ?? new VertexFlags(0);
            block.Resistance = typedBlockType.Resistance;
            block.BlockMaterial = typedBlockType.BlockMaterial;
            block.Shape = typedBlockType.Shape;
            block.TexturesInventory = typedBlockType.TexturesInventory;
            block.Textures = typedBlockType.Textures;
            block.TintIndex = typedBlockType.TintIndex;
            block.Ambientocclusion = typedBlockType.Ambientocclusion;
            block.CollisionBoxes = typedBlockType.CollisionBoxes == null ? null : (Cuboidf[])typedBlockType.CollisionBoxes.Clone();
            block.SelectionBoxes = typedBlockType.SelectionBoxes == null ? null : (Cuboidf[])typedBlockType.SelectionBoxes.Clone();
            block.MaterialDensity = typedBlockType.MaterialDensity;
            block.GuiTransform = typedBlockType.GuiTransform;
            block.FpHandTransform = typedBlockType.FpHandTransform;
            block.TpHandTransform = typedBlockType.TpHandTransform;
            block.GroundTransform = typedBlockType.GroundTransform;
            block.ShapeInventory = typedBlockType.ShapeInventory;
            block.RenderPass = typedBlockType.RenderPass;
            block.ParticleProperties = typedBlockType.ParticleProperties;
            block.Climbable = typedBlockType.Climbable;
            block.RainPermeable = typedBlockType.RainPermeable;
            block.SnowCoverage = typedBlockType.SnowCoverage;
            block.FaceCullMode = typedBlockType.FaceCullMode;
            block.Drops = typedBlockType.Drops;
            block.MaxStackSize = typedBlockType.MaxStackSize;
            block.MatterState = typedBlockType.MatterState;
            if (typedBlockType.Attributes != null)
            {
                block.Attributes = typedBlockType.Attributes.Clone();
            }
            block.NutritionProps = typedBlockType.NutritionProps;
            block.GrindingProps = typedBlockType.GrindingProps;
            block.LiquidLevel = typedBlockType.LiquidLevel;
            block.AttackPower = typedBlockType.AttackPower;
            block.MiningSpeed = typedBlockType.MiningSpeed;
            block.MiningTier = typedBlockType.MiningTier;
            block.RequiredMiningTier = typedBlockType.RequiredMiningTier;
            block.AttackRange = typedBlockType.AttackRange;
            

            if (typedBlockType.Sounds != null)
            {
                block.Sounds = typedBlockType.Sounds.Clone();
            }
            block.RandomDrawOffset = typedBlockType.RandomDrawOffset;
            block.RandomizeAxes = typedBlockType.RandomizeAxes;
            block.CombustibleProps = typedBlockType.CombustibleProps;
            block.StorageFlags = (EnumItemStorageFlags)typedBlockType.StorageFlags;
            block.RenderAlphaTest = typedBlockType.RenderAlphaTest;
            block.HeldTpHitAnimation = typedBlockType.HeldTpHitAnimation;
            block.HeldTpIdleAnimation = typedBlockType.HeldTpIdleAnimation;
            block.HeldTpUseAnimation = typedBlockType.HeldTpUseAnimation;
            block.CreativeInventoryStacks = typedBlockType.CreativeInventoryStacks == null ? null : (CreativeTabAndStackList[])typedBlockType.CreativeInventoryStacks.Clone();

            if (block.CollisionBoxes != null)
            {
                for (int i = 0; i < block.CollisionBoxes.Length; i++)
                {
                    block.CollisionBoxes[i].RoundToFracsOf16();
                }
            }

            if (block.SelectionBoxes != null)
            {
                for (int i = 0; i < block.SelectionBoxes.Length; i++)
                {
                    block.SelectionBoxes[i].RoundToFracsOf16();
                }
            }

            typedBlockType.InitBlock(api.ClassRegistry, api.World.Logger, block, searchReplace);

            return block;
        }



        void solveByType(JToken json, string codePath)
        {
            List<string> propertiesToRemove = new List<string>();
            Dictionary<string, JToken> propertiesToAdd = new Dictionary<string, JToken>();

            if (json is JObject)
            {
                foreach (var entry in (json as JObject))
                {
                    if (entry.Key.EndsWith("byType", System.StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var byTypeProperty in entry.Value.ToObject<Dictionary<string, JToken>>())
                        {
                            if (BlockType.WildCardMatch(byTypeProperty.Key, codePath))
                            {
                                propertiesToAdd.Add(entry.Key.Substring(0, entry.Key.Length - "byType".Length), byTypeProperty.Value);
                                break;
                            }
                        }
                        propertiesToRemove.Add(entry.Key);
                    }
                }

                foreach (var property in propertiesToRemove)
                {
                    (json as JObject).Remove(property);
                }

                foreach (var property in propertiesToAdd)
                {
                    (json as JObject)[property.Key] = property.Value;
                }

                foreach (var entry in (json as JObject))
                {
                    solveByType(entry.Value, codePath);
                }
            }
            else if (json is JArray)
            {
                foreach (var child in (json as JArray))
                {
                    solveByType(child, codePath);
                }
            }
        }
        #endregion


        public StandardWorldProperty GetWorldPropertyByCode(string code)
        {
            StandardWorldProperty property = null;
            worldProperties.TryGetValue(new AssetLocation(code), out property);
            return property;
        }




        List<ResolvedBlockVariant> GatherVariants(CollectibleVariantGroup[] variantgroups, AssetLocation location)
        {
            List<ResolvedBlockVariant> blockvariantsFinal = new List<ResolvedBlockVariant>();

            if (variantgroups == null || variantgroups.Length == 0) return blockvariantsFinal;

            OrderedDictionary<string, BlockVariant[]> blockvariantsMul = new OrderedDictionary<string, BlockVariant[]>();

            // 1. Collect all types
            for (int i = 0; i < variantgroups.Length; i++)
            {
                if (variantgroups[i].LoadFromProperties != null)
                {
                    CollectFromWorldProperties(variantgroups[i], variantgroups, blockvariantsMul, blockvariantsFinal, location);
                }

                if (variantgroups[i].States != null)
                {
                    CollectFromStateList(variantgroups[i], variantgroups, blockvariantsMul, blockvariantsFinal, location);
                }
            }

            // 2. Multiply multiplicative groups
            BlockVariant[,] variants = MultiplyProperties(blockvariantsMul.Values.ToArray());


            // 3. Add up multiplicative groups
            for (int i = 0; i < variants.GetLength(0); i++)
            {
                ResolvedBlockVariant resolved = new ResolvedBlockVariant();
                for (int j = 0; j < variants.GetLength(1); j++)
                {
                    BlockVariant variant = variants[i, j];

                    if (variant.Codes != null)
                    {
                        for (int k = 0; k < variant.Codes.Count; k++)
                        {
                            resolved.Codes.Add(variant.Types[k], variant.Codes[k]);
                        }
                    } else {
                        resolved.Codes.Add(blockvariantsMul.GetKeyAtIndex(j), variant.Code);
                    }
                    
                }

                blockvariantsFinal.Add(resolved);
            }

            return blockvariantsFinal;
        }

        private void CollectFromStateList(CollectibleVariantGroup variantGroup, CollectibleVariantGroup[] variantgroups, OrderedDictionary<string, BlockVariant[]> blockvariantsMul, List<ResolvedBlockVariant> blockvariantsFinal, AssetLocation filename)
        {
            if (variantGroup.Code == null)
            {
                api.Server.LogError(
                    "Error in itemtype {0}, a variantgroup using a state list must have a code. Ignoring.",
                    filename
                );
                return;
            }

            string[] states = variantGroup.States;
            string type = variantGroup.Code;

            // Additive state list
            if (variantGroup.Combine == EnumCombination.Add)
            {
                for (int j = 0; j < states.Length; j++)
                {
                    ResolvedBlockVariant resolved = new ResolvedBlockVariant();
                    resolved.Codes.Add(type, states[j]);
                    blockvariantsFinal.Add(resolved);
                }
            }

            // Multiplicative state list
            if (variantGroup.Combine == EnumCombination.Multiply)
            {
                List<BlockVariant> stateList = new List<BlockVariant>();

                for (int j = 0; j < states.Length; j++)
                {
                    stateList.Add(new BlockVariant() { Code = states[j] });
                }
                

                for (int i = 0; i < variantgroups.Length; i++)
                {
                    CollectibleVariantGroup cvg = variantgroups[i];
                    if (cvg.Combine == EnumCombination.SelectiveMultiply && cvg.OnVariant == variantGroup.Code)
                    {
                        for (int k = 0; k < stateList.Count; k++)
                        {
                            if (cvg.Code != stateList[k].Code) continue;

                            BlockVariant old = stateList[k];

                            stateList.RemoveAt(k);

                            for (int j = 0; j < cvg.States.Length; j++)
                            {
                                List<string> codes = old.Codes == null ? new List<string>() { old.Code } : old.Codes;
                                List<string> types = old.Types == null ? new List<string>() { variantGroup.Code } : old.Types;

                                codes.Add(cvg.States[j]);
                                types.Add(cvg.Code);

                                stateList.Insert(k, new BlockVariant()
                                {
                                    Code = old.Code + "-" + cvg.States[j],
                                    Codes = codes,
                                    Types = types
                                });
                            }
                        }
                    }
                }

                if (blockvariantsMul.ContainsKey(type))
                {
                    stateList.AddRange(blockvariantsMul[type]);
                    blockvariantsMul[type] = stateList.ToArray();
                } else
                {
                    blockvariantsMul.Add(type, stateList.ToArray());
                }
                
            }
        }



        private void CollectFromWorldProperties(CollectibleVariantGroup variantGroup, CollectibleVariantGroup[] variantgroups, OrderedDictionary<string, BlockVariant[]> blockvariantsMul, List<ResolvedBlockVariant> blockvariantsFinal, AssetLocation location)
        {
            StandardWorldProperty property = GetWorldPropertyByCode(variantGroup.LoadFromProperties);

            if (property == null)
            {
                api.Server.LogError(
                    "Error in item or block {0}, worldproperty {1} does not exist (or is empty). Ignoring.",
                    location, variantGroup.LoadFromProperties
                );
                return;
            }

            string typename = variantGroup.Code == null ? property.Code.Path : variantGroup.Code;

            if (variantGroup.Combine == EnumCombination.Add)
            {

                foreach (WorldPropertyVariant variant in property.Variants)
                {
                    ResolvedBlockVariant resolved = new ResolvedBlockVariant();
                    resolved.Codes.Add(typename, variant.Code.Path);
                    blockvariantsFinal.Add(resolved);
                }
            }

            if (variantGroup.Combine == EnumCombination.Multiply)
            {
                blockvariantsMul.Add(typename, worldPropertiesVariants[property.Code]);
            }
        }


        // Takes n lists of properties and returns every unique n-tuple 
        // through a 2 dimensional array blockvariants[i, ni] 
        // where i = n-tuple index and ni = index of current element in the n-tuple
        BlockVariant[,] MultiplyProperties(BlockVariant[][] blockVariants)
        {
            int resultingQuantiy = 1;

            for (int i = 0; i < blockVariants.Length; i++)
            {
                resultingQuantiy *= blockVariants[i].Length;
            }

            BlockVariant[,] multipliedProperties = new BlockVariant[resultingQuantiy, blockVariants.Length];

            for (int i = 0; i < resultingQuantiy; i++)
            {
                int div = 1;

                for (int j = 0; j < blockVariants.Length; j++) {
                    BlockVariant variant = blockVariants[j][(i / div) % blockVariants[j].Length];

                    multipliedProperties[i, j] = new BlockVariant() { Code = variant.Code, Codes = variant.Codes, Types = variant.Types };

                    div *= blockVariants[j].Length;
                }
            }
            
            return multipliedProperties;
        }

    }
}
