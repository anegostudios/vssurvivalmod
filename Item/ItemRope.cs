using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    // Rope concept
    // 1st milestone goal:
    // - Able to push/pull tamed animals
    // - Rope in hands. Right click on animal creates an in-world rope
    // - Rope in hands. Right click on fence creates an in-world rope
    // - Connect fences and animals with one another with rope
    // - Save game persistent + multiplayer
    // - Non derpy physics

    // Convention:
    // Right click + Sneak = Attach
    // Right click = Detach
    public class ItemRope : Item
    {
        ClothManager cm;
        SkillItem[] toolModes;

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            var sinkId = op.SinkSlot.Itemstack.Attributes.GetInt("clothId");
            var srcId = op.SourceSlot.Itemstack.Attributes.GetInt("clothId");

            if (sinkId != 0 || srcId != 0)
            {
                op.MovableQuantity = 0;
                return;
            }

            base.TryMergeStacks(op);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            cm = api.ModLoader.GetModSystem<ClothManager>();

            toolModes = new SkillItem[]
            {
                new SkillItem()
                {
                    Code = new AssetLocation("shorten"),
                    Name = Lang.Get("Shorten by 1m")
                },
                new SkillItem()
                {
                    Code = new AssetLocation("length"),
                    Name = Lang.Get("Lengthen by 1m")
                }
            };

            var capi = api as ICoreClientAPI;
            if (capi != null)
            {
                toolModes[0].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/shorten.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                toolModes[0].TexturePremultipliedAlpha = false;
                toolModes[1].WithIcon(capi, capi.Gui.LoadSvgWithPadding(new AssetLocation("textures/icons/lengthen.svg"), 48, 48, 5, ColorUtil.WhiteArgb));
                toolModes[1].TexturePremultipliedAlpha = false;
            }
        }


        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return toolModes;
        }
        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return -1;
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            int clothId = slot.Itemstack.Attributes.GetInt("clothId");
            ClothSystem sys = null;
            if (clothId != 0)
            {
                sys = cm.GetClothSystem(clothId);
            }
            if (sys == null) return;


            if (toolMode == 0)
            {
                if (!sys.ChangeRopeLength(-0.5))
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "tooshort", Lang.Get("Already at minimum length!"));
                }
                else
                {
                    if (api is ICoreServerAPI sapi)
                        sapi.Network.GetChannel("clothphysics")
                            .BroadcastPacket(new ClothLengthPacket() { ClothId = sys.ClothId, LengthChange = -0.5 }, byPlayer as IServerPlayer);
                }
            }
            if (toolMode == 1)
            {
                if (!sys.ChangeRopeLength(0.5))
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "tooshort", Lang.Get("Already at maximum length!"));
                }
                else
                {
                    if (api is ICoreServerAPI sapi)
                        sapi.Network.GetChannel("clothphysics")
                            .BroadcastPacket(new ClothLengthPacket() { ClothId = sys.ClothId, LengthChange = 0.5 }, byPlayer as IServerPlayer);
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (byEntity.Controls.ShiftKey && byEntity.Controls.CtrlKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            int clothId = slot.Itemstack.Attributes.GetInt("clothId");
            ClothSystem sys = null;
            if (clothId != 0)
            {
                sys = cm.GetClothSystem(clothId);
                if (sys == null) clothId = 0;
            }

            ClothPoint[] pEnds = sys?.Ends;

            // Place new rope
            if (sys == null)
            {
                if (blockSel != null && api.World.BlockAccessor.GetBlock(blockSel.Position).HasBehavior<BlockBehaviorRopeTieable>())
                {
                    sys = attachToBlock(byEntity, blockSel.Position, slot, null);
                } else
                if (entitySel != null)
                {
                    sys = attachToEntity(byEntity, entitySel, slot, null, out bool relayRopeInteractions);
                    if (relayRopeInteractions) {
                        handling = EnumHandHandling.NotHandled;
                        if (sys != null) splitStack(slot, byEntity);
                        return;
                    }

                }

                if (sys != null) splitStack(slot, byEntity);


            // Modify existing rope
            } else
            {
                // Remove rope from world (looking at block it is currently attached to)
                if (blockSel != null && (blockSel.Position.Equals(pEnds[0].PinnedToBlockPos) || blockSel.Position.Equals(pEnds[1].PinnedToBlockPos)))
                {
                    detach(sys, slot, byEntity, null, blockSel.Position);
                    return;
                }

                // Remove rope from world (looking at entity it is currenty attached to)
                if (entitySel != null && (entitySel.Entity.EntityId == pEnds[0].PinnedToEntity?.EntityId || entitySel.Entity.EntityId == pEnds[1].PinnedToEntity?.EntityId))
                {
                    detach(sys, slot, byEntity, entitySel.Entity, null);
                    return;
                }

                // Connect rope to something else
                if (blockSel != null && api.World.BlockAccessor.GetBlock(blockSel.Position).HasBehavior<BlockBehaviorRopeTieable>())
                {
                    sys = attachToBlock(byEntity, blockSel.Position, slot, sys);
                    pEnds = sys?.Ends;
                }
                else if (entitySel != null)
                {
                    attachToEntity(byEntity, entitySel, slot, sys, out bool relayRopeInteractions);

                    if (relayRopeInteractions)
                    {
                        handling = EnumHandHandling.NotHandled;
                        return;
                    }
                }
            }


            if (clothId == 0 && sys != null)
            {
                sys.WalkPoints(p => p.update(0, api.World));
                sys.setRenderCenterPos();
            }


            // No longer pinned to ourselves
            if (pEnds != null && pEnds[0].PinnedToEntity?.EntityId != byEntity.EntityId && pEnds[1].PinnedToEntity?.EntityId != byEntity.EntityId)
            {
                slot.Itemstack.Attributes.RemoveAttribute("clothId");
                slot.TakeOut(1);
                slot.MarkDirty();
            }
        }

        private void splitStack(ItemSlot slot, EntityAgent byEntity)
        {
            if (slot.StackSize > 1)
            {
                var split = slot.TakeOut(slot.StackSize - 1);
                split.Attributes.RemoveAttribute("clothId");
                split.Attributes.RemoveAttribute("ropeHeldByEntityId");

                if (!byEntity.TryGiveItemStack(split))
                {
                    api.World.SpawnItemEntity(split, byEntity.Pos.XYZ);
                }
            }
        }

        private ClothSystem createRope(ItemSlot slot, EntityAgent byEntity, Vec3d targetPos)
        {
            ClothSystem sys = ClothSystem.CreateRope(api, cm, byEntity.Pos.XYZ, targetPos, null);

            Vec3d lpos = new Vec3d(0, byEntity.LocalEyePos.Y - 0.3f, 0);
            Vec3d aheadPos = lpos
                .AheadCopy(0.1f, byEntity.Pos.Pitch, byEntity.Pos.Yaw)
                .AheadCopy(0.4f, byEntity.Pos.Pitch, byEntity.Pos.Yaw - GameMath.PIHALF)
            ;

            sys.FirstPoint.PinTo(byEntity, aheadPos.ToVec3f());
            cm.RegisterCloth(sys);

            slot.Itemstack.Attributes.SetLong("ropeHeldByEntityId", byEntity.EntityId);
            slot.Itemstack.Attributes.SetInt("clothId", sys.ClothId);
            slot.MarkDirty();
            return sys;
        }

        private void detach(ClothSystem sys, ItemSlot slot, EntityAgent byEntity, Entity toEntity, BlockPos pos)
        {
            toEntity?.GetBehavior<EntityBehaviorRopeTieable>()?.Detach(sys);

            sys.WalkPoints(point => {
                if (point.PinnedToBlockPos != null && point.PinnedToBlockPos.Equals(pos))
                {
                    point.UnPin();
                }

                if (point.PinnedToEntity?.EntityId == byEntity.EntityId)
                {
                    point.UnPin();
                }
            });

            if (!sys.PinnedAnywhere)
            {
                slot.Itemstack.Attributes.RemoveAttribute("clothId");
                slot.Itemstack.Attributes.RemoveAttribute("ropeHeldByEntityId");
                cm.UnregisterCloth(sys.ClothId);
            }
        }



        private ClothSystem attachToEntity(EntityAgent byEntity, EntitySelection toEntitySel, ItemSlot slot, ClothSystem sys, out bool relayRopeInteractions)
        {
            relayRopeInteractions = false;
            Entity toEntity = toEntitySel.Entity;

            var ebho = toEntity.GetBehavior<EntityBehaviorOwnable>();
            if (ebho != null && !ebho.IsOwner(byEntity))
            {
                (toEntity.World.Api as ICoreClientAPI)?.TriggerIngameError(this, "requiersownership", Lang.Get("mount-interact-requiresownership"));
                return null;
            }

            var icc = toEntity.GetInterface<IRopeTiedCreatureCarrier>();
            if (sys != null && icc != null)
            {
                var pEnds = sys.Ends;
                ClothPoint elkPoint = pEnds[0].PinnedToEntity?.EntityId == byEntity.EntityId && pEnds[1].Pinned ? pEnds[1] : pEnds[0];

                if (icc.TryMount(elkPoint.PinnedToEntity as EntityAgent))
                {
                    cm.UnregisterCloth(sys.ClothId);
                    return null;
                }

            }

            if (!toEntity.HasBehavior<EntityBehaviorRopeTieable>())
            {
                relayRopeInteractions = toEntity?.Properties.Attributes?["relayRopeInteractions"].AsBool(true) ?? false;
                if (!relayRopeInteractions)
                {
                    if (api.World.Side == EnumAppSide.Client)
                    {
                        (api as ICoreClientAPI).TriggerIngameError(this, "notattachable", Lang.Get("This creature is not tieable"));
                    }
                }
                return null;
            }

            var bh = toEntity.GetBehavior<EntityBehaviorRopeTieable>();
            if (!bh.CanAttach()) return null;


            if (sys == null)
            {
                sys = createRope(slot, byEntity, toEntity.Pos.XYZ);
                bh.Attach(sys, sys.LastPoint);
            }
            else
            {
                var pEnds = sys.Ends;
                ClothPoint cpoint = pEnds[0].PinnedToEntity?.EntityId == byEntity.EntityId && pEnds[1].Pinned ? pEnds[0] : pEnds[1];
                bh.Attach(sys, cpoint);
            }
            return sys;
        }


        private ClothSystem attachToBlock(EntityAgent byEntity, BlockPos toPosition, ItemSlot slot, ClothSystem sys)
        {
            if (sys == null)
            {
                sys = createRope(slot, byEntity, toPosition.ToVec3d().Add(0.5, 0.5, 0.5));
                sys.LastPoint.PinTo(toPosition, new Vec3f(0.5f, 0.5f, 0.5f));
            }
            else
            {
                var pEnds = sys.Ends;

                // 2 use cases:
                // Rope is attached to another block and player wants to connect it with a second block
                // Rope is attached to a creature and player wants to tie creature to this block

                ClothPoint cpoint = pEnds[0];

                var startEntity = pEnds[0].PinnedToEntity;
                var endEntity = pEnds[1].PinnedToEntity;
                var fromEntity = startEntity ?? endEntity;

                if (startEntity?.EntityId != byEntity.EntityId)
                    cpoint = pEnds[1];

                if (fromEntity == byEntity)
                    fromEntity = endEntity ?? startEntity;

                if (fromEntity is EntityAgent agent && (
                        (startEntity != null && startEntity != byEntity) ||
                        (endEntity != null && endEntity != byEntity))
                    )
                {
                    // Lengthen rope to accomodate
                    cm.UnregisterCloth(sys.ClothId);
                    sys = createRope(slot, agent, toPosition.ToVec3d().Add(0.5, 0.5, 0.5));

                    sys.LastPoint.PinTo(toPosition, new Vec3f(0.5f, 0.5f, 0.5f));
                }
                else
                {
                    cpoint.PinTo(toPosition, new Vec3f(0.5f, 0.5f, 0.5f));
                }
            }

            return sys;
        }


        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack extractedStack = null)
        {
            if (!(slot.Inventory is InventoryBasePlayer))
            {
                long ropeHeldByEntityId = slot.Itemstack.Attributes.GetLong("ropeHeldByEntityId");
                if (ropeHeldByEntityId != 0)
                {
                    slot.Itemstack.Attributes.RemoveAttribute("ropeHeldByEntityId");
                }

                int clothId = slot.Itemstack.Attributes.GetInt("clothId");
                if (clothId != 0)
                {
                    ClothSystem sys = cm.GetClothSystem(clothId);
                    if (sys != null)
                    {
                        //if (sys.FirstPoint.PinnedToEntity.EntityId == ropeHeldByEntityId) sys.FirstPoint.PinTo(byEntity, aheadPos.ToVec3f());
                    }
                }
            }
        }


        public override void OnGroundIdle(EntityItem entityItem)
        {
            long ropeHeldByEntityId = entityItem.Itemstack.Attributes.GetLong("ropeHeldByEntityId");
            if (ropeHeldByEntityId != 0)
            {
                entityItem.Itemstack.Attributes.RemoveAttribute("ropeHeldByEntityId");

                int clothId = entityItem.Itemstack.Attributes.GetInt("clothId");
                if (clothId != 0)
                {
                   // Console.WriteLine(api.World.Side + ", clothid {0}, dropped on ground.", clothId);

                    ClothSystem sys = cm.GetClothSystem(clothId);
                    if (sys != null)
                    {
                        ClothPoint p = null;
                        if (sys.FirstPoint.PinnedToEntity?.EntityId == ropeHeldByEntityId) p = sys.FirstPoint;
                        if (sys.LastPoint.PinnedToEntity?.EntityId == ropeHeldByEntityId) p = sys.LastPoint;

                        if (p != null)
                        {
                          //  Console.WriteLine(api.World.Side + ", clothid {0}, dropped on ground, now pinned to dropped item.", clothId);

                            p.PinTo(entityItem, new Vec3f(entityItem.SelectionBox.X2 / 2, entityItem.SelectionBox.Y2 / 2, entityItem.SelectionBox.Z2 / 2));
                        }
                    }
                }
            }
        }


        public override void OnCollected(ItemStack stack, Entity entity)
        {
            int clothId = stack.Attributes.GetInt("clothId");
            if (clothId != 0)
            {
                ClothSystem sys = cm.GetClothSystem(clothId);
                if (sys != null)
                {
                    ClothPoint p = null;
                    if (sys.FirstPoint.PinnedToEntity is EntityItem itemFirst && !itemFirst.Alive) p = sys.FirstPoint;
                    if (sys.LastPoint.PinnedToEntity is EntityItem itemLast && !itemLast.Alive) p = sys.LastPoint;

                    if (p != null)
                    {
                        Vec3d lpos = new Vec3d(0, entity.LocalEyePos.Y - 0.3f, 0);
                        Vec3d aheadPos = lpos.AheadCopy(0.1f, entity.Pos.Pitch, entity.Pos.Yaw).AheadCopy(0.4f, entity.Pos.Pitch, entity.Pos.Yaw - GameMath.PIHALF);

                        p.PinTo(entity, aheadPos.ToVec3f());

                        ItemSlot collectedSlot = null;
                        (entity as EntityPlayer).WalkInventory((slot) => {
                            if (!slot.Empty && slot.Itemstack.Attributes.GetInt("clothId") == clothId)
                            {
                                collectedSlot = slot;
                                return false;
                            }
                            return true;
                        });

                        if (sys.FirstPoint.PinnedToEntity == entity && sys.LastPoint.PinnedToEntity == entity)
                        {
                            sys.FirstPoint.UnPin();
                            sys.LastPoint.UnPin();
                            if(collectedSlot != null)
                            {
                                collectedSlot.Itemstack = null;
                                collectedSlot.MarkDirty();
                            }
                            cm.UnregisterCloth(sys.ClothId);
                            return;
                        }

                        collectedSlot?.Itemstack?.Attributes.SetLong("ropeHeldByEntityId", entity.EntityId);
                        collectedSlot?.MarkDirty();
                    }
                }
            }
        }


        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }
    }
}
