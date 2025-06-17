using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class CollectibleBehaviorSqueezable : CollectibleBehavior
    {
        public float SqueezeTime { get; set; }
        public float SqueezedLitres { get; set; }
        public string AnimationCode { get; set; } = "squeezehoneycomb";
        public JsonItemStack[]? ReturnStacks { get; set; }
        public AssetLocation? SqueezingSound { get; set; }
        protected AssetLocation? liquidItemCode { get; set; }
        public Item? SqueezedLiquid { get; set; }

        public virtual bool CanSqueezeInto(IWorldAccessor world, Block block, BlockSelection? blockSel)
        {
            var pos = blockSel?.Position;

            if (block is BlockLiquidContainerTopOpened blcto)
            {
                return pos == null || !blcto.IsFull(pos);
            }

            if (pos != null)
            {
                if (block is BlockBarrel barrel && world.BlockAccessor.GetBlockEntity(pos) is BlockEntityBarrel beb)
                {
                    return !beb.Sealed && !barrel.IsFull(pos);
                }
                else if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityGroundStorage beg)
                {
                    ItemSlot squeezeIntoSlot = beg.GetSlotAt(blockSel);
                    if (squeezeIntoSlot?.Itemstack?.Block is BlockLiquidContainerTopOpened bowl)
                    {
                        return !bowl.IsFull(squeezeIntoSlot.Itemstack);
                    }
                }
            }

            return false;
        }

        public CollectibleBehaviorSqueezable(CollectibleObject collObj) : base(collObj)
        {
            this.collObj = collObj;
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            SqueezeTime = properties["squeezeTime"].AsFloat(0);
            SqueezedLitres = properties["squeezedLitres"].AsFloat(0);
            AnimationCode = properties["AnimationCode"].AsString("squeezehoneycomb");
            ReturnStacks = properties["returnStacks"].AsObject<JsonItemStack[]?>(null);

            string code = properties["squeezingSound"].AsString("game:sounds/player/squeezehoneycomb");
            if (code != null) {
                SqueezingSound = AssetLocation.Create(code, collObj.Code.Domain);
            }

            code = properties["liquidItemCode"].AsString();
            if (code != null)
            {
                liquidItemCode = AssetLocation.Create(code, collObj.Code.Domain);
            }
        }

        WorldInteraction[]? interactions;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            ReturnStacks?.Foreach(returnStack => returnStack?.Resolve(api.World, "returnStack for squeezing item ", collObj.Code));

            SqueezedLiquid = api.World.GetItem(liquidItemCode);
            if (SqueezedLiquid == null)
            {
                api.World.Logger.Warning("Unable to resolve liquid item code '{0}' for item {1}. Will ignore.", liquidItemCode, collObj.Code);
            }

            if (api is not ICoreClientAPI capi) return;

            interactions = ObjectCacheUtil.GetOrCreate(capi, "squeezableInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (Block block in capi.World.Blocks)
                {
                    if (block.Code == null) continue;

                    if (block is BlockBarrel)
                    {
                        stacks.Add(new ItemStack(block)); // Reliant on CanSqueezeInto allowing barrels. We check if barrel is sealed with world position
                    }


                    if (CanSqueezeInto(capi.World, block, null))
                    {
                        stacks.Add(new ItemStack(block));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-squeeze",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });

            AddSqueezableHandbookInfo(capi);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (blockSel?.Block != null && CanSqueezeInto(byEntity.World, blockSel.Block, blockSel) && byEntity.Controls.ShiftKey)
            {
                handling = EnumHandling.PreventDefault;
                handHandling = EnumHandHandling.PreventDefault;
                if (byEntity.World.Side == EnumAppSide.Client)
                {
                    byEntity.World.PlaySoundAt(SqueezingSound, byEntity, null, true, 16, 0.5f);
                }
            }
            else base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (blockSel?.Block != null && CanSqueezeInto(byEntity.World, blockSel.Block, blockSel))
            {
                handling = EnumHandling.PreventDefault;

                if (!byEntity.Controls.ShiftKey) return false;
                if (byEntity.World is IClientWorldAccessor) byEntity.StartAnimation(AnimationCode);

                return secondsUsed < SqueezeTime;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            byEntity.StopAnimation(AnimationCode);

            if (blockSel != null)
            {
                Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
                if (CanSqueezeInto(byEntity.World, block, blockSel))
                {
                    handling = EnumHandling.PreventDefault;

                    if (secondsUsed < SqueezeTime - 0.05f || SqueezedLiquid == null || byEntity.World.Side == EnumAppSide.Client) return;

                    IWorldAccessor world = byEntity.World;

                    if (!CanSqueezeInto(world, block, blockSel)) return;

                    ItemStack squeezedStack = new ItemStack(SqueezedLiquid, 99999);

                    if (block is BlockLiquidContainerTopOpened blockCnt)
                    {
                        if (blockCnt.TryPutLiquid(blockSel.Position, squeezedStack, SqueezedLitres) == 0) return;
                    }
                    else if (block is BlockBarrel blockBarrel && world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel beb)
                    {
                        if (beb.Sealed) return;
                        if (blockBarrel.TryPutLiquid(blockSel.Position, squeezedStack, SqueezedLitres) == 0) return;
                    }
                    else if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGroundStorage beg &&
                            beg.GetSlotAt(blockSel) is ItemSlot squeezeIntoSlot &&
                            squeezeIntoSlot.Itemstack?.Block is BlockLiquidContainerTopOpened begCnt &&
                            CanSqueezeInto(world, begCnt, null))
                    {
                        if (begCnt.TryPutLiquid(squeezeIntoSlot.Itemstack, squeezedStack, SqueezedLitres) == 0) return;
                        beg.MarkDirty(true);
                    }

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    IPlayer byPlayer = world.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);

                    ReturnStacks?.Foreach(returnStack =>
                    {
                        if (returnStack.ResolvedItemstack?.Clone() is not ItemStack stack) return;

                        if (byPlayer?.InventoryManager.TryGiveItemstack(stack) != true)
                        {
                            world.SpawnItemEntity(stack, blockSel.Position);
                        }
                    });

                    return;
                }
            }
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handling)
        {
            byEntity.StopAnimation(AnimationCode);
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot, ref handling));
        }

        protected virtual void AddSqueezableHandbookInfo(ICoreClientAPI capi)
        {
            JToken token;
            ExtraHandbookSection?[]? extraHandbookSections = collObj.Attributes?["handbook"]?["extraSections"]?.AsObject<ExtraHandbookSection[]>();

            if (extraHandbookSections?.FirstOrDefault(s => s?.Title == "handbook-squeezinghelp-title") != null) return;

            if (collObj.Attributes?["handbook"].Exists != true)
            {
                if (collObj.Attributes == null) collObj.Attributes = new JsonObject(JToken.Parse("{ handbook: {} }"));
                else
                {
                    token = collObj.Attributes.Token;
                    token["handbook"] = JToken.Parse("{ }");
                }
            }

            ExtraHandbookSection section = new ExtraHandbookSection() { Title = "handbook-squeezinghelp-title", Text = "handbook-squeezinghelp-text" };
            if (extraHandbookSections != null) extraHandbookSections.Append(section);
            else extraHandbookSections = [section];

            token = collObj.Attributes["handbook"].Token;
            token["extraSections"] = JToken.FromObject(extraHandbookSections);
        }
    }
}