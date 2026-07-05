using System;
using System.Text;

namespace Cove.Engine.Pty;

public sealed class Osc7Parser
{
    private const int MaxPayload = 4096;

    private enum State : byte { Ground, Intro, Payload }

    private State _state = State.Ground;
    private int _introMatched;
    private readonly byte[] _payload = new byte[MaxPayload];
    private int _payloadLen;
    private bool _escPending;

    public string? Feed(ReadOnlySpan<byte> data)
    {
        string? last = null;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];

            switch (_state)
            {
                case State.Ground:
                case State.Intro:
                    if (b == 0x1B && _introMatched == 0)
                    {
                        _introMatched = 1;
                        _state = State.Intro;
                    }
                    else if (_introMatched == 1 && b == 0x5D)
                    {
                        _introMatched = 2;
                        _state = State.Intro;
                    }
                    else if (_introMatched == 2 && b == (byte)'7')
                    {
                        _introMatched = 3;
                        _state = State.Intro;
                    }
                    else if (_introMatched == 3 && b == (byte)';')
                    {
                        _introMatched = 4;
                        _state = State.Payload;
                        _payloadLen = 0;
                        _escPending = false;
                    }
                    else
                    {
                        if (b == 0x1B)
                        {
                            _introMatched = 1;
                            _state = State.Intro;
                        }
                        else
                        {
                            _introMatched = 0;
                            _state = State.Ground;
                        }
                    }
                    break;

                case State.Payload:
                    if (b == 0x07)
                    {
                        var decoded = Decode(_payload, _payloadLen);
                        if (decoded is not null)
                            last = decoded;
                        _state = State.Ground;
                        _introMatched = 0;
                        _escPending = false;
                    }
                    else if (b == 0x1B)
                    {
                        _escPending = true;
                    }
                    else if (_escPending)
                    {
                        if (b == 0x5C)
                        {
                            var decoded = Decode(_payload, _payloadLen);
                            if (decoded is not null)
                                last = decoded;
                            _state = State.Ground;
                            _introMatched = 0;
                            _escPending = false;
                        }
                        else
                        {
                            _escPending = false;
                            if (_payloadLen < MaxPayload)
                                _payload[_payloadLen++] = b;
                            else
                            {
                                _state = State.Ground;
                                _introMatched = 0;
                            }
                        }
                    }
                    else
                    {
                        if (_payloadLen < MaxPayload)
                            _payload[_payloadLen++] = b;
                        else
                        {
                            _state = State.Ground;
                            _introMatched = 0;
                        }
                    }
                    break;
            }
        }
        return last;
    }

    private static string? Decode(byte[] buf, int len)
    {
        if (len == 0)
            return null;
        var s = Encoding.UTF8.GetString(buf, 0, len);
        string path;
        if (s.StartsWith("file://", StringComparison.Ordinal))
        {
            var rest = s.Substring("file://".Length);
            var slash = rest.IndexOf('/');
            path = slash >= 0 ? rest.Substring(slash) : rest;
        }
        else
        {
            path = s;
        }
        var unescaped = Uri.UnescapeDataString(path);
        return unescaped.Length == 0 ? null : unescaped;
    }
}
