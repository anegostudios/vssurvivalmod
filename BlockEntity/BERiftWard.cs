using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Vintagestory.GameContent
{
    public class BlockEntityRiftWard : BlockEntity
    {
        protected ICoreServerAPI sapi;
        protected ILoadedSound ambientSound;
        protected double fuelDays = 0;
        protected double lastUpdateTotalDays = 0;

        protected bool HasFuel => fuelDays > 0;

        int riftsBlocked = 0;

        public bool On { get; set; }

        BlockEntityAnimationUtil animUtil
        {
            get { return GetBehavior<BEBehaviorAnimatable>().animUtil; }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            sapi = api as ICoreServerAPI;
            if (sapi != null)
            {
                api.ModLoader.GetModSystem<ModSystemRifts>().OnRiftSpawned += BlockEntityRiftWard_OnRiftSpawned;
                RegisterGameTickListener(OnServerTick, 5000);
            }

            lastUpdateTotalDays = api.World.Calendar.TotalDays;

            if (sapi == null) animUtil?.InitializeAnimator("riftward");

            if (sapi == null && On)
            {              
                Activate();
            }
        }

        public void ToggleAmbientSound(bool on)
        {
            if (Api?.Side != EnumAppSide.Client) return;

            if (on)
            {
                if (ambientSound == null || !ambientSound.IsPlaying)
                {
                    ambientSound = ((IClientWorldAccessor)Api.World).LoadSound(new SoundParams()
                    {
                        Location = new AssetLocation("sounds/block/riftward.ogg"),
                        ShouldLoop = true,
                        Position = Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f),
                        DisposeOnFinish = false,
                        Volume = 0f,
                        Range = 6,
                        SoundType = EnumSoundType.Ambient
                    });

                    if (ambientSound != null)
                    {
                        ambientSound.Start();
                        ambientSound.FadeTo(0.5f, 1f, (s)=> { });
                        ambientSound.PlaybackPosition = ambientSound.SoundLengthSeconds * (float)Api.World.Rand.NextDouble();
                    }
                } else
                {
                    if (ambientSound.IsPlaying) ambientSound.FadeTo(0.5f, 1f, (s) => { });
                }
            }
            else
            {
                ambientSound?.FadeOut(0.5f, (s) => { s.Dispose(); ambientSound = null; });
            }
        }

        private void OnServerTick(float dt)
        {
            if (On)
            {
                double dayspassed = Api.World.Calendar.TotalDays - lastUpdateTotalDays;
                fuelDays -= dayspassed;
                MarkDirty(false);
            }
            if (!HasFuel)
            {
                Deactivate();
            }

            lastUpdateTotalDays = Api.World.Calendar.TotalDays;
        }

        public void Activate()
        {
            if (!HasFuel || Api == null) return;

            On = true;
            lastUpdateTotalDays = Api.World.Calendar.TotalDays;

            animUtil?.StartAnimation(new AnimationMetaData() { Animation = "on-spin", Code = "on-spin", EaseInSpeed = 1, EaseOutSpeed = 2, AnimationSpeed = 1f });
            MarkDirty(true);
            ToggleAmbientSound(true);
        }

        public void Deactivate()
        {
            animUtil?.StopAnimation("on-spin");
            On = false;
            ToggleAmbientSound(false);
            MarkDirty(true);
        }


        public bool OnInteract(BlockSelection blockSel, IPlayer byPlayer)
        {
            var slot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot.Empty)
            {
                Api.World.PlaySoundAt(new AssetLocation("sounds/toggleswitch"), Pos, 0, byPlayer, false, 16);

                if (On) Deactivate();
                else Activate();

                return true;
            }

            if (slot.Itemstack.ItemAttributes?.IsTrue("riftwardFuel") == true && fuelDays < 0.5)
            {
                fuelDays += slot.Itemstack.ItemAttributes["rifwardfuelDays"].AsDouble(14);
                slot.TakeOut(1);
                (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
                Activate();
            }

            return true;
        }

        private void BlockEntityRiftWard_OnRiftSpawned(Rift rift)
        {
            // Instead of preventing rift spawn, we set the size to 0, which makes it invisible and inactive, 
            // so as to still consume a "rift slot"
            if (HasFuel && sapi.World.Rand.NextDouble() <= 0.95 && rift.Position.DistanceTo(Pos.X + 0.5, Pos.Y + 1, Pos.Z + 0.5) < 30)
            {
                rift.Size = 0;
                riftsBlocked++;
                MarkDirty();
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (sapi != null)
            {
                sapi.ModLoader.GetModSystem<ModSystemRifts>().OnRiftSpawned -= BlockEntityRiftWard_OnRiftSpawned;
            }

            ambientSound?.Dispose();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            ambientSound?.Dispose();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            fuelDays = tree.GetDouble("fuelUntilTotalDays", 0);
            lastUpdateTotalDays = tree.GetDouble("lastUpdateTotalDays");
            On = tree.GetBool("on");
            riftsBlocked = tree.GetInt("riftsBlocked");

            if (On) Activate();
            else Deactivate();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetDouble("lastUpdateTotalDays", lastUpdateTotalDays);
            tree.SetDouble("fuelUntilTotalDays", fuelDays);
            tree.SetBool("on", On);
            tree.SetInt("riftsBlocked", riftsBlocked);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            if (fuelDays <= 0)
            {
                dsc.AppendLine(Lang.Get("Out of power. Recharge with temporal gears."));
            } else
            {
                dsc.AppendLine(Lang.Get("Charge for {0:0.#} days", fuelDays));
            }

            dsc.AppendLine(Lang.Get("Rifts blocked: {0}", riftsBlocked));
        }
    }
}
