using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

#nullable disable


namespace Vintagestory.GameContent
{
    public enum EnumTrapState
    {
        Empty,
        Ready,
        Trapped,
        Destroyed
    }

    public class TrapChances
    {
        public float TrapChance;
        public float TrapDestroyChance;
        
        public static Dictionary<string, TrapChances> FromEntityAttr(Entity entity)
        {
            return entity.Properties.Attributes?["trappable"].AsObject<Dictionary<string, TrapChances>>();
        }

        public static bool IsTrappable(Entity entity, string traptype)
        {
            return entity.Properties.Attributes?["trappable"]?[traptype].Exists == true;
        }
    }

    public class BlockEntityAnimalTrap : BlockEntityDisplay, IAnimalFoodSource, IPointOfInterest
    {
        protected ICoreServerAPI sapi;
        protected CompositeShape destroyedShape;
        protected CompositeShape trappedShape; // Only used for the block breaking decal
        protected BlockEntityAnimationUtil animUtil => GetBehavior<BEBehaviorAnimatable>().animUtil;
        protected float rotationYDeg;
        protected float[] rotMat;
        protected string traptype;
        protected ModelTransform baitTransform;
        protected float foodTagMinWeight;

        protected InventoryGeneric inv;
        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "baskettrap";
        public override int DisplayedItems => TrapState == EnumTrapState.Ready ? 1 : 0;
        public override string AttributeTransformCode => "baskettrap";
        public EnumTrapState TrapState;
        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.25, 0.5);
        public string Type => inv.Empty ? "nothing" : "food";

        
        public float RotationYDeg
        {
            get { return rotationYDeg; }
            set {
                rotationYDeg = value;
                rotMat = Matrixf.Create().Translate(0.5f, 0, 0.5f).RotateYDeg(rotationYDeg - 90).Translate(-0.5f, 0, -0.5f).Values;
            }
        }

        public BlockEntityAnimalTrap()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            traptype = Block.Attributes["traptype"].AsString("small");
            foodTagMinWeight = Block.Attributes["foodTagMinWeight"].AsFloat(0.1f);
            baitTransform = Block.Attributes["baitTransform"].AsObject<ModelTransform>(ModelTransform.NoTransform);

            base.Initialize(api);
            inv.LateInitialize("baskettrap-" + Pos, api);

            destroyedShape = Block.Attributes["destroyedShape"].AsObject<CompositeShape>(null, Block.Code.Domain);
            trappedShape = Block.Attributes["trappedShape"].AsObject<CompositeShape>(null, Block.Code.Domain);
            destroyedShape.Bake(api.Assets, api.Logger);
            trappedShape.Bake(api.Assets, api.Logger);

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
            if (TrapState is EnumTrapState.Ready or EnumTrapState.Destroyed) return true;

            if (!Api.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

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

                    Api.World.Logger.Audit("{0} Took 1x{1} at {2}.",
                        player.PlayerName,
                        stack.Collectible.Code,
                        blockSel.Position
                    );
                }
            }
            else
            {
                if (!player.InventoryManager.TryGiveItemstack(inv[0].Itemstack))
                {
                    Api.World.SpawnItemEntity(inv[0].Itemstack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                }
                Api.World.BlockAccessor.SetBlock(0, Pos);

                Api.World.Logger.Audit("{0} Took 1x{1} with {2} at {3}.",
                    player.PlayerName,
                    inv[0].Itemstack.Collectible.Code,
                    inv[0].Itemstack.Attributes.GetString("creaturecode"),
                    blockSel.Position
                );
            }

            return true;
        }

        private void tryReadyTrap(IPlayer player)
        {
            var heldSlot = player.InventoryManager.ActiveHotbarSlot;
            if (heldSlot?.Empty != false || Block is not BlockAnimalTrap blockTrap) return;

            if (!blockTrap.IsAppetizingBait(Api, heldSlot.Itemstack))
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "unappetizingbait", Lang.Get("animaltrap-unappetizingbait-error"));
                return;
            }

            if (!blockTrap.CanFitBait(Api, heldSlot.Itemstack))
            {
                (Api as ICoreClientAPI)?.TriggerIngameError(this, "cannotfitintrap", Lang.Get("animaltrap-cannotfitintrap-error"));
                return;
            }

            TrapState = EnumTrapState.Ready;
            inv[0].Itemstack = heldSlot.TakeOut(1);
            heldSlot.MarkDirty();
            MarkDirty(true);
        }

        public bool IsSuitableFor(Entity entity, CreatureDiet diet)
        {
            if (TrapState != EnumTrapState.Ready) return false;
            if (inv[0]?.Itemstack == null || diet == null) return false;
            bool catchable = TrapChances.IsTrappable(entity, traptype);
            bool dietMatches = diet.Matches(inv[0].Itemstack, false, foodTagMinWeight);
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

            var trapChancesByTrapType = TrapChances.FromEntityAttr(entity);
            if (!trapChancesByTrapType.TryGetValue(traptype, out var trapMeta)) return;

            if (Api.World.Rand.NextDouble() < trapMeta.TrapChance)
            {
                var jstack = Block.Attributes["creatureContainer"].AsObject<JsonItemStack>();
                jstack.Resolve(Api.World, "creature container of " + Block.Code);
                inv[0].Itemstack = jstack.ResolvedItemstack;
                BlockBehaviorCreatureContainer.CatchCreature(inv[0], entity);
            }
            else
            {
                inv[0].Itemstack = null;

                float trapDestroyChance = trapMeta.TrapDestroyChance;
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
                tfMatrices[i] = new float[16];
                if (inv[i].Empty) continue;

                var attr = inv[i].Itemstack.Collectible.Attributes;
                var itemInTrapTransform = attr?["inTrapTransform"][traptype].AsObject<ModelTransform>(null) ?? attr?["inTrapTransform"].AsObject<ModelTransform>(null);
                if (itemInTrapTransform == null)
                {
                    var groundTf = inv[i].Itemstack.Collectible.GroundTransform.Clone();
                    groundTf.ScaleXYZ *= 0.2f;
                    itemInTrapTransform = groundTf;
                }

                var tf = new Matrixf().Set(baitTransform.AsMatrix)
                    .Translate(0.5f, 0, 0.5f)
                    .RotateYDeg(RotationYDeg - 90)
                    .Translate(-0.5f, 0, -0.5f)
                    .Values;

                Mat4f.Mul(tfMatrices[i], tf, itemInTrapTransform.AsMatrix);
            }

            return tfMatrices;
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (TrapState == EnumTrapState.Destroyed)
            {
                mesher.AddMeshData(GetOrCreateMesh(destroyedShape, tessThreadTesselator.GetTextureSource(Block)), rotMat);
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
                case EnumTrapState.Ready: return GetOrCreateMesh(Block.Shape);
                case EnumTrapState.Trapped: return GetOrCreateMesh(trappedShape, texSource);
                case EnumTrapState.Destroyed: return GetOrCreateMesh(destroyedShape, texSource);
            }

            return null;
        }

        public MeshData GetOrCreateMesh(CompositeShape cshape, ITexPositionSource texSource = null)
        {
            string key = Block.Variant["material"] + "BasketTrap-" + cshape.ToString();
            return ObjectCacheUtil.GetOrCreate(capi, key, () =>
                capi.TesselatorManager.CreateMesh(
                    "basket trap decal",
                    cshape,
                    (shape, name) => new ShapeTextureSource(capi, shape, name),
                    texSource
            ));
        }
    }
}
