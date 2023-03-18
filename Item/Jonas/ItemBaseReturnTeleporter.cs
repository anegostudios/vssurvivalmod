using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class ItemBaseReturnTeleporter : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;

            if (api is ICoreServerAPI sapi)
            {
                var plr = (byEntity as EntityPlayer).Player as IServerPlayer;
                var pos = plr.GetSpawnPosition(false);
                plr.Entity.TeleportToDouble(pos.X, pos.Y, pos.Z);
            }

        }
    }

    public class ModSystemCorpseReturnTeleporter : ModSystem
    {
        public Dictionary<string, Vec3d> lastDeathLocations = new Dictionary<string, Vec3d>();
        ICoreServerAPI sapi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Event.PlayerDeath += Event_PlayerDeath;
            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;

            sapi = api;
        }

        private void Event_GameWorldSave()
        {
            sapi.WorldManager.SaveGame.StoreData("lastDeathLocations", lastDeathLocations);
        }

        private void Event_SaveGameLoaded()
        {
            lastDeathLocations = sapi.WorldManager.SaveGame.GetData<Dictionary<string, Vec3d>>("lastDeathLocations", new Dictionary<string, Vec3d>());
        }

        private void Event_PlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
        {
            lastDeathLocations[byPlayer.PlayerUID] = byPlayer.Entity.Pos.XYZ;
        }

        
    }

    public class ItemCorpseReturnTeleporter : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;

            if (api is ICoreServerAPI sapi)
            {
                var plr = (byEntity as EntityPlayer).Player as IServerPlayer;
                if (sapi.ModLoader.GetModSystem<ModSystemCorpseReturnTeleporter>().lastDeathLocations.TryGetValue(plr.PlayerUID, out var location))
                {
                    plr.Entity.TeleportToDouble(location.X, location.Y, location.Z);
                }
            }
        }
    }


    public class ModSystemNightVision : ModSystem, IRenderer
    {
        public double RenderOrder => 0;
        public int RenderRange => 1;

        IInventory gearInv;
        ICoreClientAPI capi;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Before, "nightvision");
            api.Event.LevelFinalize += Event_LevelFinalize;
        }

        private void Event_LevelFinalize()
        {
            gearInv = capi.World.Player.Entity.GearInventory;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (gearInv == null) return;

            var headSlot = gearInv[(int)EnumCharacterDressType.Head];
            if (!headSlot.Empty && headSlot.Itemstack.Collectible is ItemNightvisiondevice)
            {
                capi.Render.ShaderUniforms.NightVisonStrength = 1;
            } else
            {
                capi.Render.ShaderUniforms.NightVisonStrength = 0;
            }
        }

    }

    public class ItemNightvisiondevice : Item
    {

    }

    public class ItemMechHelper : Item
    {

    }

    public class EntityMechHelper : EntityAgent
    {
        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            base.OnInteract(byEntity, slot, hitPosition, mode);
        }

    }
}
