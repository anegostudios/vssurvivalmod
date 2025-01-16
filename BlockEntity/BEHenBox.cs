using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    /** Mechanics
     * 
     * 1.  / Any well-fed hen - i.e. ready to lay - will activate task AITaskSeekBlockAndLay
     * 2.  / Once sitting on the henbox for 5 real seconds, the hen will first attempt to lay an egg in the henbox
     * 3.  / If the hen can lay an egg (fewer than 3 eggs currently) it does so; makes an egg laying sound and activates another AITask.  TODO: we could add a flapping animation
     * 4. / The egg will be fertile (it will have a chickCode) if there was a male nearby; otherwise it will be infertile; eggs added by a player are always infertile (for now).  [TODO: track individual egg items' fertility and parentage so that players can micro-manage]
     * 5.  / If the hen cannot lay an egg (henbox is full of 3 eggs already), the hen becomes "broody" and will sit on the eggs for a long time (around three quarters of a day)
     * 6.  / That broody hen or another broody hen will continue returning to the henbox and sitting on the eggs until they eventually hatch
     * 7.  / HenBox BlockEntity tracks how long a hen (any hen) has sat on the eggs warming them - as set in the chicken-hen JSON it needs 5 in-game days
     * 8.  / When the eggs have been warmed for long enough they hatch: chicks are spawned and the henbox reverts to empty
     * 
     * HenBox tracks the parent entity and the generation of each egg separately => in future could have 1 duck egg in a henbox for example, so that 1 duckling hatches and 2 hen chicks
     */

    public class BlockEntityHenBox : BlockEntity, IAnimalNest
    {
        internal InventoryGeneric inventory;
        protected string fullCode = "1egg";

        public Size2i AtlasSize => (Api as ICoreClientAPI).BlockTextureAtlas.Size;

        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "nest";

        public Entity occupier;

        protected int[] parentGenerations = new int[10];
        protected AssetLocation[] chickNames = new AssetLocation[10];
        protected double timeToIncubate;
        protected double occupiedTimeLast;


        public BlockEntityHenBox()
        {
        }


        public bool IsSuitableFor(Entity entity)
        {
            return entity is EntityAgent && entity.Code.Path == "chicken-hen";
        }

        public bool Occupied(Entity entity)
        {
            return occupier != null && occupier != entity;
        }

        public void SetOccupier(Entity entity)
        {
            occupier = entity;
        }

        public float DistanceWeighting => 2 / (CountEggs() + 2);


        public virtual bool TryAddEgg(Entity entity, string chickCode, double incubationTime)
        {
            if (Block.LastCodePart() == fullCode)
            {
                if (timeToIncubate == 0)
                {
                    timeToIncubate = incubationTime;
                    occupiedTimeLast = entity.World.Calendar.TotalDays;
                }
                this.MarkDirty();
                return false;
            }

            timeToIncubate = 0;
            int eggs = CountEggs();
            parentGenerations[eggs] = entity.WatchedAttributes.GetInt("generation", 0);
            chickNames[eggs] = chickCode == null ? null : entity.Code.CopyWithPath(chickCode);
            eggs++;
            Block replacementBlock = Api.World.GetBlock(Block.CodeWithVariant("eggCount", eggs + ((eggs > 1) ? "eggs" : "egg")));
            if (replacementBlock == null)
            {
                return false;
            }
            Api.World.BlockAccessor.ExchangeBlock(replacementBlock.Id, this.Pos);
            this.Block = replacementBlock;
            this.MarkDirty();

            return true;
        }

        protected int CountEggs()
        {
            int eggs = Block.LastCodePart()[0];
            return eggs <= '9' && eggs >= '0' ? eggs - '0' : 0;
        }

        protected virtual void On1500msTick(float dt)
        {
            if (timeToIncubate == 0) return;

            double newTime = Api.World.Calendar.TotalDays;
            if (occupier != null && occupier.Alive)   //Does this need a more sophisticated check, i.e. is the occupier's position still here?  (Also do we reset the occupier variable to null if save and re-load?)
            {
                if (newTime > occupiedTimeLast)
                {
                    timeToIncubate -= newTime - occupiedTimeLast;
                    this.MarkDirty();
                }
            }
            occupiedTimeLast = newTime;

            if (timeToIncubate <= 0)
            {
                timeToIncubate = 0;
                int eggs = CountEggs();
                Random rand = Api.World.Rand;

                for (int c = 0; c < eggs; c++)
                {
                    AssetLocation chickName = chickNames[c];
                    if (chickName == null) continue;
                    int generation = parentGenerations[c];

                    EntityProperties childType = Api.World.GetEntityType(chickName);
                    if (childType == null) continue;
                    Entity childEntity = Api.World.ClassRegistry.CreateEntity(childType);
                    if (childEntity == null) continue;

                    childEntity.ServerPos.SetFrom(new EntityPos(this.Position.X + (rand.NextDouble() - 0.5f) / 5f, this.Position.Y, this.Position.Z + (rand.NextDouble() - 0.5f) / 5f, (float) rand.NextDouble() * GameMath.TWOPI));
                    childEntity.ServerPos.Motion.X += (rand.NextDouble() - 0.5f) / 200f;
                    childEntity.ServerPos.Motion.Z += (rand.NextDouble() - 0.5f) / 200f;

                    childEntity.Pos.SetFrom(childEntity.ServerPos);
                    childEntity.Attributes.SetString("origin", "reproduction");
                    childEntity.WatchedAttributes.SetInt("generation", generation + 1);
                    Api.World.SpawnEntity(childEntity);
                }


                Block replacementBlock = Api.World.GetBlock(new AssetLocation(Block.FirstCodePart() + "-empty"));
                Api.World.BlockAccessor.ExchangeBlock(replacementBlock.Id, this.Pos);
                this.Api.World.SpawnCubeParticles(Pos.ToVec3d().Add(0.5, 0.5, 0.5), new ItemStack(this.Block), 1, 20, 1, null);
                this.Block = replacementBlock;
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            fullCode = this.Block.Attributes?["fullVariant"]?.AsString(null);
            if (fullCode == null) fullCode = "1egg";

            if (api.Side == EnumAppSide.Server)
            {
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                RegisterGameTickListener(On1500msTick, 1500);
            }
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("inc", timeToIncubate);
            tree.SetDouble("occ", occupiedTimeLast);
            for (int i = 0; i < 10; i++)
            {
                tree.SetInt("gen" + i, parentGenerations[i]);
                AssetLocation chickName = chickNames[i];
                if (chickName != null) tree.SetString("chick" + i, chickName.ToShortString());
            }
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            timeToIncubate = tree.GetDouble("inc");
            occupiedTimeLast = tree.GetDouble("occ");
            for (int i = 0; i < 10; i++)
            {
                parentGenerations[i] = tree.GetInt("gen" + i);
                string chickName = tree.GetString("chick" + i);
                chickNames[i] = chickName == null ? null : new AssetLocation(chickName);
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            int eggCount = CountEggs();
            int fertileCount = 0;
            for (int i = 0; i < eggCount; i++) if (chickNames[i] != null) fertileCount++;
            if (fertileCount > 0)
            {
                if (fertileCount > 1)
                    dsc.AppendLine(Lang.Get("{0} fertile eggs", fertileCount));
                else
                    dsc.AppendLine(Lang.Get("1 fertile egg"));

                if (timeToIncubate >= 1.5)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: {0:0} days", timeToIncubate));
                else if (timeToIncubate >= 0.75)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: 1 day"));
                else if (timeToIncubate > 0)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: {0:0} hours", timeToIncubate * 24));

                if (occupier == null && Block.LastCodePart() == fullCode)
                    dsc.AppendLine(Lang.Get("A broody hen is needed!"));
            }
            else if (eggCount > 0)
            {
                dsc.AppendLine(Lang.Get("No eggs are fertilized"));
            }
        }
    }
}
