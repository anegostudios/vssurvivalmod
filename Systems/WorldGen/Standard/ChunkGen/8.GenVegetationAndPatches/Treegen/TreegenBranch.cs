using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.NoObf
{
    public class Inheritance
    {
        public int from;
        public string[] skip;
    }

    public class TreeGenBranch
    {
        public Inheritance inherit;

        /// <summary>
        /// Thicknesss multiplier applied on the first sequence
        /// </summary>
        public float widthMultiplier = 1;

        /// <summary>
        /// Thickness loss per sequence
        /// </summary>
        public float widthloss = 0.05f;

        /// <summary>
        /// Randomise the widthloss from tree to tree - leads to taller/shorter, fatter/narrower variety
        /// </summary>
        public NatFloat randomWidthLoss = null;

        /// <summary>
        /// If this is less than 1, the width loss will reduce as it progresses -> leads to a taller more spindly tree
        /// </summary>
        public float widthlossCurve = 1f;

        /// <summary>
        /// Stop growing once size has gone below this value
        /// </summary>
        public NatFloat dieAt = NatFloat.createUniform(0.0002f, 0);

        /// <summary>
        /// Amount up vertical angle loss due to gravity
        /// </summary>
        public float gravityDrag = 0f;

        /// <summary>
        /// Vertical angle
        /// </summary>
        public NatFloat angleVert = null;

        /// <summary>
        /// Horizontal angle
        /// </summary>
        public NatFloat angleHori = NatFloat.createUniform(0, GameMath.PI);

        /// <summary>
        /// Own Thickness loss multiplier per sub branch
        /// </summary>
        public float branchWidthLossMul = 1f;


        /// <summary>
        /// Modification of vertical angle over distance
        /// </summary>
        public EvolvingNatFloat angleVertEvolve = EvolvingNatFloat.createIdentical(0f);

        /// <summary>
        /// Modification of horizontal angle over distance
        /// </summary>
        public EvolvingNatFloat angleHoriEvolve = EvolvingNatFloat.createIdentical(0f);


        public bool NoLogs;

        public NatFloat branchStart = NatFloat.createUniform(0.7f, 0f);

        public NatFloat branchSpacing = NatFloat.createUniform(0.3f, 0f);

        public NatFloat branchVerticalAngle = NatFloat.createUniform(0, GameMath.PI);

        public NatFloat branchHorizontalAngle = NatFloat.createUniform(0, GameMath.PI);


        /// <summary>
        /// Thickness of sub branches
        /// </summary>
        public NatFloat branchWidthMultiplier = NatFloat.createUniform(0, 0f);

        /// <summary>
        /// Thickness of sub branches. If null then for each branch event a new multiplier will be read from branchWidthMultiplier. Otherwise multiplier wil be read once and evolved using branchWidthMultiplierEvolve's algo.
        /// </summary>
        public EvolvingNatFloat branchWidthMultiplierEvolve = null;

        /// <summary>
        /// Amount of sub branches over distance (beginning of branch = sequence 0, end of branch = sequence 1000)
        /// </summary>            
        public NatFloat branchQuantity = NatFloat.createUniform(1, 0);

        /// <summary>
        /// Amount of sub branches over distance. If null then for each branch event a new quantity will be read from branchQuantity. Otherwise branchQuantity wil be read once and evolved using branchQuantityEvolve's algo.
        /// </summary>            
        public EvolvingNatFloat branchQuantityEvolve = null;

        public TreeGenBranch()
        {

        }

        public void InheritFrom(TreeGenBranch treeGenTrunk, string[] skip)
        {
            FieldInfo[] fields = GetType().GetFields();
            foreach (FieldInfo field in fields)
            {
                if (skip == null || !skip.Contains(field.Name))
                {
                    field.SetValue(this, treeGenTrunk.GetType().GetField(field.Name).GetValue(treeGenTrunk));
                }
            }
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            if (angleVert == null) angleVert = NatFloat.createUniform(0, 0);
        }


        public float WidthLoss(Random rand)
        {
            return randomWidthLoss != null ? randomWidthLoss.nextFloat(1f, rand) : widthloss;
        }


        public virtual int getBlockId(float width, TreeGenBlocks blocks, TreeGen gen, int treeSubType)
        {
            return
                width < 0.3f || NoLogs ? blocks.GetLeaves(width, treeSubType) :
                    (blocks.otherLogBlockCode != null && gen.TriggerRandomOtherBlock() ? blocks.otherLogBlockId : blocks.logBlockId)
            ;
        }
    }
}
