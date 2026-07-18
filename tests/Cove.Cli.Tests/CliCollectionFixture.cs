using Xunit;

namespace Cove.Cli.Tests;

[CollectionDefinition("CLI process environment", DisableParallelization = true)]
public sealed class CliCollectionFixture
{
    public const string Name = "CLI process environment";
}
