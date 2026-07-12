using System.Collections.Generic;
using System.Text;

namespace Cove.Platform.Pty.Windows;

public static class WindowsCommandLine
{
    private static readonly char[] CharsRequiringQuotes = { ' ', '\t', '\n', '\v', '"' };

    public static string Build(IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < arguments.Count; i++)
        {
            if (i > 0)
                builder.Append(' ');
            AppendArgument(builder, arguments[i]);
        }
        return builder.ToString();
    }

    public static string Quote(string argument)
    {
        var builder = new StringBuilder();
        AppendArgument(builder, argument);
        return builder.ToString();
    }

    private static void AppendArgument(StringBuilder builder, string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(CharsRequiringQuotes) < 0)
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        int index = 0;
        while (index < argument.Length)
        {
            int backslashes = 0;
            while (index < argument.Length && argument[index] == '\\')
            {
                backslashes++;
                index++;
            }

            if (index == argument.Length)
            {
                builder.Append('\\', backslashes * 2);
                break;
            }

            if (argument[index] == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', backslashes);
                builder.Append(argument[index]);
            }
            index++;
        }
        builder.Append('"');
    }
}
