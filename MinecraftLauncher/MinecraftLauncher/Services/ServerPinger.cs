using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace DedLauncher.Services;





public static class ServerPinger
{
    public record ServerPingResult(bool Success, string Version, int Online, int Max, string Description, int LatencyMs);

    public static async Task<ServerPingResult> PingAsync(string address, int port, int timeoutMs = 3000)
    {
        return await Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(timeoutMs);
            using var client = new TcpClient();
            try
            {
                await client.ConnectAsync(address, port, cts.Token);

                
                
                client.NoDelay = true;

                var stream = client.GetStream();
                stream.ReadTimeout = timeoutMs;

                
                
                using var ms = new MemoryStream();
                WriteVarInt(ms, 0x00);                       
                WriteVarInt(ms, -1);                         
                WriteString(ms, address);                    
                ms.WriteByte((byte)(port >> 8));
                ms.WriteByte((byte)(port & 0xFF));
                WriteVarInt(ms, 1);                          

                var handshake = ms.ToArray();
                using var combined = new MemoryStream();
                WriteVarInt(combined, handshake.Length);     
                combined.Write(handshake, 0, handshake.Length);
                combined.Write(new byte[] { 0x01, 0x00 }, 0, 2); 
                var frame = combined.ToArray();
                await stream.WriteAsync(frame, cts.Token);
                await stream.FlushAsync(cts.Token);

                
                int len = ReadVarInt(stream);
                var data = new byte[len];
                await ReadExactlyAsync(stream, data, len, cts.Token);

                int offset = 0;
                int packetId = ReadVarInt(data, ref offset);
                if (packetId != 0x00) throw new Exception("cyr1");

                int jsonLen = ReadVarInt(data, ref offset);
                string json = Encoding.UTF8.GetString(data, offset, jsonLen);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string version = root.TryGetProperty("version", out var v) && v.TryGetProperty("name", out var vn)
                    ? vn.GetString() ?? "" : "";

                int online = 0, max = 0;
                if (root.TryGetProperty("players", out var players))
                {
                    if (players.TryGetProperty("online", out var on)) online = on.GetInt32();
                    if (players.TryGetProperty("max", out var mx)) max = mx.GetInt32();
                }

                string description = "";
                if (root.TryGetProperty("description", out var desc))
                {
                    if (desc.ValueKind == JsonValueKind.String)
                        description = desc.GetString() ?? "";
                    else if (desc.TryGetProperty("text", out var text))
                        description = text.GetString() ?? "";
                }

                return new ServerPingResult(true, version, online, max, description, (int)sw.ElapsedMilliseconds);
            }
            catch
            {
                return new ServerPingResult(false, "", 0, 0, "", -1);
            }
            finally
            {
                sw.Stop();
            }
        });
    }

    

    private static void WriteVarInt(Stream stream, int value)
    {
        uint v = (uint)value;
        while (v >= 0x80)
        {
            stream.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }
        stream.WriteByte((byte)v);
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static int ReadVarInt(Stream stream)
    {
        int result = 0, shift = 0;
        while (true)
        {
            int b = stream.ReadByte();
            if (b < 0) throw new EndOfStreamException();
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 35) throw new Exception("VarInt too big");
        }
        return result;
    }

    private static int ReadVarInt(byte[] data, ref int offset)
    {
        int result = 0, shift = 0;
        while (true)
        {
            int b = data[offset++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return result;
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken token)
    {
        int read = 0;
        while (read < count)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read, count - read), token);
            if (n <= 0) throw new EndOfStreamException();
            read += n;
        }
    }
}
