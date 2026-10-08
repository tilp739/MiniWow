using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Pantalon : les deux jambes (au repos) réunies en un seul mesh, attaché au bassin (LegsSocket,
    /// lié au Torso). Limitation connue de ce prototype : comme les jambes se balancent indépendamment
    /// à la marche alors que le pantalon est une pièce rigide accrochée au bassin, le pantalon ne suit
    /// pas individuellement chaque jambe pendant l'animation de marche (il reste dans sa pose de repos).
    /// Une évolution future (séparer en deux sockets de jambe, ou "skinner" le mesh sur les deux jambes)
    /// corrigerait ce point sans changer l'architecture générale.
    /// </summary>
    [CreateAssetMenu(menuName = "MiniWow/Equipment/Pants", fileName = "NewPants")]
    public sealed class PantsEquipmentData : EquipmentData
    {
        public override EquipmentSlot Slot => EquipmentSlot.Legs;

        public override Mesh BuildMesh(BodyProportions proportions)
        {
            BodyLayout layout = BodyLayout.Compute(proportions);
            float widthScale = Mathf.Sqrt(layout.BodyDepth / 0.28f);

            Mesh leg = LowPolyMeshBuilder.Cylinder(
                radiusBottom: 0.1f * widthScale * 1.2f,
                radiusTop: 0.14f * widthScale * 1.25f,
                height: layout.LegLength * 1.05f,
                sides: 7,
                pivotAtTop: true);

            return LowPolyMeshBuilder.Combine(
                (leg, new Vector3(-layout.HipX, 0f, 0f)),
                (leg, new Vector3(layout.HipX, 0f, 0f)));
        }

        // Pas de BuildMaterial() dédié : on garde celui de la classe de base, piloté par Color.
    }
}
