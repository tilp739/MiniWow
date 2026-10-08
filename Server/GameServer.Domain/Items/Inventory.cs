using System;
using System.Collections.Generic;
using GameServer.Domain.Stats;

namespace GameServer.Domain.Items
{
    /// <summary>
    /// Emplacements d'équipement, alignés sur les emplacements classiques d'un MMORPG (même disposition que WoW).
    /// Les valeurs numériques des emplacements déjà utilisés ne changent jamais (personnages déjà sauvegardés) ;
    /// les nouveaux emplacements sont ajoutés à la suite, jamais insérés entre deux valeurs existantes.
    /// </summary>
    public enum EquipSlot : byte
    {
        Head = 0,
        Chest = 1,
        Hands = 2,
        Legs = 3,
        Feet = 4,
        MainHand = 5,
        OffHand = 6,
        Finger1 = 7,
        Finger2 = 8,
        Neck = 9,
        Shoulder = 10,
        Back = 11,
        Wrist = 12,
        Waist = 13,
        Trinket1 = 14,
        Trinket2 = 15,
        Ranged = 16,
        Shirt = 17,
        Tabard = 18,
    }

    /// <summary>
    /// Rareté d'un objet : purement cosmétique (couleur du nom côté client), aucun effet sur les statistiques.
    /// Mêmes paliers qu'un MMORPG classique.
    /// </summary>
    public enum ItemRarity : byte
    {
        Poor = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
    }

    /// <summary>Définition d'un objet, chargée depuis <c>Content/items.json</c>.</summary>
    public sealed class ItemDefinition
    {
        public ushort Id { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int MaxStack { get; set; } = 1;

        /// <summary>Emplacement d'équipement, ou null si l'objet ne se porte pas.</summary>
        public EquipSlot? Slot { get; set; }

        public ItemRarity Rarity { get; set; } = ItemRarity.Common;

        public int Armor { get; set; }

        public int Strength { get; set; }

        public int Agility { get; set; }

        public int Intelligence { get; set; }

        public int Stamina { get; set; }

        public int Spirit { get; set; }

        public int SellPrice { get; set; }
    }

    /// <summary>Pile d'objets dans un emplacement. Quantité toujours dans [1, MaxStack].</summary>
    public sealed class ItemStack
    {
        public ItemStack(ItemDefinition item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }

        public ItemDefinition Item { get; }

        public int Quantity { get; set; }
    }

    /// <summary>
    /// Sac et équipement d'un joueur. Toutes les opérations sont atomiques : elles réussissent entièrement
    /// ou ne modifient rien. Tout passe par le thread de simulation : pas de course, donc pas de duplication.
    /// </summary>
    public sealed class Inventory
    {
        public const int BagSize = 24;

        private readonly ItemStack?[] _bag = new ItemStack?[BagSize];
        private readonly Dictionary<EquipSlot, ItemStack> _equipment = new Dictionary<EquipSlot, ItemStack>();

        public ItemStack? GetBag(int slot) => IsValidBagSlot(slot) ? _bag[slot] : null;

        public ItemStack? GetEquipped(EquipSlot slot) => _equipment.TryGetValue(slot, out ItemStack? stack) ? stack : null;

        /// <summary>Ajoute des objets si la totalité tient dans le sac (empilement compris). Sinon, ne fait rien.</summary>
        public bool TryAdd(ItemDefinition item, int quantity)
        {
            if (quantity <= 0 || !CanAdd(item, quantity))
            {
                return false;
            }

            int remaining = quantity;

            for (int i = 0; i < BagSize && remaining > 0; i++)
            {
                ItemStack? stack = _bag[i];
                if (stack != null && stack.Item.Id == item.Id && stack.Quantity < item.MaxStack)
                {
                    int added = Math.Min(item.MaxStack - stack.Quantity, remaining);
                    stack.Quantity += added;
                    remaining -= added;
                }
            }

            for (int i = 0; i < BagSize && remaining > 0; i++)
            {
                if (_bag[i] == null)
                {
                    int added = Math.Min(item.MaxStack, remaining);
                    _bag[i] = new ItemStack(item, added);
                    remaining -= added;
                }
            }

            return true;
        }

        /// <summary>Vrai si la quantité complète peut être ajoutée (empilement et place libre).</summary>
        public bool CanAdd(ItemDefinition item, int quantity)
        {
            int capacity = 0;
            for (int i = 0; i < BagSize; i++)
            {
                ItemStack? stack = _bag[i];
                if (stack == null)
                {
                    capacity += item.MaxStack;
                }
                else if (stack.Item.Id == item.Id)
                {
                    capacity += item.MaxStack - stack.Quantity;
                }
            }

            return capacity >= quantity;
        }

        /// <summary>Retire une quantité d'un objet, où qu'il soit dans le sac. Atomique.</summary>
        public bool TryRemove(ushort itemId, int quantity)
        {
            if (quantity <= 0 || CountInBag(itemId) < quantity)
            {
                return false;
            }

            int remaining = quantity;
            for (int i = 0; i < BagSize && remaining > 0; i++)
            {
                ItemStack? stack = _bag[i];
                if (stack == null || stack.Item.Id != itemId)
                {
                    continue;
                }

                int taken = Math.Min(stack.Quantity, remaining);
                stack.Quantity -= taken;
                remaining -= taken;
                if (stack.Quantity == 0)
                {
                    _bag[i] = null;
                }
            }

            return true;
        }

        public int CountInBag(ushort itemId)
        {
            int total = 0;
            for (int i = 0; i < BagSize; i++)
            {
                ItemStack? stack = _bag[i];
                if (stack != null && stack.Item.Id == itemId)
                {
                    total += stack.Quantity;
                }
            }

            return total;
        }

        /// <summary>Déplace ou empile un objet entre deux emplacements du sac. Échange si les objets diffèrent.</summary>
        public bool TryMove(int from, int to)
        {
            if (!IsValidBagSlot(from) || !IsValidBagSlot(to) || from == to || _bag[from] == null)
            {
                return false;
            }

            ItemStack source = _bag[from]!;
            ItemStack? target = _bag[to];

            if (target != null && target.Item.Id == source.Item.Id && source.Item.MaxStack > 1)
            {
                int moved = Math.Min(source.Item.MaxStack - target.Quantity, source.Quantity);
                target.Quantity += moved;
                source.Quantity -= moved;
                if (source.Quantity == 0)
                {
                    _bag[from] = null;
                }

                return true;
            }

            _bag[from] = target;
            _bag[to] = source;
            return true;
        }

        /// <summary>Sépare <paramref name="amount"/> objets d'une pile vers un emplacement vide.</summary>
        public bool TrySplit(int from, int to, int amount)
        {
            if (!IsValidBagSlot(from) || !IsValidBagSlot(to) || from == to || _bag[to] != null)
            {
                return false;
            }

            ItemStack? source = _bag[from];
            if (source == null || amount <= 0 || amount >= source.Quantity)
            {
                return false;
            }

            source.Quantity -= amount;
            _bag[to] = new ItemStack(source.Item, amount);
            return true;
        }

        /// <summary>
        /// Place un objet à un emplacement exact du sac, sans passer par la logique d'empilement normale.
        /// Réservé au chargement d'un personnage sauvegardé : l'emplacement doit être vide.
        /// </summary>
        public void RestoreBagSlot(int slot, ItemDefinition item, int quantity)
        {
            if (IsValidBagSlot(slot) && quantity > 0 && _bag[slot] == null)
            {
                _bag[slot] = new ItemStack(item, quantity);
            }
        }

        /// <summary>Équipe directement un objet, sans passer par le sac. Réservé au chargement d'un personnage sauvegardé.</summary>
        public void RestoreEquipped(EquipSlot slot, ItemDefinition item, int quantity)
        {
            if (quantity > 0)
            {
                _equipment[slot] = new ItemStack(item, quantity);
            }
        }

        public bool TryDestroy(int slot)
        {
            if (!IsValidBagSlot(slot) || _bag[slot] == null)
            {
                return false;
            }

            _bag[slot] = null;
            return true;
        }

        /// <summary>
        /// Équipe une unité de l'objet du sac. L'objet précédemment porté revient dans le sac.
        /// Atomique : si l'ancien objet n'a pas de place, rien ne change.
        /// </summary>
        public bool TryEquip(int bagSlot)
        {
            ItemStack? stack = GetBag(bagSlot);
            if (stack?.Item.Slot is not EquipSlot slot)
            {
                return false;
            }

            ItemStack? previous = GetEquipped(slot);
            bool slotFreesUp = stack.Quantity == 1;
            if (previous != null && !slotFreesUp && FindFreeBagSlot() < 0)
            {
                return false;
            }

            stack.Quantity--;
            if (stack.Quantity == 0)
            {
                _bag[bagSlot] = null;
            }

            _equipment[slot] = new ItemStack(stack.Item, 1);

            if (previous != null)
            {
                int target = _bag[bagSlot] == null ? bagSlot : FindFreeBagSlot();
                _bag[target] = previous;
            }

            return true;
        }

        /// <summary>Retire un objet équipé et le range dans le premier emplacement libre du sac.</summary>
        public bool TryUnequip(EquipSlot slot)
        {
            if (!_equipment.TryGetValue(slot, out ItemStack? stack))
            {
                return false;
            }

            int free = FindFreeBagSlot();
            if (free < 0)
            {
                return false;
            }

            _equipment.Remove(slot);
            _bag[free] = stack;
            return true;
        }

        /// <summary>Bonus cumulés de l'équipement, utilisés par <see cref="StatCalculator"/>.</summary>
        public CombatBonuses ComputeBonuses()
        {
            int armor = 0;
            int strength = 0;
            int agility = 0;
            int intelligence = 0;
            int stamina = 0;
            int spirit = 0;

            foreach (ItemStack stack in _equipment.Values)
            {
                ItemDefinition item = stack.Item;
                armor += item.Armor;
                strength += item.Strength;
                agility += item.Agility;
                intelligence += item.Intelligence;
                stamina += item.Stamina;
                spirit += item.Spirit;
            }

            return new CombatBonuses(armor, new PrimaryStats(strength, agility, intelligence, stamina, spirit));
        }

        private int FindFreeBagSlot()
        {
            for (int i = 0; i < BagSize; i++)
            {
                if (_bag[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsValidBagSlot(int slot) => slot >= 0 && slot < BagSize;
    }
}
