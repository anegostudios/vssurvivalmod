using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    /// <summary>
    /// Allows a creature to be contained inside of this block, as well as catching and releasing the entity.
    /// Note that this behavior is built around use with the reed chest, and may have unexpected results with other blocks.
    /// This behavior uses the code "CreatureContainer", and has no properties.
    /// </summary>
    /// <example><code lang="json">
    ///"behaviors": [
	///	{ "name": "CreatureContainer" }
	///]
    /// </code></example>
    [DocumentAsJson]
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
                string type = itemstack.Attributes.GetString("type", "");
                string shapepath = itemstack.Collectible.Attributes?["creatureContainedShape"][type].AsString();

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
                            if (shapepath != null) shapepath += "-wiggle";
                            renderinfo.Transform = renderinfo.Transform.Clone();
                            var wiggleX = (float)api.World.Rand.NextDouble() * 4 - 2;
                            var wiggleZ = (float)api.World.Rand.NextDouble() * 4 - 2;
                            if (target != EnumItemRenderTarget.Gui) { wiggleX /= 25; wiggleZ /= 25; }
                            if (target == EnumItemRenderTarget.Ground) { wiggleX /= 4; wiggleZ /= 4; }
                            renderinfo.Transform.EnsureDefaultValues();
                            renderinfo.Transform.Translation.X += wiggleX;
                            renderinfo.Transform.Translation.Z += wiggleZ;
                        }
                    }
                }


                if (shapepath != null)
                {
                    if (!containedMeshrefs.TryGetValue(shapepath + type, out MultiTextureMeshRef meshref))
                    {
                        var shape = capi.Assets.TryGet(new AssetLocation(shapepath).WithPathPrefix("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
                        ITexPositionSource texSource = capi.Tesselator.GetTextureSource(block);
                        if (block is BlockGenericTypedContainer)
                        {
                            texSource = new GenericContainerTextureSource()
                            {
                                blockTextureSource = texSource,
                                curType = type
                            };
                        }
                        capi.Tesselator.TesselateShape("creature container shape", shape, out var meshdata, texSource, new Vec3f(0, 270, 0));
                        containedMeshrefs[shapepath + type] = meshref = capi.Render.UploadMultiTextureMesh(meshdata);
                    }

                    renderinfo.ModelRef = meshref;
                }
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
            var selPos = blockSel?.Position ?? entitySel?.Position?.AsBlockPos;

            if (selPos == null || !api.World.Claims.TryAccess((byEntity as EntityPlayer).Player, selPos , EnumBlockAccessFlags.Use))
            {
                return;
            }

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

            if (entitySel != null && entitySel.Entity.Alive && entitySel.Entity is not EntityBoat)
            {
                if (!IsCatchableAtThisGeneration(entitySel.Entity))
                {
                    (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, "toowildtocatch", Lang.Get("animaltrap-toowildtocatch-error"));

                    return;
                }

                if (!IsCatchableInThisTrap(entitySel.Entity))
                {
                    (byEntity.Api as ICoreClientAPI)?.TriggerIngameError(this, "notcatchable", Lang.Get("animaltrap-notcatchable-error"));

                    return;
                }

                handHandling = EnumHandHandling.PreventDefault;
                handling = EnumHandling.PreventDefault;

                ItemSlot emptyBackpackSlot = null;

                if (slot is ItemSlotBackpack)
                {
                    emptyBackpackSlot = slot;
                }
                else
                {
                    IInventory backpackInventory = (byEntity as EntityPlayer)?.Player?.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
                    if (backpackInventory != null)
                    {
                        emptyBackpackSlot = backpackInventory.Where(slot => slot is ItemSlotBackpack).FirstOrDefault(slot => slot.Empty);
                    }
                }

                if (emptyBackpackSlot == null)
                {
                    sapi?.SendIngameError(plr, "canthold", Lang.Get("Must have empty backpack slot to catch an animal"));
                    return;
                }

                ItemStack leftOverBaskets = null;
                if (slot.StackSize > 1)
                {
                    leftOverBaskets = slot.TakeOut(slot.StackSize - 1);
                }

                CatchCreature(slot, entitySel.Entity);
                slot.TryFlipWith(emptyBackpackSlot);

                if (slot.Empty) slot.Itemstack = leftOverBaskets;
                else if (!byEntity.TryGiveItemStack(leftOverBaskets))
                {
                    byEntity.World.SpawnItemEntity(leftOverBaskets, byEntity.ServerPos.XYZ);
                }

                slot.MarkDirty();
                emptyBackpackSlot.MarkDirty();



                return;
            }
        }

        private bool IsCatchableInThisTrap(Entity entity)
        {
            return TrapChances.FromEntityAttr(entity) is Dictionary<string, TrapChances> trapChancesByTrapType &&
                   trapChancesByTrapType.TryGetValue(block.Attributes["traptype"].AsString("small"), out var trapMeta) &&
                   trapMeta.TrapChance > 0;
        }

        private bool IsCatchableAtThisGeneration(Entity entity)
        {
            return entity.WatchedAttributes.GetAsInt("generation") >= (entity.Properties.Attributes?["trapPickupGeneration"].AsInt(5) ?? 5);
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
