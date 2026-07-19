using System.Runtime.InteropServices;

namespace Cove.Platform;

public static class ShutdownSignalRegistration
{
    public static IDisposable Register(Action requestShutdown)
    {
        return Register(
            requestShutdown,
            OperatingSystem.IsWindows(),
            static (signal, callback) => PosixSignalRegistration.Create(signal, _ => callback()));
    }

    public static IDisposable Register(
        Action requestShutdown,
        bool isWindows,
        Func<PosixSignal, Action, IDisposable> register)
    {
        ArgumentNullException.ThrowIfNull(requestShutdown);
        ArgumentNullException.ThrowIfNull(register);
        if (isWindows)
            return NoOpDisposable.Instance;

        var interrupt = register(PosixSignal.SIGINT, requestShutdown);
        try
        {
            var terminate = register(PosixSignal.SIGTERM, requestShutdown);
            return new RegistrationOwner([interrupt, terminate]);
        }
        catch
        {
            interrupt.Dispose();
            throw;
        }
    }

    private sealed class RegistrationOwner(IDisposable[] registrations) : IDisposable
    {
        private IDisposable[]? _registrations = registrations;

        public void Dispose()
        {
            var registrations = Interlocked.Exchange(ref _registrations, null);
            if (registrations is null)
                return;

            foreach (var registration in registrations)
                registration.Dispose();
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public static NoOpDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
