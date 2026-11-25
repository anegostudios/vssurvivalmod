using OpenTK.Graphics.ES11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityGrindingWheel : BlockEntity, IMechanicalPowerRenderable, IRotatable
    {
        public BlockFacing Facing => BlockFacing.FromCode(Block.Variant["side"]);
        public virtual BlockPos Position { get { return Pos; } }
        public virtual Vec4f LightRgba { get; set; } = new Vec4f();
        public virtual int[] AxisSign { get; set; }
        public virtual float AngleRad => mechanicalPower?.AngleRad ?? 0;

        protected CompositeShape shape = null;
        protected MechanicalPowerMod manager;
        protected ICoreClientAPI capi;
        protected ILoadedSound grindSound;
        protected ILoadedSound turnSound;
        public Dictionary<string, float> PlayersGrinding = new Dictionary<string, float>();
        public bool Grinding;
        ItemStack grindstoneStack;
        BEBehaviorMPGrindingWheel mechanicalPower;
        public bool HasGrindstone => (Block as BlockGrindingWheelAssembly)?.IsGrindstone(grindstoneStack) == true;



        public virtual CompositeShape Shape
        {
            get { return mechanicalPower?.Shape ?? shape; }
            set
            {
                shape = value;
                if (mechanicalPower != null) mechanicalPower.Shape = value;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            mechanicalPower = GetBehavior<BEBehaviorMPGrindingWheel>();
            manager = Api.ModLoader.GetModSystem<MechanicalPowerMod>();
            shape = mechanicalPower?.Shape ?? Block.Shape;
            mechanicalPower?.RefreshComponents();
            scheduleLegacyMigration();

            AxisSign = new int[3] { 0, 0, 0 };
            switch (Facing.Index)
            {
                case 0:
                    AxisSign[0] = -1;
                    break;
                case 1:
                    AxisSign[2] = -1;
                    break;
                case 2:
                    AxisSign[0] = -1;
                    break;
                default:
                    AxisSign[2] = -1;
                    break;
            }

            capi = api as ICoreClientAPI;

            if (mechanicalPower != null) RegisterGameTickListener(checkIsGrinding, 100);
        }

        const string partsVersionKey = "grindingWheelParts";
        ITreeAttribute legacyWheelState;
        long legacyMigrationCallback;


        void scheduleLegacyMigration()
        {
            if (Api?.Side != EnumAppSide.Server || legacyWheelState == null || legacyMigrationCallback != 0) return;

            // Replacing this entity while its chunk is initializing would change the collection being loaded.
            legacyMigrationCallback = RegisterDelayedCallback(migrateLegacyWheel, 0);
        }

        void migrateLegacyWheel(float dt)
        {
            legacyMigrationCallback = 0;
            IBlockAccessor blockAccessor = Api.World.BlockAccessor;
            if (legacyWheelState == null || blockAccessor.GetBlockEntity(Pos) != this || Block is not BlockGrindingWheel frameBlock) return;

            BlockGrindingWheelAssembly assembly = frameBlock.AssemblyBlock as BlockGrindingWheelAssembly
                ?? throw new InvalidOperationException($"Grinding wheel frame {Block.Code} has no grinding wheel assembly");
            BlockAxle axle = Api.World.GetBlock(AssetLocation.Create(frameBlock.Attributes["legacyAxleBlockCode"].AsString(), frameBlock.Code.Domain)) as BlockAxle
                ?? throw new InvalidOperationException($"Grinding wheel frame {Block.Code} has no legacy axle block");
            axle = Api.World.GetBlock(axle.CodeWithVariant("rotation", assembly.Variant["rotation"])) as BlockAxle
                ?? throw new InvalidOperationException($"Grinding wheel frame {Block.Code} has no legacy axle for rotation {assembly.Variant["rotation"]}");
            Item grindstone = Api.World.GetItem(AssetLocation.Create(frameBlock.Attributes["legacyGrindstoneItemCode"].AsString(), frameBlock.Code.Domain))
                ?? throw new InvalidOperationException($"Grinding wheel frame {Block.Code} has no legacy grindstone item");
            ItemStack stoneStack = new(grindstone);
            if (!assembly.IsGrindstone(stoneStack)) throw new InvalidOperationException($"Grinding wheel frame {Block.Code} has an invalid legacy grindstone item");

            ITreeAttribute state = legacyWheelState.Clone();
            state.SetBool(partsVersionKey, true);
            state.SetInt("posx", Pos.X);
            state.SetInt("posy", Pos.InternalY);
            state.SetInt("posz", Pos.Z);
            state.SetString("blockCode", assembly.Code.ToShortString());

            blockAccessor.SetBlock(assembly.BlockId, Pos);
            BlockEntityGrindingWheel wheel = blockAccessor.GetBlockEntity(Pos) as BlockEntityGrindingWheel
                ?? throw new InvalidOperationException($"Grinding wheel assembly {assembly.Code} has no grinding wheel block entity");
            wheel.FromTreeAttributes(state, Api.World);
            wheel.OnBlockPlaced(new ItemStack(axle));
            wheel.GetBehavior<BEBehaviorAxleAttachmentFrame>().SetFrameStack(new ItemStack(frameBlock));
            wheel.SetGrindstone(stoneStack);
            assembly.WasPlaced(Api.World, Pos, null);
        }

        long sparkTickListener;
        float turnSpeed => Math.Abs(mechanicalPower?.CurrentSpeed ?? 0);

        private void checkIsGrinding(float dt)
        {
            if (!HasGrindstone)
            {
                PlayersGrinding.Clear();
                if (Grinding)
                {
                    Grinding = false;
                    if (Api.Side == EnumAppSide.Server) MarkDirty(true);
                }
                stopSounds();
                return;
            }
            if (Api.Side == EnumAppSide.Server)
            {
                if (Grinding && PlayersGrinding.Count == 0)
                {
                    Grinding = false;
                    MarkDirty(true);
                }
                if (!Grinding && PlayersGrinding.Count > 0)
                {
                    Grinding = true;
                    MarkDirty(true);
                }

                if (Api.World.Rand.NextDouble() > 0.05 * turnSpeed) return;

                foreach (var uid in new List<string>(PlayersGrinding.Keys))
                {
                    var plr = Api.World.PlayerByUid(uid);
                    if (plr == null)
                    {
                        PlayersGrinding.Remove(uid);
                        continue;
                    }

                    if (plr.CurrentBlockSelection == null || plr.CurrentBlockSelection.Position != Pos)
                    {
                        PlayersGrinding.Remove(uid);
                        continue;
                    }

                    var slot = plr.InventoryManager.ActiveHotbarSlot;
                    if (slot.Empty)
                    {
                        PlayersGrinding.Remove(uid);
                        continue;
                    }

                    float maxdura = slot.Itemstack.Collectible.GetMaxDurability(slot.Itemstack);
                    if (slot != null && slot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorSharpenable>())
                    {
                        slot.Itemstack.Collectible.DamageItem(Api.World, plr.Entity, slot, (int)Math.Max(1, 0.003f * maxdura));

                    }
                    PlayersGrinding[uid] += dt;

                    var bhb = slot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorBuffable>();
                    var curBuff = bhb.GetItemBuffs(slot.Itemstack).FirstOrDefault((buff) => buff.Code == "sharpened");
                    if (curBuff == null || curBuff.Multiplier < 1.099)
                    {
                        bhb.AddBuff(slot.Itemstack, new AppliedCollectibleBuff()
                        {
                            Code = "sharpened",
                            RemainingDurability = curBuff == null ? (int)(maxdura / 4) : 0,
                            Multiplier = 1.01f,
                            StatCode = "critchance"
                        }, EnumBuffAddType.AddOnDuplicate);
                    }

                }

                return;
            }


            if (turnSound == null && turnSpeed > 0.05)
            {
                turnSound = capi.World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/block/grindingwheel.ogg"),
                    ShouldLoop = true,
                    DisposeOnFinish = false,
                    Position = Pos.ToVec3f(),
                    RelativePosition = false,
                    Range = 16,
                    SoundType = EnumSoundType.Sound,
                    Volume = 1
                });

                turnSound.Start();
            }

            if (turnSpeed > 0.05)
            {
                if (!turnSound.IsPlaying) turnSound.Start();
                turnSound.SetPitch(turnSpeed);
            } else
            {
                if (turnSound != null && turnSound.IsPlaying) turnSound.Stop();
            }

            if (grindSound == null) return;

            if (Grinding)
            {
                grindSound.SetPitch(turnSpeed);

                if (sparkTickListener == 0)
                {
                    grindSound.Start();
                    grindSound.FadeIn(0.32f, null);
                    sparkTickListener = RegisterGameTickListener(onParticleTick, 20);
                }
            } else
            {
                if (sparkTickListener != 0)
                {
                    grindSound.FadeOut(0.32f, (s) => grindSound.Stop());
                    UnregisterGameTickListener(sparkTickListener);
                    sparkTickListener = 0;
                }
            }
        }

        private void onParticleTick(float dt)
        {
            var facing = BlockFacing.FromCode(Block.Variant["side"]).Normalf.Clone();

            if (AxisSign[0] < 0)
            {
            //    facing *= -1;
            }

            var swipe = Math.Sin(Api.World.ElapsedMilliseconds / 300d) / 12.0;
            var ws = -turnSpeed / 2;

            SimpleParticleProperties props = new SimpleParticleProperties()
            {
                MinQuantity = 1,
                AddQuantity = 3,
                VertexFlags = 255,
                Color = ColorUtil.ColorFromRgba(0, 255, 255, 255),
                LifeLength = 0.05f,
                MinSize = 0.02f,
                MaxSize = 0.05f,
                ParticleModel = EnumParticleModel.Quad,
                MinVelocity = new Vec3f(-0.25f - facing.X * 2 * ws, -0.25f, -0.25f - facing.Z * 2 * ws),
                AddVelocity = new Vec3f(0.5f - facing.X * 4 * ws, -0.5f, 0.5f - facing.Z * 4 * ws),
            };

            props.MinPos = Pos.ToVec3d().Add(0.5, 0.9, 0.5);
            props.AddPos = new Vec3d(facing.Z * swipe, 0, facing.X * swipe);
            Api.World.SpawnParticles(props);
        }

        BlockGrindingWheel frameBlock => Block as BlockGrindingWheel ?? (Block as BlockGrindingWheelAssembly)?.FrameBlock as BlockGrindingWheel;

        protected MeshData StandMesh => frameBlock.GetFrameMesh(capi, capi.Tesselator, GetBehavior<BEBehaviorAxleAttachmentFrame>().GetFrameStack());
        protected MeshData getMesh(string[] selEles)
        {
            Shape shape = API.Common.Shape.TryGet(capi, "shapes/block/wood/mechanics/grindingwheel.json");
            float rotateY = 0f;
            switch (Facing.Index)
            {
                case 0:
                    rotateY = 180;
                    break;
                case 1:
                    rotateY = 90;
                    break;
                case 3:
                    rotateY = 270;
                    break;
                default:
                    break;
            }
            capi.Tesselator.TesselateShape(Block, shape, out MeshData mesh, new Vec3f(0, rotateY, 0), null, selEles);
            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            base.OnTesselation(mesher, tesselator);
            LightRgba = mechanicalPower?.LightRgba ?? Api.World.BlockAccessor.GetLightRGBs(Pos);
            mesher.AddMeshData(frameBlock.GetFrameMesh(capi, tesselator, GetBehavior<BEBehaviorAxleAttachmentFrame>().GetFrameStack()));
            return true;
        }

        public bool SetGrindstone(ItemStack stack)
        {
            if (HasGrindstone || Block is not BlockGrindingWheelAssembly assembly || !assembly.IsGrindstone(stack)) return false;

            grindstoneStack = stack.Clone();
            grindstoneStack.StackSize = 1;
            mechanicalPower?.RefreshComponents();
            MarkDirty(true);
            return true;
        }

        public ItemStack GetGrindstoneStack() => HasGrindstone ? grindstoneStack.Clone() : null;

        public string GetGrindstoneMaterial() => HasGrindstone && grindstoneStack.Collectible.Variant.TryGetValue("stone", out string stone) ? stone : null;

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            grindstoneStack = tree.GetItemstack("grindstoneStack");
            grindstoneStack?.ResolveBlockOrItem(worldAccessForResolve);
            Grinding = HasGrindstone && tree.GetBool("grinding");
            legacyWheelState = Block is BlockGrindingWheel && tree.HasAttribute("grinding") && !tree.HasAttribute(partsVersionKey) ? tree.Clone() : null;
            if (Api != null) mechanicalPower?.RefreshComponents();
            scheduleLegacyMigration();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("grinding", Grinding);
            if (grindstoneStack == null) tree.RemoveAttribute("grindstoneStack");
            else tree.SetItemstack("grindstoneStack", grindstoneStack);
            if (legacyWheelState != null)
            {
                tree.MergeTree(legacyWheelState);
                tree.SetInt("posx", Pos.X);
                tree.SetInt("posy", Pos.InternalY);
                tree.SetInt("posz", Pos.Z);
                tree.SetString("blockCode", Block.Code.ToShortString());
            }
            else tree.SetBool(partsVersionKey, true);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForNewMappings, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            base.OnLoadCollectibleMappings(worldForNewMappings, oldBlockIdMapping, oldItemIdMapping, schematicSeed, resolveImports);
            if (grindstoneStack != null)
            {
                if (!grindstoneStack.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForNewMappings) || (Block as BlockGrindingWheelAssembly)?.IsGrindstone(grindstoneStack) != true)
                {
                    grindstoneStack = null;
                }
                else
                {
                    grindstoneStack.Collectible.OnLoadCollectibleMappings(worldForNewMappings, new DummySlot(grindstoneStack), oldBlockIdMapping, oldItemIdMapping, resolveImports);
                }
            }
            if (Api != null) mechanicalPower?.RefreshComponents();
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);
            grindstoneStack?.Collectible?.OnStoreCollectibleMappings(Api.World, new DummySlot(grindstoneStack), blockIdMapping, itemIdMapping);
        }

        public void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
        {
            foreach (var behavior in Behaviors)
            {
                (behavior as IRotatable)?.OnTransformed(worldAccessor, tree, degreeRotation, oldBlockIdMapping, oldItemIdMapping, flipAxis);
            }
        }

        public bool OnInteractStart(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!HasGrindstone || !canBuff(byPlayer.InventoryManager.ActiveHotbarSlot)) return false;
            if (turnSpeed < 0.1) return false;

            PlayersGrinding[byPlayer.PlayerUID]=0;
            if (grindSound == null) loadGrindSound();
            return true;
        }

        private bool canBuff(ItemSlot slot)
        {
            if (slot.Empty) return false;
            var bhb = slot.Itemstack.Collectible.GetBehavior<CollectibleBehaviorBuffable>();
            if (bhb == null) return false;
            var curBuff = bhb.GetItemBuffs(slot.Itemstack).FirstOrDefault((buff) => buff.Code == "sharpened");
            return curBuff == null || curBuff.Multiplier < 1.099;
        }

        public bool OnInteractStep(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!HasGrindstone || !canBuff(byPlayer.InventoryManager.ActiveHotbarSlot)) return false;

            return true;
        }

        void stopSounds()
        {
            turnSound?.Stop();
            grindSound?.Stop();
            if (sparkTickListener != 0)
            {
                UnregisterGameTickListener(sparkTickListener);
                sparkTickListener = 0;
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            grindSound?.Dispose();
            turnSound?.Dispose();
        }
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            grindSound?.Dispose();
            turnSound?.Dispose();
        }

        private void loadGrindSound()
        {
            if (capi == null) return;

            grindSound = capi.World.LoadSound(new SoundParams()
            {
                Location = new AssetLocation("sounds/effect/metalgrinding.ogg"),
                ShouldLoop = true,
                DisposeOnFinish = false,
                Position = Pos.ToVec3f(),
                RelativePosition = false,
                Range = 16,
                SoundType = EnumSoundType.Sound,
                Volume = 1
            });
        }

        public void OnInteractStop(float secondsUsed, IPlayer byPlayer, BlockSelection blockSel)
        {
            PlayersGrinding.Remove(byPlayer.PlayerUID);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (turnSpeed > 0 && turnSpeed < 0.1)
            {
                dsc.AppendLine(Lang.Get("Turns too slowly to grind"));
            }

            base.GetBlockInfo(forPlayer, dsc);
        }
    }
}
