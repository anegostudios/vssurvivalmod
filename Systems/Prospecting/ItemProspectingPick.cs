using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent
{
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
				ppws = ObjectCacheUtil.GetOrCreate(api, "propickworkspace", () =>
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
            float dropMul = 1f;
            if (block.BlockMaterial == EnumBlockMaterial.Ore || block.BlockMaterial == EnumBlockMaterial.Stone) dropMul = 0;

            block.OnBlockBroken(world, blockSel.Position, byPlayer, dropMul);

            if (!isPropickable(block)) return;

			IServerPlayer splr = byPlayer as IServerPlayer;
			if (splr == null) return;

			BlockPos pos = blockSel.Position.Copy();

			Dictionary<string, int> quantityFound = new Dictionary<string, int>();

			api.World.BlockAccessor.WalkBlocks(pos.AddCopy(radius, radius, radius), pos.AddCopy(-radius, -radius, -radius), (nblock, x, y, z) =>
			{
				if (nblock.BlockMaterial == EnumBlockMaterial.Ore && nblock.Variant.ContainsKey("type"))
				{
					string key = "ore-" + nblock.Variant["type"];

                    quantityFound.TryGetValue(key, out int q);

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

        private bool isPropickable(Block block)
        {
            return block?.Attributes?["propickable"].AsBool(false) == true;
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

            if (!isPropickable(block)) return;

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
                    attr.value = Array.Empty<int>();
                }
            }
        }

        protected virtual void PrintProbeResults(IWorldAccessor world, IServerPlayer splr, ItemSlot itemslot, BlockPos pos)
        {
            var results = GenProbeResults(world, pos);
            var textResults = results.ToHumanReadable(splr.LanguageCode, ppws.pageCodes);
            splr.SendMessage(GlobalConstants.InfoLogChatGroup, textResults, EnumChatType.Notification);

            world.Api.ModLoader.GetModSystem<ModSystemOreMap>()?.DidProbe(results, splr);
        }

        protected virtual PropickReading GenProbeResults(IWorldAccessor world, BlockPos pos)
        {
            DepositVariant[] deposits = api.ModLoader.GetModSystem<GenDeposits>()?.Deposits;
            if (deposits == null) return null;

            IBlockAccessor blockAccess = world.BlockAccessor;
            int regsize = blockAccess.RegionSize;
            IMapRegion reg = world.BlockAccessor.GetMapRegion(pos.X / regsize, pos.Z / regsize);
            int lx = pos.X % regsize;
            int lz = pos.Z % regsize;

            pos = pos.Copy();
            pos.Y = world.BlockAccessor.GetTerrainMapheightAt(pos);
            int[] blockColumn = ppws.GetRockColumn(pos.X, pos.Z);

            PropickReading readings = new PropickReading();
            readings.Position = new Vec3d(pos.X, pos.Y, pos.Z);

            foreach (var val in reg.OreMaps)
            {
                IntDataMap2D map = val.Value;
                int noiseSize = map.InnerSize;

                float posXInRegionOre = (float)lx / regsize * noiseSize;
                float posZInRegionOre = (float)lz / regsize * noiseSize;

                int oreDist = map.GetUnpaddedColorLerped(posXInRegionOre, posZInRegionOre);


                if (!ppws.depositsByCode.ContainsKey(val.Key))
                {
                    continue;
                }

                ppws.depositsByCode[val.Key].GetPropickReading(pos, oreDist, blockColumn, out double ppt, out double totalFactor);

                if (totalFactor > 0)
                {
                    var reading = new OreReading();
                    reading.TotalFactor = totalFactor;
                    reading.PartsPerThousand = ppt;
                    readings.OreReadings[val.Key] = reading;
                }
            }

            return readings;
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
                    ActionLangCode = "Change tool mode",
                    HotKeyCodes = new string[] { "toolmodeselect" },
                    MouseButton = EnumMouseButton.None
                }
            };

        }
    }
}
