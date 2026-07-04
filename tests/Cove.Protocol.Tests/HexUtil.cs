using System;

namespace Cove.Protocol.Tests;

public static class HexUtil
{
    public static byte[] Bytes(string spaced)
    {
        Span<char> compact = spaced.Length <= 1024 ? stackalloc char[spaced.Length] : new char[spaced.Length];
        int n = 0;
        foreach (char c in spaced)
            if (!char.IsWhiteSpace(c))
                compact[n++] = c;
        return Convert.FromHexString(compact.Slice(0, n));
    }
}
