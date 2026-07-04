namespace Cove.Platform;

public static class CoveGitIgnore
{
    public const string Content =
        "ipc/\n" +
        "logs/\n" +
        "bin/\n" +
        "cache/\n" +
        "hook-port\n" +
        "device-id\n" +
        "memory/\n" +
        "fts/\n" +
        "notes/\n" +
        "reviews/\n" +
        "adapters/shared/\n" +
        "*.db\n" +
        "*.db-wal\n" +
        "*.db-shm\n" +
        "*.tmp-*\n" +
        "*.bak\n" +
        "workspaces/*/scrollback*\n" +
        "workspaces/*/.cache/\n";
}
