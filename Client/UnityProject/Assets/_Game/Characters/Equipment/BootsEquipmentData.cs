using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>Bottes : une chaussure + une tige qui remonte sur le mollet, même pivot que Foot (la cheville).</summary>
    [CreateAssetMenu(menuName = "MiniWow/Equipment/Boots", fileName = "NewBoots")]
    public sealed class BootsEquipmentData : EquipmentData
    {
        public override EquipmentSlot Slot => EquipmentSlot.Feet;

        public override Mesh BuildMesh(BodyProportions proportions)
        {
            BodyLayout layout = BodyLayout.Compute(proportions);
            float width = layout.HandSize * 1.1f;

            Mesh shoe = LowPolyMeshBuilder.Box(new Vector3(width, layout.FootHeight * 1.3f, layout.FootLength * 1.3f), pivotAtTop: true);
            Mesh cuff = LowPolyMeshBuilder.Box(new Vector3(width * 0.95f, layout.LegLength * 0.22f, width * 0.95f));

            return LowPolyMeshBuilder.Combine(
                (shoe, new Vector3(0f, 0f, layout.FootLength * 0.08f)),
                (cuff, Vector3.zero));
        }

        // Pas de BuildMaterial() dédié : on garde celui de la classe de base, piloté par Color.
    }
}
