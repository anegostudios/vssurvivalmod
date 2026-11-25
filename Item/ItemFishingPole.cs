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
    public sealed class FishingSupportModSystem : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        ICoreClientAPI? capi;
        ICoreServerAPI? sapi;

        public NormalizedSimplexNoise? FishFrequencyMapNoise { get => NoiseGen; set => NoiseGen = value; }
        public NormalizedSimplexNoise? NoiseGen;
        public int NoiseQuanityOctaves { get; set; } = 2;
        public double NoiseBaseFrequency { get; set; } = 1 / 10.0;
        public double NoisePersistence { get; set; } = 0.9;

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Event.BeforeActiveSlotChanged += event_BeforeActiveSlotChangedClient;
            capi = api;
        }

        private EnumHandling event_BeforeActiveSlotChangedClient(ActiveSlotChangeEventArgs args)
        {
            return capi == null ? EnumHandling.PassThrough : preventSlotChangeOnActiveFishing(capi.World.Player);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.BeforeActiveSlotChanged += event_BeforeActiveSlotChangedServer;
            api.Event.PlayerDisconnect += event_PlayerDisconnect;
            api.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, onShutdown);

            FishFrequencyMapNoise = NormalizedSimplexNoise.FromDefaultOctaves(NoiseQuanityOctaves, NoiseBaseFrequency, NoisePersistence, api.World.SeedWorldgen);
        }

        private EnumHandling event_BeforeActiveSlotChangedServer(IPlayer player, ActiveSlotChangeEventArgs args)
        {
            return preventSlotChangeOnActiveFishing(player);
        }

        private void onShutdown()
        {
            if (sapi == null) return;

            foreach (var plr in sapi.World.AllOnlinePlayers)
            {
                var slot = plr.InventoryManager.ActiveHotbarSlot;
                (slot?.Itemstack?.Collectible as ItemFishingPole)?.StopFishing(slot!.Itemstack, plr.Entity);
            }
        }

        private void event_PlayerDisconnect(IServerPlayer plr)
        {
            var slot = plr.InventoryManager.ActiveHotbarSlot;
            (slot?.Itemstack?.Collectible as ItemFishingPole)?.StopFishing(slot!.Itemstack, plr.Entity);
        }

        private EnumHandling preventSlotChangeOnActiveFishing(IPlayer player)
        {
            var slot = player.InventoryManager.ActiveHotbarSlot;

            if (slot?.Itemstack?.Collectible is not ItemFishingPole) return EnumHandling.PassThrough;

            long bobberEntityId = slot.Itemstack.Attributes.GetLong("fishingEntityId", 0);
            return bobberEntityId != 0 ? EnumHandling.PreventSubsequent : EnumHandling.PassThrough;
        }
    }



    public class ItemFishingPole : Item, IContainedInteractable, IHandBookPageCodeProvider
    {
        protected ClothManager clothManager = null!;
        protected string aimAnimation = "bowaimlong";
        protected Vec3f offsetToPoleTipFp = new(3.3f, -0.05f, 0.4f);
        protected Vec3f offsetToPoleTipTp = new(2.3f, 0f, 0f);
        protected CompositeShape? ropelessShapeLoc;
        protected EnumCameraMode? previousCameraMode;


        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            clothManager = api.ModLoader.GetModSystem<ClothManager>()
                        ?? throw new InvalidOperationException("Failed to find ClothManager mod system");

            aimAnimation = Attributes["aimAnimation"].AsString(aimAnimation);
            offsetToPoleTipFp = Attributes["offsetToPoleTipFp"].AsObject(offsetToPoleTipFp);
            offsetToPoleTipTp = Attributes["offsetToPoleTipTp"].AsObject(offsetToPoleTipTp);

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

            if (priority == EnumMergePriority.DirectMerge && sourceStack.ItemAttributes?.IsTrue("isFishBait") == true)
            {
                return 1;
            }

            return base.GetMergableQuantity(sinkStack, sourceStack, priority);
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            if (op.SinkSlot.Itemstack?.Attributes.GetBool("fishing") == true) return;
            if (op.CurrentPriority != EnumMergePriority.DirectMerge)
            {
                base.TryMergeStacks(op);
                return;
            }
            if (op.SourceSlot.Itemstack?.ItemAttributes?.IsTrue("isFishBait") != true) return;

            ItemStack? fishingPole = op.SinkSlot.Itemstack;
            if (fishingPole == null) return;

            ItemStack? oldBaitStack = fishingPole.Attributes.GetItemstack("fishingBait");
            ItemStack newBaitStack = op.SourceSlot.TakeOut(1);
            fishingPole.Attributes.SetItemstack("fishingBait", newBaitStack);
            op.MovedQuantity = 1;

            if (oldBaitStack == null) return;

            if (op.SourceSlot.StackSize <= 0 && op.SourceSlot.CanHold(new DummySlot(oldBaitStack)))
            {
                op.SourceSlot.Itemstack = oldBaitStack;
                op.SourceSlot.MarkDirty();
            }
            else if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(oldBaitStack))
            {
                op.ActingPlayer.Entity.World.SpawnItemEntity(oldBaitStack, op.ActingPlayer.Entity.Pos.XYZ);
            }
        }


        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (target != EnumItemRenderTarget.HandTp || !itemstack.Attributes.GetBool("fishing") || ropelessShapeLoc == null)
            {
                base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
                return;
            }

            var meshdata = ObjectCacheUtil.GetOrCreate(capi, "fishingPoleRopelessShape" + Code, () =>
            {
                var shape = capi.Assets.Get(ropelessShapeLoc.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
                capi.Tesselator.TesselateShape(this, shape, out var meshdata);
                return capi.Render.UploadMultiTextureMesh(meshdata);
            });

            renderinfo.ModelRef = meshdata;
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

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            updatePinOffset(slot, byEntity);
        }

        protected virtual void updatePinOffset(ItemSlot slot, EntityAgent byEntity)
        {
            var rope = getRope(slot);
            if (rope != null && !rope.FirstPoint.ApplyOffsetToAttachment)
            {
                rope.FirstPoint.ApplyOffsetToAttachment = true;
                rope.FirstPoint.MarkDirty();
            }

            if (api is not ICoreClientAPI capi) return;


            var camMode = capi.World.Player.CameraMode;
            if (camMode != previousCameraMode && previousCameraMode == EnumCameraMode.FirstPerson && camMode != EnumCameraMode.FirstPerson)
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
                    Matrixf m = new();
                    LoadHeldItemModelMatrix(m, byEntity, slot, capi);
                    var pos = m.TransformVector(new Vec4f(offsetToPoleTipFp.X, offsetToPoleTipFp.Y, offsetToPoleTipFp.Z, 1));

                    var rapi = capi.Render;
                    var pMatrixHandFov = (capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer)?.pMatrixHandFov;
                    var pMatrixNormalFov = (capi.World.Player.Entity.Properties.Client.Renderer as EntityShapeRenderer)?.pMatrixNormalFov;

                    if (pMatrixHandFov != null)
                    {
                        Matrixf tmpMatrix = new();
                        Vec4f screenSpaceCoordNormalFov = tmpMatrix.Set(pMatrixHandFov).Mul(rapi.CameraMatrixOriginf).TransformVector(pos);
                        Matrixf unprojectMatrix = new Matrixf(rapi.CameraMatrixOriginf).Invert().Mul(tmpMatrix.Set(pMatrixNormalFov).Invert());
                        pos = unprojectMatrix.TransformVector(screenSpaceCoordNormalFov);
                    }


                    cs.FirstPoint.pinnedToOffset = pos.XYZ;
                    cs.FirstPoint.NoAttachTransform = true;
                }
            }

            previousCameraMode = camMode;
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

        protected virtual void throwBobber(ItemSlot slot, EntityAgent byEntity)
        {
            byEntity.AnimManager.StopAnimation(aimAnimation);

            // prevent throwing the pole away or switching to a different slot
            bool isFishing = byEntity.ActiveHandItemSlot.Itemstack?.Attributes.GetLong("fishingEntityId") != 0;
            if (slot.Empty || !isFishing) return;

            byEntity.AnimManager.StartAnimation("fishingpole-idle");

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                slot.Itemstack.Attributes.SetBool("fishing", true);
                slot.MarkDirty();
                return;
            }

            var prevEntityId = slot.Itemstack.Attributes.GetLong("bobberEntityId");
            if (prevEntityId != 0 && api.World.GetEntityById(prevEntityId) is EntityBobber prevBobber)
            {
                slot.Itemstack.Attributes.RemoveAttribute("fishing");
                slot.MarkDirty();
                return;
            }


            byEntity.World.PlaySoundAt(new AssetLocation("sounds/effect/poleswing"), byEntity, null, false, 8);

            var type = byEntity.World.GetEntityType(new AssetLocation("bobber-normal"));
            EntityBobber ebobber = (EntityBobber)byEntity.World.ClassRegistry.CreateEntity(type);
            ebobber.FiredBy = byEntity;
            ebobber.DropOnImpactChance = 0;

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

            ebobber.PreInitialize();

            byEntity.World.SpawnPriorityEntity(ebobber);

            var cs = getRope(slot);
            if (cs != null) clothManager.UnregisterCloth(cs.ClothId);

            createRope(slot, byEntity, ebobber, ebobber.Pos.XYZ);

            slot.Itemstack.Attributes.SetLong("bobberEntityId", ebobber.EntityId);
            slot.Itemstack.Attributes.SetBool("fishing", true);
        }

        public virtual bool StopFishing(ItemStack? itemstack, EntityAgent byEntity)
        {
            byEntity.AnimManager.StopAnimation("fishingpole-idle");
            if (itemstack == null) return false;

            itemstack.Attributes.RemoveAttribute("fishing");

            long beid = itemstack.Attributes.GetLong("bobberEntityId", 0);
            if (beid != 0)
            {
                clothManager.UnregisterCloth(itemstack.Attributes.GetInt("clothId", 0));
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
                baitStack = baitStack.Clone();
                baitStack.StackSize = 1;
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
        public virtual string HandbookPageCodeForStack(IWorldAccessor world, ItemStack stack)
        {
            ItemStack cleanStack = stack.Clone();
            cleanStack.Attributes.RemoveAttribute("fishingBait");
            cleanStack.Attributes.RemoveAttribute("bobberEntityId");
            cleanStack.Attributes.RemoveAttribute("clothId");
            cleanStack.Attributes.RemoveAttribute("fishingEntityId");
            cleanStack.Attributes.RemoveAttribute("fishing");
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
            else if (inSlot.Itemstack?.Attributes.GetBool("fishing") == true)
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
            // Prevent execution of base method
        }

        public override void OnModifiedInInventorySlot(IWorldAccessor world, ItemSlot slot, ItemStack? extractedStack = null)
        {
            long eid = slot.Itemstack?.Attributes.GetLong("fishingEntityId") ?? 0;
            var eagent = world.GetEntityById(eid) as EntityAgent;

            if (extractedStack != null && eagent != null && eagent.RightHandItemSlot.Empty)
            {
                StopFishing(extractedStack, eagent);
            }
        }

        protected virtual ClothSystem? getRope(ItemSlot slot)
        {
            int clothId = slot.Itemstack?.Attributes.GetInt("clothId", 0) ?? 0;
            return clothManager.GetClothSystem(clothId);
        }

        protected virtual ClothSystem createRope(ItemSlot slot, EntityAgent byEntity, EntityBobber ebobber, Vec3d targetPos)
        {
            ClothSystem sys = ClothSystem.CreateRope(api, clothManager, byEntity.Pos.XYZ, targetPos, null, 4);

            sys.ChangeRopeLength(0.5);
            sys.CanPull = false;
            sys.CanRip = false;
            sys.StretchWarn = 1.5f;
            sys.StretchRip = 2;
            sys.FirstPoint.PinTo(byEntity, offsetToPoleTipTp);
            sys.FirstPoint.ApplyOffsetToAttachment = true;
            sys.LastPoint.PinTo(ebobber, new Vec3f());
            sys.LastPoint.NoAttachTransform = true;
            sys.RopeRenderThickness = 0.35f;
            clothManager.RegisterCloth(sys);

            // A lot of stretch force applies on the initial throw. Haxy fix: Don't let it rip for the first 2 seconds
            api.World.RegisterCallback((dt) => sys.CanRip = true, 2000);

            slot.Itemstack?.Attributes.SetLong("ropeHeldByEntityId", byEntity.EntityId);
            slot.Itemstack?.Attributes.SetInt("clothId", sys.ClothId);
            slot.MarkDirty();
            return sys;
        }

        public virtual bool LoadHeldItemModelMatrix(Matrixf mmat, EntityAgent entity, ItemSlot itemSlot, ICoreClientAPI api, bool right = true)
        {
            if (entity.Properties.Client.Renderer is not EntityShapeRenderer entityShapeRenderer) return false;

            if (itemSlot.Itemstack == null) return false;

            AttachmentPointAndPose? attachmentPointAndPose = entity.AnimManager?.Animator?.GetAttachmentPointPose(right ? "RightHand" : "LeftHand");
            if (attachmentPointAndPose == null) return false;

            AttachmentPoint attachPoint = attachmentPointAndPose.AttachPoint;
            ItemRenderInfo itemStackRenderInfo = api.Render.GetItemStackRenderInfo(itemSlot, right ? EnumItemRenderTarget.HandTp : EnumItemRenderTarget.HandTpOff, 0f);
            if (itemStackRenderInfo?.Transform is not ModelTransform itemStackRenderTransform) return false;

            mmat.Set(entityShapeRenderer.ModelMat)
                .Mul(attachmentPointAndPose.AnimModelMatrix)
                .Translate(itemStackRenderTransform.Origin.X, itemStackRenderTransform.Origin.Y, itemStackRenderTransform.Origin.Z)
                .Scale(itemStackRenderTransform.ScaleXYZ.X, itemStackRenderTransform.ScaleXYZ.Y, itemStackRenderTransform.ScaleXYZ.Z)
                .Translate(attachPoint.PosX / 16.0 + itemStackRenderTransform.Translation.X,
                           attachPoint.PosY / 16.0 + itemStackRenderTransform.Translation.Y,
                           attachPoint.PosZ / 16.0 + itemStackRenderTransform.Translation.Z)
                .RotateX((float)(attachPoint.RotationX + itemStackRenderTransform.Rotation.X) * (MathF.PI / 180f))
                .RotateY((float)(attachPoint.RotationY + itemStackRenderTransform.Rotation.Y) * (MathF.PI / 180f))
                .RotateZ((float)(attachPoint.RotationZ + itemStackRenderTransform.Rotation.Z) * (MathF.PI / 180f))
                .Translate(0f - itemStackRenderTransform.Origin.X, 0f - itemStackRenderTransform.Origin.Y, 0f - itemStackRenderTransform.Origin.Z);

            return true;
        }

        public virtual bool OnContainedInteractStart(BlockEntity be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
        {
            var handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Itemstack?.Collectible.Attributes?["isFishBait"]?.AsBool() != true) return false;

            if (slot.Itemstack?.Attributes.GetItemstack("fishingBait") == null)
            {
                slot.Itemstack?.Attributes.SetItemstack("fishingBait", handSlot.Itemstack);
                handSlot.TakeOut(1);
                handSlot.MarkDirty();
            }
            else (api as ICoreClientAPI)?.TriggerIngameError(this, "fishingpolealreadybaited", Lang.Get("ingameerror-fishingpole-alreadybaited"));

            return true;
        }

        public virtual WorldInteraction[] GetContainedInteractionHelp(BlockEntity be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
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
