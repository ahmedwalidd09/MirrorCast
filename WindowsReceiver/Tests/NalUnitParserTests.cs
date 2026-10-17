using System.Buffers.Binary;
using MirrorCast.Receiver.Models;
using MirrorCast.Receiver.Video;

namespace MirrorCast.Receiver.Tests;

/// <summary>
/// Unit tests verifying the robustness, RFC compliance, and parsing correctness of <see cref="NalUnitParser"/>.
/// </summary>
public static class NalUnitParserTests
{
    /// <summary>
    /// Executes all NalUnitParser unit test cases and returns a summary report.
    /// </summary>
    /// <returns>True if all tests pass; otherwise throws an exception or returns false.</returns>
    public static bool RunAllTests(out string summary)
    {
        int passed = 0;
        int total = 0;
        var results = new List<string>();

        void RunTest(string testName, Action testAction)
        {
            total++;
            try
            {
                testAction();
                passed++;
                results.Add($"[PASS] {testName}");
            }
            catch (Exception ex)
            {
                results.Add($"[FAIL] {testName}: {ex.Message}");
            }
        }

        RunTest(nameof(TestValidLengthPrefixedNalParsing), TestValidLengthPrefixedNalParsing);
        RunTest(nameof(TestSpsIdentification), TestSpsIdentification);
        RunTest(nameof(TestPpsIdentification), TestPpsIdentification);
        RunTest(nameof(TestIdrSliceIdentification), TestIdrSliceIdentification);
        RunTest(nameof(TestNonIdrSliceIdentification), TestNonIdrSliceIdentification);
        RunTest(nameof(TestCorruptLengthExceeds16Mb), TestCorruptLengthExceeds16Mb);
        RunTest(nameof(TestIncompleteBufferReturnsFalse), TestIncompleteBufferReturnsFalse);
        RunTest(nameof(TestAnnexBParsing), TestAnnexBParsing);
        RunTest(nameof(TestToAnnexBConversion), TestToAnnexBConversion);

        summary = $"NalUnitParser Tests: {passed}/{total} passed.\n" + string.Join("\n", results);
        return passed == total;
    }

    /// <summary>
    /// Tests standard 4-byte big-endian length-prefixed NAL unit parsing.
    /// </summary>
    public static void TestValidLengthPrefixedNalParsing()
    {
        byte[] payload = new byte[] { 0x65, 0x88, 0x84, 0x00, 0x11, 0x22 }; // IDR slice
        byte[] packet = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32BigEndian(packet, (uint)payload.Length);
        Buffer.BlockCopy(payload, 0, packet, 4, payload.Length);

        bool success = NalUnitParser.TryParseLengthPrefixed(packet, out NalUnit? nal, out int consumed);

        AssertTrue(success, "Expected successful parse of length-prefixed NAL.");
        AssertNotNull(nal, "NalUnit must not be null.");
        AssertEqual(packet.Length, consumed, "Consumed bytes should equal header + payload.");
        AssertEqual(5, nal!.NalUnitType, "Should identify NAL unit type 5 (IDR).");
        AssertTrue(nal.IsIdr, "IsIdr must be true.");
    }

    /// <summary>
    /// Tests identification of SPS (NAL unit type 7, e.g. header 0x67).
    /// </summary>
    public static void TestSpsIdentification()
    {
        byte[] sps = new byte[] { 0x67, 0x42, 0x00, 0x1f, 0x96, 0x54, 0x02, 0x80, 0x2d, 0x88 };
        var nal = new NalUnit(sps);

        AssertEqual(7, nal.NalUnitType, "NAL type must be 7.");
        AssertTrue(nal.IsSps, "IsSps must be true.");
        AssertFalse(nal.IsIdr, "IsIdr must be false.");
        AssertFalse(nal.IsPps, "IsPps must be false.");
    }

    /// <summary>
    /// Tests identification of PPS (NAL unit type 8, e.g. header 0x68).
    /// </summary>
    public static void TestPpsIdentification()
    {
        byte[] pps = new byte[] { 0x68, 0xce, 0x3c, 0x80 };
        var nal = new NalUnit(pps);

        AssertEqual(8, nal.NalUnitType, "NAL type must be 8.");
        AssertTrue(nal.IsPps, "IsPps must be true.");
        AssertFalse(nal.IsSps, "IsSps must be false.");
    }

    /// <summary>
    /// Tests identification of IDR keyframe (NAL unit type 5, e.g. header 0x65).
    /// </summary>
    public static void TestIdrSliceIdentification()
    {
        byte[] idr = new byte[] { 0x65, 0xb8, 0x00, 0x04 };
        var nal = new NalUnit(idr);

        AssertEqual(5, nal.NalUnitType, "NAL type must be 5.");
        AssertTrue(nal.IsIdr, "IsIdr must be true.");
    }

    /// <summary>
    /// Tests identification of Non-IDR P/B frame (NAL unit type 1, e.g. header 0x41 or 0x61).
    /// </summary>
    public static void TestNonIdrSliceIdentification()
    {
        byte[] nonIdr = new byte[] { 0x41, 0x9a, 0x24 };
        var nal = new NalUnit(nonIdr);

        AssertEqual(1, nal.NalUnitType, "NAL type must be 1.");
        AssertTrue(nal.IsNonIdr, "IsNonIdr must be true.");
        AssertFalse(nal.IsIdr, "IsIdr must be false.");
    }

    /// <summary>
    /// Tests that corrupt length headers (> 16MB threshold) are rejected.
    /// </summary>
    public static void TestCorruptLengthExceeds16Mb()
    {
        byte[] corruptPacket = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(corruptPacket, 17 * 1024 * 1024); // 17 MB

        bool success = NalUnitParser.TryParseLengthPrefixed(corruptPacket, out NalUnit? nal, out int consumed);

        AssertFalse(success, "Corrupt packets exceeding 16MB must return false.");
        AssertNull(nal, "NalUnit must be null when corrupt.");
        AssertEqual(0, consumed, "Consumed must be 0 when rejected.");
    }

    /// <summary>
    /// Tests that an incomplete buffer returns false without advancing consumed count.
    /// </summary>
    public static void TestIncompleteBufferReturnsFalse()
    {
        byte[] incompletePacket = new byte[10];
        BinaryPrimitives.WriteUInt32BigEndian(incompletePacket, 100); // Declares 100 bytes, but only 6 available

        bool success = NalUnitParser.TryParseLengthPrefixed(incompletePacket, out NalUnit? nal, out int consumed);

        AssertFalse(success, "Incomplete payload must return false.");
        AssertEqual(0, consumed, "Consumed count must remain 0 when incomplete.");
    }

    /// <summary>
    /// Tests extraction of multiple NAL units from an Annex B byte stream.
    /// </summary>
    public static void TestAnnexBParsing()
    {
        // 0x00000001 SPS, 0x000001 PPS, 0x00000001 IDR
        byte[] stream = new byte[]
        {
            0x00, 0x00, 0x00, 0x01, 0x67, 0x42, 0x00, 0x1f, // SPS
            0x00, 0x00, 0x01, 0x68, 0xce, 0x3c,             // PPS (3-byte start code)
            0x00, 0x00, 0x00, 0x01, 0x65, 0x88, 0x84        // IDR
        };

        var nals = NalUnitParser.ParseAnnexB(stream);

        AssertEqual(3, nals.Count, "Should parse exactly 3 NAL units from Annex B stream.");
        AssertTrue(nals[0].IsSps, "First NAL must be SPS.");
        AssertTrue(nals[1].IsPps, "Second NAL must be PPS.");
        AssertTrue(nals[2].IsIdr, "Third NAL must be IDR.");
    }

    /// <summary>
    /// Tests conversion from raw NAL payload to 4-byte Annex B framing.
    /// </summary>
    public static void TestToAnnexBConversion()
    {
        byte[] payload = new byte[] { 0x65, 0x11, 0x22, 0x33 };
        byte[] annexB = NalUnitParser.ToAnnexB(payload);

        AssertEqual(payload.Length + 4, annexB.Length, "Annex B length must be payload + 4.");
        AssertEqual(0, annexB[0], "Byte 0 must be 0x00.");
        AssertEqual(0, annexB[1], "Byte 1 must be 0x00.");
        AssertEqual(0, annexB[2], "Byte 2 must be 0x00.");
        AssertEqual(1, annexB[3], "Byte 3 must be 0x01.");
        AssertEqual(0x65, annexB[4], "Byte 4 must match NAL header.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Assertion failed: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new InvalidOperationException($"Assertion failed: {message}");
    }

    private static void AssertNotNull(object? obj, string message)
    {
        if (obj == null) throw new InvalidOperationException($"Assertion failed: {message}");
    }

    private static void AssertNull(object? obj, string message)
    {
        if (obj != null) throw new InvalidOperationException($"Assertion failed: {message}");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Assertion failed: Expected '{expected}', got '{actual}'. {message}");
        }
    }
}
