using System.Globalization;
using System.Text;

namespace Cove.Engine.Pty;

public sealed class TerminalModeTracker
{
    private static readonly int[] TrackedModes = [1, 47, 66, 1000, 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1015, 1047, 1048, 1049, 2004];
    private readonly object _gate = new();
    private readonly int[] _parameters = new int[16];
    private int _state;
    private int _currentParameter;
    private int _parameterCount;
    private bool _hasCurrentParameter;
    private int _knownModes;
    private int _enabledModes;

    public void Feed(ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            foreach (byte value in data)
                FeedByte(value);
        }
    }

    public string BuildPreamble() => BuildPreamble(includeAlternateScreenModes: true);

    public string BuildCheckpointSupplement() => BuildPreamble(includeAlternateScreenModes: false);

    private string BuildPreamble(bool includeAlternateScreenModes)
    {
        lock (_gate)
        {
            var result = new StringBuilder();
            for (var index = 0; index < TrackedModes.Length; index++)
            {
                int mode = TrackedModes[index];
                if (!includeAlternateScreenModes && mode is 47 or 1047 or 1048 or 1049)
                    continue;
                int bit = 1 << index;
                if ((_knownModes & bit) == 0)
                    continue;
                result.Append("\x1b[?");
                result.Append(mode.ToString(CultureInfo.InvariantCulture));
                result.Append((_enabledModes & bit) != 0 ? 'h' : 'l');
            }
            return result.ToString();
        }
    }

    private void FeedByte(byte value)
    {
        if (_state == 0)
        {
            if (value == 0x1b)
                _state = 1;
            else if (value == 0x9b)
                _state = 2;
            return;
        }

        if (_state == 1)
        {
            _state = value == (byte)'[' ? 2 : value == 0x1b ? 1 : value == 0x9b ? 2 : 0;
            return;
        }

        if (_state == 2)
        {
            if (value == (byte)'?')
            {
                _state = 3;
                _currentParameter = 0;
                _parameterCount = 0;
                _hasCurrentParameter = false;
            }
            else
            {
                _state = value == 0x1b ? 1 : value == 0x9b ? 2 : 0;
            }
            return;
        }

        if (value is >= (byte)'0' and <= (byte)'9')
        {
            _hasCurrentParameter = true;
            if (_currentParameter <= 100000)
                _currentParameter = (_currentParameter * 10) + value - (byte)'0';
            return;
        }

        if (value == (byte)';')
        {
            if (!CommitParameter())
                Reset();
            return;
        }

        if (value is (byte)'h' or (byte)'l')
        {
            if (CommitParameter())
                Apply(value == (byte)'h');
            Reset();
            return;
        }

        Reset();
        if (value == 0x1b)
            _state = 1;
        else if (value == 0x9b)
            _state = 2;
    }

    private bool CommitParameter()
    {
        if (!_hasCurrentParameter || _parameterCount >= _parameters.Length)
            return false;
        _parameters[_parameterCount++] = _currentParameter;
        _currentParameter = 0;
        _hasCurrentParameter = false;
        return true;
    }

    private void Apply(bool enabled)
    {
        for (var parameterIndex = 0; parameterIndex < _parameterCount; parameterIndex++)
        {
            int mode = _parameters[parameterIndex];
            int trackedIndex = Array.IndexOf(TrackedModes, mode);
            if (trackedIndex < 0)
                continue;
            int bit = 1 << trackedIndex;
            _knownModes |= bit;
            if (enabled)
                _enabledModes |= bit;
            else
                _enabledModes &= ~bit;
        }
    }

    private void Reset()
    {
        _state = 0;
        _currentParameter = 0;
        _parameterCount = 0;
        _hasCurrentParameter = false;
    }
}
