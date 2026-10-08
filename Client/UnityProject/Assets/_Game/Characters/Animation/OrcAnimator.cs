using UnityEngine;

namespace MiniWow.Client.Characters
{
    /// <summary>États d'animation implémentés dans ce prototype. Futur : Run, Sprint, Fall, Death...</summary>
    public enum OrcAnimationState
    {
        Idle,
        Walk
    }

    /// <summary>
    /// Anime le squelette d'un Orc entièrement par code : aucune animation .fbx, uniquement des rotations
    /// de Transform calculées chaque frame à partir de fonctions sinusoïdales (+ un peu de bruit de Perlin
    /// pour l'idle, afin d'éviter l'effet "métronome"). Ne connaît ni l'équipement ni les sockets : il ne
    /// touche qu'aux Transforms du corps (Body/Head/Torso/bras/jambes/mains/pieds) ; c'est le
    /// <see cref="SocketRig"/> qui fait suivre les équipements séparément, en LateUpdate.
    ///
    /// Principe du calcul : pour une articulation donnée, on part de sa rotation locale "de repos"
    /// (mémorisée une fois dans <see cref="Initialize"/>), et on lui compose une rotation delta
    /// <c>Quaternion.Euler(angle, 0, 0)</c> où <c>angle = Mathf.Sin(phase) * amplitude</c>. <c>phase</c>
    /// avance avec le temps (vitesse de marche comprise), <c>amplitude</c> est en degrés. Changer
    /// l'allure revient donc à changer une fréquence (vitesse de <c>phase</c>) et/ou une amplitude,
    /// ce qui rend les animations faciles à retoucher depuis l'Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrcAnimator : MonoBehaviour
    {
        [Header("Vitesse")]
        [Tooltip("Vitesse de marche de référence : SetState(Walk) vise cette vitesse.")]
        public float walkSpeed = 2.5f;
        [Tooltip("Temps caractéristique (s) pour que la vitesse affichée rattrape la vitesse cible.")]
        public float speedSmoothing = 6f;
        [Tooltip("Fraction de walkSpeed à partir de laquelle l'amplitude de marche est pleine (en dessous, transition progressive Idle -> Walk).")]
        [Range(0.05f, 1f)] public float walkBlendInSpeed = 0.35f;
        [Tooltip("Foulées par seconde à walkSpeed.")]
        public float strideFrequency = 1.6f;

        [Header("Idle - respiration et poids du corps")]
        public float breathingSpeed = 1.1f;
        public float breathingAmount = 0.018f;
        public float idleSwaySpeed = 0.5f;
        public float idleSwayAmount = 2.5f;

        [Header("Walk - foulée")]
        public float strideAmplitude = 30f;
        public float armSwingAmplitude = 24f;
        public float torsoTwistAmplitude = 7f;
        public float headCounterAmplitude = 4f;
        public float bodyBobAmount = 0.035f;

        [Header("Attaque (coup du bras droit)")]
        public float attackDuration = 0.45f;
        public float attackSwingAmplitude = 80f;
        public float attackLeanAmplitude = 10f;

        [Header("Touché (encaisse un coup)")]
        public float hitDuration = 0.28f;
        public float hitFlinchAmplitude = 14f;

        [Header("Saut (cosmétique : le monde serveur est plat, Y=0)")]
        public float jumpDuration = 0.55f;
        public float jumpHeight = 0.5f;

        private CharacterSkeleton _skeleton;

        private Quaternion _headRest, _torsoRest;
        private Quaternion _leftArmRest, _rightArmRest, _leftHandRest, _rightHandRest;
        private Quaternion _leftLegRest, _rightLegRest, _leftFootRest, _rightFootRest;
        private Vector3 _bodyRestLocalPosition, _torsoRestLocalPosition;

        private float _targetSpeed;
        private float _currentSpeed;
        private float _walkCycle;
        private float _idleTime;

        private float _attackTimer;
        private float _hitTimer;
        private float _jumpTimer;

        /// <summary>État dérivé de la vitesse courante (utile pour de la logique externe, ex: sons de pas).</summary>
        public OrcAnimationState CurrentState => _currentSpeed > 0.05f ? OrcAnimationState.Walk : OrcAnimationState.Idle;

        /// <summary>À appeler une fois après la génération du corps : mémorise la pose de repos (bind pose).</summary>
        public void Initialize(CharacterSkeleton skeleton)
        {
            _skeleton = skeleton;

            _headRest = skeleton.Head.localRotation;
            _torsoRest = skeleton.Torso.localRotation;
            _leftArmRest = skeleton.LeftArm.localRotation;
            _rightArmRest = skeleton.RightArm.localRotation;
            _leftHandRest = skeleton.LeftHand.localRotation;
            _rightHandRest = skeleton.RightHand.localRotation;
            _leftLegRest = skeleton.LeftLeg.localRotation;
            _rightLegRest = skeleton.RightLeg.localRotation;
            _leftFootRest = skeleton.LeftFoot.localRotation;
            _rightFootRest = skeleton.RightFoot.localRotation;

            _bodyRestLocalPosition = skeleton.Body.localPosition;
            _torsoRestLocalPosition = skeleton.Torso.localPosition;

            _targetSpeed = 0f;
            _currentSpeed = 0f;
            _walkCycle = 0f;
            _idleTime = 0f;
        }

        /// <summary>Raccourci haut niveau : Idle vise une vitesse nulle, Walk vise <see cref="walkSpeed"/>.</summary>
        public void SetState(OrcAnimationState state) => _targetSpeed = state == OrcAnimationState.Walk ? walkSpeed : 0f;

        /// <summary>
        /// Contrôle fin, prévu pour être branché sur un futur <c>character.Move(direction)</c> :
        /// pilote en continu la transition Idle/Walk ainsi que la cadence de la foulée.
        /// 0 -> Idle, 0.1 -> quasi Idle, walkSpeed -> marche "normale", au-delà -> marche plus rapide.
        /// </summary>
        public void SetMovementSpeed(float speed) => _targetSpeed = Mathf.Max(0f, speed);

        /// <summary>
        /// Déclenche un coup additif du bras droit, par-dessus l'idle/walk en cours (ne l'interrompt pas :
        /// on peut attaquer en marchant, comme l'auto-attack de ce jeu le permet déjà).
        /// </summary>
        public void PlayAttack() => _attackTimer = attackDuration;

        /// <summary>Déclenche un flinch additif (encaissement d'un coup).</summary>
        public void PlayHit() => _hitTimer = hitDuration;

        /// <summary>
        /// Petit saut cosmétique. Le serveur ne modélise aucun axe vertical (monde plat, Y=0) : ceci n'a
        /// aucun effet sur la position ou la collision, uniquement sur l'apparence.
        /// </summary>
        public void PlayJump() => _jumpTimer = jumpDuration;

        private void Update()
        {
            if (_skeleton == null)
            {
                return;
            }

            float dt = Time.deltaTime;

            // Lissage exponentiel : la vitesse affichée rattrape progressivement la cible (pas de à-coup).
            _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, 1f - Mathf.Exp(-speedSmoothing * dt));

            float walkBlend = Mathf.Clamp01(_currentSpeed / Mathf.Max(0.001f, walkSpeed * walkBlendInSpeed));
            float cadence = _currentSpeed / Mathf.Max(0.001f, walkSpeed); // 1 = cadence "normale" à walkSpeed

            _idleTime += dt;
            _walkCycle += cadence * strideFrequency * dt * Mathf.PI * 2f;

            // La respiration/les micro-mouvements idle restent présents (atténués) même en marchant,
            // pour éviter que l'animation de marche n'ait l'air totalement rigide.
            float idleWeight = 1f - 0.5f * walkBlend;

            // ---- Idle : respiration, dérive légère, report de poids ----
            float breath = Mathf.Sin(_idleTime * breathingSpeed * Mathf.PI * 2f);
            Vector3 torsoBreathOffset = new Vector3(0f, breath * breathingAmount * idleWeight, 0f);
            Quaternion torsoBreathTilt = Quaternion.Euler(breath * 2.5f * idleWeight, 0f, 0f);

            // Bruit de Perlin plutôt qu'un sinus pur pour la tête : dérive organique, non répétitive à l'œil.
            float noiseX = Mathf.PerlinNoise(_idleTime * idleSwaySpeed, 0.37f) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(0.81f, _idleTime * idleSwaySpeed) * 2f - 1f;
            Quaternion headIdle = Quaternion.Euler(noiseX * idleSwayAmount * idleWeight, 0f, noiseZ * idleSwayAmount * 0.6f * idleWeight);

            float shoulderIdleL = Mathf.Sin(_idleTime * idleSwaySpeed * 0.8f + 0.6f) * idleSwayAmount * 0.5f * idleWeight;
            float shoulderIdleR = Mathf.Sin(_idleTime * idleSwaySpeed * 0.8f + 2.1f) * idleSwayAmount * 0.5f * idleWeight;

            float weightShift = Mathf.Sin(_idleTime * breathingSpeed * Mathf.PI) * 0.01f * idleWeight;

            // ---- Walk : jambes/bras alternés, torsion du torse, rebond du corps ----
            float legPhaseL = Mathf.Sin(_walkCycle);
            float legPhaseR = Mathf.Sin(_walkCycle + Mathf.PI);

            Quaternion legSwingL = Quaternion.Euler(legPhaseL * strideAmplitude * walkBlend, 0f, 0f);
            Quaternion legSwingR = Quaternion.Euler(legPhaseR * strideAmplitude * walkBlend, 0f, 0f);
            // Le bras suit la jambe opposée (démarche naturelle).
            Quaternion armSwingL = Quaternion.Euler(legPhaseR * armSwingAmplitude * walkBlend, 0f, 0f);
            Quaternion armSwingR = Quaternion.Euler(legPhaseL * armSwingAmplitude * walkBlend, 0f, 0f);
            Quaternion footFlexL = Quaternion.Euler(legPhaseL * strideAmplitude * 0.3f * walkBlend, 0f, 0f);
            Quaternion footFlexR = Quaternion.Euler(legPhaseR * strideAmplitude * 0.3f * walkBlend, 0f, 0f);

            Quaternion torsoTwist = Quaternion.Euler(0f, legPhaseL * torsoTwistAmplitude * walkBlend, 0f);
            Quaternion headCounter = Quaternion.Euler(0f, -legPhaseL * headCounterAmplitude * walkBlend, 0f);

            // Deux rebonds par cycle de marche complet (un par appui de pied), toujours vers le haut.
            float bob = (Mathf.Abs(Mathf.Sin(_walkCycle)) - 0.5f) * 2f * bodyBobAmount * walkBlend;

            // ---- Pulsations additives : attaque, touché, saut. Chacune décroît seule, sans bloquer idle/walk. ----
            if (_attackTimer > 0f) _attackTimer = Mathf.Max(0f, _attackTimer - dt);
            if (_hitTimer > 0f) _hitTimer = Mathf.Max(0f, _hitTimer - dt);
            if (_jumpTimer > 0f) _jumpTimer = Mathf.Max(0f, _jumpTimer - dt);

            Quaternion attackSwing = Quaternion.identity;
            Quaternion attackLean = Quaternion.identity;
            if (_attackTimer > 0f)
            {
                // Enveloppe en cloche (0 -> 1 -> 0) : monte vite, redescend, jamais de à-coup aux extrémités.
                float envelope = Mathf.Sin((1f - _attackTimer / attackDuration) * Mathf.PI);
                attackSwing = Quaternion.Euler(-envelope * attackSwingAmplitude, 0f, 0f);
                attackLean = Quaternion.Euler(envelope * attackLeanAmplitude, 0f, 0f);
            }

            Quaternion hitFlinch = Quaternion.identity;
            if (_hitTimer > 0f)
            {
                float envelope = Mathf.Sin((1f - _hitTimer / hitDuration) * Mathf.PI);
                hitFlinch = Quaternion.Euler(-envelope * hitFlinchAmplitude, 0f, 0f);
            }

            float jumpOffset = 0f;
            if (_jumpTimer > 0f)
            {
                float p = 1f - _jumpTimer / jumpDuration;
                jumpOffset = 4f * jumpHeight * p * (1f - p); // parabole : monte puis redescend au sol
            }

            // ---- Application finale (une seule écriture par Transform) ----
            _skeleton.Body.localPosition = _bodyRestLocalPosition + new Vector3(weightShift, bob + jumpOffset, 0f);

            _skeleton.Torso.localPosition = _torsoRestLocalPosition + torsoBreathOffset;
            _skeleton.Torso.localRotation = _torsoRest * torsoBreathTilt * torsoTwist * attackLean * hitFlinch;

            _skeleton.Head.localRotation = _headRest * headIdle * headCounter * hitFlinch;

            _skeleton.LeftArm.localRotation = _leftArmRest * Quaternion.Euler(0f, 0f, shoulderIdleL) * armSwingL;
            _skeleton.RightArm.localRotation = _rightArmRest * Quaternion.Euler(0f, 0f, shoulderIdleR) * armSwingR * attackSwing;
            // La main est maintenant un enfant du bras (voir CharacterGenerator) : elle hérite déjà de tout son
            // mouvement (idle, marche, attaque) via la hiérarchie Unity. Lui appliquer le même delta en plus le
            // doublerait ; elle garde juste sa pose de repos relative au bras.
            _skeleton.LeftHand.localRotation = _leftHandRest;
            _skeleton.RightHand.localRotation = _rightHandRest;

            _skeleton.LeftLeg.localRotation = _leftLegRest * legSwingL;
            _skeleton.RightLeg.localRotation = _rightLegRest * legSwingR;
            // Le pied hérite du balancement de la jambe (même principe que la main) ; footFlex n'ajoute
            // qu'une légère flexion de cheville en plus, propre au pied.
            _skeleton.LeftFoot.localRotation = _leftFootRest * footFlexL;
            _skeleton.RightFoot.localRotation = _rightFootRest * footFlexR;
        }
    }
}
