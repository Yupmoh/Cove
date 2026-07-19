namespace Cove.Platform;

public static class ControlCredential
{
    public static string Read(CoveDataDir dataDirectory)
    {
        var token = File.ReadAllText(
            dataDirectory.ControlTokenPath).Trim();
        return token.Length == 0
            ? throw new InvalidDataException(
                "daemon control credential is empty")
            : token;
    }
}
