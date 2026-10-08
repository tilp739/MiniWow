using System;
using GameServer.Networking;
using Xunit;

namespace GameServer.Tests
{
    public sealed class TokenBucketTests
    {
        [Fact]
        public void TryConsume_AllowsBurstThenBlocks()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(time, ratePerSecond: 10, capacity: 3);

            Assert.True(bucket.TryConsume());
            Assert.True(bucket.TryConsume());
            Assert.True(bucket.TryConsume());
            Assert.False(bucket.TryConsume());
        }

        [Fact]
        public void TryConsume_RefillsOverTime()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(time, ratePerSecond: 10, capacity: 1);
            Assert.True(bucket.TryConsume());
            Assert.False(bucket.TryConsume());

            time.Advance(TimeSpan.FromSeconds(0.1)); // +1 jeton

            Assert.True(bucket.TryConsume());
        }

        [Fact]
        public void TryConsume_DoesNotExceedCapacity_AfterLongIdle()
        {
            var time = new ManualTimeProvider();
            var bucket = new TokenBucket(time, ratePerSecond: 10, capacity: 2);
            time.Advance(TimeSpan.FromMinutes(10));

            Assert.True(bucket.TryConsume());
            Assert.True(bucket.TryConsume());
            Assert.False(bucket.TryConsume());
        }

        private sealed class ManualTimeProvider : TimeProvider
        {
            private long _timestamp;

            public override long GetTimestamp() => _timestamp;

            public void Advance(TimeSpan delta)
            {
                _timestamp += (long)(delta.TotalSeconds * TimestampFrequency);
            }
        }
    }
}
