using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.ServerMods
{
    public class DepositBlock
    {
        public AssetLocation Code;
        public string Name;
        public string[] AllowedVariants;

        public Dictionary<AssetLocation, string[]> AllowedVariantsByInBlock;

        public bool IsWildCard => Code.Path.Contains("*");

        public int MaxGrade;

        
        internal ResolvedDepositBlock Resolve(string fileForLogging, ICoreServerAPI api, Block inblock, string key, string value)
        {
            AssetLocation oreLoc = Code.Clone();
            oreLoc.Path = oreLoc.Path.Replace("{" + key + "}", value);

            Block[] oreBlocks = api.World.SearchBlocks(oreLoc);

            if (oreBlocks.Length == 0)
            {
                api.World.Logger.Warning("Deposit {0}: No block with code/wildcard '{1}' was found (unresolved code: {2})", fileForLogging, oreLoc, Code);
            }

            if (AllowedVariants != null)
            {
                List<Block> filteredBlocks = new List<Block>();
                for (int i = 0; i < oreBlocks.Length; i++)
                {
                    if (WildcardUtil.Match(oreLoc, oreBlocks[i].Code, AllowedVariants))
                    {
                        filteredBlocks.Add(oreBlocks[i]);
                    }
                }
                
                if (filteredBlocks.Count == 0)
                {
                    api.World.Logger.Warning("Deposit {0}: AllowedVariants for {1} does not match any block! Please fix", fileForLogging, oreLoc);
                }

                oreBlocks = filteredBlocks.ToArray();

                MaxGrade = AllowedVariants.Length;
            }

            if (AllowedVariantsByInBlock != null)
            {
                List<Block> filteredBlocks = new List<Block>();
                for (int i = 0; i < oreBlocks.Length; i++)
                {
                    if (AllowedVariantsByInBlock[inblock.Code].Contains(WildcardUtil.GetWildcardValue(oreLoc, oreBlocks[i].Code)))
                    {
                        filteredBlocks.Add(oreBlocks[i]);
                    }
                }

                foreach (var val in AllowedVariantsByInBlock)
                {
                    MaxGrade = Math.Max(MaxGrade, val.Value.Length);
                }

                if (filteredBlocks.Count == 0)
                {
                    api.World.Logger.Warning("Deposit {0}: AllowedVariantsByInBlock for {1} does not match any block! Please fix", fileForLogging, oreLoc);
                }
                
                oreBlocks = filteredBlocks.ToArray();
            }

            return new ResolvedDepositBlock()
            {
                Blocks = oreBlocks
            };
        }
    }

    public class ResolvedDepositBlock
    {
        public Block[] Blocks;
    }

}
