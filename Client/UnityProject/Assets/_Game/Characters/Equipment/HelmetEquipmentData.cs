using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>Casque : un dôme low-poly + une visière, posé sur la tête (même pivot que Head : la nuque).</summary>
    [CreateAssetMenu(menuName = "MiniWow/Equipment/Helmet", fileName = "NewHelmet")]
    public sealed class HelmetEquipmentData : EquipmentData
    {
        public override EquipmentSlot Slot => EquipmentSlot.Head;

        public override Mesh BuildMesh(BodyProportions proportions)
        {
            BodyLayout layout = BodyLayout.Compute(proportions);
            float h = layout.HeadHeight;

            Mesh dome = LowPolyMeshBuilder.LowPolySphere(h * 0.36f, horizontalSegments: 8, verticalRings: 4);
            Mesh visor = LowPolyMeshBuilder.Box(new Vector3(0.5f * h, 0.12f * h, 0.16f * h));

            return LowPolyMeshBuilder.Combine(
                (dome, new Vector3(0f, 0.86f * h, 0f)),
                (visor, new Vector3(0f, 0.6f * h, 0.26f * h)));
        }

        // Pas de BuildMaterial() dédié : on garde celui de la classe de base, piloté par Color (teinte par
        // rareté côté jeu réel, personnalisable dans l'Inspector pour un asset authored).
    }
}
