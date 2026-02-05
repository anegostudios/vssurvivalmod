using Cairo;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using static OpenTK.Graphics.OpenGL.GL;

#nullable disable

namespace Vintagestory.GameContent
{
    public class ModSystemUnbreakSupportBeam : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        ICoreClientAPI capi;
        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.OnUnBreakingBlock += Event_OnUnBreakingBlock;
        }

        private void Event_OnUnBreakingBlock(BlockDamage bd)
        {
            var ba = capi.World.BlockAccessor;

            var block = ba.GetBlock(bd.Position);
            var be = ba.GetBlockEntity(bd.Position)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be?.Beams == null) return;

            foreach (var beam in be.Beams)
            {
                beam.RemainingResistance = GameMath.Min(beam.RemainingResistance + 0.1f * block.Resistance, block.Resistance);
            }
        }
    }

    public class BlockSupportBeam : Block, IMultiStageDecals
    {
        ModSystemSupportBeamPlacer bp;
        public bool PartialEnds;
        ICoreClientAPI capi;
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            bp = api.ModLoader.GetModSystem<ModSystemSupportBeamPlacer>();
            PartialSelection = true;
            capi = api as ICoreClientAPI;

            PartialEnds = Attributes?["partialEnds"].AsBool(false) ?? false;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null) return be.GetSelectionBoxes();

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null) return be.GetCollisionBoxes();
            
            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null) return;
            handling = EnumHandHandling.PreventDefault;
            bp.OnInteract(this, slot, byEntity, blockSel, PartialEnds);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (bp.CancelPlace(this, byEntity))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }


        public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();

            int stages = 10;

            if (be != null)
            {
                blockModelData = blockModelData.EmptyClone();
                decalModelData = decalModelData.EmptyClone();
                
                List<int> textureIds = new List<int>();

                for (int i = 0; i < be.Beams.Length; i++)
                {
                    var beam = be.Beams[i];

                    blockModelData.AddMeshData(be.genMesh(i, null, null));
                    var mesh = be.genMesh(i, decalTexSource, "decal");

                    float resi = beam.Block.GetResistance(world.BlockAccessor, pos);
                    int stage = Math.Min((int)(stages * (resi - beam.RemainingResistance) / resi), stages-1);

                    for (int j = 0; j < mesh.VerticesCount / mesh.VerticesPerFace; j++) textureIds.Add(stage);
                    decalModelData.AddMeshData(mesh);
                }

                // Lets abuse this field to remember the breaking stage per beam
                decalModelData.TextureIds = textureIds.ToArray(); 

                return;
            }

            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
        }


        public int StageForVertex(MeshData decalMesh, int vertexIndex)
        {
            return decalMesh.TextureIds[vertexIndex / 4];
        }

        public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position)?.GetBehavior<BEBehaviorSupportBeam>();
            if (be != null)
            {
                int beamIndex = blockSel?.SelectionBoxIndex ?? 0;
                if (beamIndex >= be.Beams.Length)
                {
                    return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
                }

                foreach (var beam in be.Beams)
                {
                    if (api.World.ElapsedMilliseconds - beam.LastModifiedMilliseconds > beam.Block.Resistance * 1000)
                    {
                        beam.RemainingResistance = beam.Block.Resistance;
                    }
                }

                var abeam = be.Beams[beamIndex];
                abeam.LastModifiedMilliseconds = capi.World.ElapsedMilliseconds;
                abeam.RemainingResistance = Math.Max(0, abeam.RemainingResistance - dt);
            }
                
            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            int? beamIndex = byPlayer?.CurrentBlockSelection?.SelectionBoxIndex;
            var be = api.World.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorSupportBeam>();

            if (beamIndex != null && be != null && be.Beams.Length > 1)
            {
                be.BreakBeam((int)beamIndex, byPlayer?.WorldData.CurrentGameMode != EnumGameMode.Creative);
                return;
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }



        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            return false;
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "Set Beam Start/End Point (Snap to 4x4 grid)",
                    MouseButton = EnumMouseButton.Right
                },
                new WorldInteraction()
                {
                    ActionLangCode = "Set Beam Start/End Point (Snap to 16x16 grid)",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "ctrl"
                },
                new WorldInteraction()
                {
                    ActionLangCode = "Cancel placement",
                    MouseButton = EnumMouseButton.Left
                },
            };
        }

        public override bool DisplacesLiquids(IBlockAccessor blockAccess, BlockPos pos)
        {
            return false;
        }

    }
}
