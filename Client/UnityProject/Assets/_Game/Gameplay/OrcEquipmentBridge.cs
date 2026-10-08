using System.Collections.Generic;
using MiniWow.Client.Characters;
using MiniWow.Client.Data;
using UnityEngine;

namespace MiniWow.Client.Gameplay
{
    /// <summary>
    /// Relie l'équipement de jeu (objets réels, serveur-autoritaire) à l'apparence cosmétique de l'Orc :
    /// quand un objet est équipé dans un des emplacements qui ont un équivalent visuel
    /// (Head/Chest/Hands/Legs/Feet/MainHand), une pièce générée de la forme correspondante, teintée selon la
    /// rareté de l'objet, apparaît sur le modèle. Les autres emplacements de jeu (Neck, Shoulder, Ranged, ...)
    /// n'ont pas encore d'équivalent cosmétique : aucun effet pour eux.
    ///
    /// Limitation connue : s'applique seulement au joueur local. Le serveur n'envoie l'inventaire qu'à son
    /// propriétaire (<c>SendInventory</c>) ; les autres joueurs ne voient donc pas encore l'équipement des uns
    /// des autres, qu'il soit Orc ou non. Étendre ça demanderait de diffuser l'équipement dans le snapshot du
    /// monde (comme la race l'est déjà via <c>EntityState.TemplateId</c>), ce qui est au-delà de ce passage.
    /// </summary>
    public static class OrcEquipmentBridge
    {
        // Index dans GameState.Equipment == valeur numérique de GameServer.Domain.Items.EquipSlot (fixe).
        private const int GameSlotHead = 0;
        private const int GameSlotChest = 1;
        private const int GameSlotHands = 2;
        private const int GameSlotLegs = 3;
        private const int GameSlotFeet = 4;
        private const int GameSlotMainHand = 5;

        private static readonly int[] TrackedGameSlots =
        {
            GameSlotHead, GameSlotChest, GameSlotHands, GameSlotLegs, GameSlotFeet, GameSlotMainHand
        };

        /// <summary>Clé = (emplacement cosmétique, rareté) ; une seule instance générée par combinaison, réutilisée.</summary>
        private static readonly Dictionary<(EquipmentSlot Slot, string Rarity), EquipmentData> Cache =
            new Dictionary<(EquipmentSlot, string), EquipmentData>();

        /// <summary>À appeler chaque fois que l'inventaire change (après <see cref="GameState.ApplyInventory"/>).</summary>
        public static void Apply(OrcCharacter orc, GameState game)
        {
            if (orc == null || orc.Equipment == null)
            {
                return; // Pas de modèle généré pour cette race (pas encore d'Orc), rien à faire.
            }

            foreach (int gameSlot in TrackedGameSlots)
            {
                EquipmentSlot cosmeticSlot = ToCosmeticSlot(gameSlot);
                ushort itemId = game.Equipment[gameSlot];

                if (itemId == 0 || !ContentCatalog.Items.TryGetValue(itemId, out ContentCatalog.ItemEntry item))
                {
                    orc.Unequip(cosmeticSlot);
                    continue;
                }

                orc.Equip(cosmeticSlot, GetOrCreateVisual(cosmeticSlot, item.rarity));
            }
        }

        private static EquipmentData GetOrCreateVisual(EquipmentSlot slot, string rarity)
        {
            string rarityKey = string.IsNullOrEmpty(rarity) ? "Common" : rarity;
            var cacheKey = (slot, rarityKey);

            if (Cache.TryGetValue(cacheKey, out EquipmentData existing) && existing != null)
            {
                return existing;
            }

            EquipmentData data = CreateVisual(slot);
            if (ColorUtility.TryParseHtmlString("#" + ContentCatalog.RarityColorHex(rarityKey), out Color color))
            {
                data.SetColor(color);
            }

            Cache[cacheKey] = data;
            return data;
        }

        private static EquipmentData CreateVisual(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Head => ScriptableObject.CreateInstance<HelmetEquipmentData>(),
            EquipmentSlot.Chest => ScriptableObject.CreateInstance<ArmorEquipmentData>(),
            EquipmentSlot.Hands => ScriptableObject.CreateInstance<GlovesEquipmentData>(),
            EquipmentSlot.Legs => ScriptableObject.CreateInstance<PantsEquipmentData>(),
            EquipmentSlot.Feet => ScriptableObject.CreateInstance<BootsEquipmentData>(),
            EquipmentSlot.MainHand => ScriptableObject.CreateInstance<WeaponEquipmentData>(),
            _ => ScriptableObject.CreateInstance<HelmetEquipmentData>(),
        };

        private static EquipmentSlot ToCosmeticSlot(int gameSlot) => gameSlot switch
        {
            GameSlotHead => EquipmentSlot.Head,
            GameSlotChest => EquipmentSlot.Chest,
            GameSlotHands => EquipmentSlot.Hands,
            GameSlotLegs => EquipmentSlot.Legs,
            GameSlotFeet => EquipmentSlot.Feet,
            GameSlotMainHand => EquipmentSlot.MainHand,
            _ => EquipmentSlot.Head,
        };
    }
}
