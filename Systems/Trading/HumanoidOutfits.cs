using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class HumanoidOutfits : ModSystem
    {
        Dictionary<string, HumanoidWearableProperties> propsByConfigFilename = new Dictionary<string, HumanoidWearableProperties>();
        
        ICoreAPI api;

        public override double ExecuteOrder()
        {
            return 1;
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            this.api = api;
        }

        public HumanoidWearableProperties loadProps(string configFilename)
        {
            var props = api.Assets.TryGet(new AssetLocation("config/"+ configFilename + ".json"))?.ToObject<HumanoidWearableProperties>();
            if (props == null) throw new FileNotFoundException("config/"+ configFilename + ".json is missing.");

            for (int i = 0; i < props.BySlot.Length; i++)
            {
                string[] shapecodes = props.BySlot[i].Variants;

                for (int j = 0; j < shapecodes.Length; j++)
                {
                    TexturedWeightedCompositeShape wcshape;
                    if (!props.Variants.TryGetValue(shapecodes[j], out wcshape))
                    {
                        api.World.Logger.Error("Typo in "+ configFilename + ".json Shape reference {0} defined for slot {1}, but not in list of shapes. Will remove.", shapecodes[j], props.BySlot[i].Code);
                        shapecodes = shapecodes.Remove(shapecodes[j]);
                        j--;
                        continue;
                    }

                    props.BySlot[i].WeightSum += wcshape.Weight;
                }
            }

            return propsByConfigFilename[configFilename] = props;
        }

        
        public Dictionary<string, string> GetRandomOutfit(string configFilename, Dictionary<string, WeightedCode[]> partialRandomOutfits = null)
        {
            if (!propsByConfigFilename.TryGetValue(configFilename, out var props))
            {
                props = loadProps(configFilename);            
            }

            Dictionary<string, string> outfit = new Dictionary<string, string>();

            for (int i = 0; i < props.BySlot.Length; i++)
            {
                SlotAlloc slotall = props.BySlot[i];

                if (partialRandomOutfits != null && partialRandomOutfits.TryGetValue(slotall.Code, out var wcodes))
                {
                    float wsum = 0;
                    for (int j = 0; j < wcodes.Length; j++) wsum += wcodes[j].Weight;
                    float rnd = (float)api.World.Rand.NextDouble() * wsum;
                    for (int j = 0; j < wcodes.Length; j++)
                    {
                        var wcode = wcodes[j];
                        rnd -= wcode.Weight;
                        if (rnd <= 0)
                        {
                            outfit[slotall.Code] = wcode.Code;
                            break;
                        }
                    }
                }
                else
                {
                    float rnd = (float)api.World.Rand.NextDouble() * slotall.WeightSum;

                    for (int j = 0; j < slotall.Variants.Length; j++)
                    {
                        TexturedWeightedCompositeShape wcshape = props.Variants[slotall.Variants[j]];
                        rnd -= wcshape.Weight;

                        if (rnd <= 0)
                        {
                            outfit[slotall.Code] = slotall.Variants[j];
                            break;
                        }
                    }
                }
            }

            return outfit;
        }

        public HumanoidWearableProperties GetConfig(string configFilename)
        {
            if (!propsByConfigFilename.TryGetValue(configFilename, out var props))
            {
                props = loadProps(configFilename);
            }

            return props;
        }

        public TexturedWeightedCompositeShape[] Outfit2Shapes(string configFilename, string[] outfit)
        {
            if (!propsByConfigFilename.TryGetValue(configFilename, out var props))
            {
                props = loadProps(configFilename);
            }

            TexturedWeightedCompositeShape[] cshapes = new TexturedWeightedCompositeShape[outfit.Length];
            for (int i = 0; i < outfit.Length; i++)
            {
                if (!props.Variants.TryGetValue(outfit[i], out cshapes[i]))
                {
                    api.Logger.Warning("Outfit code {1} for config file {0} cannot be resolved into a variant - wrong code or missing entry?", configFilename, outfit[i]);
                }
            }

            return cshapes;
        }

        public void Reload()
        {
            propsByConfigFilename.Clear();
        }
    }


    public class HumanoidWearableProperties
    {
        public Dictionary<string, TexturedWeightedCompositeShape> Variants;
        public SlotAlloc[] BySlot;
    }

    public class SlotAlloc
    {
        public string Code;
        public string[] Variants;
        public float WeightSum;
    }
    
    public class WeightedCode
    {
        public string Code;
        public float Weight = 1;
    }

    public class TexturedWeightedCompositeShape : CompositeShape
    {
        public float Weight = 1;
        public Dictionary<string, AssetLocation> Textures;
        public Dictionary<string, AssetLocation> OverrideTextures;
        public string[] DisableElements { get; set; }
    }
}
