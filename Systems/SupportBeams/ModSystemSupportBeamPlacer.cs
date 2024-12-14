using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class ModSystemSupportBeamPlacer : ModSystem, IRenderer
    {
        public override bool ShouldLoad(EnumAppSide forSide) => true;

        protected Dictionary<string, MeshData[]> origBeamMeshes = new Dictionary<string, MeshData[]>();
        Dictionary<string, BeamPlacerWorkSpace> workspaceByPlayer = new Dictionary<string, BeamPlacerWorkSpace>();
        
        ICoreAPI api;
        ICoreClientAPI capi;
        public Matrixf ModelMat = new Matrixf();

        public double RenderOrder => 0.5;
        public int RenderRange => 12;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "beamplacer");
        }


        public bool CancelPlace(BlockSupportBeam blockSupportBeam, EntityAgent byEntity)
        {
            var ws = getWorkSpace(byEntity);
            if (ws.nowBuilding)
            {
                ws.nowBuilding = false;
                ws.startOffset = null;
                ws.endOffset = null;
                return true;
            }

            return false;
        }

        public Vec3f snapToGrid(IVec3 pos, int gridSize)
        {
            var gs = gridSize;
            return new Vec3f(
                (int)Math.Round(pos.XAsFloat * gs) / (float)gs,
                (int)Math.Round(pos.YAsFloat * gs) / (float)gs,
                (int)Math.Round(pos.ZAsFloat * gs) / (float)gs
            );
        }

        public void OnInteract(Block block, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, bool partialEnds)
        {
            if (blockSel == null)
            {
                return;
            }

            var ws = getWorkSpace(byEntity);

            if (!ws.nowBuilding)
            {
                beginPlace(ws, block, byEntity, blockSel, partialEnds);
            } else
            {
                completePlace(ws, byEntity, slot);
            }
        }


        private void beginPlace(BeamPlacerWorkSpace ws, Block block, EntityAgent byEntity, BlockSelection blockSel, bool partialEnds)
        {
            ws.GridSize = byEntity.Controls.CtrlKey ? 16 : 4;

            ws.currentMeshes = getOrCreateBeamMeshes(block, (block as BlockSupportBeam)?.PartialEnds ?? false);

            var be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position);
            var beh = be?.GetBehavior<BEBehaviorSupportBeam>();
            if (beh == null)
            {
                var startPosBlock = api.World.BlockAccessor.GetBlock(blockSel.Position);
                if (startPosBlock.Replaceable >= 6000)
                {
                    ws.startPos = blockSel.Position.Copy();
                    ws.startOffset = snapToGrid(blockSel.HitPosition, ws.GridSize);
                }
                else
                {
                    var blockSelFace = blockSel.Face;
                    var wsStartPos = blockSel.Position.AddCopy(blockSelFace);
                    startPosBlock = api.World.BlockAccessor.GetBlock(wsStartPos);
                    be = api.World.BlockAccessor.GetBlockEntity(wsStartPos);
                    beh = be?.GetBehavior<BEBehaviorSupportBeam>();

                    if (beh == null && startPosBlock.Replaceable < 6000)
                    {
                        (api as ICoreClientAPI)?.TriggerIngameError(this, "notplaceablehere", Lang.Get("Cannot place here, a block is in the way"));
                        return;
                    }
                    ws.startPos = wsStartPos;
                    ws.startOffset = snapToGrid(blockSel.HitPosition, ws.GridSize).Sub(blockSel.Face.Normali);
                }
            }
            else
            {
                ws.startPos = blockSel.Position.Copy();
                ws.startOffset = snapToGrid(blockSel.HitPosition, ws.GridSize);
            }

            ws.endOffset = null;
            ws.nowBuilding = true;
            ws.block = block;
            ws.onFacing = blockSel.Face;
        }


        private void completePlace(BeamPlacerWorkSpace ws, EntityAgent byEntity, ItemSlot slot)
        {
            ws.nowBuilding = false;
            var be = api.World.BlockAccessor.GetBlockEntity(ws.startPos);
            var beh = be?.GetBehavior<BEBehaviorSupportBeam>();

            var eplr = (byEntity as EntityPlayer);

            Vec3f nowEndOffset = getEndOffset(eplr.Player, ws);

            if (nowEndOffset.DistanceTo(ws.startOffset) < 0.01f)
            {

                return;
            }

            if (beh == null)
            {
                var hereBlock = api.World.BlockAccessor.GetBlock(ws.startPos);
                if (hereBlock.Replaceable < 6000)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "notplaceablehere", Lang.Get("Cannot place here, a block is in the way"));
                    return;
                }

                var player = (byEntity as EntityPlayer)?.Player;
                if (!api.World.Claims.TryAccess(player, ws.startPos, EnumBlockAccessFlags.BuildOrBreak))
                {
                    player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    return;
                }

                if (eplr.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    int len = (int)Math.Ceiling(nowEndOffset.DistanceTo(ws.startOffset));
                    if (slot.StackSize < len)
                    {
                        (api as ICoreClientAPI)?.TriggerIngameError(this, "notenoughitems",
                            Lang.Get("You need {0} beams to place a beam at this lenth", len));
                        return;
                    }
                }

                api.World.BlockAccessor.SetBlock(ws.block.Id, ws.startPos);
                be = api.World.BlockAccessor.GetBlockEntity(ws.startPos);
                beh = be?.GetBehavior<BEBehaviorSupportBeam>();
            }

            if (eplr.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                int len = (int)Math.Ceiling(nowEndOffset.DistanceTo(ws.startOffset));
                if (slot.StackSize < len)
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "notenoughitems", Lang.Get("You need {0} beams to place a beam at this lenth", len));
                    return;
                }
                slot.TakeOut(len);
                slot.MarkDirty();
            }

            beh.AddBeam(ws.startOffset, nowEndOffset, ws.onFacing, ws.block);
            be.MarkDirty(true);
        }


        public MeshData[] getOrCreateBeamMeshes(Block block, bool partialEnds, ITexPositionSource texSource = null, string texSourceKey = null)
        {
            if (capi == null) return null;

            if (texSource != null)
            {
                capi.Tesselator.TesselateShape(texSourceKey, capi.TesselatorManager.GetCachedShape(block.Shape.Base), out var cmeshData, texSource);
                return new MeshData[] { cmeshData };
            }

            string key = block.Code + "-" + partialEnds;
            MeshData[] meshdatas;

            if (!origBeamMeshes.TryGetValue(key, out meshdatas))
            {
                if (partialEnds)
                {
                    origBeamMeshes[key] = meshdatas = new MeshData[4];
                    for (int i = 0; i < 4; i++)
                    {
                        var loc = block.Shape.Base.Clone().WithFilename("" + ((i+1) * 4));
                        var shape = capi.Assets.Get(loc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
                        capi.Tesselator.TesselateShape(block, shape, out var meshData);
                        meshdatas[i] = meshData;
                    }
                } else
                {
                    origBeamMeshes[key] = meshdatas = new MeshData[1];
                    capi.Tesselator.TesselateShape(block, capi.TesselatorManager.GetCachedShape(block.Shape.Base), out var meshData);
                    meshdatas[0] = meshData;
                }
            }

            return meshdatas;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            var ws = getWorkSpace(capi.World.Player.PlayerUID);

            if (!ws.nowBuilding) return;

            Vec3f nowEndOffset = getEndOffset(capi.World.Player, ws);

            if (ws.startOffset.DistanceTo(nowEndOffset) < 0.1) return;

            if (ws.endOffset != nowEndOffset)
            {
                ws.endOffset = nowEndOffset;
                reloadMeshRef();
            }
            if (ws.currentMeshRef == null) return;


            var prevprog = capi.Render.CurrentActiveShader;
            prevprog?.Stop();
            var prog = capi.Render.PreparedStandardShader(ws.startPos.X, ws.startPos.InternalY, ws.startPos.Z);
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            prog.Use();
            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(ws.startPos.X - camPos.X, ws.startPos.InternalY - camPos.Y, ws.startPos.Z - camPos.Z)
                .Values
            ;

            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

            capi.Render.RenderMultiTextureMesh(ws.currentMeshRef, "tex");

            prog.Stop();
            prevprog?.Use();
        }


        protected Vec3f getEndOffset(IPlayer player, BeamPlacerWorkSpace ws)
        {
            Vec3f nowEndOffset;
            Vec3d vec;
            
            if (player.CurrentBlockSelection != null)
            {
                var blockSel = player.CurrentBlockSelection;
                vec = blockSel.Position.ToVec3d().Sub(ws.startPos).Add(blockSel.HitPosition);
            }
            else
            {
                vec = player.Entity.SidedPos.AheadCopy(2).XYZ.Add(player.Entity.LocalEyePos).Sub(ws.startPos);
            }

            nowEndOffset = snapToGrid(vec, ws.GridSize);


            double dX = nowEndOffset.X - ws.startOffset.X;
            double dY = nowEndOffset.Y - ws.startOffset.Y;
            double dZ = nowEndOffset.Z - ws.startOffset.Z;
            double len = Math.Sqrt(dZ * dZ + dY * dY + dX * dX);
            double horlen = Math.Sqrt(dZ * dZ + dX * dX);

            float yaw = -GameMath.PIHALF - (float)Math.Atan2(-dX, -dZ);
            float pitch = (float)Math.Atan2(horlen, dY);

            if (player.Entity.Controls.ShiftKey)
            {
                float rotSnap = 15f;
                yaw = (float)Math.Round(yaw * GameMath.RAD2DEG / rotSnap) * (float)rotSnap * GameMath.DEG2RAD;
                pitch = (float)Math.Round(pitch * GameMath.RAD2DEG / rotSnap) * (float)rotSnap * GameMath.DEG2RAD;
            }

            double cosYaw = Math.Cos(yaw);
            double sinYaw = Math.Sin(yaw);
            double cosPitch = Math.Cos(pitch);
            double sinPitch = Math.Sin(pitch);

            len = Math.Min(len, 20);

            nowEndOffset = new Vec3f(
                ws.startOffset.X + (float)(len * sinPitch * cosYaw),
                ws.startOffset.Y + (float)(len * cosPitch),
                ws.startOffset.Z + (float)(len * sinPitch * sinYaw)
            );

            return nowEndOffset;
        }

        private void reloadMeshRef()
        {
            var ws = getWorkSpace(capi.World.Player.PlayerUID);
            ws.currentMeshRef?.Dispose();

            var mesh = generateMesh(ws.startOffset, ws.endOffset, ws.onFacing, ws.currentMeshes, ws.block.Attributes?["slumpPerMeter"].AsFloat(0) ?? 0);
            ws.currentMeshRef = capi.Render.UploadMultiTextureMesh(mesh);
        }

        public static MeshData generateMesh(Vec3f start, Vec3f end, BlockFacing facing, MeshData[] origMeshes, float slumpPerMeter)
        {
            var outMesh = new MeshData(4, 6).WithRenderpasses().WithXyzFaces().WithColorMaps();

            float[] mat = new float[16];

            double dX = end.X - start.X;
            double dY = end.Y - start.Y;
            double dZ = end.Z - start.Z;
            double len = Math.Sqrt(dZ * dZ + dY * dY + dX * dX);
            double horlen = Math.Sqrt(dZ * dZ + dX * dX);

            var normalize = 1 / Math.Max(1, len);
            var dir = new Vec3f((float)(dX * normalize), (float)(dY * normalize), (float)(dZ * normalize));
            float yaw = (float)Math.Atan2(-dX, -dZ) + GameMath.PIHALF;
            float basepitch = (float)Math.Atan2(horlen, -dY) + GameMath.PIHALF;

            // Sin and Cos are both 1 at 45 deg.
            float yawExtend = Math.Abs((float)(Math.Sin(yaw) * Math.Cos(yaw)));
            float pitchExtend = Math.Abs((float)(Math.Sin(basepitch) * Math.Cos(basepitch)));

            float distTo45Deg = Math.Max(yawExtend, pitchExtend);
            float extend = 1 / 16f * distTo45Deg * 4;

            float slump = 0;
            len += extend;
            for (float r = -extend; r < len; r++)
            {
                double sectionLen = Math.Min(1, len - r);
                if (sectionLen < 0.01) continue;

                var sectionStart = start + r * dir;

                var distance = (float)(r - len / 2);
                float pitch = basepitch + distance * slumpPerMeter;

                slump += (float)Math.Sin(distance * slumpPerMeter);

                if (origMeshes.Length > 1 && len < 18 / 16f) { sectionLen = len; r += 1; }

                // 4 sections
                // 4 voxels long: Choose for len until 6 voxels. 6/16 = 0.375   => all until 0.375*4 has to round to 0. Remove to voxels -> 0.375 becomes 0.25. 0.25*4 => 1. 
                // 8 voxels long: Choose for len until 10 voxels
                // 12 voxels long: Choose for len until 14 voxels
                // 16 voxels long: above 14
                int index = GameMath.Clamp((int)Math.Round((sectionLen - 4 / 16f) * origMeshes.Length), 0, origMeshes.Length - 1);

                float modelLen = (index+1) / 4f;
                float xscale = origMeshes.Length == 1 ? (float)(sectionLen) : ((float)sectionLen / modelLen);

                Mat4f.Identity(mat);
                Mat4f.Translate(mat, mat, sectionStart.X, sectionStart.Y + slump, sectionStart.Z);
                Mat4f.RotateY(mat, mat, yaw);
                Mat4f.RotateZ(mat, mat, pitch);
                Mat4f.Scale(mat, mat, new float[] { xscale, 1, 1 });
                Mat4f.Translate(mat, mat, -1f, -0.125f, -0.5f);

                
                var mesh = origMeshes[index].Clone();
                mesh.MatrixTransform(mat);

                outMesh.AddMeshData(mesh);
            }

            return outMesh;
        }

        public static float[] GetAlignMatrix(IVec3 startPos, IVec3 endPos, BlockFacing facing)
        {
            double dX = startPos.XAsDouble - endPos.XAsDouble;
            double dY = startPos.YAsDouble - endPos.YAsDouble;
            double dZ = startPos.ZAsDouble - endPos.ZAsDouble;
            double len = Math.Sqrt(dZ * dZ + dY * dY + dX * dX);
            double horlen = Math.Sqrt(dZ * dZ + dX * dX);

            float yaw = (float)Math.Atan2(dX, dZ) + GameMath.PIHALF;
            float pitch = (float)Math.Atan2(horlen, dY) + GameMath.PIHALF;

            float[] mat = new float[16];
            Mat4f.Identity(mat);
            Mat4f.Translate(mat, mat, (float)startPos.XAsDouble, (float)startPos.YAsDouble, (float)startPos.ZAsDouble);
            Mat4f.RotateY(mat, mat, yaw);
            Mat4f.RotateZ(mat, mat, pitch);
            Mat4f.Scale(mat, mat, new float[] { (float)len, 1, 1 });
            Mat4f.Translate(mat, mat, -1f, -0.125f, -0.5f);
            return mat;
        }




        BeamPlacerWorkSpace getWorkSpace(EntityAgent forEntity)
        {
            return getWorkSpace((forEntity as EntityPlayer)?.PlayerUID);
        }

        private BeamPlacerWorkSpace getWorkSpace(string playerUID)
        {
            if (workspaceByPlayer.TryGetValue(playerUID, out var ws))
            {
                return ws;
            }
            return workspaceByPlayer[playerUID] = new BeamPlacerWorkSpace();
        }

        public void OnBeamRemoved(Vec3d start, Vec3d end)
        {
            var startend = new StartEnd() { Start=start, End=end };
            chunkremove(start.AsBlockPos, startend);
            chunkremove(end.AsBlockPos, startend);
        }

        public void OnBeamAdded(Vec3d start, Vec3d end)
        {
            var startend = new StartEnd() { Start = start, End = end };
            chunkadd(start.AsBlockPos, startend);
            chunkadd(end.AsBlockPos, startend);
        }

        private void chunkadd(BlockPos blockpos, StartEnd startend)
        {
            var sbdata = GetSbData(blockpos);
            sbdata?.Beams.Add(startend);
        }

        private void chunkremove(BlockPos blockpos, StartEnd startend)
        {
            var sbdata = GetSbData(blockpos);
            sbdata?.Beams.Remove(startend);
        }

        public double GetStableMostBeam(BlockPos blockpos, out StartEnd beamstartend)
        {
            var sbdata = GetSbData(blockpos);
            if (sbdata.Beams == null || sbdata.Beams.Count == 0)
            {
                beamstartend = null;
                return 99999;
            }

            double minDistance = 99999;
            StartEnd nearestBeam = null;

            Vec3d point = blockpos.ToVec3d();
            foreach (var beam in sbdata.Beams)
            {
                double len = (beam.Start - beam.End).Length();
                bool mostlyVertical = len * 1.5 < Math.Abs(beam.End.Y - beam.Start.Y);

                bool stable = mostlyVertical ? (isBeamStableAt(beam.Start) || isBeamStableAt(beam.End)) : (isBeamStableAt(beam.Start) && isBeamStableAt(beam.End));
                if (!stable) continue;

                double dist = DistanceToLine(point, beam.Start, beam.End);
                if (dist < minDistance) minDistance = dist;
            }

            beamstartend = nearestBeam;
            return minDistance;
        }

        static double DistanceToLine(Vec3d point, Vec3d start, Vec3d end)
        {
            Vec3d bc = end - start;
            double length = bc.Length();
            double param = 0.0;
            if (length != 0.0) param = Math.Clamp((point - start).Dot(bc) / (length * length), 0.0, 1.0);
            return point.DistanceTo(start + bc * param);
        }

        private bool isBeamStableAt(Vec3d start)
        {
            return 
                BlockBehaviorUnstableRock.getVerticalSupportStrength(api.World, start.AsBlockPos) > 0 ||
                BlockBehaviorUnstableRock.getVerticalSupportStrength(api.World, start.Add(-1/16.0, 0, -1 / 16.0).AsBlockPos) > 0 ||
                BlockBehaviorUnstableRock.getVerticalSupportStrength(api.World, start.Add(1 / 16.0, 0, 1 / 16.0).AsBlockPos) > 0
            ;
        }

        public SupportBeamsData GetSbData(BlockPos pos)
        {
            const int chunksize = GlobalConstants.ChunkSize;
            return GetSbData(pos.X / chunksize, pos.Y / chunksize, pos.Z / chunksize);
        }

        public SupportBeamsData GetSbData(int chunkx, int chunky, int chunkz)
        {
            var chunk = api.World.BlockAccessor.GetChunk(chunkx, chunky, chunkz);
            if (chunk == null) return null;

            SupportBeamsData sbdata;
            if (chunk.LiveModData.TryGetValue("supportbeams", out var data))
            {
                sbdata = (SupportBeamsData)data;
            }
            else
            {
                chunk.LiveModData["supportbeams"] = sbdata = chunk.GetModdata<SupportBeamsData>("supportbeams");
            }

            if (sbdata == null)
            {
                chunk.LiveModData["supportbeams"] = sbdata = new SupportBeamsData();
            }

            return sbdata;
        }

    }

    [ProtoContract]
    public class SupportBeamsData
    {
        [ProtoMember(1)]
        public HashSet<StartEnd> Beams = new HashSet<StartEnd>();
    }

    [ProtoContract]
    public class StartEnd : IEquatable<StartEnd>
    {
        [ProtoMember(1)]
        public Vec3d Start;
        [ProtoMember(2)]
        public Vec3d End;

        public bool Equals(StartEnd other)
        {
            return other != null && Start.Equals(other.Start) && End.Equals(other.End);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StartEnd);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Start, End);
        }
    }
}
 