using System.Collections.Generic;
using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Fait vivre les sockets du squelette : à chaque frame (en <c>LateUpdate</c>, donc après que
    /// l'animation a bougé les parties du corps), recopie la position/rotation de chaque partie du
    /// corps sur son socket, puis recopie la position/rotation de chaque socket sur les instances
    /// d'équipement qui lui sont attachées.
    ///
    /// Ce composant est le SEUL point de couplage entre animation et équipement. Ni
    /// <see cref="OrcAnimator"/> ni <see cref="EquipmentManager"/> ne se connaissent directement :
    /// l'un anime des Transforms, l'autre y enregistre des followers, et c'est le rig qui relie les deux
    /// dans le bon ordre, sans dépendre de l'ordre d'exécution des scripts de Unity.
    /// </summary>
    public sealed class SocketRig : MonoBehaviour
    {
        private readonly List<SocketBinding> _bindings = new List<SocketBinding>();
        private readonly List<(Transform instance, Transform socket)> _followers = new List<(Transform, Transform)>();

        public void Initialize(List<SocketBinding> bindings)
        {
            _bindings.Clear();
            _bindings.AddRange(bindings);
            _followers.Clear();
        }

        /// <summary>Attache une instance d'équipement à un socket : elle suivra ce socket dès la prochaine frame.</summary>
        public void RegisterFollower(Transform instance, Transform socket)
        {
            _followers.Add((instance, socket));
        }

        /// <summary>Détache une instance d'équipement (à appeler avant de la détruire).</summary>
        public void UnregisterFollower(Transform instance)
        {
            _followers.RemoveAll(follower => follower.instance == instance);
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                SocketBinding binding = _bindings[i];
                binding.Socket.SetPositionAndRotation(
                    binding.BodyPart.TransformPoint(binding.LocalOffset),
                    binding.BodyPart.rotation * binding.LocalRotationOffset);
            }

            for (int i = 0; i < _followers.Count; i++)
            {
                (Transform instance, Transform socket) = _followers[i];
                if (instance == null)
                {
                    continue;
                }

                instance.SetPositionAndRotation(socket.position, socket.rotation);
            }
        }
    }
}
