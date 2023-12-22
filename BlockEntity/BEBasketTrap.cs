using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public enum EnumTrapState
    {
        Empty,
        Ready,
        Trapped
    }

    public class BlockEntityBasketTrap : BlockEntityDisplay, IAnimalFoodSource, IPointOfInterest
    {
        protected ICoreServerAPI sapi;

        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "baskettrap";
        public override int DisplayedItems => trapState == EnumTrapState.Ready ? 1 : 0;
        public override string AttributeTransformCode => "baskettrap";

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.25, 0.5);
        public string Type => inv.Empty ? "nothing" : "food";


        EnumTrapState trapState;


        public BlockEntityBasketTrap()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inv.LateInitialize("baskettrap-" + Pos, api);

            sapi = api as ICoreServerAPI;
            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(OnClientTick, 1000);
                animUtil?.InitializeAnimator("baskettrap");
                if (trapState == EnumTrapState.Trapped)
                {
                    animUtil?.StartAnimation(new AnimationMetaData() { Animation = "triggered", Code = "triggered" });
                }
            } else
            {
                sapi.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
            }
        }

        private void OnClientTick(float dt)
        {
            if (trapState == EnumTrapState.Trapped && Api.World.Rand.NextDouble() > 0.8 && BlockBehaviorCreatureContainer.GetStillAliveDays(Api.World, inv[0].Itemstack) > 0 && animUtil.activeAnimationsByAnimCode.Count < 2)
            {
                string anim = Api.World.Rand.NextDouble() > 0.5 ? "hopshake" : "shaking";
                animUtil?.StartAnimation(new AnimationMetaData() { Animation = anim, Code = anim });
            }
        }


        public bool Interact(IPlayer player, BlockSelection blockSel)
        {
            if (trapState == EnumTrapState.Ready) return true;

            if (inv[0].Empty)
            {
                tryReadyTrap(player);
            } else
            {
                if (!player.InventoryManager.TryGiveItemstack(inv[0].Itemstack))
                {
                    Api.World.SpawnItemEntity(inv[0].Itemstack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                }
                Api.World.BlockAccessor.SetBlock(0, Pos);
            }

            return true;
        }

        private void tryReadyTrap(IPlayer player)
        {
            var heldSlot = player.InventoryManager.ActiveHotbarSlot;
            if (heldSlot.Empty) return;

            var collobj = heldSlot?.Itemstack.Collectible;
            if (!heldSlot.Empty && (collobj.NutritionProps != null || collobj.Attributes?.IsTrue("foodTags") == true))
            {
                trapState = EnumTrapState.Ready;
                inv[0].Itemstack = heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
                MarkDirty(true);
            }
        }

        public bool IsSuitableFor(Entity entity, CreatureDiet diet)
        {
            return trapState == EnumTrapState.Ready && entity.Properties.Attributes?.IsTrue("basketCatchable") == true && diet.Matches(inv[0].Itemstack);
        }

        public float ConsumeOnePortion(Entity entity)
        {
            sapi.Event.EnqueueMainThreadTask(() => TrapAnimal(entity), "trapanimal");
            return 1f;
        }

        private void TrapAnimal(Entity entity)
        {
            animUtil?.StartAnimation(new AnimationMetaData() { Animation = "triggered", Code = "triggered" });

            var jstack = Block.Attributes["creatureContainer"].AsObject<JsonItemStack>();
            jstack.Resolve(Api.World, "creature container of " + Block.Code);

            inv[0].Itemstack = jstack.ResolvedItemstack;
            BlockBehaviorCreatureContainer.CatchCreature(inv[0], entity);
            trapState = EnumTrapState.Trapped;
            MarkDirty(true);
        }



        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (worldForResolving.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
            }

            trapState = (EnumTrapState)tree.GetInt("trapState");

            if (trapState == EnumTrapState.Trapped)
            {
                animUtil?.StartAnimation(new AnimationMetaData() { Animation = "triggered", Code = "triggered" });
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("trapState", (int)trapState);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            dsc.Append(BlockEntityShelf.PerishableInfoCompact(Api, inv[0], 0));
        }

        protected override float[][] genTransformationMatrices()
        {
            tfMatrices = new float[1][];

            for (int i = 0; i < 1; i++)
            {
                tfMatrices[i] =
                    new Matrixf()
                    .Translate(0.5f, 0.1f, 0.5f)
                    .Scale(0.75f, 0.75f, 0.75f)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values
                ;
            }

            return tfMatrices;
        }
    }
}
