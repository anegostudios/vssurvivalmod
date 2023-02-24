using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        public Dictionary<string, string> pageCodes = new Dictionary<string, string>();
        public Dictionary<string, DepositVariant> depositsByCode = new Dictionary<string, DepositVariant>();

        GenRockStrataNew rockStrataGen;
        GenDeposits depositGen;
        ICoreServerAPI sapi;
        

        public virtual void OnLoaded(ICoreAPI api)
        {
            if (api.Side == EnumAppSide.Client) return;

            ICoreServerAPI sapi = api as ICoreServerAPI;
            this.sapi = sapi;

            rockStrataGen = new GenRockStrataNew();
            rockStrataGen.setApi(sapi);

            TyronThreadPool.QueueTask(() => {   // Initialise off-thread, instead of adding 1-2 seconds to game launch time
                rockStrataGen.initWorldGen();
                GenDeposits deposits = new GenDeposits();
                deposits.addHandbookAttributes = false;
                deposits.setApi(sapi);
                deposits.initAssets(sapi, false);
                deposits.initWorldGen();
                depositGen = deposits;
            });

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


        public void Dispose(ICoreAPI api)
        {
            if (sapi != null)
            {
                pageCodes = null;
                depositsByCode = null;
                rockStrataGen?.Dispose();
                rockStrataGen = null;
                depositGen?.Dispose();
                depositGen = null;

                sapi = null;
            }
        }


        // Tyrons Brute force way of getting the correct reading for a rock strata column
        public virtual int[] GetRockColumn(int posX, int posZ)
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

            if (depositGen == null)
            {
                // Wait for off-thread initialisation to finish (should be well finished by the time any player is able to use a ProPick, but let's make sure)
                int timeOutCount = 100;
                while (depositGen == null && timeOutCount-- > 0) Thread.Sleep(30);
                if (depositGen == null) throw new NullReferenceException("Prospecting Pick internal ore generator was not initialised, likely due to an exception during earlier off-thread worldgen");
            }
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
            IChunkBlocks IWorldChunk.Data => Blocks;
            IChunkLight IWorldChunk.Lighting => throw new NotImplementedException();
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

                public void ClearBlocks()
                {
                    for (int i = 0; i < blocks.Length; i++) blocks[i] = 0;
                }

                public void ClearBlocksAndPrepare()
                {
                    ClearBlocks();
                }

                public int GetBlockId(int index3d, int layer)
                {
                    return blocks[index3d];
                }

                public int GetBlockIdUnsafe(int index3d)
                {
                    return this[index3d];
                }

                public int GetFluid(int index3d)
                {
                    throw new NotImplementedException();
                }

                public void SetBlockAir(int index3d)
                {
                    this[index3d] = 0;
                }

                public void SetBlockBulk(int chunkIndex, int v, int mantleBlockId, int mantleBlockId1)
                {
                    throw new NotImplementedException();
                }

                public void SetBlockUnsafe(int index3d, int value)
                {
                    this[index3d] = value;
                }

                public void SetFluid(int index3d, int value)
                {
                    
                }
            }

            #region unused by rockstrata gen
            public Entity[] Entities => throw new NotImplementedException();
            public int EntitiesCount => throw new NotImplementedException();
            public BlockEntity[] BlockEntities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public HashSet<int> LightPositions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public string GameVersionCreated => throw new NotImplementedException();

            public bool Disposed => throw new NotImplementedException();

            public bool Empty { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
			Dictionary<BlockPos, BlockEntity> IWorldChunk.BlockEntities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

			public IChunkBlocks MaybeBlocks => throw new NotImplementedException();

            public bool NotAtEdge => throw new NotImplementedException();

            IChunkBlocks IWorldChunk.Blocks => throw new NotImplementedException();

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
			public bool Unpack_ReadOnly()
			{
				throw new NotImplementedException();
			}
			public int UnpackAndReadBlock(int index, int layer) 
			{
				throw new NotImplementedException();
			}
			public ushort Unpack_AndReadLight(int index)
			{
				throw new NotImplementedException();
			}
            public ushort Unpack_AndReadLight(int index, out int lightSat)
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

			public bool AddDecor(IBlockAccessor blockAccessor, BlockPos pos, int faceIndex, Block block)
			{
				throw new NotImplementedException();
			}

			public void RemoveDecor(int index3d, IWorldAccessor world, BlockPos pos)
			{
				throw new NotImplementedException();
			}

			public bool GetDecors(IBlockAccessor blockAccessor, BlockPos pos, Block[] result)
			{
				throw new NotImplementedException();
			}

			public Cuboidf[] AdjustSelectionBoxForDecor(IBlockAccessor blockAccessor, BlockPos pos, Cuboidf[] orig)
			{
				throw new NotImplementedException();
			}

			public void FinishLightDoubleBuffering()
			{
				throw new NotImplementedException();
			}

            public bool SetDecor(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing onFace)
            {
                throw new NotImplementedException();
            }

			public bool SetDecor(IBlockAccessor blockAccessor, Block block, BlockPos pos, int subPosition)
			{
				throw new NotImplementedException();
			}

			public void BreakDecor(IWorldAccessor world, BlockPos pos, BlockFacing side = null)
            {
                throw new NotImplementedException();
            }

            public void BreakAllDecorFast(IWorldAccessor world, BlockPos pos, int index3d)
            {
                throw new NotImplementedException();
            }

            public Block[] GetDecors(IBlockAccessor blockAccessor, BlockPos pos)
            {
                throw new NotImplementedException();
            }

			public void SetDecors(Dictionary<int, Block> newDecors)
			{
				throw new NotImplementedException();
			}
			
			public void BreakDecorPart(IWorldAccessor world, BlockPos pos, BlockFacing side, int faceAndSubposition)
            {
                throw new NotImplementedException();
            }

            public Block GetDecor(IBlockAccessor blockAccessor, BlockPos pos, int faceAndSubposition)
            {
                throw new NotImplementedException();
            }

            public void SetModdata<T>(string key, T data)
            {
                throw new NotImplementedException();
            }

            public T GetModdata<T>(string key)
            {
                throw new NotImplementedException();
            }

            public bool BreakDecor(IWorldAccessor world, BlockPos pos, BlockFacing side = null, int? faceAndSubposition = null)
            {
                throw new NotImplementedException();
            }

            public T GetModdata<T>(string key, T defaultValue = default(T))
            {
                throw new NotImplementedException();
            }

            public int GetLightAbsorptionAt(int index3d, BlockPos blockPos, IList<Block> blockTypes)
            {
                throw new NotImplementedException();
            }

            public Block GetLocalBlockAtBlockPos(IWorldAccessor world, int posX, int posY, int posZ, int layer)
            {
                throw new NotImplementedException();
            }

            public bool SetDecor(Block block, int index3d, BlockFacing onFace)
            {
                throw new NotImplementedException();
            }

            public bool SetDecor(Block block, int index3d, int decorIndex)
            {
                throw new NotImplementedException();
            }

            #endregion
        }
    }



    public class ItemProspectingPick : Item
    {
        ProPickWorkSpace ppws;


        SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

			ICoreClientAPI capi = api as ICoreClientAPI;

            toolModes = ObjectCacheUtil.GetOrCreate(api, "proPickToolModes", () =>
            {
				SkillItem[] modes;
				if (api.World.Config.GetString("propickNodeSearchRadius").ToInt() > 0)
				{
					modes = new SkillItem[2];
					modes[0] = new SkillItem() { Code = new AssetLocation("density"), Name = Lang.Get("Density Search Mode (Long range, chance based search)") };
					modes[1] = new SkillItem() { Code = new AssetLocation("node"), Name = Lang.Get("Node Search Mode (Short range, exact search)") };
					
				} else
				{
					modes = new SkillItem[1];
					modes[0] = new SkillItem() { Code = new AssetLocation("density"), Name = Lang.Get("Density Search Mode (Long range, chance based search)") };
				}

				if (capi != null)
				{
					modes[0].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/heatmap.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
					modes[0].TexturePremultipliedAlpha = false;
					if (modes.Length > 1)
					{
						modes[1].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/rocks.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
						modes[1].TexturePremultipliedAlpha = false;
					}
				}


				return modes;
            });


			if (api.Side == EnumAppSide.Server)
			{
				ppws = ObjectCacheUtil.GetOrCreate<ProPickWorkSpace>(api, "propickworkspace", () =>
				{
					ProPickWorkSpace ppws = new ProPickWorkSpace();
					ppws.OnLoaded(api);
					return ppws;
				});
			}
		}


		public override float OnBlockBreaking(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
		{
			float remain = base.OnBlockBreaking(player, blockSel, itemslot, remainingResistance, dt, counter);
			int toolMode = GetToolMode(itemslot, player, blockSel);

			// Mines half as fast
			if (toolMode == 1) remain = (remain + remainingResistance) / 2f;

			return remain;
		}


		public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier = 1)
        {
			int toolMode = GetToolMode(itemslot, (byEntity as EntityPlayer).Player, blockSel);
			int radius = api.World.Config.GetString("propickNodeSearchRadius").ToInt();
			int damage = 1;

			if (toolMode == 1 && radius > 0)
			{
				ProbeBlockNodeMode(world, byEntity, itemslot, blockSel, radius);
				damage = 2;
			} else
			{
				ProbeBlockDensityMode(world, byEntity, itemslot, blockSel);
			}


			if (DamagedBy != null && DamagedBy.Contains(EnumItemDamageSource.BlockBreaking))
			{
				DamageItem(world, byEntity, itemslot, damage);
			}

			return true;
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
			return toolModes;
		}

		public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
		{
			return Math.Min(toolModes.Length - 1, slot.Itemstack.Attributes.GetInt("toolMode"));
		}

		public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
		{
			slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
		}

		protected virtual void ProbeBlockNodeMode(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, int radius)
		{
			IPlayer byPlayer = null;
			if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

			Block block = world.BlockAccessor.GetBlock(blockSel.Position);
			block.OnBlockBroken(world, blockSel.Position, byPlayer, 0);

			if (!block.Code.Path.StartsWith("rock") && !block.Code.Path.StartsWith("ore")) return;

			IServerPlayer splr = byPlayer as IServerPlayer;
			if (splr == null) return;

			BlockPos pos = blockSel.Position.Copy();

			Dictionary<string, int> quantityFound = new Dictionary<string, int>();

			api.World.BlockAccessor.WalkBlocks(pos.AddCopy(radius, radius, radius), pos.AddCopy(-radius, -radius, -radius), (nblock, x, y, z) =>
			{
				if (nblock.BlockMaterial == EnumBlockMaterial.Ore && nblock.Variant.ContainsKey("type"))
				{
					string key = "ore-" + nblock.Variant["type"];

					int q = 0;
					quantityFound.TryGetValue(key, out q);

					quantityFound[key] = q + 1;
				}
			});

			var resultsOrderedDesc = quantityFound.OrderByDescending(val => val.Value).ToList();

			if (resultsOrderedDesc.Count == 0)
			{
				splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(splr.LanguageCode, "No ore node nearby"), EnumChatType.Notification);
			} else
			{
				splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(splr.LanguageCode, "Found the following ore nodes"), EnumChatType.Notification);
				foreach (var val in resultsOrderedDesc)
				{
					string orename = Lang.GetL(splr.LanguageCode, val.Key);

					string resultText = Lang.GetL(splr.LanguageCode, resultTextByQuantity(val.Value), Lang.Get(val.Key));

					splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(splr.LanguageCode, resultText, orename), EnumChatType.Notification);
				}
			}
		}

		protected virtual string resultTextByQuantity(int value)
		{
			if (value < 10)
			{
				return "propick-nodesearch-traceamount";
			}
			if (value < 20)
			{
				return "propick-nodesearch-smallamount";
			}
			if (value < 40)
			{
				return "propick-nodesearch-mediumamount";
			}
			if (value < 80)
			{
				return "propick-nodesearch-largeamount";
			}
			if (value < 160)
			{
				return "propick-nodesearch-verylargeamount";
			}

			return "propick-nodesearch-hugeamount";
		}

		protected virtual void ProbeBlockDensityMode(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel)
        {
            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = world.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
			float dropMul = 1f;
			if (block.BlockMaterial == EnumBlockMaterial.Ore || block.BlockMaterial == EnumBlockMaterial.Stone) dropMul = 0;

            block.OnBlockBroken(world, blockSel.Position, byPlayer, dropMul);

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

                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(splr.LanguageCode, "Ok, need 2 more samples"), EnumChatType.Notification);
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
                            splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.GetL(splr.LanguageCode, "Sample too far away from initial reading. Sampling around this point now, need 2 more samples."), EnumChatType.Notification);
                            attr.value = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
                            return;
                        }
                    }
                }

                if (requiredSamples > 0)
                {
                    int q = (int)Math.Ceiling(requiredSamples);
                    splr.SendMessage(GlobalConstants.InfoLogChatGroup, q > 1 ? Lang.GetL(splr.LanguageCode, "propick-xsamples", q) : Lang.GetL(splr.LanguageCode, "propick-1sample"), EnumChatType.Notification);
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


		protected virtual void PrintProbeResults(IWorldAccessor world, IServerPlayer splr, ItemSlot itemslot, BlockPos pos)
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
                IntDataMap2D map = val.Value;
                int noiseSize = map.InnerSize;
                    
                float posXInRegionOre = (float)lx / regsize * noiseSize;
                float posZInRegionOre = (float)lz / regsize * noiseSize;

                int oreDist = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                double ppt;
                double totalFactor;

				if (!ppws.depositsByCode.ContainsKey(val.Key))
				{
					string text = Lang.GetL(splr.LanguageCode, "propick-reading-unknown", val.Key);
					readouts.Add(new KeyValuePair<double, string>(1, text));
					continue;
				}

                ppws.depositsByCode[val.Key].GetPropickReading(pos, oreDist, blockColumn, out ppt, out totalFactor);

                string[] names = new string[] { "propick-density-verypoor", "propick-density-poor", "propick-density-decent", "propick-density-high", "propick-density-veryhigh", "propick-density-ultrahigh" };

                if (totalFactor > 0.025)
                {
                    string pageCode = ppws.pageCodes[val.Key];
                    string text = Lang.GetL(splr.LanguageCode, "propick-reading", Lang.GetL(splr.LanguageCode, names[(int)GameMath.Clamp(totalFactor * 7.5f, 0, 5)]), pageCode, Lang.GetL(splr.LanguageCode, "ore-" + val.Key), ppt.ToString("0.#"));
                    readouts.Add(new KeyValuePair<double, string>(totalFactor, text));
                } else if (totalFactor > 0.002)
                {
                    traceamounts.Add(val.Key);
                }
            }

            StringBuilder sb = new StringBuilder();

            if (readouts.Count >= 0 || traceamounts.Count > 0)
            {
                var elems = readouts.OrderByDescending(val => val.Key);
                
                sb.AppendLine(Lang.GetL(splr.LanguageCode, "propick-reading-title", readouts.Count));
                foreach (var elem in elems) sb.AppendLine(elem.Value);

                if (traceamounts.Count > 0)
                {
                    var sbTrace = new StringBuilder();
                    int i = 0;
                    foreach (var val in traceamounts)
                    {
                        if (i > 0) sbTrace.Append(", ");
                        string pageCode = ppws.pageCodes[val];
                        string text = string.Format("<a href=\"handbook://{0}\">{1}</a>", pageCode, Lang.GetL(splr.LanguageCode, "ore-" + val));
                        sbTrace.Append(text);
                        i++;
                    }

                    sb.Append(Lang.GetL(splr.LanguageCode, "Miniscule amounts of {0}", sbTrace.ToString()));
                    sb.AppendLine();
                }
            }
            else
            {
                sb.Append(Lang.GetL(splr.LanguageCode, "propick-noreading"));
            }

            splr.SendMessage(GlobalConstants.InfoLogChatGroup, sb.ToString(), EnumChatType.Notification);
        }


        

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);
        }



		public override void OnUnloaded(ICoreAPI api)
		{
            base.OnUnloaded(api);
            if (api is ICoreServerAPI sapi)
            {
                ppws?.Dispose(api);
                sapi.ObjectCache.Remove("propickworkspace");   // Unnecessary on registered items beyond the first, but does no harm
            }

            for (int i = 0; toolModes != null && i < toolModes.Length; i++)
			{
				toolModes[i]?.Dispose();
			}
		}

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] { 
                new WorldInteraction()
                {
                    ActionLangCode = Lang.Get("Change tool mode"),
                    HotKeyCodes = new string[] { "toolmodeselect" },
                    MouseButton = EnumMouseButton.None
                }
            };

        }
    }
}
