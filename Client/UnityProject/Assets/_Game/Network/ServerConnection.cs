using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using GameShared.Protocol;

namespace MiniWow.Client.Network
{
    /// <summary>
    /// Connexion TCP bas niveau. Un thread dédié lit le flux, décode les trames et les place dans une file.
    /// Le thread principal Unity consomme cette file via <see cref="TryDequeue"/>.
    /// Les envois se font uniquement depuis le thread principal.
    /// </summary>
    public sealed class ServerConnection : IDisposable
    {
        private const int ReadBufferSize = 2 * (ProtocolConstants.LengthPrefixSize + ProtocolConstants.MaxServerBodySize);

        private readonly ConcurrentQueue<ReceivedFrame> _inbox = new ConcurrentQueue<ReceivedFrame>();
        private TcpClient _tcp;
        private NetworkStream _stream;
        private Thread _readThread;
        private volatile bool _running;

        /// <summary>Raison de la fermeture, null tant que la connexion est ouverte.</summary>
        public volatile string CloseReason;

        public bool IsOpen => _running;

        public void Connect(string host, int port)
        {
            _tcp = new TcpClient { NoDelay = true };
            _tcp.Connect(host, port);
            _stream = _tcp.GetStream();

            _running = true;
            _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "MiniWow.Network.Read" };
            _readThread.Start();
        }

        public bool TryDequeue(out ReceivedFrame frame)
        {
            return _inbox.TryDequeue(out frame);
        }

        /// <summary>Envoie une trame complète. À appeler uniquement depuis le thread principal.</summary>
        public void Send(byte[] frame)
        {
            if (!_running)
            {
                return;
            }

            try
            {
                _stream.Write(frame, 0, frame.Length);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is ObjectDisposedException)
            {
                Close("send_failed: " + ex.Message);
            }
        }

        public void Dispose()
        {
            Close("disposed");
        }

        private void ReadLoop()
        {
            byte[] buffer = new byte[ReadBufferSize];
            int filled = 0;

            try
            {
                while (_running)
                {
                    int read = _stream.Read(buffer, filled, buffer.Length - filled);
                    if (read == 0)
                    {
                        Close("server_closed");
                        return;
                    }

                    filled += read;

                    int offset = 0;
                    while (FrameCodec.TryReadFrame(
                        new ReadOnlySpan<byte>(buffer, offset, filled - offset),
                        ProtocolConstants.MaxServerBodySize,
                        out FrameHeader header,
                        out ReadOnlySpan<byte> payload,
                        out int consumed))
                    {
                        _inbox.Enqueue(new ReceivedFrame(header.Opcode, payload.ToArray()));
                        offset += consumed;
                    }

                    int remaining = filled - offset;
                    if (offset > 0)
                    {
                        Buffer.BlockCopy(buffer, offset, buffer, 0, remaining);
                        filled = remaining;
                    }
                }
            }
            catch (ProtocolException ex)
            {
                Close("protocol_error: " + ex.Message);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is ObjectDisposedException)
            {
                Close("read_failed: " + ex.Message);
            }
        }

        private void Close(string reason)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            CloseReason = reason;
            _tcp?.Close();
        }

        /// <summary>Trame reçue du serveur, payload copié hors du buffer de lecture.</summary>
        public readonly struct ReceivedFrame
        {
            public ReceivedFrame(Opcode opcode, byte[] payload)
            {
                Opcode = opcode;
                Payload = payload;
            }

            public Opcode Opcode { get; }

            public byte[] Payload { get; }
        }
    }
}
