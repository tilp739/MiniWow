using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Gants : un seul mesh, légèrement plus grand que la main. L'EquipmentManager instancie ce même
    /// mesh pour la main gauche et la main droite (une boîte est symétrique, pas besoin de variante miroir).
    /// </summary>
    [CreateAssetMenu(menuName = "MiniWow/Equipment/Gloves", fileName = "NewGloves")]
    public sealed class GlovesEquipmentData : EquipmentData
    {
        public override EquipmentSlot Slot => EquipmentSlot.Hands;

        public override Mesh BuildMesh(BodyProportions proportions)
        {
            BodyLayout layout = BodyLayout.Compute(proportions);
            float s = layout.HandSize;
            return LowPolyMeshBuilder.Box(new Vector3(s * 1.25f, s * 0.95f, s * 1.4f), pivotAtTop: true);
        }

        // Pas de BuildMaterial() dédié : on garde celui de la classe de base, piloté par Color.
    }
}
