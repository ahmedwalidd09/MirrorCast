using System.Buffers.Binary;
using MirrorCast.Receiver.Models;
using Serilog;

namespace MirrorCast.Receiver.Video;

/// <summary>
/// High-performance parser for H.264 NAL (Network Abstraction Layer) units.
/// Supports both 4-byte big-endian length-prefixed frames (AVCC framing) and 3/4-byte Annex B start codes (0x000001 / 0x00000001).
/// </summary>
public static class NalUnitParser
{
    private static readonly ILogger Logger = Log.ForContext(typeof(NalUnitParser));

    /// <summary>
    /// Maximum allowable single NAL unit size (16 MB). Anything larger is considered corrupt stream framing.
    /// </summary>
    public const int MaxNalUnitSize = 16 * 1024 * 1024;

    /// <summary>
    /// Parses a single 4-byte big-endian length-prefixed NAL unit from the given buffer slice.
    /// </summary>
    /// <param name="buffer">Memory span starting with a 4-byte big-endian length prefix.</param>
    /// <param name="nalUnit">The parsed NAL unit if successful.</param>
    /// <param name="bytesConsumed">Total bytes consumed from buffer (4 + payload length).</param>
    /// <returns>True if a complete NAL unit was parsed; false if insufficient data or corrupt.</returns>
    public static bool TryParseLengthPrefixed(
        ReadOnlySpan<byte> buffer,
        out NalUnit? nalUnit,
        out int bytesConsumed)
    {
        nalUnit = null;
        bytesConsumed = 0;

        if (buffer.Length < 4)
        {
            return false;
        }

        uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(buffer[..4]);

        if (payloadLength > MaxNalUnitSize)
        {
            Logger.Warning("Detected corrupted NAL unit length: {Length} bytes (threshold={Max})", payloadLength, MaxNalUnitSize);
            return false;
        }

        int totalRequired = 4 + (int)payloadLength;
        if (buffer.Length < totalRequired)
        {
            return false; // Incomplete NAL payload in current buffer
        }

        ReadOnlySpan<byte> payload = buffer.Slice(4, (int)payloadLength);
        nalUnit = new NalUnit(payload.ToArray());
        bytesConsumed = totalRequired;
        return true;
    }

    /// <summary>
    /// Extracts all NAL units found within an Annex B byte sequence (delimited by 0x000001 or 0x00000001 start codes).
    /// </summary>
    /// <param name="data">Raw byte array containing one or more Annex B NAL units.</param>
    /// <returns>List of parsed NAL units.</returns>
    public static List<NalUnit> ParseAnnexB(byte[] data)
    {
        var nalUnits = new List<NalUnit>();
        if (data == null || data.Length < 4)
        {
            return nalUnits;
        }

        ReadOnlySpan<byte> span = data;
        var startIndices = new List<int>();
        var prefixLengths = new List<int>();

        // Scan for start codes 0x00 00 01 or 0x00 00 00 01
        for (int i = 0; i < span.Length - 2; i++)
        {
            if (span[i] == 0 && span[i + 1] == 0)
            {
                if (span[i + 2] == 1)
                {
                    startIndices.Add(i + 3);
                    prefixLengths.Add(3);
                    i += 2;
                }
                else if (i < span.Length - 3 && span[i + 2] == 0 && span[i + 3] == 1)
                {
                    startIndices.Add(i + 4);
                    prefixLengths.Add(4);
                    i += 3;
                }
            }
        }

        for (int i = 0; i < startIndices.Count; i++)
        {
            int start = startIndices[i];
            int end = (i + 1 < startIndices.Count) ? (startIndices[i + 1] - prefixLengths[i + 1]) : span.Length;
            int length = end - start;

            if (length > 0)
            {
                byte[] nalData = span.Slice(start, length).ToArray();
                nalUnits.Add(new NalUnit(nalData));
            }
        }

        return nalUnits;
    }

    /// <summary>
    /// Prepends a 4-byte Annex B start code (0x00, 0x00, 0x00, 0x01) to a raw NAL unit for hardware decoders requiring Annex B framing.
    /// </summary>
    public static byte[] ToAnnexB(byte[] nalPayload)
    {
        // If already starts with Annex B, return as is
        if (nalPayload.Length >= 4 && nalPayload[0] == 0 && nalPayload[1] == 0 && nalPayload[2] == 0 && nalPayload[3] == 1)
        {
            return nalPayload;
        }

        byte[] annexB = new byte[nalPayload.Length + 4];
        annexB[0] = 0x00;
        annexB[1] = 0x00;
        annexB[2] = 0x00;
        annexB[3] = 0x01;
        Buffer.BlockCopy(nalPayload, 0, annexB, 4, nalPayload.Length);
        return annexB;
    }
}
