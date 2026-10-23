using System.Runtime.InteropServices;

namespace MirrorCast.Receiver.Video;

/// <summary>
/// COM interop and P/Invoke definitions for Windows Media Foundation (MF) MFT video decoding.
/// </summary>
internal static class MediaFoundationNative
{
    public const uint MF_VERSION = 0x00020070;
    public const uint MFSTARTUP_NOSOCKET = 0x1;

    public static readonly Guid CLSID_CMSH264DecoderMFT = new("62CE7E72-4C71-4d20-B15D-452831A87D9D");
    public static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f827-49ed-b64c-ae505ca22987");
    public static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    public static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4b6b-8c53-acfd204d1617");
    public static readonly Guid MF_MT_DEFAULT_STRIDE = new("64440490-4c87-4002-ba91-50ce4f072e12");

    public static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFVideoFormat_H264 = new("34363248-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFVideoFormat_NV12 = new("3231564e-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00aa00389b71");

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFStartup(uint version, uint dwFlags);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFShutdown();

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateSample(out IMFSample ppIMFSample);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateMemoryBuffer(uint cbMaxLength, out IMFMediaBuffer ppBuffer);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateMediaType(out IMFMediaType ppMFType);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
    public interface IMFAttributes
    {
        [PreserveSig] int GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, Out] ref nint pValue);
        [PreserveSig] int GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out int pType);
        [PreserveSig] int CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, nint Value, out bool pbResult);
        [PreserveSig] int Compare(IMFAttributes pTheAttrs, int MatchType, out bool pbResult);
        [PreserveSig] int GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);
        [PreserveSig] int GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue, uint cchBufSize, out uint pcchLength);
        [PreserveSig] int GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out nint ppwszValue, out uint pcchLength);
        [PreserveSig] int GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);
        [PreserveSig] int GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out nint ppBuf, out uint pcbSize);
        [PreserveSig] int GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out nint ppv);
        [PreserveSig] int SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, nint Value);
        [PreserveSig] int DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);
        [PreserveSig] int SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);
        [PreserveSig] int SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In] byte[] pBuf, uint cbBufSize);
        [PreserveSig] int SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint pcItems);
        [PreserveSig] int GetItemByIndex(uint unIndex, out Guid pGuidKey, [In, Out] ref nint pValue);
        [PreserveSig] int CopyAllItems(IMFAttributes pDest);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("44AE3870-1848-4BDF-91AD-80492E31E43D")]
    public interface IMFMediaType
    {
        // IMFAttributes vtable methods (1 to 24)
        [PreserveSig] int GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, Out] ref nint pValue);
        [PreserveSig] int GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out int pType);
        [PreserveSig] int CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, nint Value, out bool pbResult);
        [PreserveSig] int Compare(IMFAttributes pTheAttrs, int MatchType, out bool pbResult);
        [PreserveSig] int GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);
        [PreserveSig] int GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue, uint cchBufSize, out uint pcchLength);
        [PreserveSig] int GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out nint ppwszValue, out uint pcchLength);
        [PreserveSig] int GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);
        [PreserveSig] int GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out nint ppBuf, out uint pcbSize);
        [PreserveSig] int GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out nint ppv);
        [PreserveSig] int SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, nint Value);
        [PreserveSig] int DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);
        [PreserveSig] int SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);
        [PreserveSig] int SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In] byte[] pBuf, uint cbBufSize);
        [PreserveSig] int SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint pcItems);
        [PreserveSig] int GetItemByIndex(uint unIndex, out Guid pGuidKey, [In, Out] ref nint pValue);
        [PreserveSig] int CopyAllItems(IMFAttributes pDest);

        // IMFMediaType methods (25 to 29)
        [PreserveSig] int GetMajorType(out Guid pguidMajorType);
        [PreserveSig] int IsCompressedFormat(out bool pfCompressed);
        [PreserveSig] int IsEqual(IMFMediaType pIMediaType, out uint pdwFlags);
        [PreserveSig] int GetRepresentation(Guid guidRepresentation, out nint ppvRepresentation);
        [PreserveSig] int FreeRepresentation(Guid guidRepresentation, nint pvRepresentation);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("C40A00F2-B93A-4D80-AE8C-5A1C634F58E4")]
    public interface IMFSample
    {
        // IMFAttributes vtable methods (1 to 24)
        [PreserveSig] int GetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, Out] ref nint pValue);
        [PreserveSig] int GetItemType([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out int pType);
        [PreserveSig] int CompareItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, nint Value, out bool pbResult);
        [PreserveSig] int Compare(IMFAttributes pTheAttrs, int MatchType, out bool pbResult);
        [PreserveSig] int GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcchLength);
        [PreserveSig] int GetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue, uint cchBufSize, out uint pcchLength);
        [PreserveSig] int GetAllocatedString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out nint ppwszValue, out uint pcchLength);
        [PreserveSig] int GetBlobSize([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out uint pcbBlobSize);
        [PreserveSig] int GetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [Out] byte[] pBuf, uint cbBufSize, out uint pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, out nint ppBuf, out uint pcbSize);
        [PreserveSig] int GetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out nint ppv);
        [PreserveSig] int SetItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, nint Value);
        [PreserveSig] int DeleteItem([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, double fValue);
        [PreserveSig] int SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPStruct)] Guid guidValue);
        [PreserveSig] int SetString([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In] byte[] pBuf, uint cbBufSize);
        [PreserveSig] int SetUnknown([In, MarshalAs(UnmanagedType.LPStruct)] Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint pcItems);
        [PreserveSig] int GetItemByIndex(uint unIndex, out Guid pGuidKey, [In, Out] ref nint pValue);
        [PreserveSig] int CopyAllItems(IMFAttributes pDest);

        // IMFSample methods (25 to 38)
        [PreserveSig] int GetSampleFlags(out uint pdwSampleFlags);
        [PreserveSig] int SetSampleFlags(uint dwSampleFlags);
        [PreserveSig] int GetSampleTime(out long phnsSampleTime);
        [PreserveSig] int SetSampleTime(long hnsSampleTime);
        [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
        [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
        [PreserveSig] int GetBufferCount(out uint pdwBufferCount);
        [PreserveSig] int GetBufferByIndex(uint dwIndex, out IMFMediaBuffer ppBuffer);
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
        [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
        [PreserveSig] int RemoveBufferByIndex(uint dwIndex);
        [PreserveSig] int RemoveAllBuffers();
        [PreserveSig] int GetTotalLength(out uint pcbTotalLength);
        [PreserveSig] int CopyToBuffer(IMFMediaBuffer pBuffer);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("045FA593-8799-42B8-BC8D-8968C6453507")]
    public interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out nint ppbBuffer, out uint pcbMaxLength, out uint pcbCurrentLength);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out uint pcbCurrentLength);
        [PreserveSig] int SetCurrentLength(uint cbCurrentLength);
        [PreserveSig] int GetMaxLength(out uint pcbMaxLength);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MFT_INPUT_STREAM_INFO
    {
        public long hnsMaxLatency;
        public uint dwFlags;
        public uint cbSize;
        public uint cbMaxLookahead;
        public uint cbAlignment;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MFT_OUTPUT_STREAM_INFO
    {
        public uint dwFlags;
        public uint cbSize;
        public uint cbAlignment;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MFT_OUTPUT_DATA_BUFFER
    {
        public uint dwStreamID;
        public nint pSample;
        public uint dwStatus;
        public nint pEvents;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("BF94C121-5B05-4E6F-8000-BA598961414D")]
    public interface IMFTransform
    {
        [PreserveSig] int GetStreamLimits(out uint pdwInputMinimum, out uint pdwInputMaximum, out uint pdwOutputMinimum, out uint pdwOutputMaximum);
        [PreserveSig] int GetStreamCount(out uint pcInputStreams, out uint pcOutputStreams);
        [PreserveSig] int GetStreamIDs(uint dwInputIDArraySize, [Out] uint[] pdwInputIDs, uint dwOutputIDArraySize, [Out] uint[] pdwOutputIDs);
        [PreserveSig] int GetInputStreamInfo(uint dwInputStreamID, out MFT_INPUT_STREAM_INFO pStreamInfo);
        [PreserveSig] int GetOutputStreamInfo(uint dwOutputStreamID, out MFT_OUTPUT_STREAM_INFO pStreamInfo);
        [PreserveSig] int GetAttributes(out IMFAttributes pAttributes);
        [PreserveSig] int GetInputStreamAttributes(uint dwInputStreamID, out IMFAttributes pAttributes);
        [PreserveSig] int GetOutputStreamAttributes(uint dwOutputStreamID, out IMFAttributes pAttributes);
        [PreserveSig] int DeleteInputStream(uint dwStreamID);
        [PreserveSig] int AddInputStreams(uint cStreams, [In] uint[] adwStreamIDs);
        [PreserveSig] int GetInputAvailableType(uint dwInputStreamID, uint dwTypeIndex, out IMFMediaType ppType);
        [PreserveSig] int GetOutputAvailableType(uint dwOutputStreamID, uint dwTypeIndex, out IMFMediaType ppType);
        [PreserveSig] int SetInputType(uint dwInputStreamID, [In] IMFMediaType pType, uint dwFlags);
        [PreserveSig] int SetOutputType(uint dwOutputStreamID, [In] IMFMediaType pType, uint dwFlags);
        [PreserveSig] int GetInputCurrentType(uint dwInputStreamID, out IMFMediaType ppType);
        [PreserveSig] int GetOutputCurrentType(uint dwOutputStreamID, out IMFMediaType ppType);
        [PreserveSig] int GetInputStatus(uint dwInputStreamID, out uint pdwFlags);
        [PreserveSig] int GetOutputStatus(out uint pdwFlags);
        [PreserveSig] int SetOutputBounds(long hnsLowerBound, long hnsUpperBound);
        [PreserveSig] int ProcessEvent(uint dwInputStreamID, nint pEvent);
        [PreserveSig] int ProcessMessage(uint eMessage, nuint ulParam);
        [PreserveSig] int ProcessInput(uint dwInputStreamID, IMFSample pSample, uint dwFlags);
        [PreserveSig] int ProcessOutput(uint dwFlags, uint cOutputBufferCount, [In, Out] MFT_OUTPUT_DATA_BUFFER[] pOutputSamples, out uint pdwStatus);
    }
}
