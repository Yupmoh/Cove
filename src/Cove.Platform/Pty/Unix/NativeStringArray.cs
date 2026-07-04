using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Cove.Platform.Pty.Unix;

internal sealed unsafe class NativeStringArray : IDisposable
{
    private readonly List<IntPtr> _strings = new();
    private readonly IntPtr _array;

    public NativeStringArray(IReadOnlyList<string> items)
    {
        foreach (var s in items)
            _strings.Add(Utf8ToHGlobal(s));

        int count = _strings.Count;
        _array = Marshal.AllocHGlobal((count + 1) * IntPtr.Size);
        for (int i = 0; i < count; i++)
            Marshal.WriteIntPtr(_array, i * IntPtr.Size, _strings[i]);
        Marshal.WriteIntPtr(_array, count * IntPtr.Size, IntPtr.Zero);
    }

    public IntPtr Pointer => _array;

    private static IntPtr Utf8ToHGlobal(string s)
    {
        int byteCount = Encoding.UTF8.GetByteCount(s);
        IntPtr p = Marshal.AllocHGlobal(byteCount + 1);
        byte* dst = (byte*)p;
        int written = Encoding.UTF8.GetBytes(s, new Span<byte>(dst, byteCount));
        dst[written] = 0;
        return p;
    }

    public void Dispose()
    {
        foreach (var p in _strings)
            Marshal.FreeHGlobal(p);
        Marshal.FreeHGlobal(_array);
    }
}
