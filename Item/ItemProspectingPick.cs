using Cairo;
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
			Dictionary<BlockPos, BlockEntity> IWorldChunk.BlockEntities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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


        SkillItem[] toolModes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api is ICoreClientAPI capi)
            {
                toolModes = ObjectCacheUtil.GetOrCreate(api, "proPickToolModes", () =>
                {
					SkillItem[] modes;
					if (api.World.Config.GetString("propickNodeSearchRadius").ToInt() > 0)
					{
						modes = new SkillItem[2];
						modes[0] = new SkillItem() { Code = new AssetLocation("density"), Name = Lang.Get("Density Search Mode (Long range, chance based search)") }.WithIcon(capi, Drawheatmap_svg);
						modes[1] = new SkillItem() { Code = new AssetLocation("node"), Name = Lang.Get("Node Search Mode (Short range, exact search)") }.WithIcon(capi, DrawWaypointRocks);
					} else
					{
						modes = new SkillItem[1];
						modes[0] = new SkillItem() { Code = new AssetLocation("density"), Name = Lang.Get("Density Search Mode (Long range, chance based search)") }.WithIcon(capi, Drawheatmap_svg);
					}

                    return modes;
                });
            }


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


		public override bool OnBlockBrokenWith(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel)
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
			return slot.Itemstack.Attributes.GetInt("toolMode");
		}

		public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
		{
			slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
		}

		void ProbeBlockNodeMode(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel, int radius)
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

			api.World.BlockAccessor.WalkBlocks(pos.AddCopy(radius, radius, radius), pos.AddCopy(-radius, -radius, -radius), (nblock, bp) =>
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
				splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("No ore node nearby"), EnumChatType.Notification);
			} else
			{
				splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("Found the following ore nodes"), EnumChatType.Notification);
				foreach (var val in resultsOrderedDesc)
				{
					string orename = Lang.Get(val.Key);

					string resultText = Lang.Get(resultTextByQuantity(val.Value), Lang.Get(val.Key));

					splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get(resultText, orename), EnumChatType.Notification);
				}
			}
		}

		private string resultTextByQuantity(int value)
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

		void ProbeBlockDensityMode(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, BlockSelection blockSel)
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

                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("Ok, need 2 more samples"), EnumChatType.Notification);
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
                            splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("Sample too far away from initial reading. Sampling around this point now, need 2 more samples."), EnumChatType.Notification);
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
                IntDataMap2D map = val.Value;
                int noiseSize = map.InnerSize;
                    
                float posXInRegionOre = (float)lx / regsize * noiseSize;
                float posZInRegionOre = (float)lz / regsize * noiseSize;

                int oreDist = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);

                double ppt;
                double totalFactor;

				if (!ppws.depositsByCode.ContainsKey(val.Key))
				{
					string text = Lang.Get("propick-reading-unknown", val.Key);
					readouts.Add(new KeyValuePair<double, string>(1, text));
					continue;
				}

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
                    var sbTrace = new StringBuilder();
                    int i = 0;
                    foreach (var val in traceamounts)
                    {
                        if (i > 0) sbTrace.Append(", ");
                        string pageCode = ppws.pageCodes[val];
                        string text = Lang.Get("<a href=\"handbook://{0}\">{1}</a>", pageCode, Lang.Get("ore-" + val));
                        sbTrace.Append(text);
                        i++;
                    }

                    sb.Append(Lang.Get("Miniscule amounts of {0}", sbTrace.ToString()));
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



		public void Drawheatmap_svg(Context cr, int x, int y, float width, float height, double[] rgba)
		{
			Pattern pattern = null;
			Matrix matrix = cr.Matrix;

			cr.Save();
			float w = 232;
			float h = 161;
			float scale = Math.Min(width / w, height / h);
			matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
			matrix.Scale(scale, scale);
			cr.Matrix = matrix;

			cr.Operator = Operator.Over;
			cr.LineWidth = 8;
			cr.MiterLimit = 4;
			cr.LineCap = LineCap.Butt;
			cr.LineJoin = LineJoin.Miter;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(81.367188, 15.730469);
			cr.CurveTo(75.117188, 15.730469, 68.867188, 15.730469, 62.617188, 15.730469);
			cr.CurveTo(60.832031, 15.730469, 58.953125, 15.167969, 57.261719, 15.730469);
			cr.CurveTo(55.734375, 16.242188, 54.804688, 18.019531, 53.242188, 18.410156);
			cr.CurveTo(51.078125, 18.953125, 48.734375, 17.972656, 46.546875, 18.410156);
			cr.CurveTo(44.1875, 18.882813, 42.128906, 20.328125, 39.847656, 21.089844);
			cr.CurveTo(38.101563, 21.671875, 36.136719, 21.605469, 34.492188, 22.429688);
			cr.CurveTo(31.8125, 23.765625, 32.484375, 25.105469, 30.472656, 26.445313);
			cr.CurveTo(29.644531, 27, 28.5, 27.078125, 27.796875, 27.785156);
			cr.CurveTo(27.089844, 28.492188, 27.011719, 29.632813, 26.457031, 30.464844);
			cr.CurveTo(26.105469, 30.988281, 25.46875, 31.277344, 25.117188, 31.804688);
			cr.CurveTo(24.671875, 32.695313, 24.222656, 33.589844, 23.777344, 34.480469);
			cr.CurveTo(22.886719, 34.480469, 21.730469, 33.851563, 21.097656, 34.480469);
			cr.CurveTo(20.46875, 35.113281, 21.382813, 36.3125, 21.097656, 37.160156);
			cr.CurveTo(20.898438, 37.757813, 20.109375, 37.972656, 19.761719, 38.5);
			cr.CurveTo(19.207031, 39.332031, 18.734375, 40.230469, 18.421875, 41.179688);
			cr.CurveTo(18.28125, 41.601563, 18.734375, 42.203125, 18.421875, 42.515625);
			cr.CurveTo(18.105469, 42.832031, 17.527344, 42.515625, 17.082031, 42.515625);
			cr.CurveTo(16.636719, 43.410156, 16.058594, 44.25, 15.742188, 45.195313);
			cr.CurveTo(15.074219, 47.203125, 17.082031, 46.535156, 15.742188, 49.214844);
			cr.CurveTo(15.175781, 50.34375, 13.53125, 50.71875, 13.0625, 51.890625);
			cr.CurveTo(12.566406, 53.136719, 13.285156, 54.589844, 13.0625, 55.910156);
			cr.CurveTo(12.597656, 58.695313, 11.160156, 61.230469, 10.386719, 63.945313);
			cr.CurveTo(9.878906, 65.714844, 9.628906, 67.558594, 9.046875, 69.304688);
			cr.CurveTo(8.730469, 70.25, 7.902344, 71.003906, 7.707031, 71.980469);
			cr.CurveTo(7.179688, 74.609375, 8.230469, 77.390625, 7.707031, 80.015625);
			cr.CurveTo(7.511719, 80.996094, 6.5625, 81.71875, 6.367188, 82.695313);
			cr.CurveTo(6.105469, 84.007813, 6.585938, 85.394531, 6.367188, 86.714844);
			cr.CurveTo(5.902344, 89.5, 4.085938, 91.953125, 3.6875, 94.75);
			cr.CurveTo(3.4375, 96.515625, 3.6875, 98.320313, 3.6875, 100.105469);
			cr.CurveTo(3.6875, 103.230469, 3.6875, 106.355469, 3.6875, 109.480469);
			cr.CurveTo(3.6875, 112.605469, 3.6875, 115.730469, 3.6875, 118.855469);
			cr.CurveTo(3.6875, 119.75, 3.472656, 120.667969, 3.6875, 121.535156);
			cr.CurveTo(3.929688, 122.503906, 4.710938, 123.265625, 5.027344, 124.214844);
			cr.CurveTo(5.308594, 125.0625, 4.628906, 126.09375, 5.027344, 126.890625);
			cr.CurveTo(5.59375, 128.023438, 7.140625, 128.441406, 7.707031, 129.570313);
			cr.CurveTo(7.90625, 129.96875, 7.390625, 130.59375, 7.707031, 130.910156);
			cr.CurveTo(8.023438, 131.226563, 8.644531, 130.710938, 9.046875, 130.910156);
			cr.CurveTo(11.097656, 131.9375, 10.09375, 133.296875, 11.722656, 134.929688);
			cr.CurveTo(12.429688, 135.632813, 13.570313, 135.714844, 14.402344, 136.265625);
			cr.CurveTo(14.929688, 136.617188, 15.214844, 137.257813, 15.742188, 137.605469);
			cr.CurveTo(16.574219, 138.160156, 17.472656, 138.628906, 18.421875, 138.945313);
			cr.CurveTo(18.84375, 139.085938, 19.445313, 138.628906, 19.761719, 138.945313);
			cr.CurveTo(20.464844, 139.652344, 20.394531, 140.917969, 21.097656, 141.625);
			cr.CurveTo(21.804688, 142.332031, 22.945313, 142.410156, 23.777344, 142.964844);
			cr.CurveTo(24.304688, 143.3125, 24.519531, 144.105469, 25.117188, 144.304688);
			cr.CurveTo(25.964844, 144.585938, 26.949219, 144.019531, 27.796875, 144.304688);
			cr.CurveTo(28.394531, 144.503906, 28.6875, 145.195313, 29.136719, 145.640625);
			cr.CurveTo(30.027344, 146.535156, 30.683594, 147.757813, 31.8125, 148.320313);
			cr.CurveTo(32.613281, 148.71875, 33.695313, 147.921875, 34.492188, 148.320313);
			cr.CurveTo(35.621094, 148.886719, 36.121094, 150.300781, 37.171875, 151);
			cr.CurveTo(37.542969, 151.246094, 38.109375, 150.800781, 38.511719, 151);
			cr.CurveTo(39.074219, 151.28125, 39.402344, 151.890625, 39.847656, 152.339844);
			cr.CurveTo(40.742188, 152.339844, 41.679688, 152.054688, 42.527344, 152.339844);
			cr.CurveTo(43.125, 152.539063, 43.421875, 153.230469, 43.867188, 153.679688);
			cr.CurveTo(44.761719, 154.125, 45.578125, 154.777344, 46.546875, 155.015625);
			cr.CurveTo(47.410156, 155.234375, 48.332031, 155.015625, 49.222656, 155.015625);
			cr.CurveTo(49.671875, 155.015625, 50.164063, 154.816406, 50.5625, 155.015625);
			cr.CurveTo(51.128906, 155.300781, 51.304688, 156.15625, 51.902344, 156.355469);
			cr.CurveTo(52.75, 156.640625, 53.714844, 156.140625, 54.582031, 156.355469);
			cr.CurveTo(55.550781, 156.597656, 56.3125, 157.378906, 57.261719, 157.695313);
			cr.CurveTo(57.683594, 157.835938, 58.152344, 157.695313, 58.597656, 157.695313);
			cr.CurveTo(59.492188, 157.695313, 60.386719, 157.695313, 61.277344, 157.695313);
			cr.CurveTo(64.402344, 157.695313, 67.527344, 157.695313, 70.652344, 157.695313);
			cr.CurveTo(75.117188, 157.695313, 79.582031, 157.695313, 84.046875, 157.695313);
			cr.CurveTo(86.277344, 157.695313, 88.554688, 158.132813, 90.742188, 157.695313);
			cr.CurveTo(91.722656, 157.5, 92.441406, 156.550781, 93.421875, 156.355469);
			cr.CurveTo(96.046875, 155.832031, 98.8125, 156.796875, 101.457031, 156.355469);
			cr.CurveTo(102.847656, 156.125, 104.089844, 155.292969, 105.472656, 155.015625);
			cr.CurveTo(106.351563, 154.84375, 107.355469, 155.417969, 108.152344, 155.015625);
			cr.CurveTo(109.28125, 154.453125, 109.703125, 152.902344, 110.832031, 152.339844);
			cr.CurveTo(112.890625, 151.308594, 121.195313, 150.292969, 122.886719, 149.660156);
			cr.CurveTo(124.390625, 149.09375, 125.433594, 147.636719, 126.902344, 146.980469);
			cr.CurveTo(129.484375, 145.835938, 132.316406, 145.351563, 134.9375, 144.304688);
			cr.CurveTo(136.792969, 143.5625, 138.402344, 142.257813, 140.296875, 141.625);
			cr.CurveTo(142.457031, 140.90625, 144.832031, 141.003906, 146.992188, 140.285156);
			cr.CurveTo(148.886719, 139.652344, 150.5625, 138.5, 152.347656, 137.605469);
			cr.CurveTo(154.136719, 136.714844, 155.972656, 135.917969, 157.707031, 134.929688);
			cr.CurveTo(159.105469, 134.128906, 160.285156, 132.96875, 161.722656, 132.25);
			cr.CurveTo(162.988281, 131.617188, 164.480469, 131.542969, 165.742188, 130.910156);
			cr.CurveTo(167.183594, 130.191406, 168.320313, 128.953125, 169.761719, 128.230469);
			cr.CurveTo(177.425781, 124.398438, 170.605469, 129.0625, 176.457031, 125.554688);
			cr.CurveTo(177.835938, 124.726563, 179.035156, 123.59375, 180.472656, 122.875);
			cr.CurveTo(181.738281, 122.242188, 183.230469, 122.167969, 184.492188, 121.535156);
			cr.CurveTo(185.933594, 120.816406, 187.171875, 119.75, 188.511719, 118.855469);
			cr.CurveTo(189.847656, 117.964844, 191.292969, 117.207031, 192.527344, 116.179688);
			cr.CurveTo(193.984375, 114.964844, 195.089844, 113.371094, 196.546875, 112.160156);
			cr.CurveTo(197.78125, 111.128906, 199.425781, 110.621094, 200.5625, 109.480469);
			cr.CurveTo(201.703125, 108.34375, 202.238281, 106.722656, 203.242188, 105.464844);
			cr.CurveTo(204.03125, 104.476563, 204.910156, 103.542969, 205.921875, 102.785156);
			cr.CurveTo(206.71875, 102.1875, 208, 102.246094, 208.597656, 101.445313);
			cr.CurveTo(209.445313, 100.316406, 209.089844, 98.558594, 209.9375, 97.429688);
			cr.CurveTo(210.539063, 96.628906, 211.785156, 96.644531, 212.617188, 96.089844);
			cr.CurveTo(213.140625, 95.738281, 213.511719, 95.195313, 213.957031, 94.75);
			cr.CurveTo(214.847656, 93.855469, 215.742188, 92.964844, 216.636719, 92.070313);
			cr.CurveTo(217.082031, 91.625, 217.625, 91.257813, 217.972656, 90.730469);
			cr.CurveTo(218.527344, 89.902344, 218.609375, 88.757813, 219.3125, 88.054688);
			cr.CurveTo(220.019531, 87.347656, 221.285156, 87.417969, 221.992188, 86.714844);
			cr.CurveTo(222.253906, 86.449219, 225.890625, 79.15625, 226.011719, 78.679688);
			cr.CurveTo(226.226563, 77.8125, 225.792969, 76.867188, 226.011719, 76);
			cr.CurveTo(226.492188, 74.0625, 228.058594, 72.535156, 228.6875, 70.640625);
			cr.CurveTo(228.972656, 69.796875, 228.6875, 68.855469, 228.6875, 67.964844);
			cr.CurveTo(228.6875, 67.070313, 228.6875, 66.179688, 228.6875, 65.285156);
			cr.CurveTo(228.6875, 64.839844, 228.886719, 64.34375, 228.6875, 63.945313);
			cr.CurveTo(228.40625, 63.382813, 227.632813, 63.171875, 227.347656, 62.605469);
			cr.CurveTo(226.71875, 61.34375, 226.535156, 59.898438, 226.011719, 58.589844);
			cr.CurveTo(225.640625, 57.664063, 225.042969, 56.835938, 224.671875, 55.910156);
			cr.CurveTo(219.277344, 42.429688, 225.714844, 58.753906, 223.332031, 49.214844);
			cr.CurveTo(221.289063, 41.050781, 222.871094, 52.75, 219.3125, 43.855469);
			cr.CurveTo(218.816406, 42.613281, 219.914063, 41.035156, 219.3125, 39.839844);
			cr.CurveTo(218.867188, 38.945313, 217.339844, 39.207031, 216.636719, 38.5);
			cr.CurveTo(216.320313, 38.183594, 216.882813, 37.53125, 216.636719, 37.160156);
			cr.CurveTo(215.050781, 34.785156, 211.675781, 32.78125, 209.9375, 30.464844);
			cr.CurveTo(206.367188, 23.320313, 211.277344, 31.804688, 205.921875, 26.445313);
			cr.CurveTo(205.214844, 25.742188, 205.136719, 24.597656, 204.582031, 23.765625);
			cr.CurveTo(204.230469, 23.242188, 203.6875, 22.875, 203.242188, 22.429688);
			cr.CurveTo(202.347656, 21.535156, 201.457031, 20.640625, 200.5625, 19.75);
			cr.CurveTo(200.117188, 19.304688, 199.75, 18.761719, 199.222656, 18.410156);
			cr.CurveTo(198.394531, 17.855469, 197.375, 17.625, 196.546875, 17.070313);
			cr.CurveTo(194.070313, 15.421875, 196.34375, 14.703125, 193.867188, 13.054688);
			cr.CurveTo(193.125, 12.558594, 192.035156, 13.335938, 191.1875, 13.054688);
			cr.CurveTo(190.589844, 12.855469, 190.375, 12.0625, 189.847656, 11.714844);
			cr.CurveTo(189.019531, 11.160156, 188.117188, 10.691406, 187.171875, 10.375);
			cr.CurveTo(186.746094, 10.234375, 186.148438, 10.691406, 185.832031, 10.375);
			cr.CurveTo(185.125, 9.667969, 185.324219, 8.25, 184.492188, 7.695313);
			cr.CurveTo(180.472656, 5.015625, 183.152344, 9.035156, 180.472656, 7.695313);
			cr.CurveTo(179.910156, 7.414063, 179.699219, 6.640625, 179.136719, 6.355469);
			cr.CurveTo(178.335938, 5.957031, 177.253906, 6.757813, 176.457031, 6.355469);
			cr.CurveTo(175.890625, 6.074219, 175.683594, 5.300781, 175.117188, 5.015625);
			cr.CurveTo(174.238281, 4.578125, 169.609375, 5.414063, 168.421875, 5.015625);
			cr.CurveTo(167.472656, 4.703125, 166.6875, 3.992188, 165.742188, 3.679688);
			cr.CurveTo(164.9375, 3.410156, 160.113281, 3.679688, 159.046875, 3.679688);
			cr.CurveTo(153.6875, 3.679688, 148.332031, 3.679688, 142.972656, 3.679688);
			cr.CurveTo(142.144531, 3.679688, 135.359375, 3.46875, 134.9375, 3.679688);
			cr.CurveTo(134.539063, 3.878906, 135.253906, 4.703125, 134.9375, 5.015625);
			cr.CurveTo(134.542969, 5.414063, 130.410156, 4.601563, 129.582031, 5.015625);
			cr.CurveTo(129.015625, 5.300781, 128.808594, 6.074219, 128.242188, 6.355469);
			cr.CurveTo(127.445313, 6.757813, 126.363281, 5.957031, 125.5625, 6.355469);
			cr.CurveTo(125, 6.640625, 124.789063, 7.414063, 124.222656, 7.695313);
			cr.CurveTo(123.824219, 7.894531, 123.332031, 7.695313, 122.886719, 7.695313);
			cr.CurveTo(121.992188, 7.695313, 121.054688, 7.414063, 120.207031, 7.695313);
			cr.CurveTo(119.605469, 7.894531, 119.433594, 8.753906, 118.867188, 9.035156);
			cr.CurveTo(118.140625, 9.398438, 113.875, 8.667969, 113.511719, 9.035156);
			cr.CurveTo(113.195313, 9.351563, 113.824219, 10.058594, 113.511719, 10.375);
			cr.CurveTo(113.234375, 10.648438, 110.023438, 10.109375, 109.492188, 10.375);
			cr.CurveTo(108.925781, 10.65625, 108.71875, 11.429688, 108.152344, 11.714844);
			cr.CurveTo(107.621094, 11.980469, 104.410156, 11.441406, 104.136719, 11.714844);
			cr.CurveTo(103.820313, 12.03125, 104.449219, 12.738281, 104.136719, 13.054688);
			cr.CurveTo(103.8125, 13.375, 99.101563, 12.730469, 98.777344, 13.054688);
			cr.CurveTo(98.460938, 13.367188, 99.175781, 14.191406, 98.777344, 14.390625);
			cr.CurveTo(97.957031, 14.804688, 92.910156, 13.976563, 92.082031, 14.390625);
			cr.CurveTo(91.515625, 14.675781, 91.308594, 15.449219, 90.742188, 15.730469);
			cr.CurveTo(90.34375, 15.929688, 89.847656, 15.730469, 89.402344, 15.730469);
			cr.CurveTo(88.957031, 15.730469, 88.511719, 15.730469, 88.0625, 15.730469);
			cr.CurveTo(85.832031, 15.730469, 83.597656, 15.730469, 81.367188, 15.730469);
			cr.ClosePath();
			cr.MoveTo(81.367188, 15.730469);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			matrix = new Matrix(3.543307, 0, 0, 3.543307, -250.775794, -287.969644);
			pattern.Matrix = matrix;
			cr.StrokePreserve();
			if (pattern != null) pattern.Dispose();

			cr.Operator = Operator.Over;
			cr.LineWidth = 8;
			cr.MiterLimit = 4;
			cr.LineCap = LineCap.Butt;
			cr.LineJoin = LineJoin.Miter;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(50.375, 36.609375);
			cr.CurveTo(49.746094, 36.925781, 49.046875, 37.132813, 48.480469, 37.554688);
			cr.CurveTo(48.355469, 37.652344, 48.570313, 37.898438, 48.480469, 38.03125);
			cr.CurveTo(48.109375, 38.585938, 47.433594, 38.894531, 47.0625, 39.449219);
			cr.CurveTo(46.867188, 39.742188, 46.835938, 40.148438, 46.585938, 40.398438);
			cr.CurveTo(46.339844, 40.648438, 45.933594, 40.675781, 45.640625, 40.871094);
			cr.CurveTo(45.457031, 40.996094, 45.351563, 41.21875, 45.167969, 41.34375);
			cr.CurveTo(44.875, 41.539063, 44.46875, 41.566406, 44.21875, 41.816406);
			cr.CurveTo(43.972656, 42.066406, 43.996094, 42.515625, 43.746094, 42.765625);
			cr.CurveTo(43.496094, 43.015625, 43.09375, 43.042969, 42.800781, 43.238281);
			cr.CurveTo(41.710938, 43.964844, 43.003906, 43.507813, 41.851563, 44.660156);
			cr.CurveTo(41.601563, 44.910156, 41.15625, 44.882813, 40.90625, 45.132813);
			cr.CurveTo(40.65625, 45.382813, 40.683594, 45.828125, 40.433594, 46.078125);
			cr.CurveTo(40.183594, 46.328125, 39.734375, 46.304688, 39.484375, 46.554688);
			cr.CurveTo(39.234375, 46.800781, 39.207031, 47.207031, 39.011719, 47.5);
			cr.CurveTo(38.546875, 48.195313, 37.582031, 48.695313, 37.117188, 49.394531);
			cr.CurveTo(37.03125, 49.523438, 37.207031, 49.734375, 37.117188, 49.867188);
			cr.CurveTo(37.523438, 52.0625, 36.097656, 50.410156, 35.695313, 50.816406);
			cr.CurveTo(33.847656, 52.664063, 35.808594, 51.070313, 35.222656, 52.234375);
			cr.CurveTo(35.125, 52.433594, 34.90625, 52.550781, 34.75, 52.707031);
			cr.CurveTo(34.433594, 53.023438, 34.050781, 53.285156, 33.804688, 53.65625);
			cr.CurveTo(33.714844, 53.785156, 33.875, 53.988281, 33.804688, 54.128906);
			cr.CurveTo(33.601563, 54.527344, 33.054688, 54.675781, 32.855469, 55.074219);
			cr.CurveTo(32.707031, 55.375, 33.003906, 56.671875, 32.855469, 56.96875);
			cr.CurveTo(32.757813, 57.167969, 32.480469, 57.242188, 32.382813, 57.445313);
			cr.CurveTo(32.3125, 57.585938, 32.382813, 57.757813, 32.382813, 57.917969);
			cr.CurveTo(32.226563, 58.234375, 32.105469, 58.570313, 31.910156, 58.863281);
			cr.CurveTo(31.785156, 59.050781, 31.535156, 59.136719, 31.4375, 59.335938);
			cr.CurveTo(31.363281, 59.480469, 31.484375, 59.660156, 31.4375, 59.8125);
			cr.CurveTo(31.324219, 60.144531, 31.121094, 60.441406, 30.960938, 60.757813);
			cr.CurveTo(30.804688, 61.074219, 30.683594, 61.410156, 30.488281, 61.707031);
			cr.CurveTo(30.363281, 61.890625, 30.085938, 61.96875, 30.015625, 62.179688);
			cr.CurveTo(29.914063, 62.476563, 30.015625, 62.808594, 30.015625, 63.125);
			cr.CurveTo(30.015625, 63.285156, 30.085938, 63.457031, 30.015625, 63.597656);
			cr.CurveTo(29.914063, 63.796875, 29.699219, 63.914063, 29.542969, 64.074219);
			cr.CurveTo(29.542969, 64.386719, 29.640625, 64.71875, 29.542969, 65.019531);
			cr.CurveTo(29.472656, 65.230469, 29.167969, 65.292969, 29.066406, 65.492188);
			cr.CurveTo(28.996094, 65.632813, 29.066406, 65.808594, 29.066406, 65.964844);
			cr.CurveTo(29.066406, 66.28125, 29.167969, 66.613281, 29.066406, 66.914063);
			cr.CurveTo(28.925781, 67.335938, 28.320313, 67.460938, 28.121094, 67.859375);
			cr.CurveTo(27.898438, 68.308594, 27.90625, 68.851563, 27.648438, 69.28125);
			cr.CurveTo(27.417969, 69.664063, 26.898438, 69.828125, 26.699219, 70.226563);
			cr.CurveTo(26.558594, 70.511719, 26.875, 70.914063, 26.699219, 71.175781);
			cr.CurveTo(26.503906, 71.46875, 25.949219, 71.355469, 25.753906, 71.648438);
			cr.CurveTo(25.578125, 71.910156, 25.851563, 72.296875, 25.753906, 72.597656);
			cr.CurveTo(25.683594, 72.808594, 25.402344, 72.882813, 25.28125, 73.070313);
			cr.CurveTo(25.085938, 73.363281, 25.003906, 73.722656, 24.804688, 74.015625);
			cr.CurveTo(24.683594, 74.203125, 24.433594, 74.289063, 24.332031, 74.488281);
			cr.CurveTo(24.261719, 74.632813, 24.382813, 74.8125, 24.332031, 74.964844);
			cr.CurveTo(23.191406, 78.394531, 24.46875, 74.21875, 23.386719, 76.382813);
			cr.CurveTo(23.316406, 76.523438, 23.4375, 76.707031, 23.386719, 76.859375);
			cr.CurveTo(22.8125, 78.574219, 23.160156, 77.195313, 22.4375, 78.277344);
			cr.CurveTo(21.972656, 78.976563, 21.75, 79.875, 21.492188, 80.644531);
			cr.CurveTo(21.335938, 81.117188, 21.140625, 81.582031, 21.019531, 82.066406);
			cr.CurveTo(20.980469, 82.21875, 21.089844, 82.398438, 21.019531, 82.539063);
			cr.CurveTo(20.917969, 82.738281, 20.617188, 82.800781, 20.542969, 83.011719);
			cr.CurveTo(20.542969, 83.015625, 20.546875, 84.433594, 20.542969, 84.433594);
			cr.CurveTo(20.433594, 84.546875, 20.140625, 84.292969, 20.070313, 84.433594);
			cr.CurveTo(19.859375, 84.855469, 20.070313, 85.378906, 20.070313, 85.855469);
			cr.CurveTo(20.070313, 86.800781, 20.070313, 87.75, 20.070313, 88.695313);
			cr.CurveTo(20.070313, 89.957031, 20.070313, 91.21875, 20.070313, 92.484375);
			cr.CurveTo(20.070313, 92.800781, 20.070313, 93.113281, 20.070313, 93.429688);
			cr.CurveTo(20.070313, 93.589844, 20, 93.761719, 20.070313, 93.902344);
			cr.CurveTo(20.792969, 95.347656, 20.445313, 93.039063, 21.019531, 95.324219);
			cr.CurveTo(21.09375, 95.628906, 21.019531, 95.957031, 21.019531, 96.269531);
			cr.CurveTo(21.019531, 96.429688, 20.949219, 96.605469, 21.019531, 96.746094);
			cr.CurveTo(22.136719, 98.980469, 20.769531, 93.855469, 21.964844, 98.640625);
			cr.CurveTo(22.042969, 98.945313, 21.867188, 99.285156, 21.964844, 99.585938);
			cr.CurveTo(22.320313, 100.648438, 22.84375, 100.871094, 23.386719, 101.953125);
			cr.CurveTo(23.457031, 102.09375, 23.316406, 102.285156, 23.386719, 102.425781);
			cr.CurveTo(23.484375, 102.625, 23.757813, 102.699219, 23.859375, 102.902344);
			cr.CurveTo(23.929688, 103.042969, 23.789063, 103.234375, 23.859375, 103.375);
			cr.CurveTo(24.070313, 103.792969, 24.992188, 104.363281, 25.28125, 104.792969);
			cr.CurveTo(26.875, 107.1875, 25.285156, 104.804688, 25.753906, 106.214844);
			cr.CurveTo(25.863281, 106.550781, 26.03125, 106.867188, 26.226563, 107.160156);
			cr.CurveTo(26.351563, 107.347656, 26.644531, 107.417969, 26.699219, 107.636719);
			cr.CurveTo(26.816406, 108.09375, 26.488281, 108.632813, 26.699219, 109.054688);
			cr.CurveTo(26.859375, 109.371094, 27.355469, 109.332031, 27.648438, 109.53125);
			cr.CurveTo(27.832031, 109.652344, 27.964844, 109.84375, 28.121094, 110.003906);
			cr.CurveTo(28.910156, 110.792969, 29.699219, 111.582031, 30.488281, 112.371094);
			cr.CurveTo(30.804688, 112.6875, 31.0625, 113.070313, 31.4375, 113.316406);
			cr.CurveTo(31.730469, 113.511719, 32.089844, 113.59375, 32.382813, 113.792969);
			cr.CurveTo(33.464844, 114.511719, 32.085938, 114.167969, 33.804688, 114.738281);
			cr.CurveTo(33.953125, 114.789063, 34.164063, 114.625, 34.277344, 114.738281);
			cr.CurveTo(34.527344, 114.988281, 34.457031, 115.488281, 34.75, 115.683594);
			cr.CurveTo(35.011719, 115.859375, 35.433594, 115.511719, 35.695313, 115.683594);
			cr.CurveTo(35.992188, 115.882813, 35.921875, 116.382813, 36.171875, 116.632813);
			cr.CurveTo(36.421875, 116.882813, 36.835938, 116.894531, 37.117188, 117.105469);
			cr.CurveTo(37.476563, 117.375, 37.707031, 117.785156, 38.066406, 118.054688);
			cr.CurveTo(38.347656, 118.265625, 38.761719, 118.277344, 39.011719, 118.527344);
			cr.CurveTo(39.261719, 118.777344, 39.203125, 119.261719, 39.484375, 119.472656);
			cr.CurveTo(39.882813, 119.773438, 40.507813, 119.648438, 40.90625, 119.945313);
			cr.CurveTo(41.1875, 120.160156, 41.128906, 120.644531, 41.378906, 120.894531);
			cr.CurveTo(42.53125, 122.042969, 42.074219, 120.753906, 42.800781, 121.839844);
			cr.CurveTo(45.324219, 125.628906, 41.0625, 120.105469, 44.21875, 123.261719);
			cr.CurveTo(44.46875, 123.511719, 44.445313, 123.957031, 44.695313, 124.207031);
			cr.CurveTo(44.945313, 124.457031, 45.359375, 124.46875, 45.640625, 124.683594);
			cr.CurveTo(45.996094, 124.949219, 46.230469, 125.359375, 46.585938, 125.628906);
			cr.CurveTo(46.871094, 125.839844, 47.253906, 125.890625, 47.535156, 126.101563);
			cr.CurveTo(47.890625, 126.371094, 48.097656, 126.820313, 48.480469, 127.050781);
			cr.CurveTo(48.910156, 127.304688, 49.472656, 127.265625, 49.902344, 127.523438);
			cr.CurveTo(50.285156, 127.753906, 50.464844, 128.242188, 50.847656, 128.46875);
			cr.CurveTo(51.277344, 128.726563, 51.824219, 128.71875, 52.269531, 128.945313);
			cr.CurveTo(52.777344, 129.199219, 53.21875, 129.574219, 53.691406, 129.890625);
			cr.CurveTo(54.480469, 130.207031, 55.277344, 130.503906, 56.058594, 130.835938);
			cr.CurveTo(56.382813, 130.976563, 56.6875, 131.152344, 57.003906, 131.3125);
			cr.CurveTo(57.320313, 131.625, 57.554688, 132.058594, 57.953125, 132.257813);
			cr.CurveTo(58.535156, 132.550781, 59.265625, 132.441406, 59.847656, 132.730469);
			cr.CurveTo(60.246094, 132.929688, 60.421875, 133.429688, 60.792969, 133.679688);
			cr.CurveTo(60.925781, 133.765625, 61.136719, 133.589844, 61.265625, 133.679688);
			cr.CurveTo(61.636719, 133.925781, 61.832031, 134.394531, 62.214844, 134.625);
			cr.CurveTo(62.640625, 134.882813, 63.148438, 134.976563, 63.632813, 135.097656);
			cr.CurveTo(63.789063, 135.136719, 63.976563, 135.011719, 64.109375, 135.097656);
			cr.CurveTo(64.480469, 135.347656, 64.699219, 135.777344, 65.054688, 136.046875);
			cr.CurveTo(65.335938, 136.257813, 65.71875, 136.308594, 66, 136.519531);
			cr.CurveTo(66.359375, 136.789063, 66.550781, 137.265625, 66.949219, 137.464844);
			cr.CurveTo(67.894531, 137.941406, 67.660156, 137.230469, 68.371094, 137.464844);
			cr.CurveTo(71.800781, 138.609375, 67.621094, 137.332031, 69.789063, 138.414063);
			cr.CurveTo(70.085938, 138.5625, 71.386719, 138.265625, 71.683594, 138.414063);
			cr.CurveTo(71.882813, 138.511719, 71.945313, 138.816406, 72.15625, 138.886719);
			cr.CurveTo(72.410156, 138.972656, 73.652344, 138.886719, 74.050781, 138.886719);
			cr.CurveTo(74.496094, 138.886719, 76.148438, 138.976563, 76.417969, 138.886719);
			cr.CurveTo(76.628906, 138.816406, 76.707031, 138.539063, 76.890625, 138.414063);
			cr.CurveTo(77.1875, 138.21875, 77.503906, 138.050781, 77.839844, 137.941406);
			cr.CurveTo(77.988281, 137.890625, 78.160156, 137.976563, 78.3125, 137.941406);
			cr.CurveTo(80.40625, 137.417969, 79.046875, 137.644531, 80.679688, 136.992188);
			cr.CurveTo(81.144531, 136.808594, 81.671875, 136.777344, 82.101563, 136.519531);
			cr.CurveTo(82.484375, 136.289063, 82.664063, 135.800781, 83.046875, 135.574219);
			cr.CurveTo(83.476563, 135.316406, 84.003906, 135.285156, 84.46875, 135.097656);
			cr.CurveTo(84.796875, 134.96875, 85.101563, 134.785156, 85.414063, 134.625);
			cr.CurveTo(85.890625, 134.308594, 86.3125, 133.902344, 86.835938, 133.679688);
			cr.CurveTo(87.433594, 133.421875, 88.113281, 133.410156, 88.730469, 133.203125);
			cr.CurveTo(89.066406, 133.09375, 89.347656, 132.863281, 89.675781, 132.730469);
			cr.CurveTo(90.140625, 132.546875, 90.652344, 132.480469, 91.097656, 132.257813);
			cr.CurveTo(91.296875, 132.15625, 91.355469, 131.839844, 91.570313, 131.785156);
			cr.CurveTo(95.902344, 130.703125, 90, 133.074219, 94.414063, 131.3125);
			cr.CurveTo(94.738281, 131.179688, 95.03125, 130.96875, 95.359375, 130.835938);
			cr.CurveTo(95.824219, 130.652344, 96.351563, 130.621094, 96.78125, 130.363281);
			cr.CurveTo(97.164063, 130.132813, 97.371094, 129.683594, 97.726563, 129.417969);
			cr.CurveTo(99.003906, 128.457031, 98.503906, 129.425781, 100.09375, 128.46875);
			cr.CurveTo(100.476563, 128.242188, 100.683594, 127.789063, 101.042969, 127.523438);
			cr.CurveTo(101.675781, 127.046875, 102.726563, 126.847656, 103.410156, 126.574219);
			cr.CurveTo(104.195313, 126.261719, 105.140625, 125.632813, 105.777344, 125.15625);
			cr.CurveTo(106.3125, 124.753906, 106.660156, 124.136719, 107.195313, 123.734375);
			cr.CurveTo(107.480469, 123.523438, 107.894531, 123.511719, 108.144531, 123.261719);
			cr.CurveTo(108.394531, 123.011719, 108.367188, 122.5625, 108.617188, 122.3125);
			cr.CurveTo(108.867188, 122.066406, 109.28125, 122.050781, 109.566406, 121.839844);
			cr.CurveTo(110.277344, 121.304688, 110.691406, 120.40625, 111.457031, 119.945313);
			cr.CurveTo(111.886719, 119.691406, 112.449219, 119.730469, 112.878906, 119.472656);
			cr.CurveTo(113.261719, 119.242188, 113.511719, 118.84375, 113.824219, 118.527344);
			cr.CurveTo(114.140625, 118.210938, 114.457031, 117.894531, 114.773438, 117.578125);
			cr.CurveTo(115.089844, 117.261719, 115.363281, 116.898438, 115.71875, 116.632813);
			cr.CurveTo(116.003906, 116.421875, 116.417969, 116.410156, 116.667969, 116.160156);
			cr.CurveTo(118.078125, 114.75, 115.957031, 115.542969, 118.085938, 114.265625);
			cr.CurveTo(118.515625, 114.007813, 119.082031, 114.046875, 119.507813, 113.792969);
			cr.CurveTo(119.890625, 113.5625, 120.097656, 113.113281, 120.457031, 112.84375);
			cr.CurveTo(120.738281, 112.632813, 121.121094, 112.582031, 121.402344, 112.371094);
			cr.CurveTo(121.757813, 112.101563, 121.9375, 111.597656, 122.347656, 111.421875);
			cr.CurveTo(123.089844, 111.105469, 123.953125, 111.203125, 124.71875, 110.949219);
			cr.CurveTo(125.386719, 110.726563, 125.964844, 110.289063, 126.609375, 110.003906);
			cr.CurveTo(128.164063, 109.3125, 129.75, 108.691406, 131.347656, 108.109375);
			cr.CurveTo(132.285156, 107.769531, 133.25, 107.503906, 134.1875, 107.160156);
			cr.CurveTo(134.984375, 106.871094, 135.753906, 106.503906, 136.554688, 106.214844);
			cr.CurveTo(137.492188, 105.875, 138.46875, 105.640625, 139.394531, 105.269531);
			cr.CurveTo(140.050781, 105.003906, 140.691406, 104.695313, 141.289063, 104.320313);
			cr.CurveTo(142.148438, 103.785156, 143.601563, 102.453125, 144.605469, 101.953125);
			cr.CurveTo(147.429688, 100.539063, 146.410156, 101.332031, 149.339844, 100.53125);
			cr.CurveTo(150.300781, 100.269531, 151.234375, 99.902344, 152.179688, 99.585938);
			cr.CurveTo(154.074219, 98.953125, 155.984375, 98.375, 157.863281, 97.691406);
			cr.CurveTo(159.460938, 97.109375, 161, 96.378906, 162.597656, 95.796875);
			cr.CurveTo(163.535156, 95.457031, 164.503906, 95.203125, 165.4375, 94.851563);
			cr.CurveTo(165.769531, 94.726563, 166.070313, 94.535156, 166.386719, 94.378906);
			cr.CurveTo(167.015625, 94.0625, 167.679688, 93.804688, 168.28125, 93.429688);
			cr.CurveTo(168.949219, 93.011719, 169.515625, 92.449219, 170.171875, 92.011719);
			cr.CurveTo(170.46875, 91.8125, 170.804688, 91.695313, 171.121094, 91.535156);
			cr.CurveTo(172.066406, 91.0625, 173.054688, 90.660156, 173.960938, 90.117188);
			cr.CurveTo(176.078125, 88.84375, 174.871094, 89.351563, 177.277344, 87.75);
			cr.CurveTo(177.570313, 87.550781, 177.941406, 87.488281, 178.222656, 87.273438);
			cr.CurveTo(178.582031, 87.007813, 178.820313, 86.605469, 179.171875, 86.328125);
			cr.CurveTo(179.613281, 85.972656, 180.148438, 85.734375, 180.589844, 85.378906);
			cr.CurveTo(181.289063, 84.824219, 181.789063, 84.042969, 182.484375, 83.488281);
			cr.CurveTo(183.648438, 82.554688, 183.996094, 82.734375, 184.851563, 81.59375);
			cr.CurveTo(185.0625, 81.308594, 185.078125, 80.894531, 185.324219, 80.644531);
			cr.CurveTo(185.574219, 80.394531, 185.980469, 80.367188, 186.273438, 80.171875);
			cr.CurveTo(186.457031, 80.046875, 186.589844, 79.855469, 186.746094, 79.699219);
			cr.CurveTo(187.0625, 79.382813, 187.378906, 79.066406, 187.695313, 78.75);
			cr.CurveTo(187.851563, 78.59375, 188.042969, 78.464844, 188.167969, 78.277344);
			cr.CurveTo(188.363281, 77.984375, 188.390625, 77.582031, 188.640625, 77.332031);
			cr.CurveTo(188.890625, 77.082031, 189.339844, 77.105469, 189.585938, 76.859375);
			cr.CurveTo(189.835938, 76.609375, 189.8125, 76.160156, 190.0625, 75.910156);
			cr.CurveTo(190.308594, 75.660156, 190.757813, 75.6875, 191.007813, 75.4375);
			cr.CurveTo(191.121094, 75.324219, 190.957031, 75.113281, 191.007813, 74.964844);
			cr.CurveTo(191.121094, 74.628906, 191.285156, 74.308594, 191.480469, 74.015625);
			cr.CurveTo(191.605469, 73.832031, 191.832031, 73.730469, 191.957031, 73.542969);
			cr.CurveTo(192.152344, 73.25, 192.234375, 72.890625, 192.429688, 72.597656);
			cr.CurveTo(192.550781, 72.410156, 192.800781, 72.320313, 192.902344, 72.121094);
			cr.CurveTo(192.972656, 71.980469, 192.851563, 71.796875, 192.902344, 71.648438);
			cr.CurveTo(193.015625, 71.3125, 193.265625, 71.035156, 193.375, 70.703125);
			cr.CurveTo(193.472656, 70.414063, 193.375, 68.226563, 193.375, 67.859375);
			cr.CurveTo(193.375, 67.386719, 193.527344, 66.890625, 193.375, 66.441406);
			cr.CurveTo(193.304688, 66.226563, 193, 66.167969, 192.902344, 65.964844);
			cr.CurveTo(192.832031, 65.824219, 193.015625, 65.605469, 192.902344, 65.492188);
			cr.CurveTo(192.652344, 65.242188, 192.203125, 65.269531, 191.957031, 65.019531);
			cr.CurveTo(191.84375, 64.90625, 192.027344, 64.6875, 191.957031, 64.546875);
			cr.CurveTo(191.753906, 64.148438, 191.148438, 64.023438, 191.007813, 63.597656);
			cr.CurveTo(190.90625, 63.300781, 191.183594, 62.914063, 191.007813, 62.652344);
			cr.CurveTo(189.199219, 61.746094, 190.582031, 62.792969, 190.0625, 61.230469);
			cr.CurveTo(189.878906, 60.6875, 188.929688, 60.574219, 188.640625, 60.285156);
			cr.CurveTo(188.527344, 60.171875, 188.710938, 59.953125, 188.640625, 59.8125);
			cr.CurveTo(188.539063, 59.613281, 188.289063, 59.523438, 188.167969, 59.335938);
			cr.CurveTo(187.972656, 59.042969, 187.941406, 58.640625, 187.695313, 58.390625);
			cr.CurveTo(187.445313, 58.140625, 186.996094, 58.167969, 186.746094, 57.917969);
			cr.CurveTo(186.496094, 57.667969, 186.46875, 57.265625, 186.273438, 56.96875);
			cr.CurveTo(185.976563, 56.527344, 184.683594, 55.378906, 184.378906, 55.074219);
			cr.CurveTo(184.222656, 54.917969, 184.027344, 54.789063, 183.90625, 54.601563);
			cr.CurveTo(183.710938, 54.308594, 183.679688, 53.90625, 183.433594, 53.65625);
			cr.CurveTo(183.183594, 53.40625, 182.777344, 53.378906, 182.484375, 53.183594);
			cr.CurveTo(182.300781, 53.058594, 182.167969, 52.867188, 182.011719, 52.707031);
			cr.CurveTo(181.695313, 52.394531, 181.378906, 52.078125, 181.0625, 51.761719);
			cr.CurveTo(180.90625, 51.605469, 180.714844, 51.472656, 180.589844, 51.289063);
			cr.CurveTo(180.394531, 50.996094, 180.367188, 50.589844, 180.117188, 50.339844);
			cr.CurveTo(179.867188, 50.089844, 179.417969, 50.117188, 179.171875, 49.867188);
			cr.CurveTo(178.921875, 49.617188, 178.945313, 49.171875, 178.695313, 48.921875);
			cr.CurveTo(178.449219, 48.671875, 178.042969, 48.644531, 177.75, 48.445313);
			cr.CurveTo(177.5625, 48.324219, 177.433594, 48.132813, 177.277344, 47.972656);
			cr.CurveTo(176.960938, 47.65625, 176.644531, 47.34375, 176.328125, 47.027344);
			cr.CurveTo(176.171875, 46.867188, 175.980469, 46.738281, 175.855469, 46.554688);
			cr.CurveTo(173.332031, 42.765625, 177.59375, 48.289063, 174.433594, 45.132813);
			cr.CurveTo(174.1875, 44.882813, 174.210938, 44.433594, 173.960938, 44.183594);
			cr.CurveTo(173.710938, 43.9375, 173.308594, 43.90625, 173.015625, 43.710938);
			cr.CurveTo(172.625, 43.453125, 171.984375, 42.550781, 171.59375, 42.292969);
			cr.CurveTo(171.460938, 42.203125, 171.261719, 42.363281, 171.121094, 42.292969);
			cr.CurveTo(170.921875, 42.191406, 170.804688, 41.976563, 170.648438, 41.816406);
			cr.CurveTo(170.332031, 41.503906, 170.070313, 41.117188, 169.699219, 40.871094);
			cr.CurveTo(169.570313, 40.785156, 169.367188, 40.941406, 169.226563, 40.871094);
			cr.CurveTo(169.027344, 40.769531, 168.910156, 40.554688, 168.753906, 40.398438);
			cr.CurveTo(168.4375, 40.398438, 168.105469, 40.496094, 167.804688, 40.398438);
			cr.CurveTo(167.59375, 40.328125, 167.53125, 40.023438, 167.332031, 39.921875);
			cr.CurveTo(167.191406, 39.851563, 167.007813, 39.972656, 166.859375, 39.921875);
			cr.CurveTo(166.523438, 39.8125, 166.246094, 39.5625, 165.910156, 39.449219);
			cr.CurveTo(165.761719, 39.398438, 165.578125, 39.519531, 165.4375, 39.449219);
			cr.CurveTo(165.238281, 39.351563, 165.183594, 39.03125, 164.964844, 38.976563);
			cr.CurveTo(164.503906, 38.863281, 164.007813, 39.070313, 163.542969, 38.976563);
			cr.CurveTo(162.5, 38.769531, 162.222656, 37.765625, 161.175781, 37.554688);
			cr.CurveTo(160.402344, 37.402344, 159.597656, 37.554688, 158.808594, 37.554688);
			cr.CurveTo(157.546875, 37.554688, 156.285156, 37.554688, 155.019531, 37.554688);
			cr.CurveTo(154.074219, 37.554688, 153.128906, 37.554688, 152.179688, 37.554688);
			cr.CurveTo(151.707031, 37.554688, 151.214844, 37.425781, 150.761719, 37.554688);
			cr.CurveTo(150.082031, 37.75, 149.515625, 38.226563, 148.867188, 38.503906);
			cr.CurveTo(148.40625, 38.699219, 147.917969, 38.820313, 147.445313, 38.976563);
			cr.CurveTo(146.972656, 39.132813, 146.515625, 39.367188, 146.023438, 39.449219);
			cr.CurveTo(145.558594, 39.527344, 145.074219, 39.390625, 144.605469, 39.449219);
			cr.CurveTo(139.925781, 40.035156, 144.128906, 39.660156, 140.816406, 40.398438);
			cr.CurveTo(139.878906, 40.605469, 138.894531, 40.59375, 137.976563, 40.871094);
			cr.CurveTo(137.300781, 41.074219, 136.773438, 41.679688, 136.082031, 41.816406);
			cr.CurveTo(135.152344, 42.003906, 134.1875, 41.816406, 133.238281, 41.816406);
			cr.CurveTo(131.976563, 41.816406, 130.714844, 41.816406, 129.453125, 41.816406);
			cr.CurveTo(126.925781, 41.816406, 124.402344, 41.816406, 121.875, 41.816406);
			cr.CurveTo(120.929688, 41.816406, 119.980469, 41.816406, 119.035156, 41.816406);
			cr.CurveTo(118.402344, 41.816406, 117.769531, 41.878906, 117.140625, 41.816406);
			cr.CurveTo(113.777344, 41.480469, 110.574219, 40.207031, 107.195313, 39.921875);
			cr.CurveTo(106.253906, 39.84375, 105.296875, 40.019531, 104.355469, 39.921875);
			cr.CurveTo(103.707031, 39.859375, 103.109375, 39.53125, 102.460938, 39.449219);
			cr.CurveTo(101.835938, 39.371094, 101.191406, 39.539063, 100.566406, 39.449219);
			cr.CurveTo(100.074219, 39.378906, 99.640625, 39.058594, 99.148438, 38.976563);
			cr.CurveTo(98.679688, 38.898438, 98.195313, 39.054688, 97.726563, 38.976563);
			cr.CurveTo(96.378906, 38.753906, 96.464844, 38.472656, 95.359375, 38.03125);
			cr.CurveTo(94.894531, 37.84375, 94.367188, 37.8125, 93.9375, 37.554688);
			cr.CurveTo(93.554688, 37.328125, 93.347656, 36.878906, 92.992188, 36.609375);
			cr.CurveTo(92.710938, 36.398438, 92.328125, 36.347656, 92.042969, 36.136719);
			cr.CurveTo(91.6875, 35.867188, 91.480469, 35.417969, 91.097656, 35.1875);
			cr.CurveTo(90.667969, 34.933594, 90.078125, 35.015625, 89.675781, 34.714844);
			cr.CurveTo(89.394531, 34.503906, 89.453125, 34.019531, 89.203125, 33.769531);
			cr.CurveTo(88.703125, 33.269531, 87.875, 33.246094, 87.308594, 32.820313);
			cr.CurveTo(86.953125, 32.554688, 86.734375, 32.121094, 86.363281, 31.875);
			cr.CurveTo(86.230469, 31.785156, 86.039063, 31.925781, 85.890625, 31.875);
			cr.CurveTo(82.460938, 30.730469, 86.636719, 32.011719, 84.46875, 30.925781);
			cr.CurveTo(84.171875, 30.777344, 82.871094, 31.074219, 82.574219, 30.925781);
			cr.CurveTo(82.375, 30.828125, 82.300781, 30.554688, 82.101563, 30.453125);
			cr.CurveTo(81.957031, 30.382813, 80.894531, 30.453125, 80.679688, 30.453125);
			cr.CurveTo(80.050781, 30.453125, 79.417969, 30.453125, 78.785156, 30.453125);
			cr.CurveTo(78.472656, 30.453125, 78.140625, 30.355469, 77.839844, 30.453125);
			cr.CurveTo(77.503906, 30.566406, 77.226563, 30.816406, 76.890625, 30.925781);
			cr.CurveTo(76.59375, 31.027344, 76.261719, 30.925781, 75.945313, 30.925781);
			cr.CurveTo(75.3125, 30.925781, 74.683594, 30.925781, 74.050781, 30.925781);
			cr.CurveTo(73.734375, 30.925781, 73.421875, 30.925781, 73.105469, 30.925781);
			cr.CurveTo(72.945313, 30.925781, 72.773438, 30.855469, 72.628906, 30.925781);
			cr.CurveTo(72.429688, 31.027344, 72.355469, 31.300781, 72.15625, 31.402344);
			cr.CurveTo(72.015625, 31.472656, 71.839844, 31.402344, 71.683594, 31.402344);
			cr.CurveTo(71.367188, 31.402344, 71.035156, 31.300781, 70.738281, 31.402344);
			cr.CurveTo(70.523438, 31.472656, 70.464844, 31.773438, 70.261719, 31.875);
			cr.CurveTo(70.121094, 31.945313, 69.949219, 31.875, 69.789063, 31.875);
			cr.CurveTo(69.472656, 31.875, 69.160156, 31.875, 68.84375, 31.875);
			cr.CurveTo(67.582031, 31.875, 66.316406, 31.875, 65.054688, 31.875);
			cr.CurveTo(60.792969, 31.875, 56.53125, 31.875, 52.269531, 31.875);
			cr.CurveTo(51.953125, 31.875, 51.621094, 31.773438, 51.324219, 31.875);
			cr.CurveTo(49.566406, 32.460938, 51.898438, 32.347656, 50.375, 32.347656);
			cr.CurveTo(50.21875, 32.347656, 49.972656, 32.207031, 49.902344, 32.347656);
			cr.CurveTo(49.761719, 32.628906, 50.042969, 33.011719, 49.902344, 33.292969);
			cr.CurveTo(49.832031, 33.4375, 49.539063, 33.183594, 49.429688, 33.292969);
			cr.CurveTo(49.316406, 33.40625, 49.539063, 33.65625, 49.429688, 33.769531);
			cr.CurveTo(49.316406, 33.878906, 49.066406, 33.65625, 48.957031, 33.769531);
			cr.CurveTo(48.640625, 34.082031, 49.269531, 34.398438, 48.957031, 34.714844);
			cr.CurveTo(48.84375, 34.828125, 48.59375, 34.605469, 48.480469, 34.714844);
			cr.CurveTo(48.371094, 34.828125, 48.480469, 35.03125, 48.480469, 35.1875);
			cr.CurveTo(48.480469, 35.347656, 48.480469, 35.503906, 48.480469, 35.664063);
			cr.CurveTo(48.480469, 36.609375, 48.480469, 37.554688, 48.480469, 38.503906);
			cr.CurveTo(48.480469, 38.660156, 48.59375, 38.863281, 48.480469, 38.976563);
			cr.CurveTo(48.257813, 39.199219, 47.757813, 38.753906, 47.535156, 38.976563);
			cr.CurveTo(47.515625, 38.996094, 47.535156, 40.695313, 47.535156, 40.871094);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			matrix = new Matrix(3.543307, 0, 0, 3.543307, -250.775794, -287.969644);
			pattern.Matrix = matrix;
			cr.StrokePreserve();
			if (pattern != null) pattern.Dispose();

			cr.Operator = Operator.Over;
			cr.LineWidth = 8;
			cr.MiterLimit = 4;
			cr.LineCap = LineCap.Butt;
			cr.LineJoin = LineJoin.Miter;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(61.265625, 53.183594);
			cr.CurveTo(59.773438, 53.773438, 59.117188, 55.089844, 57.953125, 56.023438);
			cr.CurveTo(57.507813, 56.378906, 56.988281, 56.628906, 56.53125, 56.96875);
			cr.CurveTo(55.996094, 57.371094, 55.585938, 57.917969, 55.109375, 58.390625);
			cr.CurveTo(54.796875, 58.707031, 54.410156, 58.964844, 54.164063, 59.335938);
			cr.CurveTo(53.96875, 59.632813, 53.941406, 60.035156, 53.691406, 60.285156);
			cr.CurveTo(53.441406, 60.535156, 52.992188, 60.507813, 52.742188, 60.757813);
			cr.CurveTo(52.492188, 61.007813, 52.464844, 61.410156, 52.269531, 61.707031);
			cr.CurveTo(52.144531, 61.890625, 51.953125, 62.019531, 51.796875, 62.179688);
			cr.CurveTo(51.492188, 62.480469, 50.199219, 63.628906, 49.902344, 64.074219);
			cr.CurveTo(49.707031, 64.367188, 49.625, 64.726563, 49.429688, 65.019531);
			cr.CurveTo(49.179688, 65.390625, 48.625, 65.542969, 48.480469, 65.964844);
			cr.CurveTo(48.382813, 66.265625, 48.582031, 66.613281, 48.480469, 66.914063);
			cr.CurveTo(48.339844, 67.335938, 47.734375, 67.460938, 47.535156, 67.859375);
			cr.CurveTo(47.464844, 68, 47.605469, 68.191406, 47.535156, 68.335938);
			cr.CurveTo(47.433594, 68.535156, 47.183594, 68.621094, 47.0625, 68.808594);
			cr.CurveTo(46.867188, 69.101563, 46.785156, 69.460938, 46.585938, 69.753906);
			cr.CurveTo(46.464844, 69.941406, 46.214844, 70.027344, 46.113281, 70.226563);
			cr.CurveTo(45.972656, 70.511719, 46.214844, 70.875, 46.113281, 71.175781);
			cr.CurveTo(46.003906, 71.511719, 45.835938, 71.828125, 45.640625, 72.121094);
			cr.CurveTo(45.515625, 72.308594, 45.238281, 72.382813, 45.167969, 72.597656);
			cr.CurveTo(45.023438, 73.023438, 45.308594, 73.589844, 45.167969, 74.015625);
			cr.CurveTo(44.945313, 74.6875, 44.445313, 75.242188, 44.21875, 75.910156);
			cr.CurveTo(44.171875, 76.058594, 44.269531, 76.234375, 44.21875, 76.382813);
			cr.CurveTo(44.109375, 76.71875, 43.859375, 76.996094, 43.746094, 77.332031);
			cr.CurveTo(43.648438, 77.628906, 43.847656, 77.980469, 43.746094, 78.277344);
			cr.CurveTo(43.636719, 78.613281, 43.386719, 78.890625, 43.273438, 79.226563);
			cr.CurveTo(43.222656, 79.375, 43.324219, 79.550781, 43.273438, 79.699219);
			cr.CurveTo(43.160156, 80.035156, 42.910156, 80.3125, 42.800781, 80.644531);
			cr.CurveTo(42.707031, 80.925781, 42.800781, 82.75, 42.800781, 83.011719);
			cr.CurveTo(42.800781, 83.171875, 42.800781, 83.328125, 42.800781, 83.488281);
			cr.CurveTo(42.800781, 83.644531, 42.6875, 83.847656, 42.800781, 83.960938);
			cr.CurveTo(42.910156, 84.070313, 43.160156, 83.847656, 43.273438, 83.960938);
			cr.CurveTo(43.445313, 84.128906, 43.121094, 85.546875, 43.273438, 85.855469);
			cr.CurveTo(43.34375, 85.996094, 43.589844, 85.855469, 43.746094, 85.855469);
			cr.CurveTo(43.90625, 86.167969, 44.109375, 86.464844, 44.21875, 86.800781);
			cr.CurveTo(44.320313, 87.101563, 44.078125, 87.464844, 44.21875, 87.75);
			cr.CurveTo(44.320313, 87.949219, 44.59375, 88.023438, 44.695313, 88.222656);
			cr.CurveTo(44.765625, 88.363281, 44.625, 88.554688, 44.695313, 88.695313);
			cr.CurveTo(44.792969, 88.894531, 45.097656, 88.957031, 45.167969, 89.167969);
			cr.CurveTo(45.265625, 89.46875, 45.027344, 89.832031, 45.167969, 90.117188);
			cr.CurveTo(45.238281, 90.257813, 45.527344, 90.003906, 45.640625, 90.117188);
			cr.CurveTo(45.863281, 90.339844, 45.5, 90.78125, 45.640625, 91.0625);
			cr.CurveTo(45.742188, 91.261719, 45.957031, 91.378906, 46.113281, 91.535156);
			cr.CurveTo(46.113281, 91.851563, 46.015625, 92.183594, 46.113281, 92.484375);
			cr.CurveTo(46.253906, 92.90625, 46.921875, 93.007813, 47.0625, 93.429688);
			cr.CurveTo(47.160156, 93.730469, 46.960938, 94.078125, 47.0625, 94.378906);
			cr.CurveTo(47.132813, 94.589844, 47.410156, 94.664063, 47.535156, 94.851563);
			cr.CurveTo(48.578125, 95.089844, 47.785156, 95.824219, 48.007813, 96.269531);
			cr.CurveTo(49.09375, 98.4375, 47.8125, 94.261719, 48.957031, 97.691406);
			cr.CurveTo(49.003906, 97.839844, 48.90625, 98.015625, 48.957031, 98.164063);
			cr.CurveTo(49.066406, 98.5, 49.269531, 98.796875, 49.429688, 99.113281);
			cr.CurveTo(49.585938, 99.429688, 49.707031, 99.765625, 49.902344, 100.058594);
			cr.CurveTo(50.027344, 100.246094, 50.253906, 100.347656, 50.375, 100.53125);
			cr.CurveTo(50.765625, 101.121094, 50.929688, 101.839844, 51.324219, 102.425781);
			cr.CurveTo(52.046875, 103.511719, 51.699219, 102.128906, 52.269531, 103.847656);
			cr.CurveTo(52.371094, 104.148438, 52.171875, 104.496094, 52.269531, 104.792969);
			cr.CurveTo(53.414063, 108.222656, 52.132813, 104.046875, 53.21875, 106.214844);
			cr.CurveTo(53.800781, 107.382813, 51.84375, 105.789063, 53.691406, 107.636719);
			cr.CurveTo(53.941406, 107.886719, 54.386719, 107.859375, 54.636719, 108.109375);
			cr.CurveTo(54.75, 108.21875, 54.550781, 108.453125, 54.636719, 108.582031);
			cr.CurveTo(54.886719, 108.953125, 55.335938, 109.15625, 55.585938, 109.53125);
			cr.CurveTo(55.671875, 109.660156, 55.496094, 109.871094, 55.585938, 110.003906);
			cr.CurveTo(55.832031, 110.375, 56.285156, 110.578125, 56.53125, 110.949219);
			cr.CurveTo(56.617188, 111.082031, 56.460938, 111.28125, 56.53125, 111.421875);
			cr.CurveTo(56.730469, 111.824219, 57.277344, 111.972656, 57.480469, 112.371094);
			cr.CurveTo(57.550781, 112.511719, 57.40625, 112.703125, 57.480469, 112.84375);
			cr.CurveTo(57.578125, 113.042969, 57.792969, 113.160156, 57.953125, 113.316406);
			cr.CurveTo(58.269531, 113.632813, 58.652344, 113.894531, 58.898438, 114.265625);
			cr.CurveTo(58.988281, 114.394531, 58.898438, 114.582031, 58.898438, 114.738281);
			cr.CurveTo(59.058594, 115.054688, 59.121094, 115.4375, 59.371094, 115.683594);
			cr.CurveTo(59.484375, 115.796875, 59.777344, 115.542969, 59.847656, 115.683594);
			cr.CurveTo(59.988281, 115.96875, 59.671875, 116.371094, 59.847656, 116.632813);
			cr.CurveTo(60.042969, 116.925781, 60.542969, 116.855469, 60.792969, 117.105469);
			cr.CurveTo(60.90625, 117.21875, 60.679688, 117.46875, 60.792969, 117.578125);
			cr.CurveTo(60.90625, 117.691406, 61.15625, 117.46875, 61.265625, 117.578125);
			cr.CurveTo(61.378906, 117.691406, 61.15625, 117.941406, 61.265625, 118.054688);
			cr.CurveTo(61.378906, 118.164063, 61.628906, 117.941406, 61.738281, 118.054688);
			cr.CurveTo(61.851563, 118.164063, 61.597656, 118.457031, 61.738281, 118.527344);
			cr.CurveTo(63.242188, 119.277344, 62.179688, 117.546875, 63.632813, 119);
			cr.CurveTo(63.746094, 119.113281, 63.523438, 119.363281, 63.632813, 119.472656);
			cr.CurveTo(63.65625, 119.496094, 65.777344, 119.472656, 66, 119.472656);
			cr.CurveTo(66.460938, 119.472656, 68.703125, 119.542969, 68.84375, 119.472656);
			cr.CurveTo(71.011719, 118.390625, 66.832031, 119.667969, 70.261719, 118.527344);
			cr.CurveTo(70.414063, 118.476563, 70.605469, 118.613281, 70.738281, 118.527344);
			cr.CurveTo(71.109375, 118.277344, 71.300781, 117.808594, 71.683594, 117.578125);
			cr.CurveTo(72.113281, 117.324219, 72.65625, 117.328125, 73.105469, 117.105469);
			cr.CurveTo(73.503906, 116.90625, 73.652344, 116.359375, 74.050781, 116.160156);
			cr.CurveTo(74.496094, 115.933594, 75.074219, 115.984375, 75.472656, 115.683594);
			cr.CurveTo(75.753906, 115.472656, 75.695313, 114.988281, 75.945313, 114.738281);
			cr.CurveTo(76.058594, 114.625, 76.289063, 114.824219, 76.417969, 114.738281);
			cr.CurveTo(76.789063, 114.492188, 76.996094, 114.039063, 77.367188, 113.792969);
			cr.CurveTo(77.496094, 113.703125, 77.679688, 113.792969, 77.839844, 113.792969);
			cr.CurveTo(78.15625, 113.792969, 78.503906, 113.933594, 78.785156, 113.792969);
			cr.CurveTo(79.1875, 113.589844, 79.375, 113.113281, 79.734375, 112.84375);
			cr.CurveTo(81.203125, 111.742188, 80.15625, 112.621094, 81.152344, 112.371094);
			cr.CurveTo(85.09375, 111.386719, 80.582031, 112.402344, 83.523438, 111.421875);
			cr.CurveTo(83.820313, 111.324219, 84.1875, 111.566406, 84.46875, 111.421875);
			cr.CurveTo(84.867188, 111.222656, 84.992188, 110.617188, 85.414063, 110.476563);
			cr.CurveTo(87.570313, 110.476563, 85.441406, 110.699219, 86.835938, 110.003906);
			cr.CurveTo(86.976563, 109.933594, 87.199219, 110.113281, 87.308594, 110.003906);
			cr.CurveTo(87.421875, 109.890625, 87.238281, 109.671875, 87.308594, 109.53125);
			cr.CurveTo(87.410156, 109.332031, 87.625, 109.214844, 87.78125, 109.054688);
			cr.CurveTo(88.097656, 108.742188, 88.371094, 108.375, 88.730469, 108.109375);
			cr.CurveTo(89.011719, 107.898438, 89.425781, 107.886719, 89.675781, 107.636719);
			cr.CurveTo(89.925781, 107.386719, 89.902344, 106.9375, 90.152344, 106.6875);
			cr.CurveTo(90.398438, 106.4375, 90.796875, 106.398438, 91.097656, 106.214844);
			cr.CurveTo(91.585938, 105.921875, 92.007813, 105.523438, 92.519531, 105.269531);
			cr.CurveTo(92.964844, 105.042969, 93.492188, 105.019531, 93.9375, 104.792969);
			cr.CurveTo(94.449219, 104.539063, 94.851563, 104.101563, 95.359375, 103.847656);
			cr.CurveTo(95.804688, 103.625, 96.332031, 103.597656, 96.78125, 103.375);
			cr.CurveTo(97.796875, 102.863281, 98.632813, 102.042969, 99.621094, 101.480469);
			cr.CurveTo(100.234375, 101.128906, 100.902344, 100.882813, 101.515625, 100.53125);
			cr.CurveTo(102.691406, 100.394531, 103.015625, 99.160156, 103.882813, 98.640625);
			cr.CurveTo(104.308594, 98.382813, 104.855469, 98.390625, 105.304688, 98.164063);
			cr.CurveTo(105.703125, 97.964844, 105.835938, 97.382813, 106.25, 97.21875);
			cr.CurveTo(106.855469, 96.976563, 107.527344, 96.949219, 108.144531, 96.746094);
			cr.CurveTo(108.480469, 96.632813, 108.773438, 96.429688, 109.089844, 96.269531);
			cr.CurveTo(109.40625, 96.113281, 109.789063, 96.046875, 110.039063, 95.796875);
			cr.CurveTo(110.289063, 95.546875, 110.21875, 95.046875, 110.511719, 94.851563);
			cr.CurveTo(111.933594, 93.902344, 110.984375, 95.324219, 111.933594, 94.851563);
			cr.CurveTo(112.132813, 94.75, 112.207031, 94.476563, 112.40625, 94.378906);
			cr.CurveTo(112.6875, 94.234375, 113.128906, 94.601563, 113.351563, 94.378906);
			cr.CurveTo(113.464844, 94.265625, 113.28125, 94.046875, 113.351563, 93.902344);
			cr.CurveTo(113.875, 92.855469, 113.644531, 93.757813, 114.300781, 93.429688);
			cr.CurveTo(114.5, 93.332031, 114.574219, 93.054688, 114.773438, 92.957031);
			cr.CurveTo(114.914063, 92.886719, 115.089844, 92.957031, 115.246094, 92.957031);
			cr.CurveTo(115.5625, 92.957031, 115.894531, 93.058594, 116.195313, 92.957031);
			cr.CurveTo(116.617188, 92.816406, 116.742188, 92.210938, 117.140625, 92.007813);
			cr.CurveTo(117.28125, 91.9375, 117.472656, 92.082031, 117.613281, 92.007813);
			cr.CurveTo(117.8125, 91.910156, 117.886719, 91.636719, 118.085938, 91.535156);
			cr.CurveTo(118.230469, 91.464844, 118.410156, 91.585938, 118.5625, 91.535156);
			cr.CurveTo(118.894531, 91.425781, 119.171875, 91.175781, 119.507813, 91.0625);
			cr.CurveTo(120.382813, 90.769531, 119.734375, 91.785156, 120.929688, 90.589844);
			cr.CurveTo(121.039063, 90.476563, 120.816406, 90.226563, 120.929688, 90.117188);
			cr.CurveTo(121.039063, 90.003906, 121.253906, 90.164063, 121.402344, 90.117188);
			cr.CurveTo(121.738281, 90.003906, 122.054688, 89.839844, 122.347656, 89.640625);
			cr.CurveTo(122.542969, 89.511719, 123.640625, 88.417969, 123.769531, 88.222656);
			cr.CurveTo(123.964844, 87.929688, 123.960938, 87.488281, 124.242188, 87.273438);
			cr.CurveTo(124.644531, 86.976563, 125.265625, 87.101563, 125.664063, 86.800781);
			cr.CurveTo(125.945313, 86.589844, 125.886719, 86.105469, 126.136719, 85.855469);
			cr.CurveTo(126.636719, 85.355469, 127.464844, 85.332031, 128.03125, 84.90625);
			cr.CurveTo(131.003906, 82.675781, 126.535156, 85.332031, 130.398438, 83.011719);
			cr.CurveTo(131.003906, 82.648438, 131.707031, 82.457031, 132.292969, 82.066406);
			cr.CurveTo(132.851563, 81.695313, 133.15625, 81.015625, 133.714844, 80.644531);
			cr.CurveTo(133.84375, 80.558594, 134.054688, 80.734375, 134.1875, 80.644531);
			cr.CurveTo(134.558594, 80.398438, 134.734375, 79.898438, 135.132813, 79.699219);
			cr.CurveTo(136.632813, 78.949219, 135.574219, 80.679688, 137.027344, 79.226563);
			cr.CurveTo(137.277344, 78.976563, 137.253906, 78.527344, 137.5, 78.277344);
			cr.CurveTo(137.75, 78.027344, 138.15625, 78, 138.449219, 77.804688);
			cr.CurveTo(138.632813, 77.679688, 138.722656, 77.429688, 138.921875, 77.332031);
			cr.CurveTo(139.0625, 77.261719, 139.253906, 77.402344, 139.394531, 77.332031);
			cr.CurveTo(139.90625, 77.078125, 140.328125, 76.675781, 140.816406, 76.382813);
			cr.CurveTo(141.421875, 76.019531, 142.054688, 75.699219, 142.710938, 75.4375);
			cr.CurveTo(143.171875, 75.25, 143.65625, 75.121094, 144.128906, 74.964844);
			cr.CurveTo(144.605469, 74.804688, 145.0625, 74.585938, 145.550781, 74.488281);
			cr.CurveTo(145.859375, 74.429688, 146.195313, 74.578125, 146.5, 74.488281);
			cr.CurveTo(147.316406, 74.257813, 148.058594, 73.8125, 148.867188, 73.542969);
			cr.CurveTo(149.015625, 73.492188, 149.191406, 73.59375, 149.339844, 73.542969);
			cr.CurveTo(149.757813, 73.402344, 151.804688, 72.371094, 152.179688, 72.121094);
			cr.CurveTo(152.367188, 72, 152.496094, 71.808594, 152.652344, 71.648438);
			cr.CurveTo(152.96875, 71.332031, 153.285156, 71.015625, 153.601563, 70.703125);
			cr.CurveTo(153.757813, 70.542969, 153.949219, 70.414063, 154.074219, 70.226563);
			cr.CurveTo(154.269531, 69.933594, 154.296875, 69.53125, 154.546875, 69.28125);
			cr.CurveTo(154.660156, 69.167969, 154.878906, 69.351563, 155.019531, 69.28125);
			cr.CurveTo(155.453125, 69.066406, 155.851563, 68.207031, 155.96875, 67.859375);
			cr.CurveTo(156.015625, 67.71875, 156.015625, 65.589844, 155.96875, 65.492188);
			cr.CurveTo(155.683594, 64.925781, 154.832031, 64.640625, 154.546875, 64.074219);
			cr.CurveTo(153.953125, 62.878906, 156.347656, 64.484375, 153.601563, 62.652344);
			cr.CurveTo(153.46875, 62.566406, 153.269531, 62.722656, 153.128906, 62.652344);
			cr.CurveTo(152.707031, 62.441406, 152.136719, 61.519531, 151.707031, 61.230469);
			cr.CurveTo(151.414063, 61.035156, 151.007813, 61.007813, 150.761719, 60.757813);
			cr.CurveTo(150.511719, 60.507813, 150.535156, 60.0625, 150.285156, 59.8125);
			cr.CurveTo(150.175781, 59.699219, 149.925781, 59.921875, 149.8125, 59.8125);
			cr.CurveTo(149.703125, 59.699219, 149.925781, 59.449219, 149.8125, 59.335938);
			cr.CurveTo(149.5625, 59.089844, 149.117188, 59.113281, 148.867188, 58.863281);
			cr.CurveTo(148.753906, 58.753906, 149.007813, 58.460938, 148.867188, 58.390625);
			cr.CurveTo(148.582031, 58.25, 148.21875, 58.492188, 147.917969, 58.390625);
			cr.CurveTo(147.707031, 58.320313, 147.644531, 58.015625, 147.445313, 57.917969);
			cr.CurveTo(147.304688, 57.847656, 147.121094, 57.96875, 146.972656, 57.917969);
			cr.CurveTo(146.636719, 57.804688, 146.359375, 57.554688, 146.023438, 57.445313);
			cr.CurveTo(145.875, 57.394531, 145.699219, 57.492188, 145.550781, 57.445313);
			cr.CurveTo(142.121094, 56.300781, 146.296875, 57.582031, 144.128906, 56.496094);
			cr.CurveTo(143.988281, 56.425781, 143.796875, 56.566406, 143.65625, 56.496094);
			cr.CurveTo(143.457031, 56.398438, 143.382813, 56.121094, 143.183594, 56.023438);
			cr.CurveTo(142.902344, 55.882813, 142.535156, 56.121094, 142.238281, 56.023438);
			cr.CurveTo(141.902344, 55.910156, 141.625, 55.660156, 141.289063, 55.550781);
			cr.CurveTo(141.140625, 55.5, 140.957031, 55.621094, 140.816406, 55.550781);
			cr.CurveTo(140.617188, 55.449219, 140.554688, 55.148438, 140.34375, 55.074219);
			cr.CurveTo(140.042969, 54.976563, 139.695313, 55.175781, 139.394531, 55.074219);
			cr.CurveTo(139.183594, 55.003906, 139.121094, 54.703125, 138.921875, 54.601563);
			cr.CurveTo(138.640625, 54.460938, 138.273438, 54.703125, 137.976563, 54.601563);
			cr.CurveTo(137.640625, 54.492188, 137.371094, 54.214844, 137.027344, 54.128906);
			cr.CurveTo(136.539063, 54.007813, 135.675781, 54.128906, 135.132813, 54.128906);
			cr.CurveTo(134.027344, 54.128906, 132.925781, 54.128906, 131.820313, 54.128906);
			cr.CurveTo(131.324219, 54.128906, 129.363281, 54.03125, 128.976563, 54.128906);
			cr.CurveTo(128.636719, 54.214844, 128.367188, 54.492188, 128.03125, 54.601563);
			cr.CurveTo(127.882813, 54.652344, 127.707031, 54.550781, 127.558594, 54.601563);
			cr.CurveTo(127.222656, 54.714844, 126.945313, 54.964844, 126.609375, 55.074219);
			cr.CurveTo(126.460938, 55.125, 126.277344, 55.003906, 126.136719, 55.074219);
			cr.CurveTo(125.9375, 55.175781, 125.863281, 55.449219, 125.664063, 55.550781);
			cr.CurveTo(125.523438, 55.621094, 125.347656, 55.550781, 125.191406, 55.550781);
			cr.CurveTo(124.875, 55.550781, 124.550781, 55.472656, 124.242188, 55.550781);
			cr.CurveTo(123.902344, 55.636719, 123.632813, 55.910156, 123.296875, 56.023438);
			cr.CurveTo(123.144531, 56.074219, 122.964844, 55.953125, 122.824219, 56.023438);
			cr.CurveTo(122.625, 56.121094, 122.5625, 56.425781, 122.347656, 56.496094);
			cr.CurveTo(122.050781, 56.597656, 121.703125, 56.398438, 121.402344, 56.496094);
			cr.CurveTo(120.980469, 56.636719, 120.878906, 57.300781, 120.457031, 57.445313);
			cr.CurveTo(120.238281, 57.515625, 118.773438, 57.339844, 118.5625, 57.445313);
			cr.CurveTo(118.363281, 57.542969, 118.289063, 57.816406, 118.085938, 57.917969);
			cr.CurveTo(117.804688, 58.058594, 117.441406, 57.816406, 117.140625, 57.917969);
			cr.CurveTo(116.804688, 58.027344, 116.527344, 58.277344, 116.195313, 58.390625);
			cr.CurveTo(116.042969, 58.441406, 115.878906, 58.390625, 115.71875, 58.390625);
			cr.CurveTo(114.914063, 58.390625, 114.871094, 58.339844, 113.824219, 58.863281);
			cr.CurveTo(113.425781, 59.0625, 113.300781, 59.671875, 112.878906, 59.8125);
			cr.CurveTo(112.664063, 59.882813, 110.777344, 59.8125, 110.511719, 59.8125);
			cr.CurveTo(109.25, 59.8125, 107.984375, 59.8125, 106.722656, 59.8125);
			cr.CurveTo(106.394531, 59.8125, 104.972656, 59.882813, 104.828125, 59.8125);
			cr.CurveTo(104.628906, 59.710938, 104.566406, 59.410156, 104.355469, 59.335938);
			cr.CurveTo(104.054688, 59.238281, 103.722656, 59.335938, 103.410156, 59.335938);
			cr.CurveTo(102.933594, 59.335938, 102.460938, 59.335938, 101.988281, 59.335938);
			cr.CurveTo(101.042969, 59.335938, 100.09375, 59.335938, 99.148438, 59.335938);
			cr.CurveTo(98.984375, 59.335938, 97.507813, 59.421875, 97.253906, 59.335938);
			cr.CurveTo(96.394531, 59.050781, 96.375, 58.933594, 95.832031, 58.390625);
			cr.CurveTo(95.675781, 58.234375, 95.570313, 57.988281, 95.359375, 57.917969);
			cr.CurveTo(94.761719, 57.71875, 94.0625, 58.117188, 93.464844, 57.917969);
			cr.CurveTo(93.253906, 57.847656, 93.175781, 57.566406, 92.992188, 57.445313);
			cr.CurveTo(92.699219, 57.246094, 92.378906, 57.082031, 92.042969, 56.96875);
			cr.CurveTo(91.542969, 56.800781, 90.652344, 57.136719, 90.152344, 56.96875);
			cr.CurveTo(89.9375, 56.898438, 89.890625, 56.566406, 89.675781, 56.496094);
			cr.CurveTo(89.378906, 56.398438, 89.046875, 56.496094, 88.730469, 56.496094);
			cr.CurveTo(88.574219, 56.496094, 88.398438, 56.566406, 88.257813, 56.496094);
			cr.CurveTo(88.058594, 56.398438, 87.996094, 56.09375, 87.78125, 56.023438);
			cr.CurveTo(87.34375, 55.878906, 85.855469, 56.171875, 85.414063, 56.023438);
			cr.CurveTo(85.082031, 55.910156, 84.804688, 55.660156, 84.46875, 55.550781);
			cr.CurveTo(84.320313, 55.5, 84.136719, 55.621094, 83.996094, 55.550781);
			cr.CurveTo(83.796875, 55.449219, 83.734375, 55.148438, 83.523438, 55.074219);
			cr.CurveTo(83.222656, 54.976563, 82.878906, 55.152344, 82.574219, 55.074219);
			cr.CurveTo(79.386719, 54.277344, 82.566406, 55.070313, 81.628906, 54.128906);
			cr.CurveTo(81.390625, 53.890625, 80.445313, 54.367188, 80.207031, 54.128906);
			cr.CurveTo(80.09375, 54.015625, 80.320313, 53.765625, 80.207031, 53.65625);
			cr.CurveTo(79.984375, 53.433594, 79.542969, 53.796875, 79.261719, 53.65625);
			cr.CurveTo(79.058594, 53.554688, 78.984375, 53.28125, 78.785156, 53.183594);
			cr.CurveTo(78.503906, 53.039063, 78.121094, 53.324219, 77.839844, 53.183594);
			cr.CurveTo(75.867188, 52.195313, 78.34375, 53.214844, 77.367188, 52.234375);
			cr.CurveTo(77.253906, 52.125, 77.003906, 52.347656, 76.890625, 52.234375);
			cr.CurveTo(76.78125, 52.125, 77.003906, 51.875, 76.890625, 51.761719);
			cr.CurveTo(76.667969, 51.539063, 76.226563, 51.902344, 75.945313, 51.761719);
			cr.CurveTo(75.546875, 51.5625, 75.421875, 50.957031, 75, 50.816406);
			cr.CurveTo(74.699219, 50.714844, 74.351563, 50.914063, 74.050781, 50.816406);
			cr.CurveTo(73.839844, 50.742188, 73.777344, 50.441406, 73.578125, 50.339844);
			cr.CurveTo(73.4375, 50.269531, 73.261719, 50.339844, 73.105469, 50.339844);
			cr.CurveTo(72.789063, 50.183594, 72.449219, 50.0625, 72.15625, 49.867188);
			cr.CurveTo(71.972656, 49.742188, 71.882813, 49.492188, 71.683594, 49.394531);
			cr.CurveTo(71.261719, 49.183594, 70.6875, 49.605469, 70.261719, 49.394531);
			cr.CurveTo(70.121094, 49.324219, 70.375, 49.03125, 70.261719, 48.921875);
			cr.CurveTo(70.152344, 48.808594, 69.929688, 48.992188, 69.789063, 48.921875);
			cr.CurveTo(69.589844, 48.820313, 69.515625, 48.546875, 69.316406, 48.445313);
			cr.CurveTo(69.144531, 48.363281, 67.277344, 48.445313, 66.949219, 48.445313);
			cr.CurveTo(66.613281, 48.445313, 64.230469, 48.386719, 64.109375, 48.445313);
			cr.CurveTo(63.90625, 48.546875, 63.832031, 48.820313, 63.632813, 48.921875);
			cr.CurveTo(63.351563, 49.0625, 62.96875, 48.777344, 62.6875, 48.921875);
			cr.CurveTo(60.714844, 49.90625, 63.191406, 48.886719, 62.214844, 49.867188);
			cr.CurveTo(62.101563, 49.980469, 61.851563, 49.753906, 61.738281, 49.867188);
			cr.CurveTo(61.628906, 49.980469, 61.8125, 50.199219, 61.738281, 50.339844);
			cr.CurveTo(61.640625, 50.539063, 61.367188, 50.613281, 61.265625, 50.816406);
			cr.CurveTo(61.195313, 50.957031, 61.378906, 51.175781, 61.265625, 51.289063);
			cr.CurveTo(61.15625, 51.398438, 60.792969, 51.128906, 60.792969, 51.289063);
			cr.CurveTo(60.792969, 51.9375, 61.40625, 52.546875, 61.265625, 53.183594);
			cr.CurveTo(61.066406, 54.082031, 60.421875, 54.832031, 59.847656, 55.550781);
			cr.CurveTo(59.746094, 55.671875, 59.53125, 55.550781, 59.371094, 55.550781);
			cr.CurveTo(59.214844, 55.550781, 58.898438, 55.707031, 58.898438, 55.550781);
			cr.CurveTo(58.898438, 55.390625, 59.484375, 55.4375, 59.371094, 55.550781);
			cr.CurveTo(59.121094, 55.800781, 58.742188, 55.867188, 58.425781, 56.023438);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			matrix = new Matrix(3.543307, 0, 0, 3.543307, -250.775794, -287.969644);
			pattern.Matrix = matrix;
			cr.StrokePreserve();
			if (pattern != null) pattern.Dispose();

			cr.Restore();
		}



		public void DrawWaypointRocks(Context cr, int x, int y, float width, float height, double[] rgba)
		{
			Pattern pattern = null;
			Matrix matrix = cr.Matrix;

			cr.Save();
			float w = 128;
			float h = 69;
			float scale = Math.Min(width / w, height / h);
			matrix.Translate(x + Math.Max(0, (width - w * scale) / 2), y + Math.Max(0, (height - h * scale) / 2));
			matrix.Scale(scale, scale);
			cr.Matrix = matrix;

			cr.Operator = Operator.Over;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(47.019531, 0.488281);
			cr.LineTo(25.589844, 3.167969);
			cr.CurveTo(25.589844, 3.167969, 20.234375, 15.035156, 17.292969, 19.75);
			cr.CurveTo(19.128906, 22.652344, 22.945313, 28.414063, 22.945313, 28.414063);
			cr.LineTo(33.125, 31.847656);
			cr.LineTo(34.308594, 36.34375);
			cr.LineTo(20.574219, 30.070313);
			cr.LineTo(15.289063, 22.042969);
			cr.CurveTo(12.5625, 24.1875, 4.621094, 31.863281, 1.816406, 34.976563);
			cr.CurveTo(-1.195313, 38.324219, 1.816406, 46.695313, 1.816406, 46.695313);
			cr.LineTo(12.867188, 59.75);
			cr.LineTo(24.417969, 60.671875);
			cr.LineTo(33.960938, 52.21875);
			cr.LineTo(44.570313, 53.613281);
			cr.LineTo(47.6875, 62.011719);
			cr.LineTo(73.804688, 59.75);
			cr.LineTo(73.804688, 49.039063);
			cr.LineTo(81.503906, 37.652344);
			cr.LineTo(94.5625, 31.292969);
			cr.LineTo(80.5, 15.554688);
			cr.LineTo(70.445313, 16.179688);
			cr.LineTo(73.964844, 20.957031);
			cr.LineTo(73.90625, 27.882813);
			cr.LineTo(69.527344, 33.859375);
			cr.LineTo(68.578125, 30.605469);
			cr.LineTo(71.332031, 26.914063);
			cr.LineTo(71.449219, 21.648438);
			cr.LineTo(67.105469, 16.226563);
			cr.ClosePath();
			cr.MoveTo(47.019531, 0.488281);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			cr.FillRule = FillRule.Winding;
			cr.FillPreserve();
			if (pattern != null) pattern.Dispose();

			cr.Operator = Operator.Over;
			cr.LineWidth = 1;
			cr.MiterLimit = 4;
			cr.LineCap = LineCap.Butt;
			cr.LineJoin = LineJoin.Miter;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(47.019531, 0.488281);
			cr.LineTo(25.589844, 3.167969);
			cr.CurveTo(25.589844, 3.167969, 20.234375, 15.035156, 17.292969, 19.75);
			cr.CurveTo(19.128906, 22.652344, 22.945313, 28.414063, 22.945313, 28.414063);
			cr.LineTo(33.125, 31.847656);
			cr.LineTo(34.308594, 36.34375);
			cr.LineTo(20.574219, 30.070313);
			cr.LineTo(15.289063, 22.042969);
			cr.CurveTo(12.5625, 24.1875, 4.621094, 31.863281, 1.816406, 34.976563);
			cr.CurveTo(-1.195313, 38.324219, 1.816406, 46.695313, 1.816406, 46.695313);
			cr.LineTo(12.867188, 59.75);
			cr.LineTo(24.417969, 60.671875);
			cr.LineTo(33.960938, 52.21875);
			cr.LineTo(44.570313, 53.613281);
			cr.LineTo(47.6875, 62.011719);
			cr.LineTo(73.804688, 59.75);
			cr.LineTo(73.804688, 49.039063);
			cr.LineTo(81.503906, 37.652344);
			cr.LineTo(94.5625, 31.292969);
			cr.LineTo(80.5, 15.554688);
			cr.LineTo(70.445313, 16.179688);
			cr.LineTo(73.964844, 20.957031);
			cr.LineTo(73.90625, 27.882813);
			cr.LineTo(69.527344, 33.859375);
			cr.LineTo(68.578125, 30.605469);
			cr.LineTo(71.332031, 26.914063);
			cr.LineTo(71.449219, 21.648438);
			cr.LineTo(67.105469, 16.226563);
			cr.ClosePath();
			cr.MoveTo(47.019531, 0.488281);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			matrix = new Matrix(0.9375, 0, 0, 0.9375, -496.731573, -169.619323);
			pattern.Matrix = matrix;
			cr.StrokePreserve();
			if (pattern != null) pattern.Dispose();

			cr.Operator = Operator.Over;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(103.027344, 32.023438);
			cr.LineTo(84.855469, 40.84375);
			cr.LineTo(77.515625, 49.722656);
			cr.LineTo(76.628906, 64.402344);
			cr.LineTo(86.15625, 67.714844);
			cr.LineTo(103.324219, 68.664063);
			cr.LineTo(112.199219, 64.28125);
			cr.LineTo(121.480469, 64.402344);
			cr.LineTo(127.675781, 55.816406);
			cr.LineTo(122.738281, 39.328125);
			cr.LineTo(109.007813, 31.292969);
			cr.ClosePath();
			cr.MoveTo(103.027344, 32.023438);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			cr.FillRule = FillRule.Winding;
			cr.FillPreserve();
			if (pattern != null) pattern.Dispose();

			cr.Operator = Operator.Over;
			cr.LineWidth = 0.264583;
			cr.MiterLimit = 4;
			cr.LineCap = LineCap.Butt;
			cr.LineJoin = LineJoin.Miter;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(103.027344, 32.023438);
			cr.LineTo(84.855469, 40.84375);
			cr.LineTo(77.515625, 49.722656);
			cr.LineTo(76.628906, 64.402344);
			cr.LineTo(86.15625, 67.714844);
			cr.LineTo(103.324219, 68.664063);
			cr.LineTo(112.199219, 64.28125);
			cr.LineTo(121.480469, 64.402344);
			cr.LineTo(127.675781, 55.816406);
			cr.LineTo(122.738281, 39.328125);
			cr.LineTo(109.007813, 31.292969);
			cr.ClosePath();
			cr.MoveTo(103.027344, 32.023438);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			matrix = new Matrix(3.543307, 0, 0, 3.543307, -496.731573, -169.619323);
			pattern.Matrix = matrix;
			cr.StrokePreserve();
			if (pattern != null) pattern.Dispose();

			cr.Operator = Operator.Over;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(44.339844, 63.183594);
			cr.LineTo(41.953125, 56.363281);
			cr.LineTo(35.257813, 55.648438);
			cr.LineTo(27.558594, 62.179688);
			cr.LineTo(27.347656, 65.234375);
			cr.LineTo(32.914063, 67.789063);
			cr.LineTo(40.949219, 67.621094);
			cr.ClosePath();
			cr.MoveTo(44.339844, 63.183594);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			cr.FillRule = FillRule.Winding;
			cr.FillPreserve();
			if (pattern != null) pattern.Dispose();

			cr.Operator = Operator.Over;
			cr.LineWidth = 0.264583;
			cr.MiterLimit = 4;
			cr.LineCap = LineCap.Butt;
			cr.LineJoin = LineJoin.Miter;
			pattern = new SolidPattern(rgba[0], rgba[1], rgba[2], rgba[3]);
			cr.SetSource(pattern);

			cr.NewPath();
			cr.MoveTo(44.339844, 63.183594);
			cr.LineTo(41.953125, 56.363281);
			cr.LineTo(35.257813, 55.648438);
			cr.LineTo(27.558594, 62.179688);
			cr.LineTo(27.347656, 65.234375);
			cr.LineTo(32.914063, 67.789063);
			cr.LineTo(40.949219, 67.621094);
			cr.ClosePath();
			cr.MoveTo(44.339844, 63.183594);
			cr.Tolerance = 0.1;
			cr.Antialias = Antialias.Default;
			matrix = new Matrix(3.543307, 0, 0, 3.543307, -496.731573, -169.619323);
			pattern.Matrix = matrix;
			cr.StrokePreserve();
			if (pattern != null) pattern.Dispose();

			cr.Restore();
		}




		public override void OnUnloaded(ICoreAPI api)
		{
			for (int i = 0; toolModes != null && i < toolModes.Length; i++)
			{
				toolModes[i]?.Dispose();
			}
		}



	}
}
