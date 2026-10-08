using System.Collections.Generic;
using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Ajoute/retire des équipements sur le squelette d'un personnage. Ne connaît ni l'animation ni la
    /// génération du corps : il instancie le mesh fourni par un <see cref="EquipmentData"/>, le place dans
    /// le conteneur d'organisation correspondant (hiérarchie "Equipment/..."), puis enregistre l'instance
    /// auprès du <see cref="SocketRig"/> pour qu'elle suive le bon socket pendant l'animation.
    /// </summary>
    public sealed class EquipmentManager : MonoBehaviour
    {
        private readonly Dictionary<EquipmentSlot, List<GameObject>> _instances = new Dictionary<EquipmentSlot, List<GameObject>>();
        private readonly Dictionary<EquipmentSlot, EquipmentData> _equipped = new Dictionary<EquipmentSlot, EquipmentData>();

        private CharacterSkeleton _skeleton;
        private BodyProportions _proportions;
        private SocketRig _socketRig;

        public void Initialize(CharacterSkeleton skeleton, BodyProportions proportions, SocketRig socketRig)
        {
            // On repart d'un état propre si le personnage est régénéré.
            foreach (KeyValuePair<EquipmentSlot, List<GameObject>> entry in _instances)
            {
                foreach (GameObject instance in entry.Value)
                {
                    if (instance != null) DestroyGameObject(instance);
                }
            }

            _instances.Clear();
            _equipped.Clear();

            _skeleton = skeleton;
            _proportions = proportions;
            _socketRig = socketRig;
        }

        /// <summary>Équipement actuellement porté dans un slot, ou null si le slot est vide.</summary>
        public EquipmentData GetEquipped(EquipmentSlot slot) => _equipped.TryGetValue(slot, out EquipmentData data) ? data : null;

        /// <summary>Équipe (en remplaçant l'équipement existant du même slot). Passer null déséquipe.</summary>
        public void Equip(EquipmentSlot slot, EquipmentData data)
        {
            if (data == null)
            {
                Unequip(slot);
                return;
            }

            if (data.Slot != slot)
            {
                Debug.LogWarning($"{data.name} est prévu pour le slot {data.Slot}, pas {slot}. Équipement ignoré.");
                return;
            }

            Unequip(slot);

            Mesh mesh = data.BuildMesh(_proportions);
            Material material = data.BuildMaterial();
            var created = new List<GameObject>();

            switch (slot)
            {
                case EquipmentSlot.Head:
                    created.Add(CreateAndAttach(data.DisplayName, mesh, material, _skeleton.EquipmentHead, _skeleton.HeadSocket));
                    break;

                case EquipmentSlot.Chest:
                    created.Add(CreateAndAttach(data.DisplayName, mesh, material, _skeleton.EquipmentChest, _skeleton.ChestSocket));
                    break;

                case EquipmentSlot.Hands:
                    created.Add(CreateAndAttach(data.DisplayName + "_L", mesh, material, _skeleton.EquipmentHands, _skeleton.LeftHandSocket));
                    created.Add(CreateAndAttach(data.DisplayName + "_R", mesh, material, _skeleton.EquipmentHands, _skeleton.RightHandSocket));
                    break;

                case EquipmentSlot.Legs:
                    created.Add(CreateAndAttach(data.DisplayName, mesh, material, _skeleton.EquipmentLegs, _skeleton.LegsSocket));
                    break;

                case EquipmentSlot.Feet:
                    created.Add(CreateAndAttach(data.DisplayName + "_L", mesh, material, _skeleton.EquipmentFeet, _skeleton.LeftFootSocket));
                    created.Add(CreateAndAttach(data.DisplayName + "_R", mesh, material, _skeleton.EquipmentFeet, _skeleton.RightFootSocket));
                    break;

                case EquipmentSlot.MainHand:
                    // Convention simple : toujours en main droite (pas de gestion de gaucher/droitier pour l'instant).
                    created.Add(CreateAndAttach(data.DisplayName, mesh, material, _skeleton.EquipmentMainHand, _skeleton.RightHandSocket));
                    break;
            }

            _instances[slot] = created;
            _equipped[slot] = data;
        }

        /// <summary>Retire uniquement l'équipement du slot donné ; les autres slots ne sont pas affectés.</summary>
        public void Unequip(EquipmentSlot slot)
        {
            if (_instances.TryGetValue(slot, out List<GameObject> instances))
            {
                foreach (GameObject instance in instances)
                {
                    if (instance == null) continue;
                    _socketRig.UnregisterFollower(instance.transform);
                    DestroyGameObject(instance);
                }

                _instances.Remove(slot);
            }

            _equipped.Remove(slot);
        }

        private GameObject CreateAndAttach(string name, Mesh mesh, Material material, Transform organizationParent, Transform socket)
        {
            GameObject instance = LowPolyMeshBuilder.CreateMeshObject(name, mesh, material, organizationParent);
            _socketRig.RegisterFollower(instance.transform, socket);
            return instance;
        }

        private static void DestroyGameObject(GameObject go)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(go);
            }
            else
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
