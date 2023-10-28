using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class TraderOutfits : ModSystem
    {
        TraderWearableProperties props;
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

        public override void AssetsLoaded(ICoreAPI api)
        {
            props = api.Assets.TryGet(new AssetLocation("config/traderaccessories.json"))?.ToObject<TraderWearableProperties>();
            if (props == null) throw new FileNotFoundException("config/traderaccessories.json is missing.");

            for (int i = 0; i < props.BySlot.Length; i++)
            {
                string[] shapecodes = props.BySlot[i].Shapes;

                for (int j = 0; j < shapecodes.Length; j++)
                {
                    TexturedWeightedCompositeShape wcshape;
                    if (!props.Shapes.TryGetValue(shapecodes[j], out wcshape))
                    {
                        api.World.Logger.Error("Typo in traderaccessories.json Shape reference {0} defined for slot {1}, but not in list of shapes. Will remove.", shapecodes[j], props.BySlot[i].Code);
                        shapecodes = shapecodes.Remove(shapecodes[j]);
                        j--;
                        continue;
                    }

                    props.BySlot[i].WeightSum += wcshape.Weight;
                }
            }
        }

        public string[] GetRandomOutfit()
        {
            string[] outfitCodes = new string[props.BySlot.Length];

            for (int i = 0; i < props.BySlot.Length; i++)
            {
                SlotAlloc slotall = props.BySlot[i];
                float rnd = (float)api.World.Rand.NextDouble() * slotall.WeightSum;

                for (int j = 0; j < slotall.Shapes.Length; j++)
                {
                    TexturedWeightedCompositeShape wcshape = props.Shapes[slotall.Shapes[j]];
                    rnd -= wcshape.Weight;

                    if (rnd <= 0)
                    {
                        outfitCodes[i] = slotall.Shapes[j];
                        break;
                    }
                }
            }

            return outfitCodes;
        }

        public TexturedWeightedCompositeShape[] Outfit2Shapes(string[] outfit)
        {
            TexturedWeightedCompositeShape[] cshapes = new TexturedWeightedCompositeShape[outfit.Length];
            for (int i = 0; i < outfit.Length; i++)
            {
                cshapes[i] = props.Shapes[outfit[i]];
            }

            return cshapes;
        }
    }


    public class TraderWearableProperties
    {
        public Dictionary<string, TexturedWeightedCompositeShape> Shapes;
        public SlotAlloc[] BySlot;
    }

    public class SlotAlloc
    {
        public string Code;
        public string[] Shapes;

        public float WeightSum;
    }

    public class TexturedWeightedCompositeShape : CompositeShape
    {
        public float Weight;
        public Dictionary<string, AssetLocation> Textures;
    }
}
