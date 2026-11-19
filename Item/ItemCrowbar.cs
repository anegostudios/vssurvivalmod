using ProtoBuf;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;


#nullable disable annotations

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public class BeamIndexPacket
    {
        [ProtoMember(1)]
        public int PlaceIndex;
        [ProtoMember(2)]
        public int HotbarSlotIndex;
    }

    public class ModSystemCrowbarUtil : ModSystem, IRenderer, ITexPositionSource
    {
        ICoreClientAPI capi;

        public double RenderOrder => 1;

        public int RenderRange => 1;

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;

        public TextureAtlasPosition this[string textureCode] => capi.BlockTextureAtlas["block/creative/col32"];

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void Start(ICoreAPI api)
        {
            api.Network.RegisterChannel("crowbarUtil").RegisterMessageType<BeamIndexPacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "beamHighlight");
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Network.GetChannel("crowbarUtil").SetMessageHandler<BeamIndexPacket>(onPacket);
        }

        private void onPacket(IServerPlayer fromPlayer, BeamIndexPacket packet)
        {
            var inv = fromPlayer.InventoryManager.GetHotbarInventory();
            if (packet.HotbarSlotIndex >= 0 && packet.HotbarSlotIndex < inv.Count)
            {
                var slot = inv[packet.HotbarSlotIndex];
                if (!slot.Empty && slot.Itemstack.Collectible is ItemCrowbar itemCrowbar)
                {
                    itemCrowbar.SetSelectedBeamIndex(slot, packet.PlaceIndex);
                }
            }
        }

        string renderKey = "";
        MultiTextureMeshRef beamMeshRef;
        Matrixf ModelMat = new Matrixf();

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!capi.World.Player.Entity.Controls.CtrlKey)
            {
                beamMeshRef?.Dispose();
                renderKey = null;
                return;
            }

            var heldSlot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            var itemCrowBar = heldSlot.Itemstack?.Item as ItemCrowbar;
            if (itemCrowBar == null)
            {
                beamMeshRef?.Dispose();
                renderKey = null;
                return;
            }

            var blocksel = capi.World.Player.CurrentBlockSelection;
            var blockPos = blocksel?.Position;
            if (blockPos == null)
            {
                beamMeshRef?.Dispose();
                renderKey = null;
                return;
            }

            var block = capi.World.BlockAccessor.GetBlock(blockPos);


            var beams = ItemCrowbar.getBeamBehavior(capi, blocksel)?.Beams;
            if (beams == null)
            {
                beamMeshRef?.Dispose();
                renderKey = null;
                return;
            }

            int beamIndex = itemCrowBar.GetSelectedBeamPlaceIndex(heldSlot);
            PlacedBeam beam = beams.FirstOrDefault(b => b.PlaceIndex == beamIndex);
            if (beam == null) return;

            string newRenderKey = blocksel.Position.ToString() + "-" + beamIndex;
            if (newRenderKey != renderKey)
            {
                renderKey = newRenderKey;

                var meshes = capi.ModLoader.GetModSystem<ModSystemSupportBeamPlacer>().getOrCreateBeamMeshes(beam.Block, (block as BlockSupportBeam)?.PartialEnds ?? false, this);

                var meshdata = ModSystemSupportBeamPlacer.generateMesh(
                    beam.Start, beam.End,
                    BlockFacing.ALLFACES[beam.FacingIndex], meshes, block.Attributes?["slumpPerMeter"].AsFloat(0) ?? 0,
                    1.1f
                );
                beamMeshRef?.Dispose();
                beamMeshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }

            var prevprog = capi.Render.CurrentActiveShader;
            prevprog?.Stop();
            var prog = capi.Render.PreparedStandardShader(blockPos.X, blockPos.InternalY, blockPos.Z);
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            capi.Render.GlToggleBlend(true);
            prog.Use();
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(blockPos.X - camPos.X, blockPos.InternalY - camPos.Y, blockPos.Z - camPos.Z)
                .Values
            ;

            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;
            prog.RgbaTint = new Vec4f(1, 1, 1, 0.5f);

            capi.Render.RenderMultiTextureMesh(beamMeshRef, "tex");

            prog.Stop();
            prevprog?.Use();
        }
    }

    public class ItemCrowbar : Item, IHeldItemOnMouseWheel
    {
        public void OnMouseWheel(EntityPlayer byPlayer, ItemSlot inSlot, MouseWheelEventArgs args)
        {
            if (args.DeltaYInt == 0 || api.Side == EnumAppSide.Server) return; // Instead we do our own custom packet

            var blockSele = byPlayer.BlockSelection;
            if (blockSele == null) return;

            var beh = getBeamBehavior(api, blockSele);
            if (beh?.Beams == null) return;

            if (!byPlayer.Controls.CtrlKey) return;

            args.SetHandled(true);

            int index = GetSelectedBeamPlaceIndex(inSlot);

            for (int i = 0; i < beh.Beams.Length; i++)
            {
                var beam = beh.Beams[i];
                if (beam.PlaceIndex == index)
                {
                    if (args.DeltaYInt > 0) index = beh.Beams[GameMath.Mod(i + 1, beh.Beams.Length)].PlaceIndex;
                    else index = beh.Beams[GameMath.Mod(i - 1, beh.Beams.Length)].PlaceIndex;
                    SetSelectedBeamIndex(inSlot, index);
                    return;
                }
            }

            SetSelectedBeamIndex(inSlot, beh.Beams[0].PlaceIndex);
        }

        public int GetSelectedBeamPlaceIndex(ItemSlot inSlot)
        {
            return inSlot.Itemstack.Attributes.GetInt("beamIndex", 0);
        }

        public void SetSelectedBeamIndex(ItemSlot inSlot, int index)
        {
            inSlot.Itemstack.Attributes.SetInt("beamIndex", index);

            if (api is ICoreClientAPI capi)
            {
                capi.Network.GetChannel("crowbarUtil").SendPacket(new BeamIndexPacket() { HotbarSlotIndex = capi.World.Player.InventoryManager.ActiveHotbarSlotNumber, PlaceIndex = index });
            }
        }

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            var blockSele = (forEntity as EntityPlayer).BlockSelection;
            if (blockSele == null)
            {
                return "interactstatic";
            }
            var beh = api.World.BlockAccessor.GetBlockEntity(blockSele.Position)?.GetBehavior<BEBehaviorSupportBeam>();
            if (beh?.Beams == null || beh.Beams.Length == 0) return "interactstatic";

            return base.GetHeldTpUseAnimation(activeHotbarSlot, forEntity);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            var byPlayer = ((EntityPlayer)byEntity).Player;
            if (!firstEvent && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative) return;

            if (blockSel != null && !byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            var beh = getBeamBehavior(api, blockSel);
            if (beh != null)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public static BEBehaviorSupportBeam getBeamBehavior(ICoreAPI api, BlockSelection blockSel)
        {
            if (blockSel == null) return null;
            var beh = api.World.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorSupportBeam>();
            if (beh?.Beams == null || beh.Beams.Length == 0) return null;

            return beh;
        }

        protected BlockSounds getSounds(BEBehaviorSupportBeam beh)
        {
            return beh.Beams[beh.Beams.Length - 1].Block.Sounds;
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            var beh = getBeamBehavior(api, blockSel);
            if (beh != null)
            {
                if ((byEntity as EntityPlayer).Player.WorldData.CurrentGameMode == EnumGameMode.Creative) return secondsUsed < 0.1;

                return secondsUsed < 1;
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            var byPlayer = ((EntityPlayer) byEntity).Player;
            bool creativeMode = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;

            if (blockSel != null && !byEntity.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            var beh = getBeamBehavior(api, blockSel);
            if (beh != null && (secondsUsed > 0.5f || creativeMode) && beh.Beams.Length > 0)
            {
                api.World.PlaySoundAt(getSounds(beh).Break, blockSel.Position, 0.5, (byEntity as EntityPlayer)?.Player);

                int index = GetSelectedBeamPlaceIndex(slot);
                beh.BreakBeamByPlaceIndex(index, !creativeMode);

                if (beh.Beams.Length > 0)
                {
                    SetSelectedBeamIndex(slot, GameMath.Mod(index - 1, beh.Beams.Length));
                }
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

    }
}
