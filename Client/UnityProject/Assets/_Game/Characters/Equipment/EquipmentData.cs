using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Définition d'un équipement généré par code. Chaque type concret (casque, armure, gants, ...)
    /// hérite de cette classe, déclare son slot et fournit son propre mesh à partir des
    /// <see cref="BodyProportions"/> du porteur. Ajouter un nouvel équipement = ajouter une nouvelle
    /// sous-classe : ni <see cref="EquipmentManager"/> ni <see cref="OrcCharacter"/> n'ont besoin d'être
    /// modifiés.
    /// </summary>
    public abstract class EquipmentData : ScriptableObject
    {
        [SerializeField] private string displayName = "Équipement";
        [SerializeField] private Color color = Color.gray;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        public Color Color => color;

        /// <summary>
        /// Change la couleur à l'exécution (teinte par rareté, par exemple). Réservé aux instances créées par
        /// code (<see cref="ScriptableObject.CreateInstance{T}"/>) : ne pas appeler sur un asset du projet.
        /// </summary>
        public void SetColor(Color value) => color = value;

        /// <summary>Slot auquel cet équipement appartient (fixé par la sous-classe).</summary>
        public abstract EquipmentSlot Slot { get; }

        /// <summary>Construit le mesh de l'équipement, dimensionné d'après les proportions du corps porté.</summary>
        public abstract Mesh BuildMesh(BodyProportions proportions);

        /// <summary>Matériau de rendu (partagé/mis en cache, pas recréé à chaque équipement).</summary>
        public virtual Material BuildMaterial() =>
            CharacterMaterials.GetOrCreate($"{GetType().Name}_{ColorUtility.ToHtmlStringRGB(color)}", color);
    }
}
