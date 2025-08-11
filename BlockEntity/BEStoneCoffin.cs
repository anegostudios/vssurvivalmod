using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

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
        MultiblockStructure msOpp;
        MultiblockStructure msHighlighted;
        BlockStoneCoffinSection blockScs;
        InventoryStoneCoffin inv;
        ICoreClientAPI capi;

        bool receivesHeat;
        float receivesHeatSmooth; // 0..1
        double progress; // 0..1
        double totalHoursLastUpdate;
        bool processComplete;
        public bool StructureComplete;
        int tickCounter;

        int tempStoneCoffin;
        BlockPos[] particlePositions = new BlockPos[8];

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
            inv = new InventoryStoneCoffin(2, null, null);
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
            }


            ms = Block.Attributes["multiblockStructure"].AsObject<MultiblockStructure>();
            msOpp = Block.Attributes["multiblockStructure"].AsObject<MultiblockStructure>();
            int rotYDeg = 0;
            int rotYDegOpp = 180;
            if (Block.Variant["side"] == "east")   //BlockStoneCoffin only has a BE on north and east variants
            {
                rotYDeg = 270;
                rotYDegOpp = 90;
            }

            ms.InitForUse(rotYDeg);
            msOpp.InitForUse(rotYDegOpp);

            blockScs = Block as BlockStoneCoffinSection;
            updateSelectiveElements();


            particlePositions[0] = Pos.DownCopy(2);
            particlePositions[1] = particlePositions[0].AddCopy(blockScs.Orientation.Opposite);

            particlePositions[2] = Pos.AddCopy(blockScs.Orientation.GetCW());
            particlePositions[3] = Pos.AddCopy(blockScs.Orientation.GetCCW());

            particlePositions[4] = Pos.AddCopy(blockScs.Orientation.GetCW()).Add(blockScs.Orientation.Opposite);
            particlePositions[5] = Pos.AddCopy(blockScs.Orientation.GetCCW()).Add(blockScs.Orientation.Opposite);

            particlePositions[6] = Pos.UpCopy(1).Add(blockScs.Orientation.Opposite);
            particlePositions[7] = Pos.UpCopy(1);

            inv.SetSecondaryPos(Pos.AddCopy(blockScs.Orientation.Opposite));
        }

        public bool Interact(IPlayer byPlayer, bool preferThis)
        {
            bool sneaking = byPlayer.WorldData.EntityControls.ShiftKey;

            int damagedTiles = 0;
            int wrongTiles = 0;
            int incompleteCount = 0;
            BlockPos posMain = Pos;

            // set up incompleteCount (etc) for both orientations and pick whichever is more complete
            if (sneaking)
            {
                int ic = 0;
                int icOpp = int.MaxValue;
                int dt = 0;
                int wt = 0;
                int dtOpp = 0;
                int wtOpp = 0;

                ic = ms.InCompleteBlockCount(Api.World, Pos,
                    (haveBlock, wantLoc) =>
                    {
                        var firstCodePart = haveBlock.FirstCodePart();
                        if ((firstCodePart == "refractorybricks" || firstCodePart == "refractorybrickgrating") && haveBlock.Variant["state"] == "damaged")
                        {
                            dt++;
                        }
                        else wt++;
                    }
                );
                if (ic > 0 && blockScs.IsCompleteCoffin(Pos))
                {
                    icOpp = msOpp.InCompleteBlockCount(Api.World, Pos.AddCopy(blockScs.Orientation.Opposite),
                        (haveBlock, wantLoc) =>
                        {
                            var firstCodePart = haveBlock.FirstCodePart();
                            if ((firstCodePart == "refractorybricks" || firstCodePart == "refractorybrickgrating") && haveBlock.Variant["state"] == "damaged")
                            {
                                dt++;
                            } else wtOpp++;
                        }
                    );
                }

                // This logic aims to figure out which structure to show - if one is almost complete (3 wrong tiles or less) that one will be shown; preferThis has a preference if both are equally incomplete (newly placed stonecoffin) or if one is not much more complete than the other (allows for building errors of 1-3 tiles before the shown structure flips)
                if (wtOpp <= 3 && wt < wtOpp || wtOpp > 3 && wt < wtOpp - 3 || preferThis && wt <= wtOpp || preferThis && wt > 3 && wt <= wtOpp + 3)
                {
                    incompleteCount = ic;
                    damagedTiles = dt;
                    wrongTiles = wt;
                    if (ic > 0) msHighlighted = ms;
                }
                else
                {
                    incompleteCount = icOpp;
                    damagedTiles = dtOpp;
                    wrongTiles = wtOpp;
                    msHighlighted = msOpp;
                    posMain = Pos.AddCopy(blockScs.Orientation.Opposite);
                }
            }

            if (sneaking && incompleteCount > 0)
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
                    msHighlighted.HighlightIncompleteParts(Api.World, byPlayer, posMain);
                }
                return false;
            } else
            {
                if (Api.Side == EnumAppSide.Client)
                {
                    msHighlighted?.ClearHighlights(Api.World, byPlayer);
                }
            }

            if (!sneaking) return false;

            if (!blockScs.IsCompleteCoffin(Pos))
            {
                capi?.TriggerIngameError(this, "incomplete", Lang.Get("Cannot fill an incomplete coffin, place the other half first"));
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
                capi?.TriggerIngameError(this, "wrongfuel", Lang.Get("Needs a layer of high-quality carbon-bearing material (coke or charcoal)"));
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
                capi?.TriggerIngameError(this, "cannotmixfuels", Lang.Get("Cannot mix materials, it will mess with the carburisation process!"));
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
                capi?.TriggerIngameError(this, "wrongfuel", Lang.Get("Next add some carburizable metal ingots"));
                return false;
            }

            ItemStackMoveOperation op = new ItemStackMoveOperation(Api.World, EnumMouseButton.Left, 0, EnumMergePriority.DirectMerge, 1);
            int moved = slot.TryPutInto(inv[1], ref op);
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

        string[] selectiveElementsMain = Array.Empty<string>();
        string[] selectiveElementsSecondary = Array.Empty<string>();

        void updateSelectiveElements()
        {
            List<string> main = new List<string>();
            List<string> secondary = new List<string>();
            bool isSteel = inv[1].Itemstack?.Collectible.FirstCodePart(1) == "blistersteel";

            for (int i = 0; i < IngotCount; i++)
            {
                List<string> target = (i % 4) >= 2 ? secondary : main;
                int num = 1 + (i / 4) * 2 + (i % 2);

                target.Add("Charcoal" + ((num + 1) / 2) + "/" + (i >= 7 && isSteel ? "Steel" : "Ingot") + num);
            }

            for (int i = 0; i < CoalLayerCount; i++)
            {
                main.Add("Charcoal" + (i+1));
                secondary.Add("Charcoal" + (i+1));
            }

            selectiveElementsMain = main.ToArray();
            selectiveElementsSecondary = secondary.ToArray();
        }


        private void onServerTick1s(float dt)
        {
            if (receivesHeat)
            {
                Vec3d pos = Pos.ToVec3d().Add(0.5, 0.5, 0.5).Add(blockScs.Orientation.Opposite.Normalf.X, 0, blockScs.Orientation.Opposite.Normalf.Z);
                if (msOpp.InCompleteBlockCount(Api.World, Pos.AddCopy(blockScs.Orientation.Opposite)) == 0)
                {
                    pos = Pos.AddCopy(blockScs.Orientation.Opposite).ToVec3d().Add(0.5, 0.5, 0.5).Add(blockScs.Orientation.Normalf.X, 0, blockScs.Orientation.Normalf.Z); ;
                }
                Entity[] entities = Api.World.GetEntitiesAround(pos, 2.5f, 1, e => e.Alive && e is EntityAgent);

                foreach (var entity in entities) entity.ReceiveDamage(new DamageSource() { DamageTier = 1, SourcePos = pos, SourceBlock = Block, Type = EnumDamageType.Fire }, 4);
            }

            if (++tickCounter % 3 == 0) onServerTick3s(dt);
        }


        private void onServerTick3s(float dt)
        {
            BlockPos coalPilePos = Pos.DownCopy(2);
            BlockPos othercoalPilePos = coalPilePos.AddCopy(blockScs.Orientation.Opposite);

            bool beforeReceiveHeat = receivesHeat;
            bool beforeStructureComplete = StructureComplete;

            if (!receivesHeat)
            {
                totalHoursLastUpdate = Api.World.Calendar.TotalHours;
            }

            BlockEntityCoalPile becp = Api.World.BlockAccessor.GetBlockEntity(coalPilePos) as BlockEntityCoalPile;
            float leftHeatHoursLeft = (becp != null && becp.IsBurning) ? becp.GetHoursLeft(totalHoursLastUpdate) : 0f;
            becp = Api.World.BlockAccessor.GetBlockEntity(othercoalPilePos) as BlockEntityCoalPile;
            float rightHeatHoursLeft = (becp != null && becp.IsBurning) ? becp.GetHoursLeft(totalHoursLastUpdate) : 0f;

            receivesHeat = leftHeatHoursLeft > 0 && rightHeatHoursLeft > 0;

            MultiblockStructure msInUse = null;
            BlockPos posInUse = null;
            StructureComplete = false;
            if (ms.InCompleteBlockCount(Api.World, Pos) == 0)
            {
                msInUse = ms;
                posInUse = Pos;
                StructureComplete = true;
            }
            else if (msOpp.InCompleteBlockCount(Api.World, Pos.AddCopy(blockScs.Orientation.Opposite)) == 0)
            {
                msInUse = msOpp;
                posInUse = Pos.AddCopy(blockScs.Orientation.Opposite);
                StructureComplete = true;
            }

            if (beforeReceiveHeat != receivesHeat || beforeStructureComplete != StructureComplete)
            {
                MarkDirty();
            }

            if (processComplete || !IsFull || !hasLid())
            {
                return;
            }

            if (receivesHeat)
            {
                if (!StructureComplete) return;

                double hoursPassed = Api.World.Calendar.TotalHours - totalHoursLastUpdate;
                double heatHoursReceived = Math.Max(0, GameMath.Min((float)hoursPassed, leftHeatHoursLeft, rightHeatHoursLeft));

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

            if (progress >= 0.995)
            {
                int stacksize = inv[1].Itemstack.StackSize;

                JsonItemStack jstack = inv[1].Itemstack.ItemAttributes?["carburizableProps"]["carburizedOutput"].AsObject<JsonItemStack>(null, Block.Code.Domain);
                if (jstack.Resolve(Api.World, "carburizable output"))
                {
                    float temp = inv[1].Itemstack.Collectible.GetTemperature(Api.World, inv[0].Itemstack);
                    inv[0].Itemstack.StackSize -= 8;
                    inv[1].Itemstack = jstack.ResolvedItemstack.Clone();
                    inv[1].Itemstack.StackSize = stacksize;
                    inv[1].Itemstack.Collectible.SetTemperature(Api.World, inv[1].Itemstack, temp);
                }
                MarkDirty();

                msInUse.WalkMatchingBlocks(Api.World, posInUse, (block, pos) =>
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
                Api.World.BlockAccessor.GetBlockAbove(Pos, 1, BlockLayersAccess.Solid).FirstCodePart() == "stonecoffinlid" &&
                Api.World.BlockAccessor.GetBlockAbove(Pos.AddCopy(blockScs.Orientation.Opposite), 1, BlockLayersAccess.Solid).FirstCodePart() == "stonecoffinlid"
            ;
        }

        private void onClientTick50ms(float dt)
        {
            if (!receivesHeat) return;

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
                        particles.basePos.Set(pos.X + 0.5, pos.InternalY + 0.75, pos.Z + 0.5);
                        particles.Velocity[1].avg = (float)(0.3 + 0.3 * rnd.NextDouble()) * 2;
                        particles.PosOffset[1].var = 0.2f;
                        particles.Velocity[0].avg = (float)(rnd.NextDouble() - 0.5) / 4;
                        particles.Velocity[2].avg = (float)(rnd.NextDouble() - 0.5) / 4;

                    }
                    else
                    {
                        particles.Quantity.avg = GameMath.Sqrt(0.5f * (index == 0 ? 0.5f : (index == 1 ? 5 : 0.6f)))/2f;
                        particles.basePos.Set(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5);
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
            int coalLevel = inv[0]?.StackSize ?? 0;
            int ironLevel = inv[1]?.StackSize ?? 0;

            base.FromTreeAttributes(tree, worldAccessForResolve);

            receivesHeat = tree.GetBool("receivesHeat");
            totalHoursLastUpdate = tree.GetDouble("totalHoursLastUpdate");
            progress = tree.GetDouble("progress");
            processComplete = tree.GetBool("processComplete");
            StructureComplete = tree.GetBool("structureComplete");
            tempStoneCoffin = tree.GetInt("tempStoneCoffin");

            if (worldAccessForResolve.Api.Side == EnumAppSide.Client)
            {
                if (coalLevel != (inv[0]?.StackSize ?? 0) || ironLevel != (inv[1]?.StackSize ?? 0))
                {
                    ItemStack ingotStack = inv[1]?.Itemstack;
                    if (ingotStack != null && ingotStack.Collectible == null) ingotStack.ResolveBlockOrItem(worldAccessForResolve);
                    updateSelectiveElements();
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetBool("receivesHeat", receivesHeat);
            tree.SetDouble("totalHoursLastUpdate", totalHoursLastUpdate);
            tree.SetDouble("progress", progress);
            tree.SetBool("processComplete", processComplete);
            tree.SetBool("structureComplete", StructureComplete);
            tree.SetInt("tempStoneCoffin", tempStoneCoffin);
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Client)
            {
                msHighlighted?.ClearHighlights(Api.World, (Api as ICoreClientAPI).World.Player);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Client)
            {
                msHighlighted?.ClearHighlights(Api.World, (Api as ICoreClientAPI).World.Player);
            }
        }


        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
        {
            if (hasLid()) return false;  // no need to tesselate contents if covered

            Shape shape = capi.TesselatorManager.GetCachedShape(Block.Shape.Base);


            tessThreadTesselator.TesselateShape(Block, shape, out MeshData meshdataMain, null, null, selectiveElementsMain);
            tessThreadTesselator.TesselateShape(Block, shape, out MeshData meshdataSecondary, null, null, selectiveElementsSecondary);
            if (blockScs.Orientation == BlockFacing.EAST)
            {
                meshdataMain.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, -GameMath.PIHALF, 0);
                meshdataSecondary.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, -GameMath.PIHALF, 0);
            }

            meshdataSecondary.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, GameMath.PI, 0);
            meshdataSecondary.Translate(blockScs.Orientation.Opposite.Normalf);

            mesher.AddMeshData(meshdataMain);
            mesher.AddMeshData(meshdataSecondary);



            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (processComplete)
            {
                dsc.AppendLine(Lang.Get("Carburization process complete. Break to retrieve blister steel."));
                return;
            }

            if (IsFull) {
                if (!hasLid())
                {
                    dsc.AppendLine(Lang.Get("Stone coffin lid is missing"));
                } else
                {
                    if (!StructureComplete)
                    {
                        dsc.AppendLine(Lang.Get("Structure incomplete! Can't get hot enough, carburization paused."));
                        return;
                    }

                    if (receivesHeat)
                    {
                        dsc.AppendLine(Lang.Get("Okay! Receives heat!"));
                    } else
                    {
                        dsc.AppendLine(Lang.Get("Ready to be fired. Ignite a pile of coal below each stone coffin half."));
                    }

                }
            }

            if (progress > 0)
            {
                dsc.AppendLine(Lang.Get("Carburization: {0}% complete", (int)(progress * 100)));
            }

        }

    }

    public class InventoryStoneCoffin : InventoryGeneric
    {
        Vec3d secondaryPos;

        public InventoryStoneCoffin(int size, String invId, ICoreAPI api) : base(size, invId, api)
        {
        }

        public override void DropAll(Vec3d pos, int maxStackSize = 0)
        {
            foreach (var slot in this)
            {
                if (slot.Itemstack == null) continue;

                int count = slot.Itemstack.StackSize;
                if (count == 0) continue;

                int i = 0;
                while (i + 2 <= count)
                {
                    ItemStack newStack = slot.Itemstack.Clone();
                    newStack.StackSize = 1;
                    Api.World.SpawnItemEntity(newStack, pos);
                    Api.World.SpawnItemEntity(newStack.Clone(), secondaryPos);
                    i += 2;
                }
                if (i < count)
                {
                    ItemStack newStack = slot.Itemstack.Clone();
                    newStack.StackSize = 1;
                    Api.World.SpawnItemEntity(newStack, pos);
                }

                slot.Itemstack = null;
                slot.MarkDirty();
            }
        }

        internal void SetSecondaryPos(BlockPos blockPos)
        {
            secondaryPos = blockPos.ToVec3d();
        }
    }

}
