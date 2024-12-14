using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorCreatureContainer : BlockBehavior
    {
        public double CreatureSurvivalDays = 1;
        ICoreAPI api;

        public BlockBehaviorCreatureContainer(Block block) : base(block)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            this.api = api;
        }

        public bool HasAnimal(ItemStack itemStack)
        {
            return itemStack.Attributes?.HasAttribute("animalSerialized") == true;
        }

        public static double GetStillAliveDays(IWorldAccessor world, ItemStack itemStack)
        {
            double creatureSurvivalDays = itemStack.Block.GetBehavior<BlockBehaviorCreatureContainer>().CreatureSurvivalDays;

            return creatureSurvivalDays - (world.Calendar.TotalDays - itemStack.Attributes.GetDouble("totalDaysCaught"));
        }

        static Dictionary<string, MultiTextureMeshRef> containedMeshrefs = new Dictionary<string, MultiTextureMeshRef>();
        
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (HasAnimal(itemstack))
            {
                string shapepath = itemstack.Collectible.Attributes["creatureContainedShape"][itemstack.Attributes.GetString("type")].AsString();

                if (GetStillAliveDays(capi.World, itemstack) > 0)
                {
                    float escapeSec = itemstack.TempAttributes.GetFloat("triesToEscape") - renderinfo.dt;

                    if (api.World.Rand.NextDouble() < 0.001)
                    {
                        escapeSec = 1 + (float)api.World.Rand.NextDouble() * 2;
                    }

                    itemstack.TempAttributes.SetFloat("triesToEscape", escapeSec);

                    if (escapeSec > 0)
                    {
                        if (api.World.Rand.NextDouble() < 0.05)
                        {
                            itemstack.TempAttributes.SetFloat("wiggle", 0.05f + (float)api.World.Rand.NextDouble() / 10);
                        }

                        float wiggle = itemstack.TempAttributes.GetFloat("wiggle") - renderinfo.dt;
                        itemstack.TempAttributes.SetFloat("wiggle", wiggle);

                        if (wiggle > 0)
                        {
                            shapepath += "-wiggle";
                            renderinfo.Transform = renderinfo.Transform.Clone();
                            var wiggleX = (float)api.World.Rand.NextDouble() * 4 - 2;
                            var wiggleZ = (float)api.World.Rand.NextDouble() * 4 - 2;
                            if (target != EnumItemRenderTarget.Gui) { wiggleX /= 25; wiggleZ /= 25; }
                            if (target == EnumItemRenderTarget.Ground) { wiggleX /= 4; wiggleZ /= 4; }
                            renderinfo.Transform.Translation.X += wiggleX;
                            renderinfo.Transform.Translation.Z += wiggleZ;
                        }
                    }
                }


                MultiTextureMeshRef meshref;

                if (!containedMeshrefs.TryGetValue(shapepath, out meshref))
                {
                    var shape = capi.Assets.TryGet(new AssetLocation(shapepath).WithPathPrefix("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
                    capi.Tesselator.TesselateShape(block, shape, out var meshdata, new Vec3f(0, 270, 0));
                    containedMeshrefs[shapepath] = meshref = capi.Render.UploadMultiTextureMesh(meshdata);
                }

                renderinfo.ModelRef = meshref;
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            foreach (var val in containedMeshrefs)
            {
                val.Value.Dispose();
            }
            containedMeshrefs.Clear();
        }

        public override EnumItemStorageFlags GetStorageFlags(ItemStack itemstack, ref EnumHandling handling)
        {
            if (HasAnimal(itemstack))
            {
                handling = EnumHandling.PreventDefault;
                return EnumItemStorageFlags.Backpack;
            }
            return base.GetStorageFlags(itemstack, ref handling);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            var slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (HasAnimal(slot.Itemstack))
            {
                handling = EnumHandling.PreventSubsequent;
                if (world.Side == EnumAppSide.Client)
                {
                    handling = EnumHandling.PreventSubsequent;
                    return false;
                }

                if (!ReleaseCreature(slot, blockSel, byPlayer.Entity))
                {
                    failureCode = "creaturenotplaceablehere";
                }

                return false;
            }

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref handling, ref failureCode);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            IServerPlayer plr = (byEntity as EntityPlayer).Player as IServerPlayer;
            ICoreServerAPI sapi = api as ICoreServerAPI;
            
            if (HasAnimal(slot.Itemstack))
            {
                if (blockSel == null)
                {
                    base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
                    return;
                }

                if (!ReleaseCreature(slot, blockSel, byEntity))
                {
                    sapi?.SendIngameError(plr, "nospace", Lang.Get("Not enough space to release animal here"));
                }

                handHandling = EnumHandHandling.PreventDefault;
                handling = EnumHandling.PreventDefault;
                slot.MarkDirty();
                return;
            }

            if (entitySel != null)
            {
                if (!(slot is ItemSlotBackpack))
                {
                    sapi?.SendIngameError(plr, "canthold", Lang.Get("Must have the container in backpack slot to catch an animal"));
                    return;
                }

                if (api.Side == EnumAppSide.Server && !IsCatchable(entitySel.Entity))
                {
                    sapi?.SendIngameError(plr, "notcatchable", Lang.Get("This animal is too large, or too wild to catch with a basket"));
                    return;
                }
                
                CatchCreature(slot, entitySel.Entity);
                handHandling = EnumHandHandling.PreventDefault;
                handling = EnumHandling.PreventDefault;
                slot.MarkDirty();
                return;
            }
        }

        private bool IsCatchable(Entity entity)
        {       
            return entity.Properties.Attributes?.IsTrue("basketCatchable") == true && entity.Properties.Attributes["trapChance"].AsFloat() > 0 && entity.WatchedAttributes.GetAsInt("generation") > 4 && entity.Alive;
        }

        public static void CatchCreature(ItemSlot slot, Entity entity)
        {
            if (entity.World.Side == EnumAppSide.Client) return;

            var stack = slot.Itemstack;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(ms);
                entity.ToBytes(writer, false);

                stack.Attributes.SetString("classname", entity.Api.ClassRegistry.GetEntityClassName(entity.GetType()));
                stack.Attributes.SetString("creaturecode", entity.Code.ToShortString());
                stack.Attributes.SetBytes("animalSerialized", ms.ToArray());

                double totalDaysReleased = entity.Attributes.GetDouble("totalDaysReleased");
                double catchedDays = totalDaysReleased - entity.Attributes.GetDouble("totalDaysCaught");
                double releasedDays = entity.World.Calendar.TotalDays - totalDaysReleased;

                // A released creature should recover from being in a basket, at twice the speed that it can survive in a basket
                // If it can survive 1 day in a basket
                // It should fully recover after 0.5 days
                double unrecoveredDays = Math.Max(0, catchedDays - releasedDays * 2);

                stack.Attributes.SetDouble("totalDaysCaught", entity.World.Calendar.TotalDays - unrecoveredDays);
            }

            entity.Die(EnumDespawnReason.PickedUp);
        }

        public static bool ReleaseCreature(ItemSlot slot, BlockSelection blockSel, Entity byEntity) 
        {
            IWorldAccessor world = byEntity.World;

            if (world.Side == EnumAppSide.Client) return true;

            string classname = slot.Itemstack.Attributes.GetString("classname");
            string creaturecode = slot.Itemstack.Attributes.GetString("creaturecode");
            Entity entity = world.Api.ClassRegistry.CreateEntity(classname);
            var type = world.EntityTypes.FirstOrDefault(type => type.Code.ToShortString() == creaturecode);
            if (type == null) return false;
            var stack = slot.Itemstack;

            using (MemoryStream ms = new MemoryStream(slot.Itemstack.Attributes.GetBytes("animalSerialized")))
            {
                BinaryReader reader = new BinaryReader(ms);
                entity.FromBytes(reader, false, ((IServerWorldAccessor)world).RemappedEntities);

                Vec3d spawnPos = blockSel.FullPosition;

                Cuboidf collisionBox = type.SpawnCollisionBox.OmniNotDownGrowBy(0.1f);
                if (world.CollisionTester.IsColliding(world.BlockAccessor, collisionBox, spawnPos, false)) return false;

                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = (float)world.Rand.NextDouble() * 2 * GameMath.PI;

                entity.Pos.SetFrom(entity.ServerPos);
                entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
                entity.Attributes.SetString("origin", "playerplaced");
                entity.Attributes.SetDouble("totalDaysCaught", stack.Attributes.GetDouble("totalDaysCaught"));
                entity.Attributes.SetDouble("totalDaysReleased", world.Calendar.TotalDays);
                /*if (entity.Attributes?.IsTrue("setGuardedEntityAttribute") == true)
                {
                    entity.WatchedAttributes.SetLong("guardedEntityId", byEntity.EntityId);
                    if (byEntity is EntityPlayer eplr)
                    {
                        entity.WatchedAttributes.SetString("guardedPlayerUid", eplr.PlayerUID);
                    }
                }*/

                world.SpawnEntity(entity);
                if (GetStillAliveDays(world, slot.Itemstack) < 0)
                {
                    (world.Api as ICoreServerAPI).Event.EnqueueMainThreadTask(() =>
                    {
                        entity.Properties.ResolvedSounds = null;
                        entity.Die(EnumDespawnReason.Death, new DamageSource() { CauseEntity = byEntity, Type = EnumDamageType.Hunger });
                    }, "die");
                }

                stack.Attributes.RemoveAttribute("classname");
                stack.Attributes.RemoveAttribute("creaturecode");
                stack.Attributes.RemoveAttribute("animalSerialized");
                stack.Attributes.RemoveAttribute("totalDaysCaught");
            }

            return true;
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            AddCreatureInfo(inSlot.Itemstack, dsc, world);
        }

        public void AddCreatureInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world)
        {
            if (HasAnimal(stack))
            {
                var alivedays = GetStillAliveDays(world, stack);
                if (alivedays > 0)
                {
                    dsc.AppendLine(Lang.Get("Contains a frightened {0}", Lang.Get("item-creature-" + stack.Attributes.GetString("creaturecode"))));
                    dsc.AppendLine(Lang.Get("It remains alive for {0:0.##} more hours", GetStillAliveDays(world, stack) * world.Calendar.HoursPerDay));
                } else
                {
                    dsc.AppendLine(Lang.Get("Contains a dead {0}", Lang.Get("item-creature-" + stack.Attributes.GetString("creaturecode"))));
                }
            }
        }
    }
}