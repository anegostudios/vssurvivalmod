using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using VSSurvivalMod.Systems.ChiselModes;

#nullable disable annotations

namespace Vintagestory.GameContent
{
    public interface ICustomChiselMaterialName
    {
        string GetName(ItemStack itemStack);
    }

    public interface IConditionalChiselable
    {
        bool CanChisel(IWorldAccessor world, BlockPos pos, IPlayer player, out string errorCode);
    }

    /// <summary>
    /// When right clicked on a block, this chisel tool will exchange given block into a chiseledblock which
    /// takes on the model of the block the player interacted with in the first place, but with each voxel being selectable and removable
    /// </summary>
    public class ItemChisel : Item
    {
        public SkillItem[] ToolModes;
        SkillItem addMatItem;

        public bool carvingTime
        {
            get
            {
                var dateTime = DateTime.UtcNow;
                return dateTime.Month == 10 || dateTime.Month == 11;
            }
        }

        public static bool AllowHalloweenEvent = true;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            ToolModes = ObjectCacheUtil.GetOrCreate(api, "chiselToolModes", () =>
            {
                var skillItems = new SkillItem[7] {
                        new SkillItem() {
                            Code = new AssetLocation("1size"),
                            Name = Lang.Get("1x1x1"),
                            Data = new OneByChiselMode()
                        },

                        new SkillItem() {
                            Code = new AssetLocation("2size"),
                            Name = Lang.Get("2x2x2"),
                            Data = new TwoByChiselMode()
                        },

                        new SkillItem() {
                            Code = new AssetLocation("4size"),
                            Name = Lang.Get("4x4x4"),
                            Data = new FourByChiselMode()
                        },

                        new SkillItem() {
                            Code = new AssetLocation("8size"),
                            Name = Lang.Get("8x8x8"),
                            Data = new EightByChiselModeData()
                        },

                        new SkillItem() {
                            Code = new AssetLocation("rotate"),
                            Name = Lang.Get("Rotate"),
                            Data = new RotateChiselMode()
                        },

                        new SkillItem() {
                            Code = new AssetLocation("flip"),
                            Name = Lang.Get("Flip"),
                            Data = new FlipChiselMode()
                        },

                        new SkillItem() {
                            Code = new AssetLocation("rename"),
                            Name = Lang.Get("Set name"),
                            Data = new RenameChiselMode()
                        }
                };

                if (api is ICoreClientAPI capi)
                {
                    skillItems = skillItems.Select(i => {
                        var chiselMode = (ChiselMode)i.Data;
                        return i.WithIcon(capi, chiselMode.DrawAction(capi));
                    }).ToArray();
                }

                return skillItems;
            });

            addMatItem = new SkillItem()
            {
                Name = Lang.Get("chisel-addmat"),
                Code = new AssetLocation("addmat"),
                Enabled = false
            };

            if (api is ICoreClientAPI clientApi)
            {
                addMatItem = addMatItem.WithIcon(clientApi, "plus");
            }
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            for (int i = 0; ToolModes != null && i < ToolModes.Length; i++)
            {
                ToolModes[i]?.Dispose();
            }

            addMatItem?.Dispose();
        }


        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            if ((api as ICoreClientAPI)?.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative) return null;

            return base.GetHeldTpUseAnimation(activeHotbarSlot, forEntity);
        }

        public override string GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity)
        {
            if ((api as ICoreClientAPI)?.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative) return null;

            return base.GetHeldTpHitAnimation(slot, byEntity);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (byEntity?.LeftHandItemSlot?.Itemstack?.Collectible?.GetTool(byEntity.LeftHandItemSlot) != EnumTool.Hammer && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "nohammer", Lang.Get("Requires a hammer in the off hand"));
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            if (blockSel?.Position == null) return;
            var pos = blockSel.Position;
            Block block = byEntity.World.BlockAccessor.GetBlock(pos);

            if (api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(pos) == true)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!byEntity.World.Claims.TryAccess(byPlayer, pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!IsChiselingAllowedFor(api, pos, block, byPlayer))
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }


            if (blockSel == null)   // This line cannot be reached, see the if (blockSel?.Position == null) test 23 lines above
            {
                base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
                return;
            }

            if (block is BlockChisel)
            {
                OnBlockInteract(byEntity.World, byPlayer, blockSel, true, ref handling);
                return;
            }
        }



        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;

            if (blockSel?.Position == null) return;
            var pos = blockSel.Position;
            Block block = byEntity.World.BlockAccessor.GetBlock(pos);

            if (block is BlockForge)
            {
                return;
            }

            if (byPlayer.InventoryManager.OffhandTool != EnumTool.Hammer && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "nohammer", Lang.Get("Requires a hammer in the off hand"));
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }


            if (api.ModLoader.GetModSystem<ModSystemBlockReinforcement>()?.IsReinforced(pos) == true)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (!byEntity.World.Claims.TryAccess(byPlayer, pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            if (block is BlockGroundStorage)
            {
                BlockEntityGroundStorage begs = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityGroundStorage;
                var neslot = begs.Inventory.FirstNonEmptySlot;
                if (neslot != null && neslot.Itemstack.Block != null && IsChiselingAllowedFor(api, pos, neslot.Itemstack.Block, byPlayer))
                {
                    block = neslot.Itemstack.Block;
                }

                if (block.Code.Path == "pumpkin-fruit-4" && (!carvingTime || !AllowHalloweenEvent))
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    api.World.BlockAccessor.MarkBlockDirty(pos);
                    return;
                }
            }

            if (!IsChiselingAllowedFor(api, pos, block, byPlayer))
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (block.Resistance > 100)
            {
                if (api.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "tootoughtochisel", Lang.Get("This material is too strong to chisel"));
                }
                return;
            }


            if (blockSel == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }


            if (block is BlockChisel)
            {
                OnBlockInteract(byEntity.World, byPlayer, blockSel, false, ref handling);
                return;
            }

            if (api is ICoreServerAPI sapi && sapi.Server.Config.LogBlockBreakPlace)
            {
                sapi.Logger.Build("{0} converted {1} to a chiseledblock at {2}", byPlayer.PlayerName, block.Code.ToString(), blockSel.Position);
            }
            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));

            byEntity.World.BlockAccessor.SetBlock(chiseledblock.BlockId, blockSel.Position);

            BlockEntityChisel be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (be == null) return;

            be.WasPlaced(block, null);

            if (carvingTime && block.Code.Path == "pumpkin-fruit-4")
            {
                // Glowy material for the halloween event, but only a quarter of a block
                be.AddMaterial(api.World.GetBlock(new AssetLocation("creativeglow-35")), out _, true, 16*16*16 / 4);
            }

            handling = EnumHandHandling.PreventDefaultAction;
        }

        public static bool IsChiselingAllowedFor(ICoreAPI api, BlockPos pos, Block block, IPlayer player)
        {
            if (block is BlockMicroBlock)
            {
                if (block is BlockChisel) return true;
                return false;   // Existing Microblocks (e.g. in ruins) cannot be further chiseled
            }

            return IsValidChiselingMaterial(api, pos, block, player);
        }

        public static bool IsValidChiselingMaterial(ICoreAPI api, BlockPos pos, Block block, IPlayer player)
        {
            // Can always use a chiseled block as a material in a chiseled block
            if (block is BlockChisel) return true;

            // 1. priority: microblockChiseling disabled
            ITreeAttribute worldConfig = api.World.Config;
            string mode = worldConfig.GetString("microblockChiseling");
            if (mode == "off") return false;

            // 1.5 priority: Disabled by code
            IConditionalChiselable icc = block.GetInterface<IConditionalChiselable>(api.World, pos);
            if (icc != null)
            {
                if (icc?.CanChisel(api.World, pos, player, out string errorCode) == false || icc?.CanChisel(api.World, pos, player, out errorCode) == false)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(icc, errorCode, Lang.Get(errorCode));
                    return false;
                }
            }

            // 2. priority: canChisel flag
            bool canChiselSet = block.Attributes?["canChisel"].Exists == true;
            bool canChisel = block.Attributes?["canChisel"].AsBool(false) == true;

            if (canChisel) return true;
            if (canChiselSet && !canChisel) return false;


            // 3. prio: Never non cubic blocks
            if (block.DrawType != EnumDrawType.Cube && block.Shape?.Base.Path != "block/basic/cube") return false;

            // 4. prio: Not decor blocks
            if (block.HasBehavior<BlockBehaviorDecor>()) return false;

            // Otherwise if in creative mode, sure go ahead
            if (player?.WorldData.CurrentGameMode == EnumGameMode.Creative) return true;

            // allow liquid to be chisel able in creative but disallow otherwise
            if (block.BlockMaterial == EnumBlockMaterial.Water) return false;
			if (block.BlockMaterial == EnumBlockMaterial.Lava) return false;

            // Lastly go by the config value
            if (mode == "stonewood")
            {
                // Saratys definitely required Exception to the rule #312
                if (block.Code.Path.Contains("mudbrick")) return true;

                return block.BlockMaterial == EnumBlockMaterial.Wood || block.BlockMaterial == EnumBlockMaterial.Stone || block.BlockMaterial == EnumBlockMaterial.Ore || block.BlockMaterial == EnumBlockMaterial.Ceramic;
            }

            return true;
        }


        public void OnBlockInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool isBreak, ref EnumHandHandling handling)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return;
            }

            BlockEntityChisel bec = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (bec != null)
            {
                int materialId = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes.GetInt("materialId", -1);
                if (materialId >= 0)
                {
                    bec.SetNowMaterialId(materialId);
                }

                bec.OnBlockInteract(byPlayer, blockSel, isBreak);
                handling = EnumHandHandling.PreventDefaultAction;
            }
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return null;
            BlockEntityChisel be = forPlayer.Entity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (be != null)
            {
                if (be.BlockIds.Length <= 1)
                {
                    addMatItem.Linebreak = true;
                    return ToolModes.Append(addMatItem);
                }

                SkillItem[] mats = new SkillItem[be.BlockIds.Length + 1];
                for (int i = 0; i < be.BlockIds.Length; i++)
                {
                    Block block = api.World.GetBlock(be.BlockIds[i]);
                    DummySlot dummySlot = new(new ItemStack(block));
                    mats[i] = new SkillItem()
                    {
                        Code = block.Code,
                        Data = be.BlockIds[i],
                        Linebreak = i % 7 == 0,
                        Name = block.GetInterface<ICustomChiselMaterialName>(api.World, null)?.GetName(dummySlot.Itemstack) ?? block.GetHeldItemName(dummySlot.Itemstack),
                        RenderHandler = (AssetLocation code, float dt, double atPosX, double atPosY) =>
                        {
                            float wdt = (float)GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
                            ICoreClientAPI capi = api as ICoreClientAPI;
                            capi.Render.RenderItemstackToGui(dummySlot, atPosX + wdt/2, atPosY + wdt/2, 50, wdt/2, ColorUtil.WhiteArgb, true, false, false);
                        }
                    };
                }

                mats[mats.Length - 1] = addMatItem;
                addMatItem.Linebreak = (mats.Length - 1) % 7 == 0;

                return ToolModes.Append(mats);
            }

            return null;
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return slot.Itemstack.Attributes.GetInt("toolMode");
        }

        #nullable enable
        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection? blockSel, int toolMode)
        {
            if (blockSel == null) return;

            var blockPos = blockSel.Position;
            var mouseSlot = byPlayer.InventoryManager.MouseItemSlot;
            if (!mouseSlot.Empty && mouseSlot.Itemstack.Block != null)
            {
                var be = api.World.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityChisel;
                if (be == null) return;

                var mouseStack = mouseSlot.Itemstack;

                if (IsValidChiselingMaterial(api, blockPos, mouseStack.Block, byPlayer))
                {
                    var mouseBlock = mouseStack.Block;

                    var perms = new BlockEntity.CachedAccessPerms(api.World, new(blockPos), byPlayer);
                    if (!perms.TryAccessingBlockAtPos(EnumBlockAccessFlags.BuildOrBreak, true, "Chisel Block Palette")) return;

                    if (mouseBlock is BlockChisel)
                    {
                        int[]? matQuantity = (mouseStack.Attributes?["availMaterialQuantities"] as IntArrayAttribute)?.value;
                        int[]? mats = (mouseStack.Attributes?["materials"] as IntArrayAttribute)?.value;

                        if (mats != null && matQuantity != null)
                        {
                            for (int i = 0; i < mats.Length; i++)
                            {
                                if (mats[i] != 0 && matQuantity[i] > 0)
                                {
                                    var matBlock = api.World.GetBlock(mats[i]);
                                    be.AddMaterial(matBlock, out bool isFull, false, (ushort)matQuantity[i]);
                                }
                            }

                            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                            {
                                mouseSlot.TakeOut(1);
                                mouseSlot.MarkDirty();
                            }
                        }
                    }
                    else
                    {
                        bool compareToPickBlock = byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative;
                        be.AddMaterial(mouseBlock, out bool isFull, compareToPickBlock);

                        if (!isFull && byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        {
                            mouseSlot.TakeOut(1);
                            mouseSlot.MarkDirty();
                        }
                    }

                    if (api is ICoreServerAPI sapi && sapi.Server.Config.LogBlockBreakPlace)
                    {
                        sapi.Logger.Build("{0} added chisel material {1} at {2}", byPlayer.PlayerName, mouseBlock.Code.ToString(), blockPos);
                    }

                    be.MarkDirty();
                    api.Event.PushEvent("keepopentoolmodedlg");
                }

                return;
            }

            if (toolMode > ToolModes.Length - 1)
            {
                int matNum = toolMode - ToolModes.Length;
                var be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
                if (be != null && be.BlockIds.Length > matNum)
                {
                    slot.Itemstack!.Attributes.SetInt("materialId", be.BlockIds[matNum]);
                    slot.MarkDirty();
                }

                return;
            }

            slot.Itemstack!.Attributes.SetInt("toolMode", toolMode);
        }
        #nullable disable

    }
}
