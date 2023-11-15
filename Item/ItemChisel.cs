using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using VSSurvivalMod.Systems.ChiselModes;

namespace Vintagestory.GameContent
{
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

        public static bool carvingTime = DateTime.Now.Month == 10 || DateTime.Now.Month == 11;
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

            if (byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Tool != EnumTool.Hammer && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
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


            if (blockSel == null)
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

            if (byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Tool != EnumTool.Hammer && byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative)
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

            Block chiseledblock = byEntity.World.GetBlock(new AssetLocation("chiseledblock"));

            byEntity.World.BlockAccessor.SetBlock(chiseledblock.BlockId, blockSel.Position);

            BlockEntityChisel be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
            if (be == null) return;

            be.WasPlaced(block, null);

            if (carvingTime && block.Code.Path == "pumpkin-fruit-4")
            {
                be.AddMaterial(api.World.GetBlock(new AssetLocation("creativeglow-35")));
            }
            
            handling = EnumHandHandling.PreventDefaultAction;
        }

        public static bool IsChiselingAllowedFor(ICoreAPI api, BlockPos pos, Block block, IPlayer player)
        {
            if (block is BlockChisel) return true;

            return IsValidChiselingMaterial(api, pos, block, player);
        }

        public static bool IsValidChiselingMaterial(ICoreAPI api, BlockPos pos, Block block, IPlayer player)
        {
            // Can't use a chiseled block as a material in a chiseled block
            if (block is BlockChisel) return false;

            // 1. priority: microblockChiseling disabled
            ITreeAttribute worldConfig = api.World.Config;
            string mode = worldConfig.GetString("microblockChiseling");
            if (mode == "off") return false;

            // 1.5 priority: Disabled by code
            if (block is IConditionalChiselable icc || (icc = block.BlockBehaviors.FirstOrDefault(bh => bh is IConditionalChiselable) as IConditionalChiselable) != null)
            {
                string errorCode;
                if (icc?.CanChisel(api.World, pos, player, out errorCode) == false || icc?.CanChisel(api.World, pos, player, out errorCode) == false)
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
                    ItemSlot dummySlot = new DummySlot();
                    dummySlot.Itemstack = new ItemStack(block);
                    mats[i] = new SkillItem()
                    {
                        Code = block.Code,
                        Data = be.BlockIds[i],
                        Linebreak = i % 7 == 0,
                        Name = block.GetHeldItemName(dummySlot.Itemstack),
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

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel, int toolMode)
        {
            if (blockSel == null) return;
            var pos = blockSel.Position;
            var mouseslot = byPlayer.InventoryManager.MouseItemSlot;
            if (!mouseslot.Empty && mouseslot.Itemstack.Block != null && !(mouseslot.Itemstack.Block is BlockChisel))
            {
                BlockEntityChisel be = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityChisel;
                if (IsValidChiselingMaterial(api, pos, mouseslot.Itemstack.Block, byPlayer))
                {
                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        bool isFull;
                        be.AddMaterial(mouseslot.Itemstack.Block, out isFull);
                        if (!isFull)
                        {
                            mouseslot.TakeOut(1);
                            mouseslot.MarkDirty();
                        }
                    }
                    else
                    {
                        be.AddMaterial(mouseslot.Itemstack.Block);
                    }

                    be.MarkDirty();
                    api.Event.PushEvent("keepopentoolmodedlg");
                }

                return;
            }

            if (toolMode > ToolModes.Length - 1)
            {
                int matNum = toolMode - ToolModes.Length;
                BlockEntityChisel be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityChisel;
                if (be != null && be.BlockIds.Length > matNum)
                {
                    slot.Itemstack.Attributes.SetInt("materialId", be.BlockIds[matNum]);
                    slot.MarkDirty();
                }

                return;
            }

            slot.Itemstack.Attributes.SetInt("toolMode", toolMode);
        }
    }
}
