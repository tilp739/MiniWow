namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Emplacements d'équipement d'un personnage. Ajouter un nouveau slot (Weapon, Shield, Cape, ...)
    /// ne nécessite de modifier ni EquipmentManager ni OrcCharacter : seules la génération du squelette
    /// (sockets) et les classes EquipmentData concernées doivent suivre.
    /// </summary>
    public enum EquipmentSlot
    {
        Head,
        Chest,
        Hands,
        Legs,
        Feet,
        MainHand
    }
}
