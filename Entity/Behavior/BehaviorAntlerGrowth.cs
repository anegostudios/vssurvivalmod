using System;
using System.Drawing;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class EntityBehaviorAntlerGrowth : EntityBehavior
    {
        InventoryGeneric creatureInv;
        Item[] variants;
        float beginGrowMonth;
        float growDurationMonths;
        float grownDurationMonths;
        float shedDurationMonths;
        /// <summary>
        /// If true, the creature sheds its antlers but drops no mountable antler item for the player to find, eg. water deer has tiny "fangs"
        /// </summary>
        bool noItemDrop;

        int MaxGrowth
        {
            get => entity.WatchedAttributes.GetInt("maxGrowth", 0);
            set => entity.WatchedAttributes.SetInt("maxGrowth", value);
        }

        double LastShedTotalDays
        {
            get => entity.WatchedAttributes.GetDouble("lastShedTotalDays", -1);
            set => entity.WatchedAttributes.SetDouble("lastShedTotalDays", value);
        }

        public EntityBehaviorAntlerGrowth(Entity entity) : base(entity)
        {
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            string[] variantnames = attributes["variants"].AsArray<string>();
            if (variantnames != null)
            {
                variants = new Item[variantnames.Length];
                string entityType = entity.Properties.Variant["type"];
                for (int i = 0; i < variantnames.Length; i++)
                {
                    AssetLocation loc = new AssetLocation("antler-" + entityType + "-" + variantnames[i]);
                    if ((variants[i] = entity.Api.World.GetItem(loc)) == null)
                    {
                        entity.Api.Logger.Warning("Missing antler item of code " + loc.ToShortString() + " for creature " + entity.Code.ToShortString());
                    }
                }
            }

            beginGrowMonth = attributes["beginGrowMonth"].AsFloat(-1);
            growDurationMonths = attributes["growDurationMonths"].AsFloat();
            grownDurationMonths = attributes["grownDurationMonths"].AsFloat();
            shedDurationMonths = attributes["shedDurationMonths"].AsFloat();
            noItemDrop = attributes["noItemDrop"].AsBool(false);

            if (entity.Api.Side == EnumAppSide.Client)
            {
                entity.WatchedAttributes.RegisterModifiedListener("inventory", readInventoryFromAttributes);
            }

            readInventoryFromAttributes();
            var rnd = entity.World.Rand;

        }

        public override void OnEntitySpawn()
        {
            if (entity.World.Side == EnumAppSide.Server)
            {
                ensureHasBirthDate();
                OnGameTick(3.1f);
            }
        }

        public override void OnEntityLoaded()
        {
            if (entity.World.Side == EnumAppSide.Server)
            {
                ensureHasBirthDate();
                OnGameTick(3.1f);
            }
        }

        private void ensureHasBirthDate()
        {
            if (!entity.WatchedAttributes.HasAttribute("birthTotalDays"))
            {
                entity.WatchedAttributes.SetDouble("birthTotalDays", entity.World.Calendar.TotalDays - entity.World.Rand.Next(900));
            }
        }

        private void GearInv_SlotModified(int id)
        {
            ToBytes(true);
            entity.WatchedAttributes.MarkPathDirty("inventory");
        }

        private void readInventoryFromAttributes()
        {
            if (creatureInv == null)
            {
                creatureInv = new InventoryGeneric(1, "antler-" + entity.EntityId, entity.Api, (id, inv) => new ItemSlot(inv));
                creatureInv.SlotModified += GearInv_SlotModified;
            }

            ITreeAttribute tree = entity.WatchedAttributes["inventory"] as ITreeAttribute;
            if (creatureInv != null && tree != null)
            {
                creatureInv.FromTreeAttributes(tree);
            }

            (entity as EntityAgent).GearInventory = creatureInv;

            (entity.Properties.Client.Renderer as EntityShapeRenderer)?.MarkShapeModified();
        }

        float accum3s = 0;

        public override void OnGameTick(float deltaTime)
        {
            if (entity.World.Side == EnumAppSide.Client) return;
            accum3s += deltaTime;
            if (accum3s > 3)
            {
                accum3s = 0;

                if (beginGrowMonth >= 0)
                {
                    updateAntlerStateYearly();
                } else
                {
                    if (growDurationMonths >= 0) updateAntlerStateOnetimeGrowth();
                }
            }
        }

        private void updateAntlerStateOnetimeGrowth()
        {
            double creatureAgeMonths = (entity.World.Calendar.TotalDays - entity.WatchedAttributes.GetDouble("birthTotalDays")) / entity.World.Calendar.DaysPerMonth;

            if (creatureInv.Empty)
            {
                int cnt = variants.Length;
                MaxGrowth = Math.Min((entity.World.Rand.Next(cnt) + entity.World.Rand.Next(cnt)) / 2, cnt - 1);
            }

            int stage = GameMath.Clamp((int)(creatureAgeMonths / (growDurationMonths * MaxGrowth)), 0, MaxGrowth);
            SetAntler(stage);
        }

        private void SetAntler(int stage)
        {
            Item newItem = variants[GameMath.Clamp(stage, 0, variants.Length - 1)];   // deals with OutOfRangeException seen on TOPTS, cause is surprising given the clamp in calling code (updateAntlerStateOnetimeGrowth()) but maybe MaxGrowth for a creature was set on a previous game version when the creature had a different number of antler variants?
            if (newItem == null) return;
            ItemStack existing = creatureInv[0].Itemstack;
            if (existing == null || newItem != existing.Item)  // Performance: do not update the inventory, serialize and markDirty if the item has not in fact changed
            {
                SetCreatureItemStack(new ItemStack(newItem));
            }
        }

        private void updateAntlerStateYearly()
        {
            if (variants == null || variants.Length == 0) return;

            int stage = getGrowthStage(out bool shedNow);
            if (stage < 0)
            {
                SetCreatureItemStack(null);
            }
            else
            {
                if (shedNow)
                {
                    var lastShed = LastShedTotalDays;
                    double shedDaysAgo = entity.World.Calendar.TotalDays - lastShed;

                    if (lastShed >= 0 && shedDaysAgo / entity.World.Calendar.DaysPerMonth < 3)
                    {
                        // Recently shed. No need to grow, no need to shed
                        return;
                    }

                    if (!creatureInv[0].Empty)
                    {
                        if (!noItemDrop) entity.World.SpawnItemEntity(creatureInv[0].Itemstack, entity.Pos.XYZ);
                        SetCreatureItemStack(null);
                        LastShedTotalDays = entity.World.Calendar.TotalDays;
                    }
                }
                else
                {
                    SetAntler(stage);
                }
            }
        }

        private int getGrowthStage(out bool shedNow)
        {
            shedNow = false;

            var cal = entity.World.Calendar;
            var hemi = entity.World.Calendar.GetHemisphere(entity.Pos.AsBlockPos);
            double totalmonths = cal.TotalDays / cal.DaysPerMonth;

            double beginGrowMonth = this.beginGrowMonth;
            if (hemi == EnumHemisphere.South) beginGrowMonth += 6;
            if (beginGrowMonth >= 12) beginGrowMonth -= 12;

            double midGrowthPoint = beginGrowMonth + growDurationMonths / 2;

            int startYear = (int)(cal.TotalDays / cal.DaysPerYear);
            double distanceToMidGrowth = totalmonths - (startYear * 12 + midGrowthPoint);

            if (distanceToMidGrowth < -9) distanceToMidGrowth += 12;

            if (distanceToMidGrowth < -growDurationMonths/2) return -1;
            if (distanceToMidGrowth > growDurationMonths/2 + grownDurationMonths + shedDurationMonths) return -1;

            double stageRel = (distanceToMidGrowth + growDurationMonths / 2) / growDurationMonths;

            shedNow = distanceToMidGrowth > growDurationMonths/2 + grownDurationMonths;

            int cnt = variants.Length;
            if (creatureInv.Empty || MaxGrowth < 0)
            {
                MaxGrowth = Math.Min((entity.World.Rand.Next(cnt) + entity.World.Rand.Next(cnt)) / 2, cnt - 1);
            }

            return (int)GameMath.Clamp(stageRel * cnt, 0, MaxGrowth);
        }

        private void SetCreatureItemStack(ItemStack stack)
        {
            creatureInv[0].Itemstack = stack;

            // radfast 26/1/24:  Do we actually need the next two lines, when creatureInv.SlotModified has been set to GearInv_SlotModified ?
            ToBytes(true);
            entity.WatchedAttributes.MarkPathDirty("inventory");
        }

        public override void FromBytes(bool isSync)
        {
            readInventoryFromAttributes();
        }

        public override void ToBytes(bool forClient)
        {
            ITreeAttribute tree = new TreeAttribute();
            entity.WatchedAttributes["inventory"] = tree;
            creatureInv.ToTreeAttributes(tree);
        }



        public override string PropertyName()
        {
            return "antlergrowth";
        }
    }
}
