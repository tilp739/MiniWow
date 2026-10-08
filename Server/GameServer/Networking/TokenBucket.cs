using System;

namespace GameServer.Networking
{
    /// <summary>Limiteur de débit à seaux de jetons. Non thread-safe : une instance par connexion.</summary>
    public sealed class TokenBucket
    {
        private readonly TimeProvider _time;
        private readonly double _ratePerSecond;
        private readonly double _capacity;
        private double _tokens;
        private long _lastTimestamp;

        public TokenBucket(TimeProvider time, double ratePerSecond, double capacity)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _ratePerSecond = ratePerSecond;
            _capacity = capacity;
            _tokens = capacity;
            _lastTimestamp = time.GetTimestamp();
        }

        /// <summary>Consomme un jeton si disponible. Retourne false si le débit est dépassé.</summary>
        public bool TryConsume()
        {
            long now = _time.GetTimestamp();
            double elapsedSeconds = (double)(now - _lastTimestamp) / _time.TimestampFrequency;
            _lastTimestamp = now;

            _tokens = Math.Min(_capacity, _tokens + elapsedSeconds * _ratePerSecond);
            if (_tokens < 1d)
            {
                return false;
            }

            _tokens -= 1d;
            return true;
        }
    }
}
