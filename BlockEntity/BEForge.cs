using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockEntityForge : BlockEntity, IHeatSource, ITemperatureSensitive
    {
        ForgeContentsRenderer renderer;
        ItemStack contents;
        float fuelLevel;
        bool burning;

        double lastTickTotalHours;
        ILoadedSound ambientSound;


        public bool Lit => burning;
        public ItemStack Contents => contents;
        public float FuelLevel => fuelLevel;

        public bool IsHot => (contents?.Collectible.GetTemperature(Api.World, contents) ?? 0) > 20;


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

                // Regen mesh on transform change
                api.Event.RegisterEventBusListener((string _, ref EnumHandling _, IAttribute _) => renderer.RegenMesh(), filterByEventName: "genjsontransform");
            }

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
                BlockEntityCoalPile.SpawnBurningCoalParticles(Api, Pos.ToVec3d().Add(4 / 16f, 14 / 16f, 4 / 16f), 8/16f, 8/16f);
            }

            if (renderer != null)
            {
                renderer.SetContents(contents, fuelLevel, burning, false);
            }
        }


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

            if (!byPlayer.Entity.Controls.ShiftKey)
            {
                if (contents == null) return false;
                ItemStack split = contents.Clone();
                split.StackSize = 1;
                contents.StackSize--;

                if (contents.StackSize == 0) contents = null;

                if (!byPlayer.InventoryManager.TryGiveItemstack(split))
                {
                    world.SpawnItemEntity(split, Pos);
                }
                Api.World.Logger.Audit("{0} Took 1x{1} from Forge at {2}.",
                    byPlayer.PlayerName,
                    split.Collectible.Code,
                    blockSel.Position
                );

                renderer?.SetContents(contents, fuelLevel, burning, true);
                MarkDirty();
                Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, 0.4375, byPlayer, false);

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
                    Api.World.Logger.Audit("{0} Put 1x{1} into Forge at {2}.",
                        byPlayer.PlayerName,
                        contents.Collectible.Code,
                        blockSel.Position
                    );

                    renderer?.SetContents(contents, fuelLevel, burning, true);
                    MarkDirty();
                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, 0.4375, byPlayer, false);

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
                    Api.World.Logger.Audit("{0} Put 1x{1} into Forge at {2}.",
                        byPlayer.PlayerName,
                        contents.Collectible.Code,
                        blockSel.Position
                    );

                    renderer?.SetContents(contents, fuelLevel, burning, true);
                    Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), Pos, 0.4375, byPlayer, false);

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
                Api.World.SpawnItemEntity(contents, Pos);
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
                    dsc.AppendLine(Lang.Get("forge-contentsandtemp-cold", contents.StackSize, contents.GetName()));
                } else
                {
                    dsc.AppendLine(Lang.Get("forge-contentsandtemp", contents.StackSize, contents.GetName(), temp));
                }

            }
        }


        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, int schematicSeed, bool resolveImports)
        {
            if (contents?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false)
            {
                contents = null;
            }
            contents?.Collectible.OnLoadCollectibleMappings(worldForResolve, new DummySlot(contents), oldBlockIdMapping, oldItemIdMapping, resolveImports);
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
                contents.Collectible.OnStoreCollectibleMappings(Api.World, new DummySlot(contents), blockIdMapping, itemIdMapping);
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

        public void CoolNow(float amountRel)
        {
            bool playsound = false;
            if (burning)
            {
                playsound = true;
                fuelLevel -= (float)amountRel / 250f;
                if (Api.World.Rand.NextDouble() < amountRel / 30f || fuelLevel <= 0)
                {
                    burning = false;
                }
                MarkDirty(true);
            }

            float temp = contents == null ? 0 : contents.Collectible.GetTemperature(Api.World, contents);
            if (temp > 20)
            {
                playsound = temp > 100;
                contents.Collectible.SetTemperature(Api.World, contents, Math.Min(1100, temp - amountRel * 20), false);
                MarkDirty(true);
            }

            if (playsound)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), Pos, 0.25, null, false, 16);
            }
        }
    }
}
