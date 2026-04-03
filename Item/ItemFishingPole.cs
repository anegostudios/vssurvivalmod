using System;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class FishingSupportModSystem : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;
        ICoreClientAPI capi = null!;
        ICoreServerAPI sapi = null!;

        public NormalizedSimplexNoise NoiseGen = null!;

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Event.BeforeActiveSlotChanged += Event_BeforeActiveSlotChanged;
            capi = api;
        }

        private EnumHandling Event_BeforeActiveSlotChanged(ActiveSlotChangeEventArgs args)
        {
            return beforeActiveSlotChanged(capi.World.Player, args);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.BeforeActiveSlotChanged += beforeActiveSlotChanged;
            api.Event.PlayerDisconnect += Event_PlayerDisconnect;
            api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, onShutdown);

            NoiseGen = NormalizedSimplexNoise.FromDefaultOctaves(2, 1 / 10f, 0.9, sapi.World.Seed);
        }

        private void onShutdown()
        {
            foreach (var plr in sapi.World.AllOnlinePlayers)
            {
                var slot = plr.InventoryManager.ActiveHotbarSlot;
                (slot?.Itemstack?.Collectible as ItemFishingPole)?.StopFishing(slot!.Itemstack, plr.Entity);
            }
        }

        private void Event_PlayerDisconnect(IServerPlayer plr)
        {
            var slot = plr.InventoryManager.ActiveHotbarSlot;
            (slot?.Itemstack?.Collectible as ItemFishingPole)?.StopFishing(slot!.Itemstack, plr.Entity);
        }

        private EnumHandling beforeActiveSlotChanged(IPlayer plr, ActiveSlotChangeEventArgs args)
        {
            var slot = plr.InventoryManager.ActiveHotbarSlot;

            if (slot?.Itemstack?.Collectible is ItemFishingPole)
            {
                var beid = slot.Itemstack.Attributes.GetLong("fishingEntityId", 0);
                if (beid != 0)
                {
                    return EnumHandling.PreventSubsequent;
                }
            }

            return EnumHandling.PassThrough;
        }
    }

    public class ItemFishingPole : Item, IContainedInteractable, IHandBookPageCodeProvider
    {
        protected ClothManager cm = null!;
        protected string aimAnimation = null!;
        protected Vec3f offsetToPoleTipFp = null!;
        protected Vec3f offsetToPoleTipTp = null!;

        CompositeShape? ropelessShapeLoc;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            cm = api.ModLoader.GetModSystem<ClothManager>();

            aimAnimation = Attributes["aimAnimation"].AsString("bowaimlong");
            offsetToPoleTipFp = Attributes["offsetToPoleTipFp"].AsObject<Vec3f>(new Vec3f());

            offsetToPoleTipFp = new Vec3f(3.3f, -0.05f, 0.4f);
            offsetToPoleTipTp = new Vec3f(2.3f, 0f, 0f);

            ropelessShapeLoc = Attributes["ropelessShape"].AsObject<CompositeShape>();

            ObjectCacheUtil.GetOrCreate<ItemStack[]>(api, "fishingbaititems", () =>
            {
                return [..api.World.Collectibles.Where(obj => obj.Attributes?.IsTrue("isFishBait") == true)
                                                .SelectMany(obj => obj.GetHandBookStacks(api as ICoreClientAPI) ?? [])];
            });
        }

        public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
        {
            if (sinkStack.Attributes.GetBool("fishing")) return 0;

            if (priority == EnumMergePriority.DirectMerge)
            {
                if (sourceStack.ItemAttributes?.IsTrue("isFishBait") == true) return 1;
            }

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            if (op.SinkSlot.Itemstack?.Attributes.GetBool("fishing") == true) return;

            if (op.CurrentPriority == EnumMergePriority.DirectMerge)
            {
                if (op.SourceSlot.Itemstack?.ItemAttributes?.IsTrue("isFishBait") == true)
                {
                    var oldBaitStack = op.SinkSlot.Itemstack?.Attributes.GetItemstack("fishingBait");
                    op.SinkSlot.Itemstack?.Attributes.SetItemstack("fishingBait", op.SourceSlot.TakeOut(1));
                    op.MovedQuantity = 1;

                    if (oldBaitStack != null)
                    {
                        if (op.SourceSlot.StackSize <= 1)
                        {
                            op.SourceSlot.Itemstack = oldBaitStack;
                            op.SourceSlot.MarkDirty();
                        } else
                        {
                            if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(oldBaitStack, false))
                            {
                                op.ActingPlayer.Entity.World.SpawnItemEntity(oldBaitStack, op.ActingPlayer.Entity.Pos.XYZ);
                            }
                        }
                    }
                    return;
                }
            }

            base.TryMergeStacks(op);
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target == EnumItemRenderTarget.HandTp && itemstack.Attributes.GetBool("fishing") && ropelessShapeLoc != null)
            {
                var meshdata = ObjectCacheUtil.GetOrCreate(capi, "fishingPoleRopelessShape" + Code, () =>
                {
                    var shape = capi.Assets.Get(ropelessShapeLoc.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
                    capi.Tesselator.TesselateShape(this, shape, out var meshdata);
                    return capi.Render.UploadMultiTextureMesh(meshdata);
                });

                renderinfo.ModelRef = meshdata;
            }

            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
        }

        public override void OnCollected(ItemStack stack, Entity entity)
        {
            if (entity is EntityAgent agent)
            {
                StopFishing(stack, agent);
            }
        }
        public override void OnHeldDropped(IWorldAccessor world, IPlayer byPlayer, ItemSlot slot, int quantity, ref EnumHandling handling)
        {
            StopFishing(slot.Itemstack, byPlayer.Entity);
        }

        EnumCameraMode? previousCm;
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            updatePinOffset(slot, byEntity);
        }

        private void updatePinOffset(ItemSlot slot, EntityAgent byEntity)
        {
            var capi = api as ICoreClientAPI;
            if (capi == null) return;


            var camMode = capi.World.Player.CameraMode;
            if (camMode != previousCm && previousCm == EnumCameraMode.FirstPerson && camMode != EnumCameraMode.FirstPerson)
            {
                var cs = getRope(slot);
                if (cs != null)
                {
                    cs.FirstPoint.pinnedToOffset = offsetToPoleTipTp.Clone();
                    cs.FirstPoint.NoAttachTransform = false;
                }
            }

            if (camMode == EnumCameraMode.FirstPerson)
            {
                var cs = getRope(slot);
                if (cs != null)
                {
                    Matrixf m = new Matrixf();
                    LoadHeldItemModelMatrix(m, byEntity, slot, capi);
                    var pos = m.TransformVector(new Vec4f(offsetToPoleTipFp.X, offsetToPoleTipFp.Y, offsetToPoleTipFp.Z, 1));

                    var rapi = capi.Render;
                    var pMatrixHandFov = (capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer)?.pMatrixHandFov;
                    var pMatrixNormalFov = (capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer)?.pMatrixNormalFov;

                    if (pMatrixHandFov != null)
                    {
                        Matrixf tmpMatrix = new Matrixf();
                        Vec4f screenSpaceCoordNormalFov = tmpMatrix.Set(pMatrixHandFov).Mul(rapi.CameraMatrixOriginf).TransformVector(pos);
                        Matrixf unprojectMatrix = new Matrixf(rapi.CameraMatrixOriginf).Invert().Mul(tmpMatrix.Set(pMatrixNormalFov).Invert());
                        pos = unprojectMatrix.TransformVector(screenSpaceCoordNormalFov);
                    }


                    cs.FirstPoint.pinnedToOffset = pos.XYZ;
                    cs.FirstPoint.NoAttachTransform = true;
                }
            }

            previousCm = camMode;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection? entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            if (StopFishing(slot.Itemstack, byEntity))
            {
                slot.MarkDirty();
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            var controls = byEntity.MountedOn?.Controls ?? byEntity.Controls;
            if (controls.CtrlKey
                && (entitySel?.SelectionBoxIndex ?? -1) >= 0
                && entitySel?.Entity?.GetBehavior<EntityBehaviorAttachable>() != null)
            {
                return;
            }

            // Not ideal to code the aiming controls this way. Needs an elegant solution - maybe an event bus?
            byEntity.AnimManager.StartAnimation(aimAnimation);

             slot.Itemstack?.Attributes.SetLong("fishingEntityId", byEntity.EntityId);
            byEntity.AnimManager.RegisterFrameCallback(new AnimFrameCallback()
            {
                Animation = aimAnimation,
                Frame = 40,
                Callback = () => throwBobber(slot, byEntity)
            });

            handling = EnumHandHandling.PreventDefault;
        }

        private void throwBobber(ItemSlot slot, EntityAgent byEntity)
        {
            byEntity.AnimManager.StopAnimation(aimAnimation);

            // prevent throwing the pole away or switching to a different slot
            var isFishing = byEntity.ActiveHandItemSlot.Itemstack?.Attributes.GetLong("fishingEntityId") != 0;
            if (slot.Empty || !isFishing) return;

            byEntity.AnimManager.StartAnimation("fishingpole-idle");

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                slot.Itemstack.Attributes.SetBool("fishing", true);
                slot.MarkDirty();
                return;
            }

            byEntity.World.PlaySoundAt(new AssetLocation("sounds/effect/poleswing"), byEntity, null, false, 8);

            var type = byEntity.World.GetEntityType(new AssetLocation("bobber-normal"));
            EntityBobber ebobber = (EntityBobber)byEntity.World.ClassRegistry.CreateEntity(type);
            var earrow = ebobber as IProjectile;
            earrow.FiredBy = byEntity;
            earrow.DropOnImpactChance = 0;

            Vec3d pos = byEntity.Pos.XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
            Vec3d aheadPos = pos.AheadCopy(1, byEntity.Pos.Pitch, byEntity.Pos.Yaw);
            Vec3d velocity = (aheadPos - pos) * byEntity.Stats.GetBlended("bowDrawingStrength") / 6;
            velocity.Y += 0.125;

            ebobber.Pos.SetPosWithDimension(byEntity.Pos.BehindCopy(0.21).XYZ.Add(0, byEntity.LocalEyePos.Y, 0));
            ebobber.Pos.Motion.Set(velocity);
            ebobber.World = byEntity.World;
            ebobber.AttachedToEntityId = byEntity.EntityId;
            ebobber.BaitStack = slot.Itemstack.Attributes.GetItemstack("fishingBait");
            ebobber.BaitStack?.ResolveBlockOrItem(byEntity.World);
            slot.Itemstack.Attributes.RemoveAttribute("fishingBait");

            earrow.PreInitialize();

            byEntity.World.SpawnPriorityEntity(ebobber);

            var cs = getRope(slot);
            if (cs != null) cm.UnregisterCloth(cs.ClothId);

            createRope(slot, byEntity, ebobber, ebobber.Pos.XYZ);

            slot.Itemstack.Attributes.SetLong("bobberEntityId", ebobber.EntityId);

            slot.Itemstack.Attributes.SetBool("fishing", true);
        }

        public bool StopFishing(ItemStack? itemstack, EntityAgent byEntity)
        {
            byEntity.AnimManager.StopAnimation("fishingpole-idle");
            if(itemstack == null) return false;

            itemstack.Attributes.SetBool("fishing", false);

            var beid = itemstack.Attributes.GetLong("bobberEntityId", 0);
            if (beid != 0)
            {
                cm.UnregisterCloth(itemstack.Attributes.GetInt("clothId", 0));
                if (api.Side == EnumAppSide.Server)
                {
                    var be = api.World.GetEntityById(beid) as EntityBobber;
                    be?.TryCatchFish(byEntity);
                    be?.Die();
                    if (be?.BaitStack != null)
                    {
                        itemstack.Attributes.SetItemstack("fishingBait", be.BaitStack);
                    }
                    else
                    {
                        itemstack.Attributes.RemoveAttribute("fishingBait");
                    }
                }

                itemstack.Attributes.RemoveAttribute("bobberEntityId");
                itemstack.Attributes.RemoveAttribute("clothId");
                itemstack.Attributes.RemoveAttribute("fishingEntityId");

                return true;
            }

            return false;
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputSlots, ItemSlot outputSlot, IRecipeBase byRecipe)
        {
            var baitStack = allInputSlots.FirstOrDefault(slot => slot.Itemstack?.Collectible.Attributes?.IsTrue("isFishBait") == true)?.Itemstack;
            if (baitStack != null)
            {
                outputSlot.Itemstack?.Attributes.SetItemstack("fishingBait", baitStack);
            }
        }

        public override bool MatchesForCrafting(ItemStack inputStack, IRecipeBase gridRecipe, IRecipeIngredient ingredient)
        {
            bool baitRecipe = gridRecipe.RecipeIngredients.Any(ingred => ingred?.ResolvedItemStack?.Collectible?.Attributes?.IsTrue("isFishBait") == true);
            if (baitRecipe && inputStack.Attributes.GetItemstack("fishingBait") != null) return false;

            return base.MatchesForCrafting(inputStack, gridRecipe, ingredient);
        }

        // We want to direct the player to the unbaited page because that's where the grid recipes show up
        public string HandbookPageCodeForStack(IWorldAccessor world, ItemStack stack)
        {
            var cleanStack = stack.Clone();
            cleanStack.Attributes.RemoveAttribute("fishingBait");
            return GuiHandbookItemStackPage.PageCodeForStack(cleanStack);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            var baitStack = inSlot.Itemstack?.Attributes.GetItemstack("fishingBait");
            baitStack?.ResolveBlockOrItem(world);
            if (baitStack != null)
            {
                dsc.AppendLine(Lang.Get("item-fishingpole-bait", baitStack.GetName()));
            }
            else if (inSlot.Itemstack.Attributes.GetBool("fishing"))
            {
                var ebobber = api.World.GetEntityById(inSlot.Itemstack.Attributes.GetLong("bobberEntityId")) as EntityBobber;
                if (ebobber?.BaitStack != null)
                {
                    dsc.AppendLine(Lang.Get("item-fishingpole-bait", ebobber.BaitStack.GetName()));
                }
            }
        }


        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            updatePinOffset(slot, byEntity);
            return true;
        }


        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {

        }

        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack? extractedStack = null)
        {
            var eid = slot.Itemstack?.Attributes.GetLong("fishingEntityId") ?? 0;
            var eagent = world.GetEntityById(eid) as EntityAgent;

            if (extractedStack != null && eagent != null && eagent.RightHandItemSlot.Empty)
            {
                StopFishing(extractedStack, eagent);
            }
        }

        private ClothSystem? getRope(ItemSlot slot)
        {
            int clothId = slot.Itemstack?.Attributes.GetInt("clothId", 0) ?? 0;
            return cm.GetClothSystem(clothId);
        }


        private ClothSystem createRope(ItemSlot slot, EntityAgent byEntity, EntityBobber ebobber, Vec3d targetPos)
        {
            ClothSystem sys = ClothSystem.CreateRope(api, cm, byEntity.Pos.XYZ, targetPos, null, 4);

            sys.ChangeRopeLength(0.5);
            sys.CanPull = false;
            sys.CanRip = false;
            sys.FirstPoint.PinTo(byEntity, offsetToPoleTipTp);
            sys.LastPoint.PinTo(ebobber, new Vec3f());
            sys.LastPoint.NoAttachTransform = true;
            sys.RopeRenderThickness = 0.35f;
            cm.RegisterCloth(sys);

            slot.Itemstack?.Attributes.SetLong("ropeHeldByEntityId", byEntity.EntityId);
            slot.Itemstack?.Attributes.SetInt("clothId", sys.ClothId);
            slot.MarkDirty();
            return sys;
        }



        public static bool LoadHeldItemModelMatrix(Matrixf mmat, EntityAgent entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
        {
            if (entity.Properties.Client.Renderer is not EntityShapeRenderer entityShapeRenderer) return false;

            var itemStack = itemSlot.Itemstack;
            if (itemStack == null) return false;

            AttachmentPointAndPose? attachmentPointAndPose = entity.AnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
            if (attachmentPointAndPose == null) return false;

            AttachmentPoint attachPoint = attachmentPointAndPose.AttachPoint;
            ItemRenderInfo itemStackRenderInfo = api.Render.GetItemStackRenderInfo(itemSlot, right ? EnumItemRenderTarget.HandTp : EnumItemRenderTarget.HandTpOff, 0f);
            if (itemStackRenderInfo?.Transform == null) return false;

            mmat.Set(entityShapeRenderer.ModelMat)
                .Mul(attachmentPointAndPose.AnimModelMatrix)
                .Translate(itemStackRenderInfo.Transform.Origin.X, itemStackRenderInfo.Transform.Origin.Y, itemStackRenderInfo.Transform.Origin.Z)
                .Scale(itemStackRenderInfo.Transform.ScaleXYZ.X, itemStackRenderInfo.Transform.ScaleXYZ.Y, itemStackRenderInfo.Transform.ScaleXYZ.Z)
                .Translate(attachPoint.PosX / 16.0 + itemStackRenderInfo.Transform.Translation.X, attachPoint.PosY / 16.0 + itemStackRenderInfo.Transform.Translation.Y, attachPoint.PosZ / 16.0 + itemStackRenderInfo.Transform.Translation.Z)
                .RotateX((float)(attachPoint.RotationX + itemStackRenderInfo.Transform.Rotation.X) * (MathF.PI / 180f))
                .RotateY((float)(attachPoint.RotationY + itemStackRenderInfo.Transform.Rotation.Y) * (MathF.PI / 180f))
                .RotateZ((float)(attachPoint.RotationZ + itemStackRenderInfo.Transform.Rotation.Z) * (MathF.PI / 180f))
                .Translate(0f - itemStackRenderInfo.Transform.Origin.X, 0f - itemStackRenderInfo.Transform.Origin.Y, 0f - itemStackRenderInfo.Transform.Origin.Z);

            return true;
        }

        public bool OnContainedInteractStart(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            var handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Itemstack?.Collectible.Attributes?["isFishBait"]?.AsBool() == true)
            {
                if (slot.Itemstack?.Attributes.GetItemstack("fishingBait") == null)
                {
                    slot.Itemstack?.Attributes.SetItemstack("fishingBait", handSlot.Itemstack);
                    handSlot.TakeOut(1);
                    handSlot.MarkDirty();
                }
                else (api as ICoreClientAPI)?.TriggerIngameError(this, "fishingpolealreadybaited", Lang.Get("ingameerror-fishingpole-alreadybaited"));

                return true;
            }

            return false;
        }

        public bool OnContainedInteractStep(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            return false;
        }

        public void OnContainedInteractStop(float secondsUsed, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {

        }


        public WorldInteraction[] GetContainedInteractionHelp(BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (slot.Itemstack?.Attributes.GetItemstack("fishingBait") != null) return [];

            return
            [
                new() {
                    ActionLangCode = "blockhelp-fishingpole-bait",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = ObjectCacheUtil.TryGet<ItemStack[]>(api, "fishingbaititems")
                }
            ];
        }
    }
}
