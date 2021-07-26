using System;
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

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // Disabled outside of creative mode because its too broken
            if ((byEntity as EntityPlayer)?.Player?.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                return;
            }

            // sneak = attach
            // non-sneak = detach

            int clothId = slot.Itemstack.Attributes.GetInt("clothId");
            handling = EnumHandHandling.PreventDefault;
            ClothSystem sys = null;
            

            if (clothId != 0)
            {
                sys = cm.GetClothSystem(clothId);
                if (sys == null) clothId = 0;
            }


            // Detach
            if (!byEntity.Controls.Sneak)
            {
                if (clothId != 0)
                {
                    //Console.WriteLine(api.World.Side + ", clothid {0}, detach", clothId);

                    detach(sys, slot, byEntity, entitySel?.Entity, blockSel?.Position);
                }

                return;
            }


            // Attach
            if (clothId == 0)
            {
                float xsize = 2;

                sys = ClothSystem.CreateRope(api, cm, byEntity.Pos.AsBlockPos.Add(0, 1, 0), xsize, null);
                
                Vec3d lpos = new Vec3d(0, byEntity.LocalEyePos.Y - 0.3f, 0);
                Vec3d aheadPos = lpos.AheadCopy(0.1f, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw).AheadCopy(0.4f, byEntity.SidedPos.Pitch, byEntity.SidedPos.Yaw - GameMath.PIHALF);
                EntityPos pos = byEntity.SidedPos;

                //Console.WriteLine(api.World.Side + ", clothid {0}, new cloth. attach to self", sys.ClothId);

                sys.FirstPoint.PinTo(byEntity, aheadPos.ToVec3f());

                cm.RegisterCloth(sys);

                slot.Itemstack.Attributes.SetLong("ropeHeldByEntityId", byEntity.EntityId);
                slot.Itemstack.Attributes.SetInt("clothId", sys.ClothId);
                slot.MarkDirty();
            }


            ClothPoint[] pEnds = sys.Ends;

            if (blockSel != null)
            {
                Block block = api.World.BlockAccessor.GetBlock(blockSel.Position);

                if (blockSel.Position.Equals(pEnds[0].PinnedToBlockPos) || blockSel.Position.Equals(pEnds[1].PinnedToBlockPos))
                {
                    //Console.WriteLine(api.World.Side + ", clothid {0}, detach from block", sys.ClothId);

                    detach(sys, slot, byEntity, null, blockSel.Position);
                    return;
                }


                if (block.HasBehavior<BlockBehaviorRopeTieable>())
                {
                    //Console.WriteLine(api.World.Side + ", clothid {0}, attach to block", sys.ClothId);

                    attachToBlock(byEntity, blockSel.Position, sys, slot);
                }

            }

            if (entitySel != null)
            {
                if (entitySel.Entity.EntityId == pEnds[0].PinnedToEntity?.EntityId || entitySel.Entity.EntityId == pEnds[1].PinnedToEntity?.EntityId)
                {
                    //Console.WriteLine(api.World.Side + ", clothid {0}, detach from entity", sys.ClothId);
                    detach(sys, slot, byEntity, entitySel.Entity, null);
                    return;
                }

                //Console.WriteLine(api.World.Side + ", clothid {0}, attach to entity", sys.ClothId);
                attachToEntity(byEntity, entitySel.Entity, sys, slot);
                
            }


            if (clothId == 0)
            {
                sys.WalkPoints(p => p.update(0));

                Vec3d startPos = sys.FirstPoint.Pos;
                Vec3d endPos = sys.LastPoint.Pos;
                
                double dx = endPos.X - startPos.X;
                double dy = endPos.Y - startPos.Y;
                double dz = endPos.Z - startPos.Z;

                sys.WalkPoints(p => {
                    float f = p.PointIndex / (float)sys.Length;

                    if (!p.Pinned)
                    {
                        p.Pos.Set(startPos.X + dx * f, startPos.Y + dy * f, startPos.Z + dz * f);
                    }
                });

                sys.setRenderCenterPos();
            }


            // No longer pinned to ourselves
            if (pEnds[0].PinnedToEntity?.EntityId != byEntity.EntityId && pEnds[1].PinnedToEntity?.EntityId != byEntity.EntityId)
            {
                //Console.WriteLine(api.World.Side + ", clothid {0}, rope assigned. removed one from inv.", sys.ClothId);

                slot.Itemstack.Attributes.RemoveAttribute("clothId");
                slot.TakeOut(1);
                slot.MarkDirty();
            }


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
                cm.UnregisterCloth(sys.ClothId);
            }
        }



        private void attachToEntity(EntityAgent byEntity, Entity toEntity, ClothSystem sys, ItemSlot slot)
        {
            if (!toEntity.HasBehavior<EntityBehaviorRopeTieable>())
            {
                if (api.World.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "notattachable", Lang.Get("This creature is not tieable"));
                }
                return;
            }
            
            var pEnds = sys.Ends;
            ClothPoint cpoint = pEnds[0].PinnedToEntity?.EntityId == byEntity.EntityId && pEnds[1].Pinned ? pEnds[0] : pEnds[1];

            toEntity.GetBehavior<EntityBehaviorRopeTieable>().Attach(sys, cpoint);
        }


        private void attachToBlock(EntityAgent byEntity, BlockPos toPosition, ClothSystem sys, ItemSlot slot)
        {
            var pEnds = sys.Ends;

            // 2 possible cases
            // - We just created a new rope: use pEnds[1] because its unattached
            // - We already have both ends attached and want to re-attach the player held end

            ClothPoint cpoint = pEnds[0].PinnedToEntity?.EntityId == byEntity.EntityId && pEnds[1].Pinned ? pEnds[0] : pEnds[1];

            cpoint.PinTo(toPosition, new Vec3f(0.5f, 0.5f, 0.5f));
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
                        //sys.Points[0][0].PinTo(byEntity, aheadPos.ToVec3f());
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

                            p.PinTo(entityItem, new Vec3f(entityItem.CollisionBox.X2 / 2, entityItem.CollisionBox.Y2 / 2, entityItem.CollisionBox.Z2 / 2));
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
