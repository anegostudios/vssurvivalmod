using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityForge : BlockEntity, IHeatSource
    {
        ForgeContentsRenderer renderer;
        ItemStack contents;
        float fuelLevel;
        bool burning;

        double lastTickTotalHours;
        ILoadedSound ambientSound;


        public ItemStack Contents => contents;
        public float FuelLevel => fuelLevel;

        static SimpleParticleProperties smokeParticles;

        static BlockEntityForge()
        {
            smokeParticles = new SimpleParticleProperties(
                   1, 1,
                   ColorUtil.ToRgba(150, 80, 80, 80),
                   new Vec3d(),
                   new Vec3d(0.75, 0, 0.75),
                   new Vec3f(-1 / 32f, 0.1f, -1 / 32f),
                   new Vec3f(1 / 32f, 0.1f, 1 / 32f),
                   2f,
                   -0.025f / 4,
                   0.2f,
                   0.4f,
                   EnumParticleModel.Quad
               );

            smokeParticles.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.25f);
            smokeParticles.SelfPropelled = true;
            smokeParticles.AddPos.Set(8 / 16.0, 0, 8 / 16.0);
        }



        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (contents != null) contents.ResolveBlockOrItem(api.World);

            if (api is ICoreClientAPI)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                capi.Event.RegisterRenderer(renderer = new ForgeContentsRenderer(Pos, capi), EnumRenderStage.Opaque, "forge");  
                renderer.SetContents(contents, fuelLevel, burning, true);

                RegisterGameTickListener(OnClientTick, 50);
            }

            
            wsys = api.ModLoader.GetModSystem<WeatherSystemBase>();

            RegisterGameTickListener(OnCommonTick, 200);
        }

        public void ToggleAmbientSounds(bool on)
        {
            if (Api.Side != EnumAppSide.Client) return;

            if (on)
            {
                if (ambientSound == null || !ambientSound.IsPlaying)
                {
                    ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/effect/embers.ogg"),
                        ShouldLoop = true,
                        Position = Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = 1
                    });

                    ambientSound.Start();
                }
            }
            else
            {
                ambientSound.Stop();
                ambientSound.Dispose();
                ambientSound = null;
            }

        }


        bool clientSidePrevBurning;
        private void OnClientTick(float dt)
        {
            if (Api?.Side == EnumAppSide.Client && clientSidePrevBurning != burning)
            {
                ToggleAmbientSounds(IsBurning);
                clientSidePrevBurning = IsBurning;
            }

            if (burning && Api.World.Rand.NextDouble() < 0.13)
            {
                smokeParticles.MinPos.Set(Pos.X + 4 / 16f, Pos.Y + 14 / 16f, Pos.Z + 4 / 16f);
                int g = 50 + Api.World.Rand.Next(50);
                smokeParticles.Color = ColorUtil.ToRgba(150, g, g, g);
                Api.World.SpawnParticles(smokeParticles);
            }
            if (renderer != null)
            {
                renderer.SetContents(contents, fuelLevel, burning, false);
            }
        }


        WeatherSystemBase wsys;
        Vec3d tmpPos = new Vec3d();
        private void OnCommonTick(float dt)
        {
            if (burning)
            {
                double hoursPassed = Api.World.Calendar.TotalHours - lastTickTotalHours;

                if (fuelLevel > 0) fuelLevel = Math.Max(0, fuelLevel - (float)(2.5 / 24 * hoursPassed));

                if (fuelLevel <= 0)
                {
                    burning = false;
                }

                if (contents != null)
                {
                    float temp = contents.Collectible.GetTemperature(Api.World, contents);
                    if (temp < 1100)
                    {
                        float tempGain = (float)(hoursPassed * 1500);

                        contents.Collectible.SetTemperature(Api.World, contents, Math.Min(1100, temp + tempGain));
                    }
                }
            }


            tmpPos.Set(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            double rainLevel = 0;
            bool rainCheck =
                Api.Side == EnumAppSide.Server
                && Api.World.Rand.NextDouble() < 0.15
                && Api.World.BlockAccessor.GetRainMapHeightAt(Pos.X, Pos.Z) <= Pos.Y
                && (rainLevel = wsys.GetPrecipitation(tmpPos)) > 0.1
            ;

            if (rainCheck && Api.World.Rand.NextDouble() < rainLevel * 5)
            {
                bool playsound = false;
                if (burning)
                {
                    playsound = true;
                    fuelLevel -= (float)rainLevel / 250f;
                    if (Api.World.Rand.NextDouble() < rainLevel / 30f || fuelLevel <= 0)
                    {
                        burning = false;
                    }
                    MarkDirty(true);
                }


                float temp = contents == null ? 0 : contents.Collectible.GetTemperature(Api.World, contents);
                if (temp > 20)
                {
                    playsound = temp > 100;
                    contents.Collectible.SetTemperature(Api.World, contents, Math.Min(1100, temp - 8), false);
                    MarkDirty(true);
                }
                
                if (playsound)
                {
                    Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos.X + 0.5, Pos.Y + 0.75, Pos.Z + 0.5, null, false, 16);
                }
            }

            lastTickTotalHours = Api.World.Calendar.TotalHours;
        }

        public bool IsBurning
        {
            get { return burning; }
        }

        public bool CanIgnite
        {
            get { return !burning && fuelLevel > 0; }
        }

        internal void TryIgnite()
        {
            if (burning) return;

            burning = true;
            renderer?.SetContents(contents, fuelLevel, burning, false);
            lastTickTotalHours = Api.World.Calendar.TotalHours;
            MarkDirty();
        }

        internal bool OnPlayerInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!byPlayer.Entity.Controls.Sneak)
            {
                if (contents == null) return false;
                ItemStack split = contents.Clone();
                split.StackSize = 1;
                contents.StackSize--;
                
                if (contents.StackSize == 0) contents = null;

                if (!byPlayer.InventoryManager.TryGiveItemstack(split))
                {
                    world.SpawnItemEntity(split, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                renderer?.SetContents(contents, fuelLevel, burning, true);
                MarkDirty();
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);

                return true;

            } else
            {   
                if (slot.Itemstack == null) return false;

                // Add fuel
                CombustibleProperties combprops = slot.Itemstack.Collectible.CombustibleProps;
                if (combprops != null && combprops.BurnTemperature > 1000)
                {
                    if (fuelLevel >= 5 / 16f) return false;
                    fuelLevel += 1 / 16f;

                    if (slot.Itemstack.Collectible is ItemCoal || slot.Itemstack.Collectible is ItemOre)
                    {
                        Api.World.PlaySoundAt(new AssetLocation("sounds/block/charcoal"), byPlayer, byPlayer, true, 16);
                    }
                    (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

                    renderer?.SetContents(contents, fuelLevel, burning, false);
                    MarkDirty();

                    if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                    }


                    return true;
                }


                string firstCodePart = slot.Itemstack.Collectible.FirstCodePart();
                bool forgableGeneric = slot.Itemstack.Collectible.Attributes?.IsTrue("forgable") == true;

                // Add heatable item
                if (contents == null && (firstCodePart == "ingot" || firstCodePart == "metalplate" || firstCodePart == "workitem" || forgableGeneric))
                {
                    contents = slot.Itemstack.Clone();
                    contents.StackSize = 1;

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    renderer?.SetContents(contents, fuelLevel, burning, true);
                    MarkDirty();
                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);

                    return true;
                }

                // Merge heatable item
                if (!forgableGeneric && contents != null && contents.Equals(Api.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes) && contents.StackSize < 4 && contents.StackSize < contents.Collectible.MaxStackSize)
                {
                    float myTemp = contents.Collectible.GetTemperature(Api.World, contents);
                    float histemp = slot.Itemstack.Collectible.GetTemperature(Api.World, slot.Itemstack);

                    contents.Collectible.SetTemperature(world, contents, (myTemp * contents.StackSize + histemp * 1) / (contents.StackSize + 1));
                    contents.StackSize++;

                    slot.TakeOut(1);
                    slot.MarkDirty();

                    renderer?.SetContents(contents, fuelLevel, burning, true);
                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos.X, Pos.Y, Pos.Z, byPlayer, false);

                    MarkDirty();
                    return true;
                }

                return false;
            }
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            if (renderer != null)
            {
                renderer.Dispose();
                renderer = null;
            }

            ambientSound?.Dispose();
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);

            if (contents != null)
            {
                Api.World.SpawnItemEntity(contents, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            ambientSound?.Dispose();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            contents = tree.GetItemstack("contents");
            fuelLevel = tree.GetFloat("fuelLevel");
            burning = tree.GetInt("burning") > 0;
            lastTickTotalHours = tree.GetDouble("lastTickTotalHours");

            if (Api != null)
            {
                contents?.ResolveBlockOrItem(Api.World);
            }
            if (renderer != null)
            {
                renderer.SetContents(contents, fuelLevel, burning, true);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contents", contents);
            tree.SetFloat("fuelLevel", fuelLevel);
            tree.SetInt("burning", burning ? 1 : 0);
            tree.SetDouble("lastTickTotalHours", lastTickTotalHours);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (contents != null)
            {
                int temp = (int)contents.Collectible.GetTemperature(Api.World, contents);
                if (temp <= 25)
                {
                    dsc.AppendLine(string.Format("Contents: {0}x {1}\nTemperature: {2}", contents.StackSize, contents.GetName(), Lang.Get("Cold")));
                } else
                {
                    dsc.AppendLine(string.Format("Contents: {0}x {1}\nTemperature: {2}°C", contents.StackSize, contents.GetName(), temp));
                }
                
            }
        }


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed)
        {
            if (contents?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                contents = null;
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping)
        {
            if (contents != null)
            {
                if (contents.Class == EnumItemClass.Item)
                {
                    blockIdMapping[contents.Id] = contents.Item.Code;
                }
                else
                {
                    itemIdMapping[contents.Id] = contents.Block.Code;
                }
            }
            
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            renderer?.Dispose();
        }

        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos)
        {
            return IsBurning ? 7 : 0;
        }
    }
}
