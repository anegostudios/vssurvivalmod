using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemPlumbAndSquare : Item
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "plumbAndSquareInteractions", () =>
            {
                List<ItemStack> stacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj.Attributes?["reinforcementStrength"].AsInt(0) > 0)
                    {
                        stacks.Add(new ItemStack(obj));
                    }
                }

                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-reinforceblock",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = "heldhelp-removereinforcement",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });
        }


        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            if (blockSel == null)
            {
                return;
            }

            ModSystemBlockReinforcement bre = byEntity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();

            IPlayer player = (byEntity as EntityPlayer).Player;
            if (player == null) return;

            ItemSlot resSlot = bre.FindResourceForReinforcing(player);
            if (resSlot == null) return;

            int strength = resSlot.Itemstack.ItemAttributes["reinforcementStrength"].AsInt(0);
            
            if (!bre.StrengthenBlock(blockSel.Position, player, strength))
            {
                (player as IServerPlayer).SendIngameError("alreadyreinforced", "Cannot reinforce block, it's already reinforced!");
                return;
            }

            resSlot.TakeOut(1);
            resSlot.MarkDirty();

            BlockPos pos = blockSel.Position;
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/reinforce"), pos.X, pos.Y, pos.Z, null);

            handling = EnumHandHandling.PreventDefaultAction;
            if (byEntity.World.Side == EnumAppSide.Client) ((byEntity as EntityPlayer)?.Player as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        }



        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            if (blockSel == null)
            {
                return;
            }

            ModSystemBlockReinforcement modBre = byEntity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            IServerPlayer player = (byEntity as EntityPlayer).Player as IServerPlayer;
            if (player == null) return;

            BlockReinforcement bre = modBre.GetReinforcment(blockSel.Position);

            string errorCode = "";
            if (!modBre.TryRemoveReinforcement(blockSel.Position, player, ref errorCode))
            {
                if (errorCode == "notownblock")
                {
                    (player as IServerPlayer).SendIngameError("cantremove", "Cannot remove reinforcement. This block does not belong to you");
                } else
                {
                    (player as IServerPlayer).SendIngameError("cantremove", "Cannot remove reinforcement. It's not reinforced");
                }
                
                return;
            } else
            {
                if (bre.Locked)
                {
                    ItemStack stack = new ItemStack(byEntity.World.GetItem(new AssetLocation(bre.LockedByItemCode)));
                    if (!player.InventoryManager.TryGiveItemstack(stack, true))
                    {
                        byEntity.World.SpawnItemEntity(stack, byEntity.ServerPos.XYZ);
                    }
                }
            }

            BlockPos pos = blockSel.Position;
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/reinforce"), pos.X, pos.Y, pos.Z, null);

            handling = EnumHandHandling.PreventDefaultAction;
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
