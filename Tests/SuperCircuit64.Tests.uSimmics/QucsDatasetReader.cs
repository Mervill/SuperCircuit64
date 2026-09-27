using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SuperCircuit64.Tests.uSimmics;

/// <summary>
/// One named array decoded from a QucsStudio binary dataset (.dat) file.
/// </summary>
internal sealed record QucsVariable(string Name, string? DependencyName, double[] Real, double[]? Imaginary);

/// <summary>
/// Reads QucsStudio's proprietary binary "QucsData" dataset format, as exported by the .sch
/// fixtures under sources/. The layout was reverse-engineered from a reference MATLAB reader
/// (loadQucsDataset, posted to the QucsStudio forums):
///
/// magic "QucsData" (8 bytes) | version int32 | headerSize int32 | header[headerSize] | data...
///
/// The header is a packed sequence of variable descriptors:
/// type int32 | count int32 | name (nul-terminated) | dependencyName (nul-terminated, only if
/// bit 1 of type is set). Bit 2 of type is set for real-valued data (count doubles); when clear
/// the data is complex (count interleaved real/imaginary double pairs). Each descriptor's data
/// array follows immediately after the header block, in the same order as the descriptors.
/// </summary>
internal static class QucsDatasetReader
{
    private const string Magic = "QucsData";

    public static IReadOnlyList<QucsVariable> Read(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(Magic.Length));
        if (magic != Magic)
            throw new InvalidDataException($"'{path}' is not a QucsStudio dataset file.");

        reader.ReadInt32(); // format version, unused
        int headerSize = reader.ReadInt32();
        byte[] header = reader.ReadBytes(headerSize);

        var descriptors = ParseDescriptors(header);

        var variables = new List<QucsVariable>(descriptors.Count);
        foreach (var descriptor in descriptors)
            variables.Add(ReadVariable(reader, descriptor));

        return variables;
    }

    private static QucsVariable ReadVariable(BinaryReader reader, Descriptor descriptor)
    {
        if (descriptor.IsReal)
        {
            double[] real = ReadDoubles(reader, descriptor.Count);
            return new QucsVariable(descriptor.Name, descriptor.DependencyName, real, Imaginary: null);
        }

        double[] interleaved = ReadDoubles(reader, descriptor.Count * 2);
        var complexReal = new double[descriptor.Count];
        var imaginary = new double[descriptor.Count];
        for (int i = 0; i < descriptor.Count; i++)
        {
            complexReal[i] = interleaved[2 * i];
            imaginary[i] = interleaved[2 * i + 1];
        }

        return new QucsVariable(descriptor.Name, descriptor.DependencyName, complexReal, imaginary);
    }

    private static double[] ReadDoubles(BinaryReader reader, int count)
    {
        var values = new double[count];
        for (int i = 0; i < count; i++)
            values[i] = reader.ReadDouble();

        return values;
    }

    private readonly record struct Descriptor(string Name, string? DependencyName, int Count, bool IsReal);

    private static List<Descriptor> ParseDescriptors(byte[] header)
    {
        var descriptors = new List<Descriptor>();
        int offset = 0;

        while (offset + 9 <= header.Length)
        {
            int type = BitConverter.ToInt32(header, offset);
            int count = BitConverter.ToInt32(header, offset + 4);
            offset += 8;

            string name = ReadCString(header, ref offset);

            bool isDependent = (type & 0x2) != 0;
            bool isReal = (type & 0x4) != 0;
            string? dependencyName = isDependent ? ReadCString(header, ref offset) : null;

            descriptors.Add(new Descriptor(name, dependencyName, count, isReal));
        }

        return descriptors;
    }

    private static string ReadCString(byte[] header, ref int offset)
    {
        int start = offset;
        while (header[offset] != 0)
            offset++;

        string value = Encoding.ASCII.GetString(header, start, offset - start);
        offset++; // consume the nul terminator
        return value;
    }
}
