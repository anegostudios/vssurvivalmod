using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public enum EnumPinPart
    {
        Start,
        End
    }

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

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            cm = api.ModLoader.GetModSystem<ClothManager>();
        }


        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            return new SkillItem[]
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
        }
        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return 0;
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
                if (!sys.ChangeRopeLength(-1))
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "tooshort", Lang.Get("Already at minimum length!"));
                }
            }
            if (toolMode == 1)
            {
                if (!sys.ChangeRopeLength(1))
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "tooshort", Lang.Get("Already at maximum length!"));
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
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
                        return;
                    }
                }


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

        private ClothSystem createRope(ItemSlot slot, EntityAgent byEntity, Vec3d targetPos)
        {
            ClothSystem sys;

            sys = ClothSystem.CreateRope(api, cm, byEntity.Pos.XYZ, targetPos, null);

            Vec3d lpos = new Vec3d(0, byEntity.LocalEyePos.Y - 0.3f, 0);
            Vec3d aheadPos = lpos.AheadCopy(0.1f, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw).AheadCopy(0.4f, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw - GameMath.PIHALF);
            EntityPos pos = byEntity.SidedPos;

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

            if (sys == null)
            {
                sys = createRope(slot, byEntity, toEntity.SidedPos.XYZ);
                toEntity.GetBehavior<EntityBehaviorRopeTieable>().Attach(sys, sys.LastPoint);
            }
            else
            {
                var pEnds = sys.Ends;
                ClothPoint cpoint = pEnds[0].PinnedToEntity?.EntityId == byEntity.EntityId && pEnds[1].Pinned ? pEnds[0] : pEnds[1];
                toEntity.GetBehavior<EntityBehaviorRopeTieable>().Attach(sys, cpoint);
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

                ClothPoint cpoint = pEnds[0];
                if (pEnds[0].PinnedToEntity?.EntityId != byEntity.EntityId) cpoint = pEnds[1];

                if (pEnds[0].PinnedToEntity != null || pEnds[1].PinnedToEntity != null)
                {
                    var fromEntity = pEnds[0].PinnedToEntity ?? pEnds[1].PinnedToEntity;
                    if (fromEntity == byEntity) fromEntity = pEnds[1].PinnedToEntity ?? pEnds[0].PinnedToEntity;

                    // Lengthen rope to accomodate
                    cm.UnregisterCloth(sys.ClothId);

                    sys = createRope(slot, fromEntity as EntityAgent, toPosition.ToVec3d().Add(0.5, 0.5, 0.5));
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
                    if (sys.FirstPoint.PinnedToEntity is EntityItem) p = sys.FirstPoint;
                    if (sys.LastPoint.PinnedToEntity is EntityItem) p = sys.LastPoint;

                    if (p != null)
                    {
                        Vec3d lpos = new Vec3d(0, entity.LocalEyePos.Y - 0.3f, 0);
                        Vec3d aheadPos = lpos.AheadCopy(0.1f, entity.SidedPos.Pitch, entity.SidedPos.Yaw).AheadCopy(0.4f, entity.SidedPos.Pitch, entity.SidedPos.Yaw - GameMath.PIHALF);
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
