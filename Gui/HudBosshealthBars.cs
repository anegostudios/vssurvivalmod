using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

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

            var plrpos = capi.World.Player.Entity.Pos.XYZ;
            partUtil.WalkEntities(plrpos, 60, (e) => {
                EntityBehaviorBoss bh;
                if (e.Alive && e.IsInteractable && (bh = e.GetBehavior<EntityBehaviorBoss>()) != null)
                {
                    var dist = e.Pos.DistanceTo(plrpos);
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
        }
    }


    public class HudBosshealthBars : HudElement
    {
        float lastHealth;
        float lastMaxHealth;
        public int barIndex;
        public EntityAgent TargetEntity;

        GuiElementStatbar healthbar;
        long listenerId;

        public override double InputOrder { get { return 1; } }

        public HudBosshealthBars(ICoreClientAPI capi, EntityAgent bossEntity, int barIndex) : base(capi)
        {
            this.TargetEntity = bossEntity;
            listenerId = capi.Event.RegisterGameTickListener(this.OnGameTick, 20);

            this.barIndex = barIndex;

            ComposeGuis();
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

            ITreeAttribute healthTree = TargetEntity.WatchedAttributes.GetTreeAttribute("health");
            string key = "bosshealthbar-" + TargetEntity.EntityId;
            Composers["bosshealthbar"] =
                capi.Gui
                .CreateCompo(key, dialogBounds.FlatCopy().FixedGrow(0, 20))
                .BeginChildElements(dialogBounds)
                    .AddIf(healthTree != null)
                        .AddStaticText(Lang.Get(TargetEntity.Code.Domain + ":item-creature-" + TargetEntity.Code.Path), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, 0, 200, 20))
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
