using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ManipulableInput : JsonItemStack
    {
        public bool IsTool;
        public string[] AllowedVariants;
    }

    public class ManipulableConfig
    {
        public Dictionary<string, ManipulableInput> Input;
        public JsonItemStack Output;

        public ProductionStep[] ProductionSteps;

    }

    public class ProductionStep
    {
        public string Name;
        public string Input;
        public ProductionStepInteraction Interaction;
        public CompositeShape Shape;
        public CompositeTexture Texture;
        public EnumShapeMergeType ShapeMergeType;
        public Cuboidf[] SelectionBoxes;

    }

    public class ProductionStepInteraction
    {
        public string Type;
        public string Target;
        public float Duration;
    }

    public enum EnumShapeMergeType
    {
        Add,
        Replace
    }





    public class BEBehaviorManipulable : BlockEntityBehavior
    {
        public BEBehaviorManipulable(BlockEntity blockentity) : base(blockentity)
        {
        }


    }
}
