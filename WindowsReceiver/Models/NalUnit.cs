namespace MirrorCast.Receiver.Models;

/// <summary>
/// Represents a parsed H.264 Network Abstraction Layer (NAL) unit.
/// </summary>
public class NalUnit
{
    /// <summary>
    /// Gets the raw NAL unit byte payload (including NAL header byte).
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the NAL unit type (lower 5 bits of header byte).
    /// </summary>
    public byte NalUnitType { get; }

    /// <summary>
    /// Gets the NAL reference IDC (bits 5 and 6 of header byte).
    /// </summary>
    public byte NalRefIdc { get; }

    /// <summary>
    /// Gets a value indicating whether the forbidden zero bit was set (violating H.264 standard).
    /// </summary>
    public bool ForbiddenZeroBit { get; }

    /// <summary>
    /// Gets a value indicating whether this NAL is an IDR slice (Type 5), i.e., a keyframe.
    /// </summary>
    public bool IsIdr => NalUnitType == 5;

    /// <summary>
    /// Gets a value indicating whether this NAL is a Sequence Parameter Set (Type 7).
    /// </summary>
    public bool IsSps => NalUnitType == 7;

    /// <summary>
    /// Gets a value indicating whether this NAL is a Picture Parameter Set (Type 8).
    /// </summary>
    public bool IsPps => NalUnitType == 8;

    /// <summary>
    /// Gets a value indicating whether this NAL is a Non-IDR slice (Type 1), typically P-frame or B-frame.
    /// </summary>
    public bool IsNonIdr => NalUnitType == 1;

    /// <summary>
    /// Gets a value indicating whether this NAL is Supplemental Enhancement Information (Type 6).
    /// </summary>
    public bool IsSei => NalUnitType == 6;

    /// <summary>
    /// Initializes a new instance of the <see cref="NalUnit"/> class.
    /// </summary>
    /// <param name="data">The raw payload of the NAL unit.</param>
    public NalUnit(byte[] data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        if (data.Length > 0)
        {
            byte header = data[0];
            ForbiddenZeroBit = (header & 0x80) != 0;
            NalRefIdc = (byte)((header & 0x60) >> 5);
            NalUnitType = (byte)(header & 0x1F);
        }
        else
        {
            ForbiddenZeroBit = false;
            NalRefIdc = 0;
            NalUnitType = 0;
        }
    }

    /// <summary>
    /// Returns a diagnostic string summarizing this NAL unit.
    /// </summary>
    public override string ToString()
    {
        string typeName = NalUnitType switch
        {
            1 => "Non-IDR Slice (P/B)",
            5 => "IDR Slice (Keyframe)",
            6 => "SEI",
            7 => "SPS",
            8 => "PPS",
            9 => "Access Unit Delimiter",
            _ => $"Type {NalUnitType}"
        };
        return $"NAL [{typeName}] (Length={Data.Length} bytes, RefIdc={NalRefIdc})";
    }
}
