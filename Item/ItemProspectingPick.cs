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
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

namespace Vintagestory.GameContent
{
    public class ProPickWorkSpace
    {
        public Dictionary<string, float> absAvgQuantity = new Dictionary<string, float>();
        public Dictionary<string, string> pageCodes = new Dictionary<string, string>();

        GenRockStrataNew rockStrataGen;
        ICoreServerAPI sapi;
        
        public void OnLoaded(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client) return;

            ICoreServerAPI sapi = api as ICoreServerAPI;
            this.sapi = sapi;

            rockStrataGen = new GenRockStrataNew();
            rockStrataGen.setApi(sapi);
            rockStrataGen.initWorldGen();

            sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () =>
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
                        }
                        else
                        {
                            absAvgQuantity[variant.Code] = variant.GetAbsAvgQuantity();
                            pageCodes[variant.Code] = variant.HandbookPageCode;
                        }
                    }



                    for (int k = 0; variant.ChildDeposits != null && k < variant.ChildDeposits.Length; k++)
                    {
                        DepositVariant childVariant = variant.ChildDeposits[k];
                        if (!childVariant.WithOreMap) continue;

                        absAvgQuantity[childVariant.Code] = childVariant.GetAbsAvgQuantity();
                        pageCodes[childVariant.Code] = childVariant.HandbookPageCode;
                    }
                }
            });
        }

        // Tyrons Brute force way of getting the correct reading for a rock strata column
        public ushort[] GetRockColumn(int posX, int posZ)
        {
            int chunksize = sapi.World.BlockAccessor.ChunkSize;
            DummyChunk[] chunks = new DummyChunk[sapi.World.BlockAccessor.MapSizeY / chunksize];
            int chunkX = posX / chunksize;
            int chunkZ = posZ / chunksize;
            int lx = posX % chunksize;
            int lz = posZ % chunksize;

            IMapChunk mapchunk = sapi.World.BlockAccessor.GetMapChunk(new Vec2i(chunkX, chunkZ));

            for (int chunkY = 0; chunkY < chunks.Length; chunkY++)
            {
                chunks[chunkY] = new DummyChunk();
                chunks[chunkY].MapChunk = mapchunk;
                chunks[chunkY].chunkY = chunkY;
                chunks[chunkY].Blocks = new ushort[chunksize * chunksize * chunksize];
            }

            int surfaceY = mapchunk.WorldGenTerrainHeightMap[lz * chunksize + lx];
            for (int y = 0; y < surfaceY; y++)
            {
                int chunkY = y / chunksize;
                int lY = y - chunkY * chunksize;
                int localIndex3D = (chunksize * lY + lz) * chunksize + lx;

                chunks[chunkY].Blocks[localIndex3D] = rockStrataGen.rockBlockId;
            }

            rockStrataGen.preLoad(chunks, chunkX, chunkZ);
            rockStrataGen.genBlockColumn(chunks, chunkX, chunkZ, lx, lz);

            ushort[] rockColumn = new ushort[surfaceY];

            for (int y = 0; y < surfaceY; y++)
            {
                int chunkY = y / chunksize;
                int lY = y - chunkY * chunksize;
                int localIndex3D = (chunksize * lY + lz) * chunksize + lx;

                rockColumn[y] = chunks[chunkY].Blocks[localIndex3D];
            }

            return rockColumn;
        }

        public class DummyChunk : IServerChunk
        {
            public int chunkY;
            public IMapChunk MapChunk { get; set; }
            public ushort[] Blocks { get; set; }

            #region unused by rockstrata gen
            public ushort[] Light { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public byte[] LightSat { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public Entity[] Entities => throw new NotImplementedException();
            public int EntitiesCount => throw new NotImplementedException();
            public BlockEntity[] BlockEntities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public HashSet<int> LightPositions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public void AddEntity(Entity entity)
            {
                throw new NotImplementedException();
            }
            public byte[] GetModdata(string key)
            {
                throw new NotImplementedException();
            }
            public byte[] GetServerModdata(string key)
            {
                throw new NotImplementedException();
            }
            public void MarkModified()
            {
                throw new NotImplementedException();
            }
            public bool RemoveEntity(long entityId)
            {
                throw new NotImplementedException();
            }
            public void RemoveModdata(string key)
            {
                throw new NotImplementedException();
            }
            public void SetModdata(string key, byte[] data)
            {
                throw new NotImplementedException();
            }
            public void SetServerModdata(string key, byte[] data)
            {
                throw new NotImplementedException();
            }
            public void Unpack()
            {
                throw new NotImplementedException();
            }
            #endregion
        }
    }



    public class ItemProspectingPick : Item
    {
        ProPickWorkSpace ppws;
            
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side == EnumAppSide.Client) return;

            ppws = ObjectCacheUtil.GetOrCreate<ProPickWorkSpace>(api, "propickworkspace", () =>
            {
                ProPickWorkSpace ppws = new ProPickWorkSpace();
                ppws.OnLoaded(api);
                return ppws;
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

            if (splr == null) return;

            
            IntArrayAttribute attr = itemslot.Itemstack.Attributes["probePositions"] as IntArrayAttribute;

            if (attr == null || attr.value == null || attr.value.Length == 0)
            {
                itemslot.Itemstack.Attributes["probePositions"] = attr = new IntArrayAttribute();
                attr.AddInt(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

                splr.SendMessage(GlobalConstants.InfoLogChatGroup, "Ok, need 2 more samples", EnumChatType.Notification);
            }
            else
            {
                float requiredSamples = 2;

                attr.AddInt(blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z);

                int[] vals = attr.value;
                for (int i = 0; i < vals.Length; i += 3)
                {
                    int x = vals[i];
                    int y = vals[i + 1];
                    int z = vals[i + 2];

                    float mindist = 99;


                    for (int j = i + 3; j < vals.Length; j += 3)
                    {
                        int dx = x - vals[j];
                        int dy = y - vals[j + 1];
                        int dz = z - vals[j + 2];

                        mindist = Math.Min(mindist, GameMath.Sqrt(dx * dx + dy * dy + dz * dz));
                    }

                    if (i + 3 < vals.Length)
                    {
                        requiredSamples -= GameMath.Clamp(mindist * mindist, 3, 16) / 16;

                        if (mindist > 20)
                        {
                            splr.SendMessage(GlobalConstants.InfoLogChatGroup, "Sample too far away from initial reading. Sampling around this point now, need 2 more samples.", EnumChatType.Notification);
                            attr.value = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
                            return;
                        }
                    }
                }

                if (requiredSamples > 0)
                {
                    int q = (int)Math.Ceiling(requiredSamples);
                    splr.SendMessage(GlobalConstants.InfoLogChatGroup, q > 1 ? Lang.Get("propick-xsamples", q) : Lang.Get("propick-1sample"), EnumChatType.Notification);
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

            ushort[] blockColumn = ppws.GetRockColumn(pos.X, pos.Z);

            List<KeyValuePair<double, string>> readouts = new List<KeyValuePair<double, string>>();

            foreach (var val in reg.OreMaps)
            {
                IntMap map = val.Value;
                int noiseSize = map.InnerSize;
                    
                float posXInRegionOre = (float)lx / regsize * noiseSize;
                float posZInRegionOre = (float)lz / regsize * noiseSize;

                int oreDist = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                double absAvgQuantity = ppws.absAvgQuantity[val.Key];
                double oreMapFactor = (oreDist & 0xff) / 255.0;
                double rockFactor = oreBearingBlockQuantityRelative(val.Key, deposits, blockColumn);
                double totalFactor = oreMapFactor * rockFactor;

                double quantityOres = totalFactor * absAvgQuantity;
                
                //world.Logger.Notification(val.Key + "rock factor: " + rockFactor);

                double relq = quantityOres / qchunkblocks;
                double ppt = relq * 1000;
                string[] names = new string[] { "propick-density-verypoor", "propick-density-poor", "propick-density-decent", "propick-density-high", "propick-density-veryhigh", "propick-density-ultrahigh" };

                if (totalFactor > 0.025)
                {
                    string pageCode = ppws.pageCodes[val.Key];
                    string text = Lang.Get("propick-reading", Lang.Get(names[(int)GameMath.Clamp(totalFactor * 7.5f, 0, 5)]), pageCode, Lang.Get("ore-"+val.Key), ppt.ToString("0.#"));
                    readouts.Add(new KeyValuePair<double, string>(totalFactor, text));
                }
            }

            string outtext;
            IServerPlayer splr = byPlayer as IServerPlayer;
            if (readouts.Count == 0) outtext = Lang.Get("propick-noreading");
            else
            {
                var elems = readouts.OrderByDescending(val => val.Key);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(Lang.Get("propick-reading-title", readouts.Count));
                foreach (var elem in elems) sb.AppendLine(elem.Value);

                outtext = sb.ToString();
                
            }
            splr.SendMessage(GlobalConstants.InfoLogChatGroup, outtext.ToString(), EnumChatType.Notification);
        }



        private double oreBearingBlockQuantityRelative(string oreCode, DepositVariant[] deposits, ushort[] blockColumn)
        {
            HashSet<ushort> oreBearingBlocks = new HashSet<ushort>();

            DepositVariant deposit = getDepositByOreMapCode(oreCode, deposits);
            if (deposit == null) return 0;

            if (deposit.parentDeposit != null) deposit = deposit.parentDeposit;
            

            ushort[] blocks = deposit.GeneratorInst.GetBearingBlocks();
            if (blocks == null) return 1;

            foreach (var val in blocks) oreBearingBlocks.Add(val);

            int q = 0;
            for (int i = 0; i < blockColumn.Length; i++)
            {
                if (oreBearingBlocks.Contains(blockColumn[i])) q++;
            }

            return (double)q / blockColumn.Length;
        }


        public DepositVariant getDepositByOreMapCode(string oreCode, DepositVariant[] deposits)
        {
            for (int i = 0; i < deposits.Length; i++)
            {
                DepositVariant deposit = deposits[i];

                if (deposit.Code == oreCode)
                {
                    return deposit;
                }

                if (deposit.ChildDeposits != null)
                {
                    foreach (var val in deposit.ChildDeposits)
                    {
                        if (val.Code == oreCode) return val;
                    }
                }
            }

            return null;
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);
        }

    }
}
