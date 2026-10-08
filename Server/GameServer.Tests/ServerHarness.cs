using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GameServer.Configuration;
using GameServer.Networking;
using GameServer.Simulation;
using GameShared.Messages;
using GameShared.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GameServer.Tests
{
    /// <summary>
    /// Lance un vrai serveur sur un port éphémère, dans le processus de test.
    /// Par défaut : zone "quiet" (sans créature). Les tests de combat passent la zone "combat".
    /// </summary>
    public sealed class ServerHarness : IAsyncDisposable
    {
        private readonly IHost _host;

        private ServerHarness(IHost host, IPEndPoint endpoint)
        {
            _host = host;
            Endpoint = endpoint;
        }

        public IPEndPoint Endpoint { get; }

        /// <summary>Registre des mondes : permet aux tests de déclencher des actions d'administration.</summary>
        public IWorldDirectory Directory => _host.Services.GetRequiredService<IWorldDirectory>();

        public static async Task<ServerHarness> StartAsync(IReadOnlyDictionary<string, string?>? overrides = null)
        {
            var values = new Dictionary<string, string?>
            {
                ["GameServer:BindAddress"] = "127.0.0.1",
                ["GameServer:Port"] = "0",
                ["GameServer:TickRate"] = "20",
                ["GameServer:ContentDirectory"] = Path.Combine(AppContext.BaseDirectory, "TestContent"),
                ["GameServer:ZoneId"] = "quiet",
                ["GameServer:RandomSeed"] = "1",
            };
            if (overrides != null)
            {
                foreach (KeyValuePair<string, string?> pair in overrides)
                {
                    values[pair.Key] = pair.Value;
                }
            }

            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.Configuration.AddInMemoryCollection(values);
            builder.Services.AddGameServer(builder.Configuration);

            IHost host = builder.Build();
            await host.StartAsync();

            IPEndPoint endpoint = host.Services.GetRequiredService<GameTcpListener>().LocalEndpoint
                ?? throw new InvalidOperationException("Le listener n'a pas démarré.");
            return new ServerHarness(host, endpoint);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    /// <summary>Client TCP minimal qui parle le protocole réel.</summary>
    public sealed class TestClient : IAsyncDisposable
    {
        private readonly TcpClient _tcp;
        private readonly PacketWriter _writer = new PacketWriter(256);

        public TestClient(IPEndPoint endpoint)
        {
            _tcp = new TcpClient();
            _tcp.Connect(endpoint);
        }

        public async Task SendAsync<T>(Opcode opcode, T message, CancellationToken ct) where T : IWireMessage
        {
            byte[] frame = MessageFrame.Create(opcode, message, _writer);
            await _tcp.GetStream().WriteAsync(frame, ct);
        }

        public async Task SendRawAsync(byte[] bytes, CancellationToken ct)
        {
            await _tcp.GetStream().WriteAsync(bytes, ct);
        }

        public async Task<(Opcode Opcode, byte[] Payload)> ReceiveAsync(CancellationToken ct)
        {
            NetworkStream stream = _tcp.GetStream();

            byte[] lengthBytes = new byte[ProtocolConstants.LengthPrefixSize];
            await stream.ReadExactlyAsync(lengthBytes, ct);
            uint bodyLength = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);

            byte[] body = new byte[bodyLength];
            await stream.ReadExactlyAsync(body, ct);

            var opcode = (Opcode)BinaryPrimitives.ReadUInt16LittleEndian(body);
            byte[] payload = new byte[body.Length - ProtocolConstants.BodyHeaderSize];
            Array.Copy(body, ProtocolConstants.BodyHeaderSize, payload, 0, payload.Length);
            return (opcode, payload);
        }

        /// <summary>Lit les trames jusqu'à trouver un snapshot qui satisfait le prédicat.</summary>
        public async Task<WorldSnapshotMessage> WaitForSnapshotAsync(Func<WorldSnapshotMessage, bool> predicate, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await ReceiveAsync(ct);
                if (opcode != Opcode.WorldSnapshot)
                {
                    continue;
                }

                WorldSnapshotMessage snapshot = WorldSnapshotMessage.Read(payload);
                if (predicate(snapshot))
                {
                    return snapshot;
                }
            }
        }

        /// <summary>Lit les trames jusqu'à un événement de combat qui satisfait le prédicat.</summary>
        public async Task<CombatEventMessage> WaitForCombatEventAsync(Func<CombatEventMessage, bool> predicate, CancellationToken ct)
        {
            while (true)
            {
                (Opcode opcode, byte[] payload) = await ReceiveAsync(ct);
                if (opcode != Opcode.CombatEvent)
                {
                    continue;
                }

                CombatEventMessage combatEvent = CombatEventMessage.Read(payload);
                if (predicate(combatEvent))
                {
                    return combatEvent;
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            _tcp.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
