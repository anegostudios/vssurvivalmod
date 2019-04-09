using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class ItemProspectingPick : Item
    {
        Dictionary<string, float> absAvgQuantity = new Dictionary<string, float>();

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client) return;

            ((ICoreServerAPI)api).Event.ServerRunPhase(EnumServerRunPhase.RunGame, () =>
            {

                DepositVariant[] deposits = api.ModLoader.GetModSystem<GenDeposits>()?.Deposits;
                if (deposits == null) return;

                for (int i = 0; i < deposits.Length; i++)
                {
                    DepositVariant variant = deposits[i];

                    if (variant.WithOreMap)
                    {
                        if (absAvgQuantity.ContainsKey(variant.Code))
                        {
                            absAvgQuantity[variant.Code] += variant.GetAbsAvgQuantity();
                        } else
                        {
                            absAvgQuantity[variant.Code] = variant.GetAbsAvgQuantity();
                        }            
                    }

                    

                    for (int k = 0; variant.ChildDeposits != null && k < variant.ChildDeposits.Length; k++)
                    {
                        DepositVariant childVariant = variant.ChildDeposits[k];
                        if (!childVariant.WithOreMap) continue;
                        absAvgQuantity[childVariant.Code] = childVariant.GetAbsAvgQuantity();
                    }
                }
            });
        }


        public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel)
        {
            ProbeBlock(world, byEntity, itemslot, blockSel);

            if (DamagedBy != null && DamagedBy.Contains(EnumItemDamageSource.BlockBreaking))
            {
                DamageItem(world, byEntity, itemslot);
            }

            return true;
        }


        void ProbeBlock(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel)
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            block.OnBlockBroken(world, blockSel.Position, byPlayer, 0);

            if (!block.Code.Path.StartsWith("rock") && !block.Code.Path.StartsWith("ore")) return;

            IServerPlayer splr = byPlayer as IServerPlayer;

            if (splr != null)
            {
                IntArrayAttribute attr = itemslot.Itemstack.Attributes["probePositions"] as IntArrayAttribute;

                if (attr == null || attr.value == null || attr.value.Length == 0)
                {
                    itemslot.Itemstack.Attributes["probePositions"] = attr = new IntArrayAttribute();
                    attr.AddInt(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

                    splr.SendMessage(GlobalConstants.CurrentChatGroup, "Ok, need 2 more samples", EnumChatType.Notification);
                }
                else
                {
                    float requiredSamples = 2;

                    attr.AddInt(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

                    int[] vals = attr.value;
                    for (int i = 0; i < vals.Length; i += 3)
                    {
                        int x = vals[i];
                        int z = vals[i + 2];

                        float mindist = 99;


                        for (int j = i + 3; j < vals.Length; j += 3)
                        {
                            int dx = x - vals[j];
                            int dz = z - vals[j + 2];

                            mindist = Math.Min(mindist, GameMath.Sqrt(dx * dx + dz * dz));
                        }

                        if (i + 3 < vals.Length)
                        {
                            requiredSamples -= GameMath.Clamp(mindist * mindist, 0, 16) / 16;

                            if (mindist > 16)
                            {
                                splr.SendMessage(GlobalConstants.CurrentChatGroup, "Sample too far away from initial reading. Sampling around this point now, need 2 more samples.", EnumChatType.Notification);
                                attr.value = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
                                return;
                            }
                        }
                    }

                    if (requiredSamples > 0)
                    {
                        int q = (int)Math.Ceiling(requiredSamples);
                        splr.SendMessage(GlobalConstants.CurrentChatGroup, "Ok, need " + q + " more " + (q == 1 ? "sample" : "samples"), EnumChatType.Notification);
                    }
                    else
                    {
                        int startX = vals[0];
                        int startY = vals[1];
                        int startZ = vals[2];
                        PrintProbeResults(world, splr, itemslot, new BlockPos(startX, startY, startZ));
                        attr.value = new int[0];
                    }
                }
            }
        }


        void PrintProbeResults(IWorldAccessor world, IServerPlayer byPlayer, ItemSlot itemslot, BlockPos pos)
        {
            DepositVariant[] deposits = api.ModLoader.GetModSystem<GenDeposits>()?.Deposits;
            if (deposits == null) return;

            IBlockAccessor blockAccess = world.BlockAccessor;
            int chunksize = blockAccess.ChunkSize;
            int regsize = blockAccess.RegionSize;

            int mapheight = blockAccess.GetTerrainMapheightAt(pos);
            int qchunkblocks = mapheight * chunksize * chunksize;

            IMapRegion reg = world.BlockAccessor.GetMapRegion(pos.X / regsize, pos.Z / regsize);
            int lx = pos.X % regsize;
            int lz = pos.Z % regsize;

            StringBuilder outtext = new StringBuilder();
            int found = 0;

            ushort[] blockColumn = loadBlockColumn(world, pos);

            foreach (var val in reg.OreMaps)
            {
                IntMap map = val.Value;
                int noiseSize = map.InnerSize;
                    
                float posXInRegionOre = (float)lx / regsize * noiseSize;
                float posZInRegionOre = (float)lz / regsize * noiseSize;

                int oreDist = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                double absAvgQuantity = this.absAvgQuantity[val.Key];
                double oreMapFactor = (oreDist & 0xff) / 255.0;
                double rockFactor = oreBearingBlockQuantityRelative(val.Key, deposits, blockColumn);
                double totalFactor = oreMapFactor * rockFactor;

                double quantityOres = totalFactor * absAvgQuantity;
                
                //world.Logger.Notification(val.Key + "rock factor: " + rockFactor);

                double relq = quantityOres / qchunkblocks;
                double ppt = relq * 1000;
                string[] names = new string[] { "Very poor density", "Poor density", "Decent density", "High density", "Very high density", "Ultra high density" };

                if (totalFactor > 0.05)
                {
                    if (found > 0) outtext.Append("\n");
                    outtext.Append(string.Format("{1}: {2} ({0}‰)", ppt.ToString("0.#"), val.Key.Substring(0,1).ToUpper() + val.Key.Substring(1), names[(int)GameMath.Clamp(totalFactor * 5, 0, 5)]));
                    found++;
                }
            }

            IServerPlayer splr = byPlayer as IServerPlayer;
            if (outtext.Length == 0) outtext.Append("No significant resources here.");
            else outtext.Insert(0, "Found "+found+" traces of ore\n");
            splr.SendMessage(GlobalConstants.CurrentChatGroup,  outtext.ToString(), EnumChatType.Notification);
        }

        private ushort[] loadBlockColumn(IWorldAccessor world, BlockPos pos)
        {
            List<ushort> blocks = new List<ushort>();

            int maxy = world.BlockAccessor.GetRainMapHeightAt(pos);
            for (int y = 0; y < maxy; y++)
            {
                blocks.Add(world.BlockAccessor.GetBlock(pos.X, y, pos.Z).BlockId);
            }

            return blocks.ToArray();
        }

        private double oreBearingBlockQuantityRelative(string oreCode, DepositVariant[] deposits, ushort[] blockColumn)
        {
            HashSet<ushort> oreBearingBlocks = new HashSet<ushort>();

            for (int i = 0; i < deposits.Length; i++)
            {
                DepositVariant deposit = deposits[i];
                if (deposit.Code == oreCode)
                {
                    ushort[] blocks = deposit.GeneratorInst.GetBearingBlocks();
                    if (blocks == null) return 1;

                    foreach (var val in blocks) oreBearingBlocks.Add(val);
                }
            }

            int q = 0;
            for (int i = 0; i < blockColumn.Length; i++)
            {
                if (oreBearingBlocks.Contains(blockColumn[i])) q++;
            }

            return (double)q / blockColumn.Length;
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);
        }

    }
}
