using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.MathTools;

namespace Vintagestory.ServerMods.NoObf
{
    public class TreeGenTrunk : TreeGenBranch
    {
        public float dx = 0.5f;
        public float dz = 0.5f;
        public float probability = 1;

        public void InheritFrom(TreeGenTrunk treeGenTrunk, string[] skip)
        {
            FieldInfo[] fields = GetType().GetFields();
            foreach (FieldInfo field in fields)
            {
                if (!skip.Contains(field.Name) && field.Name != "inerhit")
                {
                    field.SetValue(this, treeGenTrunk.GetType().GetField(field.Name).GetValue(treeGenTrunk));
                }
            }
        }

        [OnDeserialized]
        internal new void OnDeserializedMethod(StreamingContext context)
        {
            if (angleVert == null) angleVert = NatFloat.createUniform(GameMath.PI, 0);
        }


    }
}

