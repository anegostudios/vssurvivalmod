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
        //public Dictionary<string, float> absAvgQuantity = new Dictionary<string, float>();
        public Dictionary<string, string> pageCodes = new Dictionary<string, string>();

        public Dictionary<string, DepositVariant> depositsByCode = new Dictionary<string, DepositVariant>();

        GenRockStrataNew rockStrataGen;
        GenDeposits depositGen;
        ICoreServerAPI sapi;
        

        public void OnLoaded(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client) return;

            ICoreServerAPI sapi = api as ICoreServerAPI;
            this.sapi = sapi;

            rockStrataGen = new GenRockStrataNew();
            rockStrataGen.setApi(sapi);
            rockStrataGen.initWorldGen();

            depositGen = new GenDeposits();
            depositGen.setApi(sapi);
            depositGen.initWorldGen(false);


            sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () =>
            {
                DepositVariant[] deposits = api.ModLoader.GetModSystem<GenDeposits>()?.Deposits;
                if (deposits == null) return;

                for (int i = 0; i < deposits.Length; i++)
                {
                    DepositVariant variant = deposits[i];

                    if (variant.WithOreMap)
                    {
                        pageCodes[variant.Code] = variant.HandbookPageCode;
                        depositsByCode[variant.Code] = variant;
                        if (variant.HandbookPageCode == null)
                        {
                            api.World.Logger.Warning("Deposit " + variant.Code + " has no handbook page code. Links created by the prospecting pick will not work without it.");
                        }

                    }

                    for (int k = 0; variant.ChildDeposits != null && k < variant.ChildDeposits.Length; k++)
                    {
                        DepositVariant childVariant = variant.ChildDeposits[k];
                        if (!childVariant.WithOreMap) continue;

                        if (childVariant.HandbookPageCode == null)
                        {
                            api.World.Logger.Warning("Child Deposit " + childVariant.Code + " of deposit " + variant.Code + " has no handbook page code. Links created by the prospecting pick will not work without it.");
                        }

                        pageCodes[childVariant.Code] = childVariant.HandbookPageCode;
                        depositsByCode[childVariant.Code] = childVariant;
                    }
                }
            });
        }

        // Tyrons Brute force way of getting the correct reading for a rock strata column
        public int[] GetRockColumn(int posX, int posZ)
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
                chunks[chunkY] = new DummyChunk(chunksize);
                chunks[chunkY].MapChunk = mapchunk;
                chunks[chunkY].chunkY = chunkY;
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

            depositGen.GeneratePartial(chunks, chunkX, chunkZ, 0, 0);


            int[] rockColumn = new int[surfaceY];

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
            IChunkBlocks IWorldChunk.Blocks => Blocks;
            public IChunkBlocks Blocks;

            public DummyChunk(int chunksize)
            {
                Blocks = new DummyChunkData(chunksize);
            }
 
            public class DummyChunkData : IChunkBlocks
            {
                public int[] blocks;
                

                public DummyChunkData(int chunksize)
                {
                    blocks = new int[chunksize * chunksize * chunksize];
                }

                public int this[int index3d] { get => blocks[index3d]; set => blocks[index3d] = value; }

                public int Length => blocks.Length;


            }

            #region unused by rockstrata gen
            public ushort[] Light { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public byte[] LightSat { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public Entity[] Entities => throw new NotImplementedException();
            public int EntitiesCount => throw new NotImplementedException();
            public BlockEntity[] BlockEntities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public HashSet<int> LightPositions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public string GameVersionCreated => throw new NotImplementedException();

            public bool Disposed => throw new NotImplementedException();

            public bool Empty { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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

            public Block GetLocalBlockAtBlockPos(IWorldAccessor world, BlockPos position)
            {
                throw new NotImplementedException();
            }

            public void MarkFresh()
            {
                throw new NotImplementedException();
            }

            public BlockEntity GetLocalBlockEntityAtBlockPos(BlockPos pos)
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

            if (splr.WorldData.CurrentGameMode == EnumGameMode.Creative) {
                PrintProbeResults(world, splr, itemslot, blockSel.Position);
                return;
            }
            
            IntArrayAttribute attr = itemslot.Itemstack.Attributes["probePositions"] as IntArrayAttribute;

            if ((attr == null || attr.value == null || attr.value.Length == 0))
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
            
            IMapRegion reg = world.BlockAccessor.GetMapRegion(pos.X / regsize, pos.Z / regsize);
            int lx = pos.X % regsize;
            int lz = pos.Z % regsize;

            pos = pos.Copy();
            pos.Y = world.BlockAccessor.GetTerrainMapheightAt(pos);

            int[] blockColumn = ppws.GetRockColumn(pos.X, pos.Z);

            List<KeyValuePair<double, string>> readouts = new List<KeyValuePair<double, string>>();

            List<string> traceamounts = new List<string>();

            foreach (var val in reg.OreMaps)
            {
                IntMap map = val.Value;
                int noiseSize = map.InnerSize;
                    
                float posXInRegionOre = (float)lx / regsize * noiseSize;
                float posZInRegionOre = (float)lz / regsize * noiseSize;

                int oreDist = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                double ppt;
                double totalFactor;
                ppws.depositsByCode[val.Key].GetPropickReading(pos, oreDist, blockColumn, out ppt, out totalFactor);

                string[] names = new string[] { "propick-density-verypoor", "propick-density-poor", "propick-density-decent", "propick-density-high", "propick-density-veryhigh", "propick-density-ultrahigh" };

                if (totalFactor > 0.025)
                {
                    string pageCode = ppws.pageCodes[val.Key];
                    string text = Lang.Get("propick-reading", Lang.Get(names[(int)GameMath.Clamp(totalFactor * 7.5f, 0, 5)]), pageCode, Lang.Get("ore-"+val.Key), ppt.ToString("0.#"));
                    readouts.Add(new KeyValuePair<double, string>(totalFactor, text));
                } else if (totalFactor > 0.002)
                {
                    traceamounts.Add(val.Key);
                }
            }

            StringBuilder sb = new StringBuilder();

            IServerPlayer splr = byPlayer as IServerPlayer;
            if (readouts.Count >= 0 || traceamounts.Count > 0)
            {
                var elems = readouts.OrderByDescending(val => val.Key);
                
                sb.AppendLine(Lang.Get("propick-reading-title", readouts.Count));
                foreach (var elem in elems) sb.AppendLine(elem.Value);

                if (traceamounts.Count > 0)
                {
                    sb.Append(Lang.Get("Miniscule amounts of "));
                    int i = 0;
                    foreach (var val in traceamounts)
                    {
                        if (i > 0) sb.Append(", ");
                        string pageCode = ppws.pageCodes[val];
                        string text = Lang.Get("<a href=\"handbook://{0}\">{1}</a>", pageCode, Lang.Get("ore-" + val));
                        sb.Append(text);
                        i++;
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.Append(Lang.Get("propick-noreading"));
            }

            splr.SendMessage(GlobalConstants.InfoLogChatGroup, sb.ToString(), EnumChatType.Notification);
        }


        

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);
        }

    }
}
