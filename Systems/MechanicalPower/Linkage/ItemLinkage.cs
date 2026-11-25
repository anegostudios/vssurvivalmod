using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent.Mechanics;

namespace Vintagestory.GameContent
{
    /// <summary>
    /// The rod extends along positive X between the two pins.
    /// </summary>
    public class LinkageMeshProperties
    {
        public required Vec3f FirstPin;
        public required Vec3f SecondPin;
        public required Vec3f ShaftOrigin;
        public float ShaftLength;
        public required string[] FirstEndElements;
        public required string[] SecondEndElements;
        public required string[] ShaftElements;

        public float PinSpan => SecondPin.X - FirstPin.X;
    }

    public class ItemLinkage : Item
    {
        const float shapeUnitsPerBlock = 16f;

        ModSystemLinkage linkageSystem = null!;

        public CollectibleBehaviorMechanicalMaterial Material { get; private set; } = null!;

        public int PinSize { get; private set; }

        public float CollisionHalfHeight { get; private set; }
        public float CollisionHalfWidth { get; private set; }
        public LinkageMeshProperties MeshProperties { get; private set; } = null!;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Material = GetCollectibleBehavior<CollectibleBehaviorMechanicalMaterial>(true)
                       ?? throw new InvalidOperationException($"Linkage {Code} needs the MechanicalMaterial behavior.");

            var properties = Attributes["linkageProperties"][Variant["type"]];
            PinSize = properties["pinSize"].AsInt();
            float collisionHeight = properties["collisionHeight"].AsFloat();
            float collisionWidth = properties["collisionWidth"].AsFloat();
            if (PinSize <= 0 || collisionHeight <= 0 || collisionWidth <= 0)
            {
                throw new InvalidOperationException($"Invalid linkage properties for {Code}.");
            }

            CollisionHalfHeight = collisionHeight / shapeUnitsPerBlock / 2;
            CollisionHalfWidth = collisionWidth / shapeUnitsPerBlock / 2;
            LinkageMeshProperties? mesh = Attributes["linkageMesh"].AsObject<LinkageMeshProperties>();
            if (mesh?.FirstPin == null || mesh.SecondPin == null || mesh.ShaftOrigin == null
                || mesh.FirstEndElements?.Length is not > 0 || mesh.SecondEndElements?.Length is not > 0 || mesh.ShaftElements?.Length is not > 0
                || mesh.ShaftLength <= 0 || mesh.PinSpan <= 0 || mesh.FirstPin.Y != mesh.SecondPin.Y || mesh.FirstPin.Z != mesh.SecondPin.Z)
            {
                throw new InvalidOperationException($"Missing or invalid linkageMesh for {Code}.");
            }
            mesh.FirstPin /= shapeUnitsPerBlock;
            mesh.SecondPin /= shapeUnitsPerBlock;
            mesh.ShaftOrigin /= shapeUnitsPerBlock;
            mesh.ShaftLength /= shapeUnitsPerBlock;
            MeshProperties = mesh;

            linkageSystem = api.ModLoader.GetModSystem<ModSystemLinkage>()
                ?? throw new InvalidOperationException("The linkage system is not loaded?");
        }

        public bool CanAttachTo(ILinkageEndpoint endpoint, int point)
        {
            return endpoint.IsValidLinkagePoint(point) && endpoint.CanAttachLinkage(this, point);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel != null && (linkageSystem.IsPlacing(byEntity) || linkageSystem.IsLinkageEndpoint(blockSel)))
            {
                handling = EnumHandHandling.PreventDefault;
                linkageSystem.OnInteract(byEntity, blockSel, this);
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (linkageSystem.TryCancelPlace(byEntity))
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return ObjectCacheUtil.GetOrCreate(api, "linkageHeldInteractions", () =>
            {
                List<WorldInteraction> interactions = [.. base.GetHeldInteractionHelp(inSlot)];
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = "linkage-select-endpoints",
                    MouseButton = EnumMouseButton.Right
                });
                interactions.Add(new WorldInteraction
                {
                    ActionLangCode = "linkage-cancel",
                    MouseButton = EnumMouseButton.Left
                });
                return interactions.ToArray();
            });
        }
    }
}
