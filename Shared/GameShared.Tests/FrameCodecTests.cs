using GameShared.Protocol;
using Xunit;

namespace GameShared.Tests
{
    public sealed class FrameCodecTests
    {
        private const int MaxBody = ProtocolConstants.MaxClientBodySize;

        [Fact]
        public void WriteThenRead_RoundTripsOpcodeRequestIdAndPayload()
        {
            byte[] payload = { 1, 2, 3, 4, 5 };
            byte[] frame = new byte[FrameCodec.GetFrameSize(payload.Length)];

            int written = FrameCodec.WriteFrame(frame, Opcode.Hello, 42, payload);

            Assert.Equal(frame.Length, written);
            bool complete = FrameCodec.TryReadFrame(frame, MaxBody, out FrameHeader header, out var readPayload, out int consumed);

            Assert.True(complete);
            Assert.Equal(Opcode.Hello, header.Opcode);
            Assert.Equal(42u, header.RequestId);
            Assert.Equal(payload, readPayload.ToArray());
            Assert.Equal(frame.Length, consumed);
        }

        [Fact]
        public void TryReadFrame_ReturnsFalse_WhenPrefixIsIncomplete()
        {
            bool complete = FrameCodec.TryReadFrame(new byte[] { 1, 0 }, MaxBody, out _, out _, out int consumed);

            Assert.False(complete);
            Assert.Equal(0, consumed);
        }

        [Fact]
        public void TryReadFrame_ReturnsFalse_WhenBodyIsIncomplete()
        {
            byte[] frame = new byte[FrameCodec.GetFrameSize(4)];
            FrameCodec.WriteFrame(frame, Opcode.Hello, 1, new byte[4]);

            bool complete = FrameCodec.TryReadFrame(frame.AsSpan(0, frame.Length - 1), MaxBody, out _, out _, out int consumed);

            Assert.False(complete);
            Assert.Equal(0, consumed);
        }

        [Fact]
        public void TryReadFrame_ConsumesOnlyFirstFrame_WhenTwoFramesAreBuffered()
        {
            byte[] first = new byte[FrameCodec.GetFrameSize(0)];
            byte[] second = new byte[FrameCodec.GetFrameSize(2)];
            FrameCodec.WriteFrame(first, Opcode.Hello, 1, ReadOnlySpan<byte>.Empty);
            FrameCodec.WriteFrame(second, Opcode.HelloAck, 2, new byte[] { 9, 9 });
            byte[] buffer = Concat(first, second);

            FrameCodec.TryReadFrame(buffer, MaxBody, out FrameHeader header, out _, out int consumed);

            Assert.Equal(first.Length, consumed);
            Assert.Equal(Opcode.Hello, header.Opcode);
        }

        [Fact]
        public void TryReadFrame_Throws_WhenLengthIsSmallerThanHeader()
        {
            byte[] invalid = { 1, 0, 0, 0, 0 }; // corps de 1 octet, alors que l'en-tête en fait 6

            Assert.Throws<ProtocolException>(() => FrameCodec.TryReadFrame(invalid, MaxBody, out _, out _, out _));
        }

        [Fact]
        public void TryReadFrame_Throws_WhenFrameExceedsMaxSize()
        {
            byte[] frame = new byte[FrameCodec.GetFrameSize(100)];
            FrameCodec.WriteFrame(frame, Opcode.Hello, 1, new byte[100]);

            Assert.Throws<ProtocolException>(() => FrameCodec.TryReadFrame(frame, 50, out _, out _, out _));
        }

        [Fact]
        public void WriteFrame_Throws_WhenDestinationIsTooSmall()
        {
            byte[] tooSmall = new byte[3];

            Assert.Throws<ArgumentException>(() => FrameCodec.WriteFrame(tooSmall, Opcode.Hello, 1, ReadOnlySpan<byte>.Empty));
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }
    }
}
