using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.ServerMods;

namespace Vintagestory.API.Common
{
    [JsonObject(MemberSerialization.OptIn)]
    public class RockStrataConfig : WorldProperty<RockStratum>
    {
        public Dictionary<EnumRockGroup, float> MaxThicknessPerGroup = new Dictionary<EnumRockGroup, float>();
    }
    
    public enum EnumStratumGenDir
    {
        BottomUp,
        TopDown
    }

    public class RockStratum
    {
        public AssetLocation BlockCode;
        public string Generator;
        public double[] Amplitudes;
        public double[] Frequencies;
        public double[] Thresholds;
        public EnumStratumGenDir GenDir = EnumStratumGenDir.BottomUp;
        public bool IsDeposit = false;
        public EnumRockGroup RockGroup;

        public int BlockId;

        public void Init(IWorldAccessor worldForResolve)
        {
            Block block = worldForResolve.GetBlock(BlockCode);
            if (block == null)
            {
                worldForResolve.Logger.Warning("Rock stratum with block code {0} - no such block was loaded. Will generate air instead!", BlockCode);
                return;
            }
            BlockId = block.BlockId;
        }
    }
}
