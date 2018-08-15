using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskMeleeAttack : AiTaskBase, IWorldIntersectionSupplier
    {
        IEntity targetEntity;

        long lastCheckOrAttackMs;

        float damage = 2f;
        float minHorDist = 1.5f;
        float minVerDist = 1f;


        bool damageInflicted = false;

        int attackDurationMs = 1500;
        int damagePlayerAtMs = 500;

        BlockSelection blockSel = new BlockSelection();
        EntitySelection entitySel = new EntitySelection();

        string[] seekEntityCodesExact = new string[] { "player" };
        string[] seekEntityCodesBeginsWith = new string[0];



        public Vec3i MapSize { get { return entity.World.BlockAccessor.MapSize; } }

        public AiTaskMeleeAttack(EntityAgent entity) : base(entity)
        {            
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            this.damage = taskConfig["damage"].AsFloat(2);
            this.attackDurationMs = taskConfig["attackDurationMs"].AsInt(1500);
            this.damagePlayerAtMs = taskConfig["damagePlayerAtMs"].AsInt(1000);

            this.minHorDist = taskConfig["minHorDist"].AsFloat(1.5f);
            this.minVerDist = taskConfig["minVerDist"].AsFloat(1f);

            if (taskConfig["entityCodes"] != null)
            {
                string[] codes = taskConfig["entityCodes"].AsStringArray(new string[] { "player" });

                List<string> exact = new List<string>();
                List<string> beginswith = new List<string>();

                for (int i = 0; i < codes.Length; i++)
                {
                    string code = codes[i];
                    if (code.EndsWith("*")) beginswith.Add(code.Substring(0, code.Length - 1));
                    else exact.Add(code);
                }

                seekEntityCodesExact = exact.ToArray();
                seekEntityCodesBeginsWith = beginswith.ToArray();
            }
        }

        public override bool ShouldExecute()
        {
            long ellapsedMs = entity.World.ElapsedMilliseconds;
            if (ellapsedMs - lastCheckOrAttackMs < attackDurationMs || cooldownUntilMs > ellapsedMs)
            {
                return false;
            }
            if (whenInEmotionState != null && !entity.HasEmotionState(whenInEmotionState)) return false;
            if (whenNotInEmotionState != null && entity.HasEmotionState(whenNotInEmotionState)) return false;

            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead((entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2, 0, entity.ServerPos.Yaw);


            targetEntity = entity.World.GetNearestEntity(pos, 3f, 3f, (e) => {
                if (!e.Alive || !e.IsInteractable || e.Type == null || e.Entityid == this.entity.Entityid) return false;

                for (int i = 0; i < seekEntityCodesExact.Length; i++)
                {
                    if (e.Type.Code.Path == seekEntityCodesExact[i])
                    {
                        if (e.Type.Code.Path == "player")
                        {
                            IPlayer player = entity.World.PlayerByUid(((EntityPlayer)e).PlayerUID);
                            return player == null || player.WorldData.CurrentGameMode != EnumGameMode.Creative;
                        }
                        return true;
                    }
                }


                for (int i = 0; i < seekEntityCodesBeginsWith.Length; i++)
                {
                    if (e.Type.Code.Path.StartsWith(seekEntityCodesBeginsWith[i]))
                    {
                        if (e.Type.Code.Path == "player")
                        {
                            IPlayer player = entity.World.PlayerByUid(((EntityPlayer)e).PlayerUID);
                            return player == null || player.WorldData.CurrentGameMode != EnumGameMode.Creative;
                        }

                        return true;
                    }
                }

                return false;
            });

            lastCheckOrAttackMs = entity.World.ElapsedMilliseconds;
            damageInflicted = false;

            return targetEntity != null;
        }



        public override bool ContinueExecute(float dt)
        {
            EntityPos own = entity.ServerPos;
            EntityPos his = targetEntity.ServerPos;
            entity.ServerPos.Yaw = (float)(float)Math.Atan2(his.X - own.X, his.Z - own.Z);

            if (lastCheckOrAttackMs + damagePlayerAtMs > entity.World.ElapsedMilliseconds) return true;

            if (!damageInflicted)
            {
                Cuboidd targetBox = targetEntity.CollisionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
                Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead((entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2, 0, entity.ServerPos.Yaw);
                double dist = targetBox.ShortestDistanceFrom(pos);
                double vertDist = Math.Abs(targetBox.ShortestVerticalDistanceFrom(pos.Y));
                if (dist >= 2 || vertDist >= 1) return false;

                Vec3d rayTraceFrom = entity.ServerPos.XYZ;
                rayTraceFrom.Y += 1 / 32f;
                Vec3d rayTraceTo = targetEntity.ServerPos.XYZ;
                rayTraceTo.Y += 1 / 32f;
                bool directContact = false;

                entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
                directContact = blockSel == null;

                if (!directContact)
                {
                    rayTraceFrom.Y += entity.CollisionBox.Y2 * 7/16f;
                    rayTraceTo.Y += targetEntity.CollisionBox.Y2 * 7 / 16f;
                    entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
                    directContact = blockSel == null;
                }

                if (!directContact)
                {
                    rayTraceFrom.Y += entity.CollisionBox.Y2 * 7 / 16f;
                    rayTraceTo.Y += targetEntity.CollisionBox.Y2 * 7 / 16f;
                    entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
                    directContact = blockSel == null;
                }

                if (!directContact) return false;

                bool alive = targetEntity.Alive;

                ((EntityAgent)targetEntity).ReceiveDamage(
                    new DamageSource() { source = EnumDamageSource.Entity, sourceEntity = entity, type = EnumDamageType.BluntAttack },
                    damage
                );

                if (alive && !targetEntity.Alive)
                {
                    this.entity.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState("saturated");
                }

                damageInflicted = true;
            }

            if (lastCheckOrAttackMs + attackDurationMs > entity.World.ElapsedMilliseconds) return true;
            return false;
        }


        public Block GetBlock(BlockPos pos)
        {
            return entity.World.BlockAccessor.GetBlock(pos);
        }

        public Cuboidf[] GetBlockIntersectionBoxes(BlockPos pos)
        {
            return entity.World.BlockAccessor.GetBlock(pos).GetCollisionBoxes(entity.World.BlockAccessor, pos);
        }

        public bool IsValidPos(BlockPos pos)
        {
            return entity.World.BlockAccessor.IsValidPos(pos);
        }
    }
}