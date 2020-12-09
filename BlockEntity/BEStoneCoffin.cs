using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockEntityStoneCoffin : BlockEntityContainer
    {
        static AdvancedParticleProperties smokeParticles = new AdvancedParticleProperties()
        {
            HsvaColor = new NatFloat[] { NatFloat.createUniform(0, 0), NatFloat.createUniform(0, 0), NatFloat.createUniform(40, 30), NatFloat.createUniform(220, 50) },
            OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.QUADRATIC, -16),
            GravityEffect = NatFloat.createUniform(0, 0),
            Velocity = new NatFloat[] { NatFloat.createUniform(0f, 0.05f), NatFloat.createUniform(0.2f, 0.3f), NatFloat.createUniform(0f, 0.05f) },
            Size = NatFloat.createUniform(0.3f, 0.05f),
            Quantity = NatFloat.createUniform(0.25f, 0),
            SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, 1.5f),
            LifeLength = NatFloat.createUniform(4.5f, 0),
            ParticleModel = EnumParticleModel.Quad,
            SelfPropelled = true,
        };


        MultiblockStructure ms;
        BlockStoneCoffinSection blockScs;
        InventoryGeneric inv;
        ICoreClientAPI capi;

        bool receivesHeat;
        float receivesHeatSmooth; // 0..1
        double progress; // 0..1
        double totalHoursLastUpdate;
        bool processComplete;
        bool structureComplete;

        int tempStoneCoffin;
        BlockPos tmpPos = new BlockPos();
        BlockPos[] particlePositions = new BlockPos[7];

        public override InventoryBase Inventory => inv;
        public override string InventoryClassName => "stonecoffin";

        public bool IsFull
        {
            get
            {
                return IngotCount == 16 && CoalLayerCount == 5;
            }
        }


        public BlockEntityStoneCoffin()
        {
            inv = new InventoryGeneric(2, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            inv.LateInitialize(InventoryClassName + "-" + Pos, api);

            capi = api as ICoreClientAPI;

            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(onClientTick50ms, 50);
            } else
            {
                RegisterGameTickListener(onServerTick1s, 1000);
                RegisterGameTickListener(onServerTick3s, 3000);
            }


            ms = Block.Attributes["multiblockStructure"].AsObject<MultiblockStructure>();
            int rotYDeg = 0;
            if (Block.Variant["side"] == "east") rotYDeg = 270;
            if (Block.Variant["side"] == "south") rotYDeg = 180;
            if (Block.Variant["side"] == "west") rotYDeg = 90;

            ms.InitForUse(rotYDeg);

            blockScs = Block as BlockStoneCoffinSection;
            updateSelectiveElements();


            particlePositions[0] = Pos.DownCopy(2);
            particlePositions[1] = particlePositions[0].AddCopy(blockScs.Orientation.Opposite);

            particlePositions[2] = Pos.AddCopy(blockScs.Orientation.GetCW());
            particlePositions[3] = Pos.AddCopy(blockScs.Orientation.GetCCW());

            particlePositions[4] = Pos.AddCopy(blockScs.Orientation.GetCW()).Add(blockScs.Orientation.Opposite);
            particlePositions[5] = Pos.AddCopy(blockScs.Orientation.GetCCW()).Add(blockScs.Orientation.Opposite);

            particlePositions[6] = Pos.UpCopy(1).Add(blockScs.Orientation.Opposite);

        }

        public bool Interact(IPlayer byPlayer)
        {
            bool sneaking = byPlayer.WorldData.EntityControls.Sneak;

            int damagedTiles = 0;
            int wrongTiles = 0;

            if (sneaking && ms.InCompleteBlockCount(Api.World, Pos, (haveBlock, wantLoc) => { if (haveBlock.FirstCodePart() == "refractorybricks" && haveBlock.Variant["state"] == "damaged") damagedTiles++; else wrongTiles++; }) > 0)
            {
                if (wrongTiles > 0 && damagedTiles > 0)
                {
                    capi?.TriggerIngameError(this, "incomplete", Lang.Get("Structure is not complete, {0} blocks are missing or wrong, {1} tiles are damaged!", wrongTiles, damagedTiles));
                } else
                {
                    if (wrongTiles > 0)
                    {
                        capi?.TriggerIngameError(this, "incomplete", Lang.Get("Structure is not complete, {0} blocks are missing or wrong!", wrongTiles));
                    } else
                    {
                        if (damagedTiles == 1)
                        {
                            capi?.TriggerIngameError(this, "incomplete", Lang.Get("Structure is not complete, {0} tile is damaged!", damagedTiles));
                        } else
                        {
                            capi?.TriggerIngameError(this, "incomplete", Lang.Get("Structure is not complete, {0} tiles are damaged!", damagedTiles));
                        }
                        
                    }
                }
                
                if (Api.Side == EnumAppSide.Client)
                {
                    ms.HighlightIncompleteParts(Api.World, byPlayer, Pos);
                }
                return false;
            } else
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    ms.ClearHighlights(Api.World, byPlayer);
                }
            }

            if (!sneaking) return false;

            if (!blockScs.IsCompleteCoffin(Pos))
            {
                capi?.TriggerIngameError(this, "incomplete", Lang.Get("Cannot fill an incomplete coffing, place the other half first"));
                return false;
            }

            ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;


            if (!slot.Empty)
            {
                if (IngotCount / 4 >= CoalLayerCount) return AddCoal(slot);
                else return AddIngot(slot);
            }

            return true;
        }

        

        bool AddCoal(ItemSlot slot)
        {
            if (CoalLayerCount >= 5)
            {
                capi?.TriggerIngameError(this, "notenoughfuel", Lang.Get("This stone coffin is full already"));
                return false;
            }

            var props = slot.Itemstack.Collectible.CombustibleProps;
            if (props == null || props.BurnTemperature < 1300)
            {
                capi?.TriggerIngameError(this, "wrongfuel", Lang.Get("Must add fuel of high enough quality now (burn temperature of 1300°C or higher)"));
                return false;
            }

            if (slot.Itemstack.StackSize < 8)
            {
                capi?.TriggerIngameError(this, "notenoughfuel", Lang.Get("Each layer requires 8 pieces of fuel"));
                return false;
            }

            int moved = slot.TryPutInto(Api.World, inv[0], 8);
            if (moved == 0)
            {
                capi?.TriggerIngameError(this, "cannotmixfuels", Lang.Get("Cannot mix fuels, it will mess with the carburisation process!"));
                return false;
            }

            updateSelectiveElements();
            MarkDirty(true);

            return true;
        }

        bool AddIngot(ItemSlot slot)
        {
            if (IngotCount >= 16)
            {
                capi?.TriggerIngameError(this, "notenoughfuel", Lang.Get("This stone coffin is full already"));
                return false;
            }

            if (slot.Itemstack.ItemAttributes?["carburizableProps"].Exists == false)
            {
                capi?.TriggerIngameError(this, "wrongfuel", Lang.Get("Can only add carburizable metal ingots"));
                return false;
            }

            int moved = slot.TryPutInto(Api.World, inv[1], 1);
            if (moved == 0)
            {
                capi?.TriggerIngameError(this, "cannotmixfuels", Lang.Get("Cannot mix ingots, it will mess with the carburisation process!"));
                return false;
            }

            updateSelectiveElements();
            MarkDirty(true);

            return true;
        }


        public int IngotCount => inv[1].StackSize;
        public int CoalLayerCount => inv[0].StackSize / 8;

        public int CoffinTemperature => tempStoneCoffin;

        string[] selectiveElementsMain = new string[0];
        string[] selectiveElementsSlave = new string[0];

        void updateSelectiveElements()
        {
            List<string> main = new List<string>();
            List<string> slave = new List<string>();

            for (int i = 0; i < IngotCount; i++)
            {
                List<string> target = (i % 4) >= 2 ? slave : main;
                int num = 1 + (i / 4) * 2 + (i % 2);

                target.Add("Ingot" + num);
            }

            for (int i = 0; i < CoalLayerCount; i++)
            {
                main.Add("Charcoal" + (i+1));
                slave.Add("Charcoal" + (i+1));
            }

            selectiveElementsMain = main.ToArray();
            selectiveElementsSlave = slave.ToArray();
        }


        private void onServerTick1s(float dt)
        {
            if (receivesHeat)
            {
                Vec3d pos = Pos.ToVec3d().Add(0.5, 0.5, 0.5).Add(blockScs.Orientation.Opposite.Normalf.X, 0, blockScs.Orientation.Opposite.Normalf.Z);
                Entity[] entities = Api.World.GetEntitiesAround(pos, 2.5f, 1, e => e.Alive && e is EntityAgent);

                //Api.World.SpawnParticles(10, ColorUtil.WhiteArgb, pos, pos, new Vec3f(), new Vec3f(), 4, 0, 3);

                foreach (var entity in entities) entity.ReceiveDamage(new DamageSource() { DamageTier = 1, SourcePos = pos, SourceBlock = Block, Type = EnumDamageType.Fire }, 4);
            }
        }


        private void onServerTick3s(float dt)
        {
            BlockPos coalPilePos = Pos.DownCopy(2);
            BlockPos othercoalPilePos = coalPilePos.AddCopy(blockScs.Orientation.Opposite);

            bool beforeReceiveHeat = receivesHeat;
            bool beforeStructureComplete = structureComplete;

            if (!receivesHeat)
            {
                totalHoursLastUpdate = Api.World.Calendar.TotalHours;
            }

            BlockEntityCoalPile becp = Api.World.BlockAccessor.GetBlockEntity(coalPilePos) as BlockEntityCoalPile;
            float leftHeatHoursLeft = (becp != null && becp.IsBurning) ? becp.GetHoursLeft(totalHoursLastUpdate) : 0f;
            becp = Api.World.BlockAccessor.GetBlockEntity(othercoalPilePos) as BlockEntityCoalPile;
            float rightHeatHoursLeft = (becp != null && becp.IsBurning) ? becp.GetHoursLeft(totalHoursLastUpdate) : 0f;

            receivesHeat = leftHeatHoursLeft > 0 && rightHeatHoursLeft > 0;


            if (processComplete || !IsFull || !hasLid()) return;

            structureComplete = ms.InCompleteBlockCount(Api.World, Pos) == 0;

            if (beforeReceiveHeat != receivesHeat || beforeStructureComplete != structureComplete)
            {
                MarkDirty();
            }

            if (receivesHeat)
            {
                if (!structureComplete) return;

                double hoursPassed = Api.World.Calendar.TotalHours - totalHoursLastUpdate;
                double heatHoursReceived = Math.Max(0, Math.Min(hoursPassed, Math.Min(leftHeatHoursLeft, rightHeatHoursLeft)));

                progress += heatHoursReceived / 160f;
                totalHoursLastUpdate = Api.World.Calendar.TotalHours;

                float temp = inv[1].Itemstack.Collectible.GetTemperature(Api.World, inv[1].Itemstack);
                float tempGain = (float)(hoursPassed * 500);
                inv[1].Itemstack.Collectible.SetTemperature(Api.World, inv[1].Itemstack, Math.Min(800, temp + tempGain));

                if (Math.Abs(tempStoneCoffin - temp) > 25)
                {
                    tempStoneCoffin = (int)temp;
                    if (tempStoneCoffin > 500)
                    {
                        MarkDirty(true);
                    }
                }

                MarkDirty();
            }

            if (progress >= 1)
            {
                int stacksize = inv[1].Itemstack.StackSize;

                JsonItemStack jstack = inv[1].Itemstack.ItemAttributes?["carburizableProps"]["carburizedOutput"].AsObject<JsonItemStack>(null, Block.Code.Domain);
                if (jstack.Resolve(Api.World, "carburizable output"))
                {
                    float temp = inv[1].Itemstack.Collectible.GetTemperature(Api.World, inv[0].Itemstack);
                    inv[0].Itemstack = null;
                    inv[1].Itemstack = jstack.ResolvedItemstack.Clone();
                    inv[1].Itemstack.StackSize = stacksize;
                    inv[1].Itemstack.Collectible.SetTemperature(Api.World, inv[1].Itemstack, temp);
                }

                ms.WalkMatchingBlocks(Api.World, Pos, (block, pos) =>
                {
                    float resis = block.Attributes?["heatResistance"].AsFloat(1) ?? 1;

                    if (Api.World.Rand.NextDouble() > resis)
                    {
                        Block nowblock = Api.World.GetBlock(block.CodeWithVariant("state", "damaged"));
                        Api.World.BlockAccessor.SetBlock(nowblock.Id, pos);
                    }
                });

                processComplete = true;
            }
        }


        bool hasLid()
        {
            return
                Api.World.BlockAccessor.GetBlock(Pos.X, Pos.Y + 1, Pos.Z).FirstCodePart() == "stonecoffinlid" &&
                Api.World.BlockAccessor.GetBlock(Pos.X + blockScs.Orientation.Opposite.Normali.X, Pos.Y + 1, Pos.Z + blockScs.Orientation.Opposite.Normali.Z).FirstCodePart() == "stonecoffinlid"
            ;
        }

        private void onClientTick50ms(float dt)
        {
            if (processComplete || !structureComplete) return;

            receivesHeatSmooth = GameMath.Clamp(receivesHeatSmooth + (receivesHeat ? dt / 10 : -dt / 3), 0, 1);

            if (receivesHeatSmooth == 0) return;

            Random rnd = Api.World.Rand;

            for (int i = 0; i < Entity.FireParticleProps.Length; i++)
            {
                int index = Math.Min(Entity.FireParticleProps.Length - 1, Api.World.Rand.Next(Entity.FireParticleProps.Length + 1));
                AdvancedParticleProperties particles = Entity.FireParticleProps[index];

                for (int j = 0; j < particlePositions.Length; j++)
                {
                    BlockPos pos = particlePositions[j];

                    if (j >= 6)
                    {
                        particles = smokeParticles;
                        particles.Quantity.avg = 0.2f;
                        particles.basePos.Set(pos.X + 0.5, pos.Y + 0.75, pos.Z + 0.5);
                        particles.Velocity[1].avg = (float)(0.3 + 0.3 * rnd.NextDouble()) * 2;
                        particles.PosOffset[1].var = 0.2f;
                        particles.Velocity[0].avg = (float)(rnd.NextDouble() - 0.5) / 4;
                        particles.Velocity[2].avg = (float)(rnd.NextDouble() - 0.5) / 4;

                    }
                    else
                    {
                        particles.Quantity.avg = GameMath.Sqrt(0.5f * (index == 0 ? 0.5f : (index == 1 ? 5 : 0.6f)))/2f;
                        particles.basePos.Set(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5);
                        particles.Velocity[1].avg = (float)(0.5 + 0.5 * rnd.NextDouble()) * 2;
                        particles.PosOffset[1].var = 1;
                        particles.Velocity[0].avg = (float)(rnd.NextDouble() - 0.5);
                        particles.Velocity[2].avg = (float)(rnd.NextDouble() - 0.5);
                    }

                    
                    particles.PosOffset[0].var = 0.49f;
                    particles.PosOffset[2].var = 0.49f;
                    
                    
                    Api.World.SpawnParticles(particles);
                }
            }
        }



        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            receivesHeat = tree.GetBool("receivesHeat");
            totalHoursLastUpdate = tree.GetDouble("totalHoursLastUpdate");
            progress = tree.GetDouble("progress");
            processComplete = tree.GetBool("processComplete");
            structureComplete = tree.GetBool("structureComplete");
            tempStoneCoffin = tree.GetInt("tempStoneCoffin");
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("receivesHeat", receivesHeat);
            tree.SetDouble("totalHoursLastUpdate", totalHoursLastUpdate);
            tree.SetDouble("progress", progress);
            tree.SetBool("processComplete", processComplete);
            tree.SetBool("structureComplete", structureComplete);
            tree.SetInt("tempStoneCoffin", tempStoneCoffin);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Client)
            {
                ms.ClearHighlights(Api.World, (Api as ICoreClientAPI).World.Player);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Client)
            {
                ms.ClearHighlights(Api.World, (Api as ICoreClientAPI).World.Player);
            }
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            Shape shape = capi.TesselatorManager.GetCachedShape(Block.Shape.Base);

            MeshData meshdatamain;
            MeshData meshdataslave;

            tessThreadTesselator.TesselateShape(Block, shape, out meshdatamain, null, null, selectiveElementsMain);
            tessThreadTesselator.TesselateShape(Block, shape, out meshdataslave, null, null, selectiveElementsSlave);
            if (blockScs.Orientation == BlockFacing.EAST)
            {
                meshdatamain.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, -GameMath.PIHALF, 0);
                meshdataslave.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, -GameMath.PIHALF, 0);
            }

            meshdataslave.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, GameMath.PI, 0);
            meshdataslave.Translate(blockScs.Orientation.Opposite.Normalf);

            mesher.AddMeshData(meshdatamain);
            mesher.AddMeshData(meshdataslave);

            

            return false;
        }



        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (processComplete)
            {
                dsc.AppendLine("Carburization process complete. Break to retrieve blister steel.");
                return;
            }

            if (IsFull) {
                if (!hasLid())
                {
                    dsc.AppendLine("Stone coffin lid is missing");
                } else
                {
                    if (!structureComplete)
                    {
                        dsc.AppendLine("Structure incomplete! Caburization paused.");
                        return;
                    }

                    if (receivesHeat)
                    {
                        dsc.AppendLine("Okay! Receives heat!");
                    } else
                    {
                        dsc.AppendLine("Ready to be fired. Ignite a pile of coal below each stone coffin half.");
                    }
                    
                }
            }

            if (progress > 0)
            {
                dsc.AppendLine(string.Format("Carburization: {0}% complete", (int)(progress * 100)));
            }
            
        }

    }
}
