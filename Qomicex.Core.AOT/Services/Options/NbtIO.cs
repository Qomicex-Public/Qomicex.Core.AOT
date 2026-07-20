using System.Text;

namespace Qomicex.Core.AOT.Services.Options;

#region NBT 标签类型常量

internal static class NbtTagType
{
    public const byte End = 0;
    public const byte Byte = 1;
    public const byte String = 8;
    public const byte List = 9;
    public const byte Compound = 10;
}

#endregion

#region NBT 复合标签

internal sealed class NbtCompound : Dictionary<string, object>
{
    public NbtCompound(IEqualityComparer<string>? comparer)
        : base(comparer)
    {
    }
}

#endregion

#region NBT 读写

internal static class NbtIO
{
    public static NbtCompound Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return ReadRootCompound(reader);
    }

    public static void Write(Stream stream, NbtCompound root)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteRootCompound(writer, root);
    }

    private static NbtCompound ReadRootCompound(BinaryReader reader)
    {
        var tagType = reader.ReadByte();
        if (tagType != NbtTagType.Compound)
        {
            throw new InvalidDataException($"Expected root compound tag, but found type {tagType}.");
        }

        _ = ReadString(reader);
        return ReadCompoundPayload(reader);
    }

    private static void WriteRootCompound(BinaryWriter writer, NbtCompound root)
    {
        writer.Write(NbtTagType.Compound);
        WriteString(writer, string.Empty);
        WriteCompoundPayload(writer, root);
    }

    private static NbtCompound ReadCompoundPayload(BinaryReader reader)
    {
        var compound = new NbtCompound(StringComparer.Ordinal);

        while (true)
        {
            var tagType = reader.ReadByte();
            if (tagType == NbtTagType.End)
            {
                return compound;
            }

            var name = ReadString(reader);
            compound[name] = ReadTagPayload(reader, tagType);
        }
    }

    private static object ReadTagPayload(BinaryReader reader, byte tagType)
    {
        return tagType switch
        {
            NbtTagType.Byte => reader.ReadByte() != 0,
            NbtTagType.String => ReadString(reader),
            NbtTagType.List => ReadListPayload(reader),
            NbtTagType.Compound => ReadCompoundPayload(reader),
            _ => throw new InvalidDataException($"Unsupported NBT tag type {tagType} in servers.dat.")
        };
    }

    private static object ReadListPayload(BinaryReader reader)
    {
        var elementType = reader.ReadByte();
        var length = ReadInt32BigEndian(reader);
        if (length < 0)
        {
            throw new InvalidDataException("NBT list length cannot be negative.");
        }

        if (elementType != NbtTagType.Compound)
        {
            throw new InvalidDataException($"Unsupported NBT list element type {elementType} in servers.dat.");
        }

        var items = new List<NbtCompound>(length);
        for (var i = 0; i < length; i++)
        {
            items.Add(ReadCompoundPayload(reader));
        }

        return items;
    }

    private static void WriteCompoundPayload(BinaryWriter writer, NbtCompound compound)
    {
        foreach (var entry in compound)
        {
            WriteNamedTag(writer, entry.Key, entry.Value);
        }

        writer.Write(NbtTagType.End);
    }

    private static void WriteNamedTag(BinaryWriter writer, string name, object value)
    {
        switch (value)
        {
            case bool boolean:
                writer.Write(NbtTagType.Byte);
                WriteString(writer, name);
                writer.Write(boolean ? (byte)1 : (byte)0);
                break;
            case string text:
                writer.Write(NbtTagType.String);
                WriteString(writer, name);
                WriteString(writer, text);
                break;
            case List<NbtCompound> compounds:
                writer.Write(NbtTagType.List);
                WriteString(writer, name);
                writer.Write(NbtTagType.Compound);
                WriteInt32BigEndian(writer, compounds.Count);
                foreach (var compound in compounds)
                {
                    WriteCompoundPayload(writer, compound);
                }
                break;
            case NbtCompound compound:
                writer.Write(NbtTagType.Compound);
                WriteString(writer, name);
                WriteCompoundPayload(writer, compound);
                break;
            default:
                throw new InvalidOperationException($"Unsupported NBT value type '{value.GetType().FullName}'.");
        }
    }

    private static string ReadString(BinaryReader reader)
    {
        var length = ReadUInt16BigEndian(reader);
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length)
        {
            throw new EndOfStreamException("Unexpected end of stream while reading NBT string.");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("NBT string length exceeds UInt16.MaxValue.");
        }

        WriteUInt16BigEndian(writer, (ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static int ReadInt32BigEndian(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        ReadExactly(reader, bytes, "NBT Int32");

        if (BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }

        return BitConverter.ToInt32(bytes);
    }

    private static ushort ReadUInt16BigEndian(BinaryReader reader)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        ReadExactly(reader, bytes, "NBT UInt16");

        if (BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }

        return BitConverter.ToUInt16(bytes);
    }

    private static void WriteInt32BigEndian(BinaryWriter writer, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(bytes, value);
        if (BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }

        writer.Write(bytes);
    }

    private static void WriteUInt16BigEndian(BinaryWriter writer, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BitConverter.TryWriteBytes(bytes, value);
        if (BitConverter.IsLittleEndian)
        {
            bytes.Reverse();
        }

        writer.Write(bytes);
    }

    private static void ReadExactly(BinaryReader reader, Span<byte> buffer, string valueName)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = reader.Read(buffer[totalRead..]);
            if (read == 0)
            {
                throw new EndOfStreamException($"Unexpected end of stream while reading {valueName}.");
            }

            totalRead += read;
        }
    }

    public static string GetOptionalString(NbtCompound compound, string name)
    {
        if (!compound.TryGetValue(name, out var value))
        {
            return null!;
        }

        if (value is not string text)
        {
            throw new InvalidDataException($"Server entry tag '{name}' is not a string.");
        }

        return text;
    }

    public static bool GetOptionalBool(NbtCompound compound, string name)
    {
        if (!compound.TryGetValue(name, out var value))
        {
            return false;
        }

        return value switch
        {
            bool boolean => boolean,
            byte number => number != 0,
            _ => throw new InvalidDataException($"Server entry tag '{name}' is not a byte/boolean.")
        };
    }
}

#endregion
