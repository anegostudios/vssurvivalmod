using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class JuicableProperties
    {
        public int Quantity;
        public JsonItemStack LiquidStack;
    }

    public enum EnumFruitPressSection
    {
        Ground,
        MashContainer,
        Screw
    }

    public class BlockEntityFruitPress : BlockEntityContainer
    {
        // Slot 0: Berries / Mash
        // Slot 1: Bucket
        InventoryGeneric inv;
        
        public override InventoryBase Inventory => inv;

        public override string InventoryClassName => "fruitpress";


        ICoreClientAPI capi;
        BlockFruitPress ownBlock;

        MeshData meshMovable;

        FruitpressContentsRenderer renderer;


        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>()?.animUtil; }
        }


        public BlockEntityFruitPress()
        {
            inv = new InventoryGeneric(2, "fruitpress-0", null, null);
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            this.ownBlock = Block as BlockFruitPress;
            capi = api as ICoreClientAPI;

            if (api.Side == EnumAppSide.Client && ownBlock != null)
            {
                Shape shape = api.Assets.TryGet("shapes/block/wood/fruitpress/part-movable.json").ToObject<Shape>();
                capi.Tesselator.TesselateShape(ownBlock, shape, out meshMovable);
                animUtil?.InitializeAnimator("fruitpress", new Vec3f(0, ownBlock.Shape.rotateY, 0), shape);

                renderer = new FruitpressContentsRenderer(api as ICoreClientAPI, Pos, this);
                (api as ICoreClientAPI).Event.RegisterRenderer(renderer, EnumRenderStage.Opaque, "fruitpress");
            }
        }


        bool nowCompress;

        // What needs to happen while squeezing:
        // 0..3 sec: Linearly squeeze mash mesh, stay squeezed
        // 0..3 sec: Spawn juice particles all around
        // 1..6 sec: Grow juice quad, spawn particles at the spout
        // 6..12 sec: Shrink juice quad
        // 1..12 sec: Add liquid to bucket
        public bool OnBlockInteractStart(IPlayer byPlayer, BlockSelection blockSel, EnumFruitPressSection section)
        {
            ItemSlot handslot = byPlayer.InventoryManager.ActiveHotbarSlot;
            ItemStack handStack = handslot.Itemstack;
            ItemSlot contentSlot = inv[0];

            if (section == EnumFruitPressSection.MashContainer)
            {
                if (!handslot.Empty)
                {
                    var props = getJuicableProps(handStack);
                    if (props != null && handStack.StackSize >= props.Quantity && handslot.TryPutInto(Api.World, contentSlot, props.Quantity) > 0)
                    {
                        handslot.MarkDirty();
                        MarkDirty(true);
                        renderer?.reloadMeshes(true);
                    }
                }

                return true;
            }

            if (section == EnumFruitPressSection.Ground)
            {
                if (handStack != null && handStack.Collectible is BlockLiquidContainerBase blockLiqCont && blockLiqCont.CapacityLitres > 5 && blockLiqCont.CapacityLitres < 20 && inv[1].Empty)
                {
                    bool moved = handslot.TryPutInto(Api.World, inv[1], 1) > 0;
                    if (moved)
                    {
                        handslot.MarkDirty();
                        MarkDirty(true);
                    }
                }

                return true;
            }

            nowCompress = true;

            if (Api.Side == EnumAppSide.Client)
            {
                if (animUtil.activeAnimationsByAnimCode.ContainsKey("compress"))
                {
                    animUtil.StopAnimation("compress");
                }
                else
                {

                    animUtil.StartAnimation(new AnimationMetaData()
                    {
                        Animation = "compress",
                        Code = "compress",
                        AnimationSpeed = 0.75f,
                        EaseOutSpeed = 0.5f,
                        EaseInSpeed = 3
                    });

                    Api.World.PlaySoundAt(new AssetLocation("sounds/player/wetclothsqueeze.ogg"), Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5, byPlayer, false);
                }
            }

            

            return true;
        }

        public JuicableProperties getJuicableProps(ItemStack stack)
        {
            return stack.ItemAttributes?["juicableProperties"].Exists == true ? stack.ItemAttributes["juicableProperties"].AsObject<JuicableProperties>(null, stack.Collectible.Code.Domain) : null;
        }

        public bool OnBlockInteractStep(float secondsUsed, IPlayer byPlayer, EnumFruitPressSection section)
        {
            if (section != EnumFruitPressSection.Screw) return false;

            return nowCompress && secondsUsed < 4;
        }


        public void OnBlockInteractStop(float secondsUsed, IPlayer byPlayer)
        {
            if (secondsUsed >= 3)
            {

            }

            if (animUtil.activeAnimationsByAnimCode.ContainsKey("compress"))
            {
                animUtil.StopAnimation("compress");
            }
        }


        public bool OnBlockInteractCancel(float secondsUsed, IPlayer byPlayer)
        {
            if (animUtil.activeAnimationsByAnimCode.ContainsKey("compress"))
            {
                animUtil.StopAnimation("compress");
            }

            return true;
        }


        public override void OnBlockBroken()
        {
            //base.OnBlockBroken(); - don't drop contents
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            bool wasEmpty = Inventory.Empty;

            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                renderer?.reloadMeshes(wasEmpty != Inventory.Empty);
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            bool skip = base.OnTesselation(mesher, tessThreadTesselator);

            if (!skip) mesher.AddMeshData(meshMovable);

            return false;
        }

    }
}
