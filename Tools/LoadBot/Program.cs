using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GameShared.Messages;
using GameShared.Protocol;

// Bot de charge : simule N clients qui marchent au hasard, et mesure les snapshots reçus.
// Usage : dotnet run --project Tools/LoadBot -- [bots] [secondes] [port]
int bots = args.Length > 0 && int.TryParse(args[0], out int b) ? b : 10;
int seconds = args.Length > 1 && int.TryParse(args[1], out int s) ? s : 10;
int port = args.Length > 2 && int.TryParse(args[2], out int p) ? p : 7777;

Console.WriteLine($"LoadBot : {bots} bots pendant {seconds} s sur 127.0.0.1:{port}");

var stats = new BotStats();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds + 15));
var tasks = Enumerable.Range(0, bots).Select(i => RunBotAsync(i, port, seconds, stats, cts.Token)).ToArray();
await Task.WhenAll(tasks);

Console.WriteLine();
Console.WriteLine("Résultats :");
Console.WriteLine($"  bots connectés        : {stats.Connected}/{bots}");
Console.WriteLine($"  échecs                : {stats.Failed}");
Console.WriteLine($"  snapshots reçus       : {stats.Snapshots}");
Console.WriteLine($"  snapshots / bot / s   : {stats.Snapshots / Math.Max(1.0, stats.Connected) / seconds:F1} (attendu ≈ 20)");
Console.WriteLine($"  entités moyennes/snap : {(stats.Snapshots == 0 ? 0 : stats.EntitiesTotal / (double)stats.Snapshots):F1}");
Console.WriteLine($"  octets reçus / s      : {stats.Bytes / (double)seconds:N0}");
Console.WriteLine($"  erreurs serveur       : {stats.ServerErrors}");

static async Task RunBotAsync(int index, int port, int seconds, BotStats stats, CancellationToken ct)
{
    try
    {
        using var tcp = new TcpClient { NoDelay = true };
        await tcp.ConnectAsync("127.0.0.1", port, ct);
        NetworkStream stream = tcp.GetStream();
        var writer = new PacketWriter(256);

        await SendAsync(stream, writer, Opcode.Hello, new HelloMessage(ProtocolConstants.Version, $"bot{index}"), ct);
        (Opcode ackOpcode, _) = await ReadFrameAsync(stream, ct);
        if (ackOpcode != Opcode.HelloAck)
        {
            Interlocked.Increment(ref stats.Failed);
            return;
        }

        await SendAsync(stream, writer, Opcode.EnterWorld, new EnterWorldMessage((byte)(1 + index % 3)), ct);
        Interlocked.Increment(ref stats.Connected);

        var random = new Random(index);
        var walk = Stopwatch.StartNew();
        uint sequence = 1;
        sbyte dirX = 0;
        sbyte dirZ = 0;

        var reader = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                (Opcode opcode, byte[] payload) = await ReadFrameAsync(stream, ct);
                if (opcode == Opcode.WorldSnapshot)
                {
                    WorldSnapshotMessage snapshot = WorldSnapshotMessage.Read(payload);
                    Interlocked.Increment(ref stats.Snapshots);
                    Interlocked.Add(ref stats.EntitiesTotal, snapshot.Entities.Count);
                    Interlocked.Add(ref stats.Bytes, payload.Length + 10);
                }
                else if (opcode == Opcode.Error)
                {
                    Interlocked.Increment(ref stats.ServerErrors);
                }
            }
        }, ct);

        while (walk.Elapsed < TimeSpan.FromSeconds(seconds) && !ct.IsCancellationRequested)
        {
            // Changement de direction toutes les secondes environ.
            if (random.Next(20) == 0)
            {
                dirX = (sbyte)random.Next(-127, 128);
                dirZ = (sbyte)random.Next(-127, 128);
            }

            await SendAsync(stream, writer, Opcode.MoveInput, new MoveInputMessage(sequence++, dirX, dirZ), ct);
            await Task.Delay(50, ct);
        }
    }
    catch (Exception ex) when (ex is SocketException or System.IO.IOException or OperationCanceledException or ObjectDisposedException)
    {
        Interlocked.Increment(ref stats.Failed);
    }
}

static async Task SendAsync<T>(NetworkStream stream, PacketWriter writer, Opcode opcode, T message, CancellationToken ct) where T : IWireMessage
{
    writer.Reset();
    message.Write(writer);
    await stream.WriteAsync(FrameCodec.CreateFrame(opcode, 0, writer.WrittenSpan), ct);
}

static async Task<(Opcode Opcode, byte[] Payload)> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
{
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

sealed class BotStats
{
    public int Connected;
    public int Failed;
    public long Snapshots;
    public long EntitiesTotal;
    public long Bytes;
    public long ServerErrors;
}
