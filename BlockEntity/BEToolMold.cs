using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityToolMold : BlockEntity, ILiquidMetalSink, ITemperatureSensitive, ITexPositionSource
    {
        protected ToolMoldRenderer renderer;
        protected Cuboidf[] fillQuadsByLevel = null;
        protected int requiredUnits = 100;
        protected float fillHeight = 1;

        public ItemStack MetalContent;
        public int FillLevel = 0;
        public bool FillSide;
        public bool Shattered;


        public float Temperature => MetalContent?.Collectible.GetTemperature(Api.World, MetalContent) ?? 0;
        public bool IsHardened => Temperature < 0.3f * MetalContent?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(MetalContent));
        public float ShatterChance => MetalContent == null ? 0 : GameMath.Clamp((Temperature - 0.3f * MetalContent.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(MetalContent))) / 1000f, 0, 1);
        public bool IsLiquid => Temperature > 0.8f * MetalContent?.Collectible.GetMeltingPoint(Api.World, null, new DummySlot(MetalContent));
        public bool IsFull => FillLevel >= requiredUnits;
        public bool CanReceiveAny => !Shattered && Block.Code.Path.Contains("burned");
        public bool IsHot => Temperature >= 200;

        ICoreClientAPI capi;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (MetalContent != null)
            {
                MetalContent.ResolveBlockOrItem(Api.World);
            }

            if (Block == null || Block.Code == null || Block.Attributes == null) return;

            fillHeight = Block.Attributes["fillHeight"].AsFloat(1);
            requiredUnits = Block.Attributes["requiredUnits"].AsInt(100);

            if (Block.Attributes["fillQuadsByLevel"].Exists)
            {
                fillQuadsByLevel = Block.Attributes["fillQuadsByLevel"].AsObject<Cuboidf[]>();
            }


            if (fillQuadsByLevel == null)
            {
                fillQuadsByLevel = new Cuboidf[] { new Cuboidf(2, 0, 2, 14, 0, 14), };
            }

            capi = api as ICoreClientAPI;
            if (capi != null && !Shattered)
            {
                capi.Event.RegisterRenderer(renderer = new ToolMoldRenderer(Pos, capi, fillQuadsByLevel), EnumRenderStage.Opaque, "toolmoldrenderer");
                UpdateRenderer();
            }

            if (!Shattered)
            {
                RegisterGameTickListener(OnGameTick, 50);
            }
        }


        private void OnGameTick(float dt)
        {
            if (renderer != null)
            {
                renderer.Level = (float)FillLevel * fillHeight / requiredUnits;
            }

            if (MetalContent != null && renderer != null)
            {
                renderer.stack = MetalContent;
                renderer.Temperature = Math.Min(1300, MetalContent.Collectible.GetTemperature(Api.World, MetalContent));
            }

        }



        public bool CanReceive(ItemStack metal)
        {
            return
                (MetalContent == null || (MetalContent.Collectible.Equals(MetalContent, metal, GlobalConstants.IgnoredStackAttributes) && FillLevel < requiredUnits))
                && GetMoldedStacks(metal) != null && GetMoldedStacks(metal).Length > 0
                && !Shattered
            ;
        }


        public void BeginFill(Vec3d hitPosition)
        {
            FillSide = hitPosition.X >= 0.5f;
        }


        public bool OnPlayerInteract(IPlayer byPlayer, BlockFacing onFace, Vec3d hitPosition)
        {
            if (Shattered) return false;

            bool sneaking = byPlayer.Entity.Controls.ShiftKey;

            if (!sneaking)
            {
                if (byPlayer.Entity.Controls.HandUse != EnumHandInteract.None) return false;

                bool handled = TryTakeContents(byPlayer);

                if (!handled && FillLevel == 0)
                {
                    ItemSlot activeSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
                    if (activeSlot.Itemstack == null || activeSlot.Itemstack.Collectible is BlockToolMold)
                    {
                        if (!byPlayer.InventoryManager.TryGiveItemstack(new ItemStack(Block)))
                        {
                            Api.World.SpawnItemEntity(new ItemStack(Block), Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                        }

                        Api.World.BlockAccessor.SetBlock(0, Pos);

                        if (Block.Sounds?.Place != null)
                        {
                            Api.World.PlaySoundAt(Block.Sounds.Place, Pos, -0.5, byPlayer, false);
                        }

                        handled = true;
                    }


                }

                return handled;
            }


            return false;
        }

        protected virtual bool TryTakeContents(IPlayer byPlayer)
        {
            if (Shattered) return false;
            if (Api is ICoreServerAPI) MarkDirty();

            if (MetalContent != null && FillLevel >= requiredUnits && IsHardened)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, -0.5, byPlayer, false);

                if (Api is ICoreServerAPI)
                {
                    ItemStack[] outstacks = GetStateAwareMoldedStacks();

                    if (outstacks != null)
                    {
                        foreach (ItemStack outstack in outstacks)
                        {
                            if (!byPlayer.InventoryManager.TryGiveItemstack(outstack))
                            {
                                Api.World.SpawnItemEntity(outstack, Pos.ToVec3d().Add(0.5, 0.2, 0.5));
                            }
                        }
                    }

                    MetalContent = null;
                    FillLevel = 0;
                }

                UpdateRenderer();

                return true;
            }


            return false;
        }


        public void UpdateRenderer()
        {
            if (renderer == null) return;

            if (Shattered && renderer != null)
            {
                (Api as ICoreClientAPI).Event.UnregisterRenderer(renderer, EnumRenderStage.Opaque);
                renderer = null;
                return;
            }

            renderer.Level = (float)FillLevel * fillHeight / requiredUnits;


            if (MetalContent?.Collectible != null)
            {
                renderer.TextureName = new AssetLocation("block/metal/ingot/" + MetalContent.Collectible.LastCodePart() + ".png");
            }
            else
            {
                renderer.TextureName = null;
            }
        }

        public void ReceiveLiquidMetal(ItemStack metal, ref int amount, float temperature)
        {
            if (FillLevel < requiredUnits && (MetalContent == null || metal.Collectible.Equals(MetalContent, metal, GlobalConstants.IgnoredStackAttributes)))
            {
                if (MetalContent == null)
                {
                    MetalContent = metal.Clone();
                    MetalContent.ResolveBlockOrItem(Api.World);
                    MetalContent.Collectible.SetTemperature(Api.World, MetalContent, temperature, false);
                    MetalContent.StackSize = 1;
                    (MetalContent.Attributes["temperature"] as ITreeAttribute)?.SetFloat("cooldownSpeed", 300);
                }
                else
                {
                    MetalContent.Collectible.SetTemperature(Api.World, MetalContent, temperature, false);
                }

                int amountToFill = Math.Min(amount, requiredUnits - FillLevel);
                FillLevel += amountToFill;
                amount -= amountToFill;
                UpdateRenderer();
                return;
            }

        }

        public void OnPourOver()
        {
            MarkDirty(true);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            renderer?.Dispose();
            renderer = null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            renderer?.Dispose();
            renderer = null;
        }



        /// <summary>
        /// Retrieves the molded stacks, will always return null for incomplete pours. Will return the shattered version if the mold is shattered
        /// </summary>
        /// <returns></returns>
        public ItemStack[] GetStateAwareMoldedStacks()
        {
            if (FillLevel < requiredUnits) return null;
            if (MetalContent?.Collectible == null) return null;

            if (Shattered)
            {
                var shatteredStack = MetalContent.Collectible.Attributes?["shatteredStack"].AsObject<JsonItemStack>();
                if (shatteredStack != null)
                {
                    shatteredStack.Resolve(Api.World, "shatteredStack for" + MetalContent.Collectible.Code);
                    if (shatteredStack.ResolvedItemstack != null)
                    {
                        var stacks = new ItemStack[] { shatteredStack.ResolvedItemstack };
                        stacks[0].StackSize = (int)(FillLevel / 5f * (0.7f + Api.World.Rand.NextDouble() * 0.1f));
                        return stacks;
                    }
                }
            }

            return GetMoldedStacks(MetalContent);
        }

        public ItemStack[] GetMoldedStacks(ItemStack fromMetal)
        {
            try
            {
                if (Block.Attributes["drop"].Exists)
                {
                    JsonItemStack jstack = Block.Attributes["drop"].AsObject<JsonItemStack>(null, Block.Code.Domain);
                    if (jstack == null) return null;

                    ItemStack stack = stackFromCode(jstack, fromMetal);
                    if (stack == null) return new ItemStack[0];

                    if (MetalContent != null) stack.Collectible.SetTemperature(Api.World, stack, MetalContent.Collectible.GetTemperature(Api.World, MetalContent));

                    return new ItemStack[] { stack };
                }
                else
                {
                    JsonItemStack[] jstacks = Block.Attributes["drops"].AsObject<JsonItemStack[]>(null, Block.Code.Domain);
                    List<ItemStack> stacks = new List<ItemStack>();

                    foreach (var jstack in jstacks)
                    {
                        ItemStack stack = stackFromCode(jstack, fromMetal);

                        if (MetalContent != null) stack.Collectible.SetTemperature(Api.World, stack, MetalContent.Collectible.GetTemperature(Api.World, MetalContent));

                        if (stack != null)
                        {
                            stacks.Add(stack);
                        }
                    }

                    return stacks.ToArray();
                }
            } catch (JsonReaderException)
            {
                Api.World.Logger.Error("Failed getting molded stacks from tool mold of block {0}, probably unable to parse drop or drops attribute", Block.Code);
                throw;
            }
        }


        public ItemStack stackFromCode(JsonItemStack jstack, ItemStack fromMetal)
        {
            string metaltype = fromMetal.Collectible.LastCodePart();
            string tooltype = Block.LastCodePart();
            jstack.Code.Path = jstack.Code.Path.Replace("{tooltype}", tooltype).Replace("{metal}", metaltype);
            jstack.Resolve(Api.World, "tool mold drop for " + Block.Code);
            return jstack.ResolvedItemstack;
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolve)
        {
            base.FromTreeAttributes(tree, worldForResolve);

            MetalContent = tree.GetItemstack("contents");
            FillLevel = tree.GetInt("fillLevel");
            Shattered = tree.GetBool("shattered");
            if (Api?.World != null && MetalContent != null) MetalContent.ResolveBlockOrItem(Api.World);

            UpdateRenderer();

            if (Api?.Side == EnumAppSide.Client)
            {
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contents", MetalContent);
            tree.SetInt("fillLevel", FillLevel);
            tree.SetBool("shattered", Shattered);
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            string contents;

            if (Shattered)
            {
                dsc.AppendLine(Lang.Get("Has shattered."));
                return;
            }

            if (this.MetalContent != null)
            {
                string state = IsLiquid ? Lang.Get("liquid") : (IsHardened ? Lang.Get("hardened") : Lang.Get("soft"));

                string temp = Temperature < 21 ? Lang.Get("Cold") : Lang.Get("{0}°C", (int)Temperature);
                string matkey = "material-" + MetalContent.Collectible.Variant["metal"];
                string mat = Lang.HasTranslation(matkey) ? Lang.Get(matkey) : Lang.Get(MetalContent.GetName());
                contents = Lang.Get("{0}/{4} units of {1} {2} ({3})", FillLevel, state, mat, temp, requiredUnits) + "\n";
            }
            else
            {
                contents = Lang.Get("0/{0} units of metal", requiredUnits) + "\n";
            }

            dsc.AppendLine(contents.Length == 0 ? Lang.Get("Empty") : contents);
        }


        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            MetalContent?.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(MetalContent), blockIdMapping, itemIdMapping);
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (MetalContent?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == null)
            {
                MetalContent = null;
            }
            // update the time for the temperature to the current ingame time if imported from another game
            if ((MetalContent?.Attributes["temperature"] as ITreeAttribute)?.HasAttribute("temperatureLastUpdate") == true)
            {
                ((ITreeAttribute)MetalContent.Attributes["temperature"]).SetDouble("temperatureLastUpdate", worldForResolve.Calendar.TotalHours);
            }
        }

        public void CoolNow(float amountRel)
        {
            float breakchance = Math.Max(0, amountRel - 0.6f) * Math.Max(Temperature - 250f, 0) / 5000f;

            if (Api.World.Rand.NextDouble() < breakchance)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ceramicbreak"), Pos, -0.4);
                this.Block.SpawnBlockBrokenParticles(Pos);
                this.Block.SpawnBlockBrokenParticles(Pos);
                MetalContent.Collectible.SetTemperature(Api.World, MetalContent, 20, false);
                Shattered = true;
                MarkDirty(true);
            } else
            {
                if (MetalContent != null)
                {
                    float temp = Temperature;
                    if (temp > 120)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, -0.5, null, false, 16);
                    }

                    MetalContent.Collectible.SetTemperature(Api.World, MetalContent, Math.Max(20, temp - amountRel * 20), false);
                    MarkDirty(true);
                }
            }
        }


        #region Shattered mesh gen
        ITexPositionSource tmpTextureSource;
        AssetLocation metalTexLoc;
        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "metal")
                {
                    return capi.BlockTextureAtlas[metalTexLoc];
                }

                return tmpTextureSource[textureCode];
            }
        }

        MeshData shatteredMesh;
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (Shattered)
            {
                if (shatteredMesh == null)
                {
                    metalTexLoc = MetalContent == null ? new AssetLocation("block/transparent") : new AssetLocation("block/metal/ingot/" + MetalContent.Collectible.LastCodePart());
                    tmpTextureSource = capi.Tesselator.GetTextureSource(Block);
                    ITesselatorAPI tess = capi.Tesselator;
                    var cshape = Block.Attributes["shatteredShape"].AsObject<CompositeShape>();
                    cshape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
                    Shape shape = Shape.TryGet(Api, cshape.Base);
                    tess.TesselateShape("shatteredmold", shape, out shatteredMesh, this);
                }

                mesher.AddMeshData(shatteredMesh);
                return true;
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        #endregion
    }
}
