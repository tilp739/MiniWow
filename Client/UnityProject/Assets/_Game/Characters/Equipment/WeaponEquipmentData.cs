using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Arme à une main : poignée + garde + lame, pivot à la garde (même convention que la main : pivot en
    /// haut, la poignée plonge dans le poing, la lame remonte). Toute l'arme est teintée par la rareté
    /// (simplification : pas de distinction lame/poignée).
    /// </summary>
    [CreateAssetMenu(menuName = "MiniWow/Equipment/Weapon", fileName = "NewWeapon")]
    public sealed class WeaponEquipmentData : EquipmentData
    {
        public override EquipmentSlot Slot => EquipmentSlot.MainHand;

        public override Mesh BuildMesh(BodyProportions proportions)
        {
            BodyLayout layout = BodyLayout.Compute(proportions);
            float scale = layout.HandSize / 0.2f; // ~1 pour un gabarit standard, suit la carrure du porteur

            Mesh grip = LowPolyMeshBuilder.Cylinder(0.025f * scale, 0.025f * scale, 0.16f * scale, 6, pivotAtTop: true);
            Mesh guard = LowPolyMeshBuilder.Box(new Vector3(0.14f * scale, 0.02f * scale, 0.05f * scale));
            Mesh blade = LowPolyMeshBuilder.TaperedBox(
                bottomSize: new Vector3(0.06f * scale, 0f, 0.025f * scale),
                topSize: new Vector3(0.01f * scale, 0f, 0.015f * scale),
                height: 0.55f * scale);

            return LowPolyMeshBuilder.Combine(
                (grip, Vector3.zero, Quaternion.identity, Vector3.one),
                (guard, Vector3.zero, Quaternion.identity, Vector3.one),
                (blade, Vector3.zero, Quaternion.identity, Vector3.one));
        }

        // Pas de BuildMaterial() dédié : on garde celui de la classe de base, piloté par Color.
    }
}
