using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class CatchCreaturePacket
    {
        [ProtoMember(1)]
        public long entityId;
    }

    public class ModSystemCatchCreature : ModSystem
    {
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("catchcreature").RegisterMessageType<CatchCreaturePacket>();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Network.GetChannel("catchcreature").SetMessageHandler<CatchCreaturePacket>(onClientPacket);
        }

        private void onClientPacket(IServerPlayer fromPlayer, CatchCreaturePacket packet)
        {
            var heldItem = fromPlayer.Entity.ActiveHandItemSlot?.Itemstack?.Collectible;
            if (heldItem is not ItemBugnet)
            {
                return;
            }

            var entity = sapi.World.GetEntityById(packet.entityId);
            if (entity == null || !fromPlayer.IsInInteractionRangeOf(entity))
            {
                return;
            }

            var caughtItemCode = entity.Properties.Attributes?["netCaughtItemCode"];
            if (caughtItemCode?.Exists != true)
            {
                return;
            }

            entity.Die(EnumDespawnReason.Death, new DamageSource() { Source = EnumDamageSource.Entity, SourceEntity = fromPlayer.Entity, Type = EnumDamageType.BluntAttack });

            var stack = new ItemStack(sapi.World.GetItem(new AssetLocation(caughtItemCode.AsString())));

            if (!fromPlayer.Entity.TryGiveItemStack(stack))
            {
                sapi.World.SpawnItemEntity(stack, fromPlayer.Entity.Pos.XYZ);
            }

            if (fromPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                heldItem.DamageItem(sapi.World, fromPlayer.Entity, fromPlayer.Entity.ActiveHandItemSlot, 1);
            }
        }
    }

    public class ItemBugnet : Item
    {
        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (api.Side == EnumAppSide.Client)
            {
                Vec3d srcpos = byEntity.Pos.XYZ.Add(byEntity.LocalEyePos);
                var range = 2.5f;

                BlockSelection? bsel = new BlockSelection();
                EntitySelection? esel = new EntitySelection();
                BlockFilter bfilter = (pos, block) => (block == null || block.RenderPass != EnumChunkRenderPass.Meta/* || ClientSettings.RenderMetaBlocks*/);
                EntityFilter efilter = (e) => e.Alive && (e.IsInteractable || e.Properties.Attributes?["netCaughtItemCode"].Exists == true) && e.EntityId != byEntity.EntityId;

                api.World.RayTraceForSelection(srcpos, byEntity.Pos.Pitch, byEntity.Pos.Yaw, range, ref bsel, ref esel, bfilter, efilter);

                if (esel?.Entity?.Properties.Attributes?["netCaughtItemCode"].Exists == true)
                {
                    ((ICoreClientAPI)api).Network.GetChannel("catchcreature").SendPacket(new CatchCreaturePacket() { entityId = esel.Entity.EntityId });
                }
            }

            handling = EnumHandHandling.PreventDefaultAction;
        }
    }


}
