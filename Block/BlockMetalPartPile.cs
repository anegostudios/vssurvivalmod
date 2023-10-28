using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockMetalPartPile : Block
    {
        Dictionary<string, DropChanceEntry> dropChances;

        class DropChanceEntry
        {
            public double ScrapChance;

            public Dictionary<int, double> ScrapQuantityChances = new Dictionary<int, double>();
            public Dictionary<int, double> PartQuantityChances = new Dictionary<int, double>();
        }


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            dropChances = new Dictionary<string, DropChanceEntry>()
            {
                { "tiny", new DropChanceEntry() {
                    ScrapChance = 0.8,
                    ScrapQuantityChances = new Dictionary<int, double>()
                    {
                        { 1, 0.9 },
                        { 2, 0.1 }
                    },
                    PartQuantityChances = new Dictionary<int, double>()
                    {
                        { 1, 0.9 },
                        { 2, 0.1 }
                    }
                } },

                { "small", new DropChanceEntry() {
                    ScrapChance = 0.6,
                    ScrapQuantityChances = new Dictionary<int, double>()
                    {
                        { 1, 0.8 },
                        { 2, 0.2 }
                    },
                    PartQuantityChances = new Dictionary<int, double>()
                    {
                        { 1, 0.1 },
                        { 2, 0.8 },
                        { 3, 0.1 }
                    }
                } },

                { "medium", new DropChanceEntry() {
                    ScrapChance = 0.4,
                    ScrapQuantityChances = new Dictionary<int, double>()
                    {
                        { 2, 0.8 },
                        { 3, 0.2 }
                    },
                    PartQuantityChances = new Dictionary<int, double>()
                    {
                        { 2, 0.1 },
                        { 3, 0.8 },
                        { 4, 0.1 }
                    }
                } },

                { "large", new DropChanceEntry() {
                    ScrapChance = 0.2,
                    ScrapQuantityChances = new Dictionary<int, double>()
                    {
                        { 2, 0.2 },
                        { 3, 0.8 }
                    },
                    PartQuantityChances = new Dictionary<int, double>()
                    {
                        { 3, 0.1 },
                        { 4, 0.8 },
                        { 5, 0.1 }
                    }
                } },

            };
        }

        public string Size()
        {
            return Variant["size"];
        }

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            Random rand = world.Rand;

            DropChanceEntry entry = dropChances[Size()];
            
            int index = rand.NextDouble() < entry.ScrapChance ? 0 : 1;

            float chance = (byPlayer?.Entity.Stats.GetBlended("rustyGearDropRate") ?? 0) - 1;
            if (chance > 0 && rand.NextDouble() < chance)
            {
                index = 2;
            }

            ItemStack drop = Drops[index].GetNextItemStack(dropQuantityMultiplier);
            if (drop == null) return new ItemStack[0];

            double rndVal = rand.NextDouble();
            Dictionary<int, double> quantityDict = index==0 ? entry.ScrapQuantityChances : entry.PartQuantityChances;
            
            foreach (var val in quantityDict)
            {
                rndVal -= val.Value;
                if (rndVal <= 0)
                {
                    drop.StackSize = val.Key;
                    break;
                }
            }

            return new ItemStack[] { drop };
        }

    }
}
