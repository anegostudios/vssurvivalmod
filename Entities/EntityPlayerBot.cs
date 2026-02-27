using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class EntityPlayerBot : EntityAnimalBot
    {
        EntityBehaviorSeraphInventory invbh;
        public override bool StoreWithChunk
        {
            get { return true; }
        }

        public override ItemSlot RightHandItemSlot => invbh.Inventory[15];
        public override ItemSlot LeftHandItemSlot => invbh.Inventory[16];

        public EntityPlayerBot() : base() { }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long chunkindex3d)
        {
            base.Initialize(properties, api, chunkindex3d);

            Name = WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name");

            invbh = GetBehavior<EntityBehaviorSeraphInventory>();
        }


        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            if (World.Side == EnumAppSide.Client)
            {
                (Properties.Client.Renderer as EntityShapeRenderer).DoRenderHeldItem = true;
            }

            var inv = Properties.Attributes?["inventory"];
            if (inv?.Exists == true)
            {
                foreach (var jstack in inv.AsArray<JsonItemStack>())
                {
                    if (jstack.Resolve(World, "player bot inventory"))
                    {
                        TryGiveItemStack(jstack.ResolvedItemstack);
                    }
                }
            }
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            var curCommand = WatchedAttributes.GetString("currentCommand", "");
            if (curCommand != "")
            {
                AnimManager.StopAnimation("idle");
                AnimManager.StopAnimation("idle1");
            }

            HandleHandAnimations(dt);
        }



        protected string lastRunningHeldUseAnimation;
        protected string lastRunningRightHeldIdleAnimation;
        protected string lastRunningLeftHeldIdleAnimation;
        protected string lastRunningHeldHitAnimation;

        protected override void HandleHandAnimations(float dt)
        {
            ItemStack rightstack = RightHandItemSlot?.Itemstack;

            EnumHandInteract interact = servercontrols.HandUse;

            bool nowUseStack = (interact == EnumHandInteract.BlockInteract || interact == EnumHandInteract.HeldItemInteract) || (servercontrols.RightMouseDown && !servercontrols.LeftMouseDown);
            bool wasUseStack = lastRunningHeldUseAnimation != null && AnimManager.ActiveAnimationsByAnimCode.ContainsKey(lastRunningHeldUseAnimation);

            bool nowHitStack = interact == EnumHandInteract.HeldItemAttack || (servercontrols.LeftMouseDown);
            bool wasHitStack = lastRunningHeldHitAnimation != null && AnimManager.ActiveAnimationsByAnimCode.ContainsKey(lastRunningHeldHitAnimation);


            string nowHeldRightUseAnim = rightstack?.Collectible.GetHeldTpUseAnimation(RightHandItemSlot, this);
            string nowHeldRightHitAnim = rightstack?.Collectible.GetHeldTpHitAnimation(RightHandItemSlot, this);
            string nowHeldRightIdleAnim = rightstack?.Collectible.GetHeldTpIdleAnimation(RightHandItemSlot, this, EnumHand.Right);
            string nowHeldLeftIdleAnim = LeftHandItemSlot?.Itemstack?.Collectible.GetHeldTpIdleAnimation(LeftHandItemSlot, this, EnumHand.Left);

            bool nowRightIdleStack = nowHeldRightIdleAnim != null && !nowUseStack && !nowHitStack;
            bool wasRightIdleStack = lastRunningRightHeldIdleAnimation != null && AnimManager.ActiveAnimationsByAnimCode.ContainsKey(lastRunningRightHeldIdleAnimation);

            bool nowLeftIdleStack = nowHeldLeftIdleAnim != null;
            bool wasLeftIdleStack = lastRunningLeftHeldIdleAnimation != null && AnimManager.ActiveAnimationsByAnimCode.ContainsKey(lastRunningLeftHeldIdleAnimation);

            if (rightstack == null)
            {
                nowHeldRightHitAnim = "breakhand";
                nowHeldRightUseAnim = "interactstatic";
            }

            if (nowUseStack != wasUseStack || (lastRunningHeldUseAnimation != null && nowHeldRightUseAnim != lastRunningHeldUseAnimation))
            {
                AnimManager.StopAnimation(lastRunningHeldUseAnimation);
                lastRunningHeldUseAnimation = null;

                if (nowUseStack)
                {
                    AnimManager.StopAnimation(lastRunningRightHeldIdleAnimation);
                    AnimManager.StartAnimation(lastRunningHeldUseAnimation = nowHeldRightUseAnim);
                }
            }

            if (nowHitStack != wasHitStack || (lastRunningHeldHitAnimation != null && nowHeldRightHitAnim != lastRunningHeldHitAnimation))
            {
                AnimManager.StopAnimation(lastRunningHeldHitAnimation);
                lastRunningHeldHitAnimation = null;


                if (nowHitStack)
                {
                    AnimManager.StopAnimation(lastRunningLeftHeldIdleAnimation);
                    AnimManager.StopAnimation(lastRunningRightHeldIdleAnimation);
                    AnimManager.StartAnimation(lastRunningHeldHitAnimation = nowHeldRightHitAnim);
                }
            }

            if (nowRightIdleStack != wasRightIdleStack || (lastRunningRightHeldIdleAnimation != null && nowHeldRightIdleAnim != lastRunningRightHeldIdleAnimation))
            {
                AnimManager.StopAnimation(lastRunningRightHeldIdleAnimation);
                lastRunningRightHeldIdleAnimation = null;

                if (nowRightIdleStack)
                {
                    AnimManager.StartAnimation(lastRunningRightHeldIdleAnimation = nowHeldRightIdleAnim);
                }
            }

            if (nowLeftIdleStack != wasLeftIdleStack || (lastRunningLeftHeldIdleAnimation != null && nowHeldLeftIdleAnim != lastRunningLeftHeldIdleAnimation))
            {
                AnimManager.StopAnimation(lastRunningLeftHeldIdleAnimation);

                lastRunningLeftHeldIdleAnimation = null;

                if (nowLeftIdleStack)
                {
                    AnimManager.StartAnimation(lastRunningLeftHeldIdleAnimation = nowHeldLeftIdleAnim);
                }
            }
        }



        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            base.OnInteract(byEntity, slot, hitPosition, mode);

            var eplr = byEntity as EntityPlayer;
            if (eplr?.Controls.Sneak == true && mode == EnumInteractMode.Interact && byEntity.World.Side == EnumAppSide.Server && eplr.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                if (!LeftHandItemSlot.Empty || !RightHandItemSlot.Empty)
                {
                    LeftHandItemSlot.Itemstack = null;
                    RightHandItemSlot.Itemstack = null;
                }
                else
                {
                    invbh.Inventory.DiscardAll();
                }

                WatchedAttributes.MarkAllDirty();
            }
        }
    }
}
