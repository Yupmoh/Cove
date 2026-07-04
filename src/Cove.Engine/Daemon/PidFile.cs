using System.Globalization;

namespace Cove.Engine.Daemon;

public static class PidFile
{
    public static int? Read(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            string text = File.ReadAllText(path).Trim();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pid) ? pid : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
