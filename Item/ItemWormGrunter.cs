using ProtoBuf;
using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.CommandAbbr;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

#nullable disable

namespace Vintagestory.GameContent
{
    [ProtoContract]
    public struct CreatureHarvest
    {
        [ProtoMember(1)]
        public double TotalDays;
        [ProtoMember(2)]
        public int Quantity;
    }

    public class ModSystemWormGrunting : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        protected NormalizedSimplexNoise noiseGen;
        protected ICoreServerAPI sapi;
        protected Dictionary<BlockPos, CreatureHarvest> harvestedLocations = new Dictionary<BlockPos, CreatureHarvest>();

        public override double ExecuteOrder() => 1;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;
            api.Event.ServerRunPhase(EnumServerRunPhase.WorldReady, onWorldReady);
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;
            api.Event.RegisterGameTickListener(restoreEarthWorms, 10000, sapi.World.Rand.Next(1000));

            sapi.ChatCommands.GetOrCreate("wgen").BeginSub("wormmap").WithDesc("generates a bunch of blocks at y=150 to visualize worm density map").HandleWith(onCmdWormMap);
        }

        private TextCommandResult onCmdWormMap(TextCommandCallingArgs args)
        {
            for (int dx = -15; dx <= 15; dx++)
            {
                for (int dz = -15; dz <= 15; dz++)
                {
                    var pos = args.Caller.Pos.AsBlockPos.Add(dx, 0, dz);
                    pos.Y = 150;

                    var f = GetInitialDensity(pos);

                    // Creativeblock 64 to 79 is black to white
                    int num = (int)GameMath.Clamp(Math.Round(64 + 15 * f), 64, 79);
                    var block = sapi.World.GetBlock("creativeblock-" + num);
                    sapi.World.BlockAccessor.SetBlock(block.Id, pos);
                }
            }

            return TextCommandResult.Success("generated");
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("harvestedEarthWormLocations", harvestedLocations);
        }

        private void Event_SaveGameLoaded()
        {
            try
            {
                harvestedLocations = sapi.WorldManager.SaveGame.GetData<Dictionary<BlockPos, CreatureHarvest>>("harvestedEarthWormLocations");
            }
            catch
            {
                // Don't care if this is corrupted data, its unessential
            }

            if (harvestedLocations == null)
            {
                harvestedLocations = new Dictionary<BlockPos, CreatureHarvest>();
            }
        }

        private void restoreEarthWorms(float dt)
        {
            var positions = new List<BlockPos>(harvestedLocations.Keys);
            var totaldays = sapi.World.Calendar.TotalDays;
            foreach (var pos in positions)
            {
                if (totaldays - harvestedLocations[pos].TotalDays > 7)
                {
                    harvestedLocations.Remove(pos);
                }
            }
        }

        private void onWorldReady()
        {
            noiseGen = NormalizedSimplexNoise.FromDefaultOctaves(2, 1, 0.9, sapi.World.Seed);
        }

        public float GetInitialDensity(BlockPos pos)
        {
            var f = Math.Max(0, (float)(noiseGen.Noise(pos.X, pos.Z) - 0.5f) * 3);
            var climate = sapi.World.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);
            return  f * Math.Max(0, (climate.Fertility - 0.2f) * 1.3f);
        }

        public float GetEarthWormAmount(BlockPos pos)
        {
            var availableWorms = GetInitialDensity(pos) * 20;

            if (harvestedLocations.TryGetValue(pos / 3, out var harvest)) // Super simple "area" check by dividing the position by 3
            {
                availableWorms -= harvest.Quantity;
            }

            return availableWorms;
        }

        public void AddHarvest(BlockPos pos, int quantity)
        {
            CreatureHarvest harvest;
            harvestedLocations.TryGetValue(pos / 3, out harvest);
            harvestedLocations[pos/3] = new CreatureHarvest() { TotalDays = sapi.World.Calendar.TotalDays, Quantity = harvest.Quantity + quantity };
        }
    }

    public class ItemWormGrunter : Item
    {
        ILoadedSound gruntingSound;

        public override void OnUnloaded(ICoreAPI api)
        {
            base.OnUnloaded(api);
            gruntingSound?.Dispose();
        }

        protected void startSound(EntityAgent byEntity)
        {
            var capi = api as ICoreClientAPI;
            if (capi == null) return;

            if (gruntingSound == null)
            {
                gruntingSound = capi.World.LoadSound(new SoundParams()
                {
                    Location = new AssetLocation("sounds/effect/wormgrunting"),
                    Range = 12,
                    ShouldLoop = true,
                    DisposeOnFinish = false,
                });
            }

            gruntingSound.SetPosition(byEntity.Pos.XYZFloat);
            gruntingSound.Start();
        }

        protected void stopSound()
        {
            gruntingSound?.Stop();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel == null || blockSel.Face != BlockFacing.UP || !byEntity.Controls.ShiftKey) return;
            if (byEntity.Controls.CtrlKey) return;

            var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block.Fertility <= 0) return;

            if (byEntity.LeftHandItemSlot.Empty || byEntity.LeftHandItemSlot.Itemstack.Collectible.Code.Path != "stick")
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "missingstick", Lang.Get("Requires a stick in offhand"));
                return;
            }

            handling = EnumHandHandling.PreventDefault;
            startSound(byEntity);

            if (api.Side == EnumAppSide.Server)
            {
                var mswg = api.ModLoader.GetModSystem<ModSystemWormGrunting>();
                float maxQuantity = mswg.GetEarthWormAmount(blockSel.Position);

                int cnt = GameMath.RoundRandom(api.World.Rand, (float)api.World.Rand.NextDouble() * maxQuantity);

                slot.Itemstack.TempAttributes.SetInt("spawnAmount", cnt);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null || blockSel.Face != BlockFacing.UP) return false;

            var th = api.Side == EnumAppSide.Server ? 4f : 5f;
            if (secondsUsed > th || blockSel == null)
            {
                return false;
            }

            if (api.Side == EnumAppSide.Server)
            {
                var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
                int cnt = slot.Itemstack.TempAttributes.GetInt("spawnAmount");
                if (block.Fertility > 0 && secondsUsed >= 1 && cnt > 0 && api.World.Rand.NextDouble() < 0.06)
                {
                    int nowamount = 1 + (cnt > 1 && api.World.Rand.NextDouble() < 0.1 ? 1 : 0);
                    spawnWorm(slot, byEntity, blockSel, nowamount);

                    slot.Itemstack.TempAttributes.SetInt("spawnAmount", cnt - nowamount);
                }
            }

            return true;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
        {
            return false;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            stopSound();

            if ((byEntity as EntityPlayer).Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot);
                slot.MarkDirty();
            }

            if (blockSel == null || blockSel.Face != BlockFacing.UP) return;

            var block = api.World.BlockAccessor.GetBlock(blockSel.Position);
            if (block.Fertility > 0 && secondsUsed > 4f && api.Side == EnumAppSide.Server)
            {
                int cnt = slot.Itemstack.TempAttributes.GetInt("spawnAmount");
                if (cnt > 0)
                {
                    spawnWorm(slot, byEntity, blockSel, cnt);
                }
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        private void spawnWorm(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, int amount)
        {
            var mswg = api.ModLoader.GetModSystem<ModSystemWormGrunting>();
            if (amount > 0)
            {
                mswg.AddHarvest(blockSel.Position, amount);
            }


            EntityProperties type = byEntity.World.GetEntityType(new AssetLocation("earthworm"));
            for (int i = 0; i < amount; i++)
            {
                Entity eWorm = byEntity.World.ClassRegistry.CreateEntity(type);

                eWorm.Pos.X = blockSel.Position.X + (float)api.World.Rand.NextDouble();
                eWorm.Pos.Y = blockSel.Position.Y + 1;
                eWorm.Pos.Z = blockSel.Position.Z + (float)api.World.Rand.NextDouble();
                eWorm.Pos.Yaw = (float)api.World.Rand.NextDouble() * 2 * GameMath.PI;

                byEntity.World.SpawnEntity(eWorm);
            }
        }
    }
}
