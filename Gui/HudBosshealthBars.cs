using OpenTK.Mathematics;
using System.Collections.Generic;
using System.Diagnostics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#nullable disable

namespace Vintagestory.Client.NoObf
{
    public class ModSystemBossHealthBars : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        ICoreClientAPI capi;
        EntityPartitioning partUtil;
        List<HudBosshealthBars> trackedBosses = new List<HudBosshealthBars>();

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            partUtil = api.ModLoader.GetModSystem<EntityPartitioning>();
            api.Event.RegisterGameTickListener(onTick, 200, 12);
        }

        private void onTick(float dt)
        {
            List<EntityAgent> foundBosses = new List<EntityAgent>();

            // Look for bosses in player's dimension
            var plrpos = capi.World.Player.Entity.Pos.XYZ;
            partUtil.WalkEntities(plrpos, 60, (e) => {
                EntityBehaviorBoss bh;
                if (e.Alive && e.IsInteractable && (bh = e.GetBehavior<EntityBehaviorBoss>()) != null)
                {
                    var dist = getDistance(capi.World.Player.Entity, e);
                    if (bh.ShowHealthBar && dist <= bh.BossHpbarRange) foundBosses.Add(e as EntityAgent);
                }
                return true;
            }, EnumEntitySearchType.Creatures);

            // Look for bosses in dimension 2 if player is in dimension 0 and vice versa
            int dimensionDiff = 0;
            if (capi.World.Player.Entity.Pos.Dimension == Dimensions.NormalWorld)
            {
                dimensionDiff = 2;
            }
            else
            {
                dimensionDiff = -capi.World.Player.Entity.Pos.Dimension;
            }
            plrpos.Y += dimensionDiff * BlockPos.DimensionBoundary;
            partUtil.WalkEntities(plrpos, 60, (e) => {
                EntityBehaviorBoss bh;
                if (e.Alive && e.IsInteractable && (bh = e.GetBehavior<EntityBehaviorBoss>()) != null)
                {
                    var dist = getDistance(capi.World.Player.Entity, e);
                    if (bh.ShowHealthBar && dist <= bh.BossHpbarRange) foundBosses.Add(e as EntityAgent);
                }
                return true;
            }, EnumEntitySearchType.Creatures);

            int reorganizePositionsAt = -1;

            for (int i = 0; i < trackedBosses.Count; i++)
            {
                var hud = trackedBosses[i];
                if (foundBosses.Contains(hud.TargetEntity))
                {
                    foundBosses.Remove(hud.TargetEntity);
                } else
                {
                    trackedBosses[i].TryClose();
                    trackedBosses[i].Dispose();
                    trackedBosses.RemoveAt(i);
                    reorganizePositionsAt = i;
                    i--;
                }
            }

            foreach (var eagent in foundBosses)
            {
                trackedBosses.Add(new HudBosshealthBars(capi, eagent, trackedBosses.Count));
            }

            if (reorganizePositionsAt >= 0)
            {
                for (int i = reorganizePositionsAt; i < trackedBosses.Count; i++)
                {
                    trackedBosses[i].barIndex = i;
                    trackedBosses[i].ComposeGuis();
                }
            }

            foreach (var hudbar in trackedBosses)
            {
                int previousDimension = hudbar.Dimension;
                int currentDimesnsion = hudbar.TargetEntity.ServerPos.Dimension;
                if (currentDimesnsion != previousDimension)
                {
                    hudbar.ComposeGuis();
                    hudbar.Dimension = currentDimesnsion;
                }
            }
        }

        /// <summary>
        /// Returns distance as if both entities in same dimension
        /// </summary>
        private double getDistance(Entity player, Entity entity)
        {
            Vector3d entityPos = new(entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            Vector3d playerPos = new(player.Pos.X, player.Pos.Y, player.Pos.Z);
            return Vector3d.Distance(entityPos, playerPos);
        }
    }


    public class HudBosshealthBars : HudElement
    {
        float lastHealth;
        float lastMaxHealth;
        public int barIndex;
        public EntityAgent TargetEntity;
        public int Dimension;

        GuiElementStatbar healthbar;
        long listenerId;

        public override double InputOrder { get { return 1; } }

        public HudBosshealthBars(ICoreClientAPI capi, EntityAgent bossEntity, int barIndex) : base(capi)
        {
            this.TargetEntity = bossEntity;
            listenerId = capi.Event.RegisterGameTickListener(this.OnGameTick, 20);

            this.barIndex = barIndex;

            ComposeGuis();

            Dimension = bossEntity.ServerPos.Dimension;
        }
        public override string ToggleKeyCombinationCode { get { return null; } }

        private void OnGameTick(float dt)
        {
            UpdateHealth();
        }
      

        void UpdateHealth()
        {
            ITreeAttribute healthTree = TargetEntity.WatchedAttributes.GetTreeAttribute("health");
            if (healthTree == null) return;

            float? health = healthTree.TryGetFloat("currenthealth");
            float? maxHealth = healthTree.TryGetFloat("maxhealth");

            if (health == null || maxHealth == null) return;
            if (lastHealth == health && lastMaxHealth == maxHealth) return;
            if (healthbar == null) return;

            healthbar.SetLineInterval(1);
            healthbar.SetValues((float)health, 0, (float)maxHealth);

            lastHealth = (float)health;
            lastMaxHealth = (float)maxHealth;
        }

        public void ComposeGuis()
        {
            float width = 850;
            ElementBounds dialogBounds = new ElementBounds()
            {
                Alignment = EnumDialogArea.CenterFixed,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = width,
                fixedHeight = 50,
                fixedY = 10 + barIndex * 25
            }.WithFixedAlignmentOffset(0, 5);

            ElementBounds healthBarBounds = ElementBounds.Fixed(0, 18, width, 14);

            string name = TargetEntity.GetBehavior<EntityBehaviorBoss>()?.BossName ?? "";

            ITreeAttribute healthTree = TargetEntity.WatchedAttributes.GetTreeAttribute("health");
            string key = "bosshealthbar-" + TargetEntity.EntityId;
            Composers["bosshealthbar"] =
                capi.Gui
                .CreateCompo(key, dialogBounds.FlatCopy().FixedGrow(0, 20))
                .BeginChildElements(dialogBounds)
                    .AddIf(healthTree != null)
                        .AddStaticText(name, CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 0, 200, 20))
                        .AddStatbar(healthBarBounds, GuiStyle.HealthBarColor, "healthstatbar")
                    .EndIf()
                .EndChildElements()
                .Compose()
            ;

            healthbar = Composers["bosshealthbar"].GetStatbar("healthstatbar");
            TryOpen();
        }
        

        // Can't be closed
        public override bool TryClose()
        {
            return base.TryClose();
        }

        public override bool ShouldReceiveKeyboardEvents()
        {
            return false;
        }
        

        public override void OnRenderGUI(float deltaTime)
        {   
            base.OnRenderGUI(deltaTime);
        }
        
        
        // Can't be focused
        public override bool Focusable => false;

        // Can't be focused
        protected override void OnFocusChanged(bool on)
        {

        }

        public override void OnMouseDown(MouseEvent args)
        {
            // Can't be clicked
        }

        public override void Dispose()
        {
            base.Dispose();

            capi.Event.UnregisterGameTickListener(listenerId);
        }

    }
}
