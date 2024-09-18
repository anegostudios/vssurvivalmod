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
        Trapped,
        Destroyed
    }

    public class BlockEntityBasketTrap : BlockEntityDisplay, IAnimalFoodSource, IPointOfInterest
    {
        protected ICoreServerAPI sapi;

        InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "baskettrap";
        public override int DisplayedItems => TrapState == EnumTrapState.Ready ? 1 : 0;
        public override string AttributeTransformCode => "baskettrap";

        AssetLocation destroyedShapeLoc;
        AssetLocation trappedShapeLoc; // Only used for the block breaking decal

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.25, 0.5);
        public string Type => inv.Empty ? "nothing" : "food";


        public EnumTrapState TrapState;
        float rotationYDeg;
        float[] rotMat;

        public float RotationYDeg
        {
            get { return rotationYDeg; }
            set { 
                rotationYDeg = value;
                rotMat = Matrixf.Create().Translate(0.5f, 0, 0.5f).RotateYDeg(rotationYDeg - 90).Translate(-0.5f, 0, -0.5f).Values;
            }
        }

        public BlockEntityBasketTrap()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            inv.LateInitialize("baskettrap-" + Pos, api);

            destroyedShapeLoc = AssetLocation.Create(Block.Attributes["destroyedShape"].AsString(), Block.Code.Domain).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            trappedShapeLoc = AssetLocation.Create(Block.Attributes["trappedShape"].AsString(), Block.Code.Domain).WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            sapi = api as ICoreServerAPI;
            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(OnClientTick, 1000);
                animUtil?.InitializeAnimator("baskettrap", null, null, new Vec3f(0, rotationYDeg, 0));
                if (TrapState == EnumTrapState.Trapped)
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
            if (TrapState == EnumTrapState.Trapped && !inv.Empty && Api.World.Rand.NextDouble() > 0.8 && BlockBehaviorCreatureContainer.GetStillAliveDays(Api.World, inv[0].Itemstack) > 0 && animUtil.activeAnimationsByAnimCode.Count < 2)
            {
                string anim = Api.World.Rand.NextDouble() > 0.5 ? "hopshake" : "shaking";
                animUtil?.StartAnimation(new AnimationMetaData() { Animation = anim, Code = anim });
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/reedtrapshake*"), Pos, -0.25, null, true, 16);
            }
        }


        public bool Interact(IPlayer player, BlockSelection blockSel)
        {
            if (TrapState == EnumTrapState.Ready || TrapState == EnumTrapState.Destroyed) return true;
            
            if (inv[0].Empty)
            {
                var stack = new ItemStack(Block);

                if (TrapState == EnumTrapState.Empty) tryReadyTrap(player);
                else
                {
                    if (!player.InventoryManager.ActiveHotbarSlot.Empty) return true;

                    if (!player.InventoryManager.TryGiveItemstack(stack))
                    {
                        Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                    }

                    Api.World.BlockAccessor.SetBlock(0, Pos);
                }
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
            if (!heldSlot.Empty && (collobj.NutritionProps != null || collobj.Attributes?["foodTags"].Exists == true))
            {
                TrapState = EnumTrapState.Ready;
                inv[0].Itemstack = heldSlot.TakeOut(1);
                heldSlot.MarkDirty();
                MarkDirty(true);
            }
        }

        public bool IsSuitableFor(Entity entity, CreatureDiet diet)
        {
            if (TrapState != EnumTrapState.Ready) return false;
            bool catchable = entity.Properties.Attributes?.IsTrue("basketCatchable") == true;
            bool dietMatches = diet.Matches(inv[0].Itemstack);
            return catchable && dietMatches;
        }

        public float ConsumeOnePortion(Entity entity)
        {
            sapi.Event.EnqueueMainThreadTask(() => TrapAnimal(entity), "trapanimal");
            return 1f;
        }

        private void TrapAnimal(Entity entity)
        {
            animUtil?.StartAnimation(new AnimationMetaData() { Animation = "triggered", Code = "triggered" });

            float trapChance = entity.Properties.Attributes["trapChance"].AsFloat(0.5f);
            if (Api.World.Rand.NextDouble() < trapChance)
            {
                var jstack = Block.Attributes["creatureContainer"].AsObject<JsonItemStack>();
                jstack.Resolve(Api.World, "creature container of " + Block.Code);
                inv[0].Itemstack = jstack.ResolvedItemstack;
                BlockBehaviorCreatureContainer.CatchCreature(inv[0], entity);
            }
            else
            {
                inv[0].Itemstack = null;

                float trapDestroyChance = entity.Properties.Attributes["trapDestroyChance"].AsFloat(0f);
                if (Api.World.Rand.NextDouble() < trapDestroyChance)
                {
                    TrapState = EnumTrapState.Destroyed;
                    MarkDirty(true);
                    Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), Pos, -0.25, null, false, 16);
                    return;
                }
            }

            TrapState = EnumTrapState.Trapped;
            MarkDirty(true);
            Api.World.PlaySoundAt(new AssetLocation("sounds/block/reedtrapshut"), Pos, -0.25, null, false, 16);
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

            TrapState = (EnumTrapState)tree.GetInt("trapState");
            RotationYDeg = tree.GetFloat("rotationYDeg");

            if (TrapState == EnumTrapState.Trapped)
            {
                animUtil?.StartAnimation(new AnimationMetaData() { Animation = "triggered", Code = "triggered" });
            }

            // Do this last
            RedrawAfterReceivingTreeAttributes(worldForResolving);     // Redraw on client after we have completed receiving the update from server
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("trapState", (int)TrapState);
            tree.SetFloat("rotationYDeg", rotationYDeg);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (TrapState == EnumTrapState.Trapped && !inv.Empty)
            {
                ItemStack stack = inv[0].Itemstack;
                var bh = stack.Collectible.GetBehavior<BlockBehaviorCreatureContainer>();
                if (bh != null)
                {
                    bh.AddCreatureInfo(stack, dsc, Api.World);
                }
            }
            else
            {
                dsc.Append(BlockEntityShelf.PerishableInfoCompact(Api, inv[0], 0));
            }
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


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (TrapState == EnumTrapState.Destroyed)
            {
                mesher.AddMeshData(GetOrCreateMesh(destroyedShapeLoc), rotMat);
                return true;
            }

            bool skip = base.OnTesselation(mesher, tessThreadTesselator);
            if (!skip) mesher.AddMeshData(capi.TesselatorManager.GetDefaultBlockMesh(Block), rotMat);
            return true;
        }

        public MeshData GetCurrentMesh(ITexPositionSource texSource)
        {
            switch (TrapState)
            {
                case EnumTrapState.Empty:
                case EnumTrapState.Ready: return GetOrCreateMesh(Block.Shape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
                case EnumTrapState.Trapped: return GetOrCreateMesh(trappedShapeLoc, texSource);
                case EnumTrapState.Destroyed: return GetOrCreateMesh(destroyedShapeLoc, texSource);
            }

            return null;
        }

        public MeshData GetOrCreateMesh(AssetLocation loc, ITexPositionSource texSource = null)
        {
            return ObjectCacheUtil.GetOrCreate(Api, "destroyedBasketTrap-" + loc + (texSource == null ? "-d" : "-t"), () =>
            {
                var shape = Api.Assets.Get<Shape>(loc);
                if (texSource == null)
                {
                    texSource = new ShapeTextureSource(capi, shape, loc.ToShortString());
                }

                (Api as ICoreClientAPI).Tesselator.TesselateShape("basket trap decal", Api.Assets.Get<Shape>(loc), out var meshdata, texSource);
                return meshdata;
            });
        }
    }
}
