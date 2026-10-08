using System.Collections.Generic;
using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>
    /// Référence vers un couple (partie du corps, socket) : le <see cref="SocketRig"/> recopie chaque
    /// frame la position/rotation monde de <see cref="BodyPart"/> (+ un décalage local) sur <see cref="Socket"/>.
    /// C'est ce qui permet à un équipement attaché au socket de suivre l'animation sans que l'animateur
    /// ou le système d'équipement n'aient besoin de se connaître.
    /// </summary>
    public struct SocketBinding
    {
        public Transform BodyPart;
        public Transform Socket;
        public Vector3 LocalOffset;
        public Quaternion LocalRotationOffset;
    }

    /// <summary>
    /// Résultat de <see cref="CharacterGenerator.Generate"/> : toutes les références dont ont besoin
    /// <see cref="OrcAnimator"/>, <see cref="SocketRig"/> et <see cref="EquipmentManager"/>, construites
    /// une seule fois à la génération du personnage.
    /// </summary>
    public sealed class CharacterSkeleton
    {
        // Corps.
        public Transform Body;
        public Transform Head;
        public Transform Torso;
        public Transform LeftArm;
        public Transform RightArm;
        public Transform LeftHand;
        public Transform RightHand;
        public Transform LeftLeg;
        public Transform RightLeg;
        public Transform LeftFoot;
        public Transform RightFoot;

        // Conteneurs d'organisation pour les instances d'équipement (purement visuel dans la hiérarchie).
        public Transform EquipmentHead;
        public Transform EquipmentChest;
        public Transform EquipmentHands;
        public Transform EquipmentLegs;
        public Transform EquipmentFeet;
        public Transform EquipmentMainHand;

        // Points d'attache, synchronisés chaque frame par le SocketRig.
        public Transform HeadSocket;
        public Transform ChestSocket;
        public Transform LeftHandSocket;
        public Transform RightHandSocket;
        public Transform LegsSocket;
        public Transform LeftFootSocket;
        public Transform RightFootSocket;

        public readonly List<SocketBinding> SocketBindings = new List<SocketBinding>();
    }
}
