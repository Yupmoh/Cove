using System.Text;

namespace Cove.Engine.Tui;

public abstract record VtInputEvent
{
    public sealed record Key(KeyModifiers Modifiers, KeyName Name, int KeyCode) : VtInputEvent;
    public sealed record Mouse(MouseModifiers Modifiers, MouseKind Kind, int X, int Y) : VtInputEvent;
    public sealed record Paste(string Text) : VtInputEvent;
    public sealed record Unknown(string Raw) : VtInputEvent;
}

public enum KeyName { Unknown, Enter, Tab, Backspace, Escape, Space, Up, Down, Left, Right, Home, End, PageUp, PageDown, Insert, Delete, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }
public enum KeyModifiers { None = 0, Shift = 1, Alt = 2, Ctrl = 4, Super = 8 }
public enum MouseKind { Down, Up, Move, Scroll, Drag }
public enum MouseModifiers { None = 0, Left = 1, Middle = 2, Right = 4, Shift = 8, Ctrl = 16, Alt = 32 }

public sealed class VtInputDecoder
{
    public IReadOnlyList<VtInputEvent> Decode(ReadOnlySpan<byte> data)
    {
        var events = new System.Collections.Generic.List<VtInputEvent>();
        var i = 0;
        while (i < data.Length)
        {
            var b = data[i];
            if (b == 0x1b)
            {
                var (evt, consumed) = DecodeEscape(data, i);
                if (evt is not null)
                    events.Add(evt);
                i += consumed;
            }
            else if (b < 0x20 || b == 0x7f)
            {
                var key = b switch
                {
                    0x0d => new VtInputEvent.Key(KeyModifiers.None, KeyName.Enter, 13),
                    0x0a => new VtInputEvent.Key(KeyModifiers.None, KeyName.Enter, 10),
                    0x09 => new VtInputEvent.Key(KeyModifiers.None, KeyName.Tab, 9),
                    0x08 => new VtInputEvent.Key(KeyModifiers.None, KeyName.Backspace, 8),
                    0x7f => new VtInputEvent.Key(KeyModifiers.None, KeyName.Backspace, 127),
                    _ => null
                };
                if (key is not null) events.Add(key);
                i++;
            }
            else
            {
                var (text, consumed) = DecodeUtf8(data, i);
                var runeCount = 0;
                foreach (var r in text.EnumerateRunes()) runeCount++;
                if (runeCount == 1 && System.Text.Rune.TryGetRuneAt(text, 0, out var rune))
                    events.Add(new VtInputEvent.Key(KeyModifiers.None, KeyName.Unknown, (int)rune.Value));
                else
                    events.Add(new VtInputEvent.Paste(text));
                i += consumed;
            }
        }
        return events;
    }

    private (VtInputEvent? evt, int consumed) DecodeEscape(ReadOnlySpan<byte> data, int start)
    {
        if (start + 1 >= data.Length)
            return (new VtInputEvent.Key(KeyModifiers.Alt, KeyName.Unknown, 0), 1);

        var next = data[start + 1];
        if (next == (byte)'[')
            return DecodeCsi(data, start);
        if (next == (byte)'O')
            return DecodeSs3(data, start);
        if (next == (byte)'P' || next == (byte)'[')
            return (new VtInputEvent.Key(KeyModifiers.None, KeyName.F1, 0), 2);

        var (text, consumed) = DecodeUtf8(data, start + 1);
        var mods = KeyModifiers.Alt;
        if (text.Length == 1)
        {
            var c = text[0];
            if (char.IsLetter(c))
            {
                if (char.IsUpper(c)) mods |= KeyModifiers.Shift;
                return (new VtInputEvent.Key(mods, KeyName.Unknown, char.ToLowerInvariant(c)), 1 + consumed);
            }
            return (new VtInputEvent.Key(mods, KeyName.Unknown, c), 1 + consumed);
        }
        return (new VtInputEvent.Paste(text), 1 + consumed);
    }

    private (VtInputEvent? evt, int consumed) DecodeCsi(ReadOnlySpan<byte> data, int start)
    {
        var paramList = new System.Collections.Generic.List<int>();
        var j = start + 2;
        var current = -1;
        var hasParam = false;
        var kind = '\0';
        var isSgrMouse = false;
        if (j < data.Length && data[j] == (byte)'<')
        {
            isSgrMouse = true;
            j++;
        }

        while (j < data.Length)
        {
            var b = data[j];
            if (b >= (byte)'0' && b <= (byte)'9')
            {
                if (current < 0) current = 0;
                current = current * 10 + (b - '0');
                hasParam = true;
            }
            else if (b == (byte)';')
            {
                paramList.Add(current);
                current = -1;
                hasParam = false;
            }
            else if (b == (byte)' ')
            {
                paramList.Add(current);
                current = -1;
                hasParam = false;
            }
            else if (b >= 0x40 && b <= 0x7e)
            {
                if (hasParam) paramList.Add(current);
                kind = (char)b;
                break;
            }
            j++;
        }

        if (kind == '\0')
            return (new VtInputEvent.Unknown("\x1b["), j - start);

        var consumed = j - start + 1;

        if (kind == 'u')
        {
            var keyCode = paramList.Count > 0 ? paramList[0] : 0;
            var modVal = paramList.Count > 1 ? paramList[1] : 0;
            var mods = DecodeKittyModifiers(modVal);
            var name = MapKeyCodeToName(keyCode);
            return (new VtInputEvent.Key(mods, name, keyCode), consumed);
        }

        if (isSgrMouse && (kind == 'M' || kind == 'm'))
        {
            var mouseEvt = DecodeSgrMouse(paramList, kind);
            return (mouseEvt, consumed);
        }

        if (kind == '~')
        {
            var code = paramList.Count > 0 ? paramList[0] : 0;
            var name = code switch
            {
                1 => KeyName.Home, 2 => KeyName.Insert, 3 => KeyName.Delete,
                4 => KeyName.End, 5 => KeyName.PageUp, 6 => KeyName.PageDown,
                _ => KeyName.Unknown
            };
            return (new VtInputEvent.Key(KeyModifiers.None, name, code), consumed);
        }

        var arrowName = kind switch
        {
            'A' => KeyName.Up, 'B' => KeyName.Down, 'C' => KeyName.Right, 'D' => KeyName.Left,
            'H' => KeyName.Home, 'F' => KeyName.End,
            _ => KeyName.Unknown
        };
        if (arrowName != KeyName.Unknown)
        {
            var mods = paramList.Count > 1 ? DecodeXtermModifiers(paramList[1]) : KeyModifiers.None;
            return (new VtInputEvent.Key(mods, arrowName, 0), consumed);
        }

        return (new VtInputEvent.Unknown($"\\x1b[{kind}"), consumed);
    }

    private (VtInputEvent? evt, int consumed) DecodeSs3(ReadOnlySpan<byte> data, int start)
    {
        if (start + 2 >= data.Length)
            return (new VtInputEvent.Unknown("\x1bO"), 2);
        var name = data[start + 2] switch
        {
            (byte)'A' => KeyName.Up, (byte)'B' => KeyName.Down,
            (byte)'C' => KeyName.Right, (byte)'D' => KeyName.Left,
            (byte)'H' => KeyName.Home, (byte)'F' => KeyName.End,
            (byte)'P' => KeyName.F1, (byte)'Q' => KeyName.F2,
            (byte)'R' => KeyName.F3, (byte)'S' => KeyName.F4,
            _ => KeyName.Unknown
        };
        return (new VtInputEvent.Key(KeyModifiers.None, name, 0), 3);
    }
    private VtInputEvent? DecodeSgrMouse(System.Collections.Generic.IReadOnlyList<int> paramList, char kind)
    {
        if (paramList.Count < 3)
            return new VtInputEvent.Unknown($"\\x1b[<{kind}");

        var button = paramList[0];
        var x = paramList[1];
        var y = paramList[2];

        var mouseKind = kind == 'M' ? MouseKind.Down : MouseKind.Up;
        var mods = MouseModifiers.None;
        var buttonNum = button & 3;
        mods = buttonNum switch
        {
            0 => MouseModifiers.Left, 1 => MouseModifiers.Middle, 2 => MouseModifiers.Right, _ => MouseModifiers.None
        };
        if ((button & 4) != 0) mods |= MouseModifiers.Shift;
        if ((button & 8) != 0) mods |= MouseModifiers.Alt;
        if ((button & 16) != 0) mods |= MouseModifiers.Ctrl;
        if ((button & 64) != 0) mouseKind = MouseKind.Scroll;
        if ((button & 32) != 0) mouseKind = MouseKind.Move;

        return new VtInputEvent.Mouse(mods, mouseKind, x, y);
    }

    private static KeyName MapKeyCodeToName(int keyCode)
    {
        return keyCode switch
        {
            13 => KeyName.Enter, 32 => KeyName.Space, 9 => KeyName.Tab, 127 => KeyName.Backspace,
            27 => KeyName.Escape,
            57344 => KeyName.Up, 57345 => KeyName.Down, 57346 => KeyName.Right, 57347 => KeyName.Left,
            57348 => KeyName.Home, 57349 => KeyName.End, 57350 => KeyName.PageUp, 57351 => KeyName.PageDown,
            57352 => KeyName.Insert, 57353 => KeyName.Delete,
            57356 => KeyName.F1, 57357 => KeyName.F2, 57358 => KeyName.F3, 57359 => KeyName.F4,
            57360 => KeyName.F5, 57361 => KeyName.F6, 57362 => KeyName.F7, 57363 => KeyName.F8,
            57364 => KeyName.F9, 57365 => KeyName.F10, 57366 => KeyName.F11, 57367 => KeyName.F12,
            _ => KeyName.Unknown
        };
    }

    private static KeyModifiers DecodeKittyModifiers(int modVal)
    {
        if (modVal <= 1) return KeyModifiers.None;
        var v = modVal - 1;
        var mods = KeyModifiers.None;
        if ((v & 1) != 0) mods |= KeyModifiers.Shift;
        if ((v & 2) != 0) mods |= KeyModifiers.Alt;
        if ((v & 4) != 0) mods |= KeyModifiers.Ctrl;
        if ((v & 8) != 0) mods |= KeyModifiers.Super;
        return mods;
    }

    private static KeyModifiers DecodeXtermModifiers(int modVal)
    {
        var mods = KeyModifiers.None;
        var v = modVal - 1;
        if ((v & 1) != 0) mods |= KeyModifiers.Shift;
        if ((v & 2) != 0) mods |= KeyModifiers.Alt;
        if ((v & 4) != 0) mods |= KeyModifiers.Ctrl;
        return mods;
    }

    private static (string text, int consumed) DecodeUtf8(ReadOnlySpan<byte> data, int start)
    {
        if (start >= data.Length) return ("", 0);
        var b = data[start];
        int byteCount;
        if (b < 0x80) byteCount = 1;
        else if ((b & 0xe0) == 0xc0) byteCount = 2;
        else if ((b & 0xf0) == 0xe0) byteCount = 3;
        else if ((b & 0xf8) == 0xf0) byteCount = 4;
        else byteCount = 1;

        if (start + byteCount > data.Length) byteCount = 1;
        return (Encoding.UTF8.GetString(data.Slice(start, byteCount)), byteCount);
    }
}
