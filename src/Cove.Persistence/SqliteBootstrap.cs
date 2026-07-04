namespace Cove.Persistence;

public static class SqliteBootstrap
{
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
            SQLitePCL.Batteries_V2.Init();
    }
}
