using System.IO.Compression;
using System.Text;

namespace DedLauncher.Helpers;

/// <summary>
/// Читает сервера из minecraft servers.dat (NBT, gzip).
/// Минимальный NBT-парсер: Compound, List, String, Int, Byte, Long, ByteArray(пропуск).
/// </summary>
public static class NbtServersReader
{
    public record McServer(string Name, string Ip, int Port, string? IconBase64);

    public static List<McServer> ReadServersDat(string path)
    {
        var result = new List<McServer>();
        try
        {
            if (!File.Exists(path)) return result;

            byte[] raw = File.ReadAllBytes(path);
            byte[] data;

            // servers.dat лежит в gzip (первые байты 1F 8B)
            if (raw.Length > 2 && raw[0] == 0x1F && raw[1] == 0x8B)
            {
                using var gz = new GZipStream(new MemoryStream(raw), CompressionMode.Decompress);
                using var ms = new MemoryStream();
                gz.CopyTo(ms);
                data = ms.ToArray();
            }
            else
            {
                data = raw;
            }

            var reader = new NbtReader(data);
            var root = reader.ReadTag();
            if (root is not NbtCompound rootCompound) return result;

            foreach (var tag in rootCompound.Children)
            {
                if (tag is NbtList list && list.ElementType == NbtType.Compound)
                {
                    foreach (var item in list.Items)
                    {
                        if (item is not NbtCompound server) continue;
                        var name = server.GetString("name") ?? "";
                        var ip = server.GetString("ip") ?? "";
                        var port = server.GetInt("port", 25565);
                        if (string.IsNullOrEmpty(ip)) continue;
                        // В ip может быть вшит порт? В новых версиях порт отдельным интом.
                        result.Add(new McServer(
                            string.IsNullOrEmpty(name) ? ip : name,
                            ip,
                            port,
                            server.GetString("icon")
                        ));
                    }
                }
            }
        }
        catch { }
        return result;
    }

    private enum NbtType : byte
    {
        End = 0, Byte = 1, Short = 2, Int = 3, Long = 4,
        Float = 5, Double = 6, ByteArray = 7, String = 8,
        List = 9, Compound = 10, IntArray = 11, LongArray = 12
    }

    private abstract class NbtTag
    {
        public string Name { get; set; } = "";
    }

    private sealed class NbtCompound : NbtTag
    {
        public List<NbtTag> Children { get; } = new();

        public string? GetString(string key)
            => Children.OfType<NbtValue>().FirstOrDefault(c => c.Name == key && c.Type == NbtType.String)?.StringValue;

        public int GetInt(string key, int fallback)
        {
            var v = Children.OfType<NbtValue>().FirstOrDefault(c => c.Name == key && c.Type == NbtType.Int);
            return v?.IntValue ?? fallback;
        }
    }

    private sealed class NbtValue : NbtTag
    {
        public NbtType Type;
        public string StringValue = "";
        public int IntValue;
        public long LongValue;
        public int Length;          // для массивов
        public byte[] Bytes = Array.Empty<byte>();
    }

    private sealed class NbtList : NbtTag
    {
        public NbtType ElementType;
        public List<NbtTag> Items { get; } = new();
    }

    private sealed class NbtReader
    {
        private readonly byte[] _data;
        private int _pos;

        public NbtReader(byte[] data)
        {
            _data = data;
        }

        public NbtTag ReadTag()
        {
            var type = (NbtType)_data[_pos++];
            if (type == NbtType.End) return new NbtValue { Type = type };
            var name = ReadString();
            var tag = ReadPayload(type);
            tag.Name = name;
            return tag;
        }

        private NbtTag ReadPayload(NbtType type)
        {
            switch (type)
            {
                case NbtType.Compound:
                    var compound = new NbtCompound();
                    while ((NbtType)_data[_pos] != NbtType.End)
                        compound.Children.Add(ReadTag());
                    _pos++; // TAG_End
                    return compound;

                case NbtType.List:
                    var list = new NbtList { ElementType = (NbtType)_data[_pos++] };
                    int count = ReadInt();
                    for (int i = 0; i < count; i++)
                    {
                        var item = ReadPayload(list.ElementType);
                        list.Items.Add(item);
                    }
                    return list;

                case NbtType.String:
                    return new NbtValue { Type = type, StringValue = ReadString() };

                case NbtType.Int:
                    return new NbtValue { Type = type, IntValue = ReadInt() };

                case NbtType.Long:
                    return new NbtValue { Type = type, LongValue = ReadLong() };

                case NbtType.Byte:
                    return new NbtValue { Type = type, IntValue = _data[_pos++] };

                case NbtType.Short:
                    return new NbtValue { Type = type, IntValue = ReadShort() };

                case NbtType.ByteArray:
                case NbtType.IntArray:
                case NbtType.LongArray:
                    int len = ReadInt();
                    _pos += len * (type == NbtType.ByteArray ? 1 : 4);
                    return new NbtValue { Type = type, Length = len };

                default:
                    // Float/Double — пропускаем 4/8 байт
                    _pos += type == NbtType.Float ? 4 : 8;
                    return new NbtValue { Type = type };
            }
        }

        private string ReadString()
        {
            ushort len = (ushort)((_data[_pos] << 8) | _data[_pos + 1]);
            _pos += 2;
            var s = Encoding.UTF8.GetString(_data, _pos, len);
            _pos += len;
            return s;
        }

        private int ReadInt()
        {
            int v = (_data[_pos] << 24) | (_data[_pos + 1] << 16) | (_data[_pos + 2] << 8) | _data[_pos + 3];
            _pos += 4;
            return v;
        }

        private long ReadLong()
        {
            long v = 0;
            for (int i = 0; i < 8; i++) v = (v << 8) | _data[_pos++];
            return v;
        }

        private int ReadShort()
        {
            int v = (_data[_pos] << 8) | _data[_pos + 1];
            _pos += 2;
            return v;
        }
    }
}
