using System;

namespace GameServer.Domain.Movement
{
    /// <summary>
    /// Intègre le mouvement d'un joueur à pas fixe, côté serveur uniquement.
    /// Le client fournit une direction, jamais une position ni une durée : la vitesse maximale
    /// est donc fixée par <see cref="MovementSettings.WalkSpeed"/> et ne peut pas être dépassée.
    /// </summary>
    public sealed class PlayerMotor
    {
        /// <summary>Valeur maximale d'une composante de direction envoyée par le client.</summary>
        private const float MaxComponent = 127f;

        private const float RadToDeg = 180f / MathF.PI;

        private readonly MovementSettings _settings;

        private float _directionX;
        private float _directionZ;
        private uint _latestSequence;
        private int _ticksSinceInput;

        public PlayerMotor(MovementSettings settings, float startX = 0f, float startZ = 0f)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            X = startX;
            Z = startZ;
        }

        public float X { get; private set; }

        public float Z { get; private set; }

        public float YawDegrees { get; private set; }

        /// <summary>Dernière séquence d'entrée appliquée au cours d'un tick.</summary>
        public uint LastProcessedInput { get; private set; }

        /// <summary>Arrête immédiatement le joueur (mort, téléportation). La séquence reste croissante.</summary>
        public void Halt()
        {
            _directionX = 0f;
            _directionZ = 0f;
        }

        /// <summary>Place le joueur à une position précise, sans vérification de distance (respawn uniquement).</summary>
        public void Teleport(float x, float z)
        {
            X = Clamp(x);
            Z = Clamp(z);
            Halt();
        }

        /// <summary>
        /// Enregistre la direction demandée. Les séquences doivent être strictement croissantes (la première est 1) :
        /// une séquence plus ancienne est ignorée, et la méthode retourne false.
        /// </summary>
        public bool SetInput(uint sequence, sbyte directionX, sbyte directionZ)
        {
            if (sequence <= _latestSequence)
            {
                return false;
            }

            _latestSequence = sequence;
            _ticksSinceInput = 0;
            (_directionX, _directionZ) = NormalizeDirection(directionX, directionZ);
            return true;
        }

        /// <summary>Avance le joueur d'un tick de simulation.</summary>
        public void Advance(float deltaSeconds)
        {
            // Une entrée reste valable pendant InputTimeoutTicks ticks, puis le joueur s'arrête.
            if (_ticksSinceInput <= _settings.InputTimeoutTicks)
            {
                _ticksSinceInput++;
            }

            if (_ticksSinceInput > _settings.InputTimeoutTicks)
            {
                _directionX = 0f;
                _directionZ = 0f;
            }

            LastProcessedInput = _latestSequence;

            if (_directionX == 0f && _directionZ == 0f)
            {
                return;
            }

            float step = _settings.WalkSpeed * deltaSeconds;
            X = Clamp(X + _directionX * step);
            Z = Clamp(Z + _directionZ * step);
            YawDegrees = MathF.Atan2(_directionX, _directionZ) * RadToDeg;
        }

        private float Clamp(float value)
        {
            return Math.Clamp(value, -_settings.WorldHalfExtent, _settings.WorldHalfExtent);
        }

        private static (float X, float Z) NormalizeDirection(sbyte rawX, sbyte rawZ)
        {
            float x = Math.Clamp(rawX / MaxComponent, -1f, 1f);
            float z = Math.Clamp(rawZ / MaxComponent, -1f, 1f);

            float length = MathF.Sqrt(x * x + z * z);
            if (length > 1f)
            {
                x /= length;
                z /= length;
            }

            return (x, z);
        }
    }
}
