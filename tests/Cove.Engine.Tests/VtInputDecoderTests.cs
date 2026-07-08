using System.Text;
using Cove.Engine.Tui;
using Xunit;

namespace Cove.Engine.Tests;

public sealed class VtInputDecoderTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void Decode_PlainText()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("abc"));
        Assert.Equal(3, events.Count);
        var k1 = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal('a', (char)k1.KeyCode);
        var k2 = Assert.IsType<VtInputEvent.Key>(events[1]);
        Assert.Equal('b', (char)k2.KeyCode);
        var k3 = Assert.IsType<VtInputEvent.Key>(events[2]);
        Assert.Equal('c', (char)k3.KeyCode);
    }

    [Fact]
    public void Decode_Enter()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\r"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Enter, key.Name);
    }

    [Fact]
    public void Decode_Tab()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\t"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Tab, key.Name);
    }

    [Fact]
    public void Decode_Backspace()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x7f"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Backspace, key.Name);
    }

    [Fact]
    public void Decode_ArrowKeys_Csi()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[A"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Up, key.Name);

        events = decoder.Decode(B("\x1b[B"));
        key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Down, key.Name);

        events = decoder.Decode(B("\x1b[C"));
        key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Right, key.Name);

        events = decoder.Decode(B("\x1b[D"));
        key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Left, key.Name);
    }

    [Fact]
    public void Decode_ArrowKeys_Ss3()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1bOA"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Up, key.Name);
    }

    [Fact]
    public void Decode_HomeEnd_Csi()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[H"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Home, key.Name);

        events = decoder.Decode(B("\x1b[F"));
        key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.End, key.Name);
    }

    [Fact]
    public void Decode_PageUpDown_Tilde()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[5~"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.PageUp, key.Name);

        events = decoder.Decode(B("\x1b[6~"));
        key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.PageDown, key.Name);
    }

    [Fact]
    public void Decode_Delete_Tilde()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[3~"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Delete, key.Name);
    }

    [Fact]
    public void Decode_FunctionKeys_Ss3()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1bOP"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.F1, key.Name);

        events = decoder.Decode(B("\x1bOQ"));
        key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.F2, key.Name);
    }

    [Fact]
    public void Decode_AltLetter()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\u001ba"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyModifiers.Alt, key.Modifiers);
        Assert.Equal('a', (char)key.KeyCode);
    }

    [Fact]
    public void Decode_AltShiftLetter()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\u001bA"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.True(key.Modifiers.HasFlag(KeyModifiers.Alt));
        Assert.True(key.Modifiers.HasFlag(KeyModifiers.Shift));
        Assert.Equal('a', (char)key.KeyCode);
    }

    [Fact]
    public void Decode_CsiU_KittyProtocol()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[97;5u"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal('a', (char)key.KeyCode);
        Assert.True(key.Modifiers.HasFlag(KeyModifiers.Ctrl));
    }

    [Fact]
    public void Decode_CsiU_EnterKey()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[13u"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal(KeyName.Enter, key.Name);
    }

    [Fact]
    public void Decode_CsiU_ShiftModifier()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[97;2u"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.True(key.Modifiers.HasFlag(KeyModifiers.Shift));
    }

    [Fact]
    public void Decode_SgrMouse_Click()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[<0;10;20M"));
        var mouse = Assert.IsType<VtInputEvent.Mouse>(events[0]);
        Assert.Equal(MouseKind.Down, mouse.Kind);
        Assert.Equal(10, mouse.X);
        Assert.Equal(20, mouse.Y);
        Assert.True(mouse.Modifiers.HasFlag(MouseModifiers.Left));
    }

    [Fact]
    public void Decode_SgrMouse_RightClick()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[<2;5;5M"));
        var mouse = Assert.IsType<VtInputEvent.Mouse>(events[0]);
        Assert.True(mouse.Modifiers.HasFlag(MouseModifiers.Right));
    }

    [Fact]
    public void Decode_SgrMouse_Release()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[<0;10;20m"));
        var mouse = Assert.IsType<VtInputEvent.Mouse>(events[0]);
        Assert.Equal(MouseKind.Up, mouse.Kind);
    }

    [Fact]
    public void Decode_SgrMouse_Scroll()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[<64;10;20M"));
        var mouse = Assert.IsType<VtInputEvent.Mouse>(events[0]);
        Assert.Equal(MouseKind.Scroll, mouse.Kind);
    }

    [Fact]
    public void Decode_SgrMouse_CtrlModifier()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[<16;10;20M"));
        var mouse = Assert.IsType<VtInputEvent.Mouse>(events[0]);
        Assert.True(mouse.Modifiers.HasFlag(MouseModifiers.Ctrl));
    }

    [Fact]
    public void Decode_MultipleEvents()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("a\x1b[Ab"));
        Assert.Equal(3, events.Count);
        Assert.Equal('a', (char)Assert.IsType<VtInputEvent.Key>(events[0]).KeyCode);
        Assert.Equal(KeyName.Up, Assert.IsType<VtInputEvent.Key>(events[1]).Name);
        Assert.Equal('b', (char)Assert.IsType<VtInputEvent.Key>(events[2]).KeyCode);
    }

    [Fact]
    public void Decode_Utf8_Multibyte()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("é"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.Equal('é', (char)key.KeyCode);
    }

    [Fact]
    public void Decode_EmptyInput()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B(""));
        Assert.Empty(events);
    }

    [Fact]
    public void Decode_LoneEscape()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.True(key.Modifiers.HasFlag(KeyModifiers.Alt));
    }

    [Fact]
    public void Decode_CsiU_AltModifier()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[97;3u"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.True(key.Modifiers.HasFlag(KeyModifiers.Alt));
    }

    [Fact]
    public void Decode_CsiU_CtrlShiftCombo()
    {
        var decoder = new VtInputDecoder();
        var events = decoder.Decode(B("\x1b[97;5u"));
        var key = Assert.IsType<VtInputEvent.Key>(events[0]);
        Assert.True(key.Modifiers.HasFlag(KeyModifiers.Ctrl));
        Assert.False(key.Modifiers.HasFlag(KeyModifiers.Shift));
    }
}
