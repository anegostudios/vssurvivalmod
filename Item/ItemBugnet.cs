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
            api.Network.GetChannel("catchcreature").SetMessageHandler<CatchCreaturePacket>(onCatchCreature);
        }

        private void onCatchCreature(IServerPlayer fromPlayer, CatchCreaturePacket packet)
        {
            var entity = sapi.World.GetEntityById(packet.entityId);

            if (entity == null || entity.Pos.DistanceTo(fromPlayer.Entity.Pos.XYZ.Add(fromPlayer.Entity.LocalEyePos)) > fromPlayer.WorldData.PickingRange)
            {
                return;
            }

            if (entity.Properties.Attributes?["netCaughtItemCode"].Exists == true)
            {
                entity.Die(EnumDespawnReason.Death, new DamageSource() { Source = EnumDamageSource.Entity, SourceEntity = fromPlayer.Entity, Type = EnumDamageType.BluntAttack });

                var stack = new ItemStack(sapi.World.GetItem(new AssetLocation(entity.Properties.Attributes["netCaughtItemCode"].AsString())));

                if (!fromPlayer.Entity.TryGiveItemStack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, fromPlayer.Entity.Pos.XYZ);
                }
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

                BlockSelection bsel = new BlockSelection();
                EntitySelection esel = new EntitySelection();
                BlockFilter bfilter = (pos, block) => (block == null || block.RenderPass != EnumChunkRenderPass.Meta/* || ClientSettings.RenderMetaBlocks*/);
                EntityFilter efilter = (e) => e.Alive && (e.IsInteractable || e.Properties.Attributes?["netCaughtItemCode"].Exists == true) && e.EntityId != byEntity.EntityId;

                api.World.RayTraceForSelection(srcpos, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw, range, ref bsel, ref esel, bfilter, efilter);

                if (esel?.Entity?.Properties.Attributes?["netCaughtItemCode"].Exists == true)
                {
                    (api as ICoreClientAPI).Network.GetChannel("catchcreature").SendPacket(new CatchCreaturePacket() { entityId = esel.Entity.EntityId });
                }
            }

            handling = EnumHandHandling.PreventDefaultAction;
        }
    }


}
