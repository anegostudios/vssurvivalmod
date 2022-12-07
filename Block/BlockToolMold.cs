using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockToolMold : Block
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            string createdByText = "metalmolding";
            if (Attributes?["createdByText"].Exists == true)
            {
                createdByText = Attributes["createdByText"].AsString();
            }

            if (Attributes?["drop"].Exists == true)
            {
                JsonItemStack ojstack = Attributes["drop"].AsObject<JsonItemStack>();
                if (ojstack != null)
                {
                    MetalProperty metals = api.Assets.TryGet("worldproperties/block/metal.json").ToObject<MetalProperty>();
                    for (int i = 0; i < metals.Variants.Length; i++)
                    {
                        string metaltype = metals.Variants[i].Code.Path;
                        string tooltype = LastCodePart();
                        JsonItemStack jstack = ojstack.Clone();
                        jstack.Code.Path = jstack.Code.Path.Replace("{tooltype}", tooltype).Replace("{metal}", metaltype);

                        CollectibleObject collObj;

                        if (jstack.Type == EnumItemClass.Block)
                        {
                            collObj = api.World.GetBlock(jstack.Code);
                        }
                        else
                        {
                            collObj = api.World.GetItem(jstack.Code);
                        }

                        if (collObj == null) continue;

                        JToken token;

                        if (collObj.Attributes?["handbook"].Exists != true)
                        {
                            if (collObj.Attributes == null) collObj.Attributes = new JsonObject(JToken.Parse("{ handbook: {} }"));
                            else
                            {
                                token = collObj.Attributes.Token;
                                token["handbook"] = JToken.Parse("{ }");
                            }
                        }

                        token = collObj.Attributes["handbook"].Token;
                        token["createdBy"] = JToken.FromObject(createdByText);
                    }
                }
            }


            interactions = ObjectCacheUtil.GetOrCreate(api, "toolmoldBlockInteractions", () =>
            {
                List<ItemStack> smeltedContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is BlockSmeltedContainer)
                    {
                        smeltedContainerStacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolmold-pour",
                        HotKeyCode = "shift",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = smeltedContainerStacks.ToArray(),
                        GetMatchingStacks = (wi, bs, es) =>
                        {
                            BlockEntityToolMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityToolMold;
                            return (betm != null && !betm.IsFull) ? wi.Itemstacks : null;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolmold-takeworkitem",
                        HotKeyCode = null,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityToolMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityToolMold;
                            return betm != null && betm.IsFull && betm.IsHardened;
                        }
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-toolmold-pickup",
                        HotKeyCode = null,
                        RequireFreeHand = true,
                        MouseButton = EnumMouseButton.Right,
                        ShouldApply = (wi, bs, es) =>
                        {
                            BlockEntityToolMold betm = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityToolMold;
                            return betm != null && betm.metalContent == null;
                        }
                    }
                };
            });
        }



        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null)
            {
               base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
               return;
            }

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face.Opposite));

            IPlayer byPlayer = null;
            if (byEntity is EntityPlayer) byPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);

            if (byPlayer != null && be is BlockEntityToolMold)
            {
                BlockEntityToolMold beim = (BlockEntityToolMold)be;
                if (beim.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition))
                {
                    handHandling = EnumHandHandling.PreventDefault;
                }
            }

            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            BlockEntityToolMold be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityToolMold;
            bool handled = false;

            if (be != null)
            {
                handled = be.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition);
            }
            

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                failureCode = "onlywhensneaking";
                return false;
            }

            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            Block belowBlock = world.BlockAccessor.GetBlock(blockSel.Position.DownCopy());

            if (belowBlock.CanAttachBlockAt(world.BlockAccessor, this, blockSel.Position.DownCopy(), BlockFacing.UP))
            {
                DoPlaceBlock(world, byPlayer, blockSel, itemstack);
                return true;
            }

            failureCode = "requiresolidground";

            return false;
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return GetHandbookDropsFromBreakDrops(handbookStack, forPlayer);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            stacks.Add(new ItemStack(this));

            BlockEntityToolMold bet = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityToolMold;
           
            if (bet != null)
            {
                ItemStack[] outstack = bet.GetReadyMoldedStacks();
                if (outstack != null) {
                    stacks.AddRange(outstack);
                }
            }


            return stacks.ToArray();
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

    }
}
