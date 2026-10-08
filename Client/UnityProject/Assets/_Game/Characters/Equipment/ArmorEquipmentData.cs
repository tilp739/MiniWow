using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>Plastron : un torse légèrement agrandi + deux épaulières, même pivot que Torso (le bassin).</summary>
    [CreateAssetMenu(menuName = "MiniWow/Equipment/Armor", fileName = "NewArmor")]
    public sealed class ArmorEquipmentData : EquipmentData
    {
        public override EquipmentSlot Slot => EquipmentSlot.Chest;

        public override Mesh BuildMesh(BodyProportions proportions)
        {
            BodyLayout layout = BodyLayout.Compute(proportions);

            float hipWidth = layout.HipX * 2f * 0.85f * 1.18f;
            float shoulderWidth = layout.ShoulderX * 2f * 0.95f * 1.15f;

            Mesh chest = LowPolyMeshBuilder.TaperedBox(
                bottomSize: new Vector3(hipWidth, 0f, layout.BodyDepth * 1.25f),
                topSize: new Vector3(shoulderWidth, 0f, layout.BodyDepth * 1.3f),
                height: layout.TorsoHeight * 1.03f);

            Mesh pad = LowPolyMeshBuilder.Box(new Vector3(0.22f * shoulderWidth, 0.14f * layout.TorsoHeight, 0.3f * layout.BodyDepth));

            return LowPolyMeshBuilder.Combine(
                (chest, Vector3.zero, Quaternion.identity, Vector3.one),
                (pad, new Vector3(-shoulderWidth * 0.5f, layout.TorsoHeight * 0.92f, 0f), Quaternion.identity, Vector3.one),
                (pad, new Vector3(shoulderWidth * 0.5f, layout.TorsoHeight * 0.92f, 0f), Quaternion.identity, Vector3.one));
        }

        // Pas de BuildMaterial() dédié : on garde celui de la classe de base, piloté par Color.
    }
}
