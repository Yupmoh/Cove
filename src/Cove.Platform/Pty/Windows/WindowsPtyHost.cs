using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace Cove.Platform.Pty.Windows;

public sealed class WindowsPtyHost : IPtyHost
{
    private static long _nextSessionId;
    private readonly ILogger _logger;

    public WindowsPtyHost(ILogger logger) => _logger = logger;

    public bool IsSupported => OperatingSystem.IsWindows();

    public IPtySession Spawn(PtySpawnRequest request)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WindowsPtyHost runs on Windows only.");

        return SpawnWithOptions(request, ConPtyDiagnosticOptions.Production, _logger).Session;
    }

    internal static ConPtyDiagnosticSpawn SpawnWithOptions(PtySpawnRequest request, ConPtyDiagnosticOptions options, ILogger logger)
    {
        ushort cols = (ushort)Math.Clamp(request.Cols, PtyConstants.MinCols, PtyConstants.MaxCols);
        ushort rows = (ushort)Math.Clamp(request.Rows, PtyConstants.MinRows, PtyConstants.MaxRows);
        logger.WinSpawnBegin(request.Command, request.WorkingDirectory, cols, rows);

        SafeFileHandle inputRead;
        SafeFileHandle inputWrite;
        SafeFileHandle outputRead;
        SafeFileHandle outputWrite;
        unsafe
        {
            ConPtyNative.SecurityAttributes security = default;
            IntPtr securityPointer = IntPtr.Zero;
            if (options.ExplicitNonInheritablePipes)
            {
                security.nLength = sizeof(ConPtyNative.SecurityAttributes);
                security.lpSecurityDescriptor = IntPtr.Zero;
                security.bInheritHandle = 0;
                securityPointer = (IntPtr)(&security);
            }

            if (!ConPtyNative.CreatePipe(out inputRead, out inputWrite, securityPointer, 0))
                throw SpawnFailure(request, "CreatePipe(input)", logger);
            logger.WinPipeCreated("input", !inputRead.IsInvalid && !inputWrite.IsInvalid);
            if (!ConPtyNative.CreatePipe(out outputRead, out outputWrite, securityPointer, 0))
            {
                inputRead.Dispose();
                inputWrite.Dispose();
                throw SpawnFailure(request, "CreatePipe(output)", logger);
            }
            logger.WinPipeCreated("output", !outputRead.IsInvalid && !outputWrite.IsInvalid);
        }

        var size = new ConPtyNative.Coord { X = (short)cols, Y = (short)rows };
        uint pseudoConsoleFlags = options.InheritCursor ? ConPtyNative.PSEUDOCONSOLE_INHERIT_CURSOR : 0;
        int hr = ConPtyNative.CreatePseudoConsole(size, inputRead, outputWrite, pseudoConsoleFlags, out IntPtr pseudoConsole);
        bool pseudoConsoleValid = pseudoConsole != IntPtr.Zero;
        if (!options.KeepConptySideHandles)
        {
            inputRead.Dispose();
            outputWrite.Dispose();
        }
        logger.WinPseudoConsoleCreated(hr, pseudoConsoleValid);
        if (hr != 0)
        {
            if (options.KeepConptySideHandles)
            {
                inputRead.Dispose();
                outputWrite.Dispose();
            }
            inputWrite.Dispose();
            outputRead.Dispose();
            logger.WinPseudoConsoleFailed(request.Command, hr);
            throw new PtySpawnException($"CreatePseudoConsole failed for '{request.Command}' (hr 0x{hr:X8}).");
        }

        IntPtr attributeList = IntPtr.Zero;
        try
        {
            attributeList = AllocateAttributeList(pseudoConsole, request, logger, out int updateAttributeLastError);

            var startupInfo = new ConPtyNative.StartupInfoEx
            {
                cb = Unsafe.SizeOf<ConPtyNative.StartupInfoEx>(),
                lpAttributeList = attributeList,
            };
            if (options.ExplicitZeroStdHandles)
            {
                startupInfo.dwFlags = 0;
                startupInfo.hStdInput = IntPtr.Zero;
                startupInfo.hStdOutput = IntPtr.Zero;
                startupInfo.hStdError = IntPtr.Zero;
            }

            string commandLine;
            if (options.CommandLineOverride is not null)
            {
                commandLine = options.CommandLineOverride;
            }
            else
            {
                var argv = new List<string>(1 + request.Args.Count) { request.Command };
                argv.AddRange(request.Args);
                commandLine = WindowsCommandLine.Build(argv);
            }

            char[]? environmentBlock = options.InheritParentEnvironment
                ? null
                : WindowsEnvironmentBlock.BuildBlock(request.Environment);

            uint creationFlags = ConPtyNative.EXTENDED_STARTUPINFO_PRESENT;
            if (options.IncludeUnicodeEnvironmentFlag)
                creationFlags |= ConPtyNative.CREATE_UNICODE_ENVIRONMENT;
            logger.WinCreateProcessBegin(commandLine.Length, creationFlags);

            bool created;
            ConPtyNative.ProcessInformation processInfo;
            unsafe
            {
                fixed (char* environmentPointer = environmentBlock)
                {
                    IntPtr environment = environmentBlock is null ? IntPtr.Zero : (IntPtr)environmentPointer;
                    created = ConPtyNative.CreateProcessW(
                        null,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        creationFlags,
                        environment,
                        request.WorkingDirectory,
                        ref startupInfo,
                        out processInfo);
                }
            }

            if (!created)
            {
                int error = Marshal.GetLastPInvokeError();
                ConPtyNative.ClosePseudoConsole(pseudoConsole);
                if (options.KeepConptySideHandles)
                {
                    inputRead.Dispose();
                    outputWrite.Dispose();
                }
                inputWrite.Dispose();
                outputRead.Dispose();
                logger.WinCreateProcessFailed(request.Command, error);
                throw new PtySpawnException($"CreateProcessW failed for '{request.Command}' (error {error}).");
            }

            logger.WinCreateProcessSucceeded(request.Command, processInfo.dwProcessId);
            long id = Interlocked.Increment(ref _nextSessionId);
            logger.SessionSpawned(id, request.Command, request.WorkingDirectory, processInfo.dwProcessId, cols, rows);
            var session = new WindowsPtySession(
                id,
                pseudoConsole,
                outputRead,
                inputWrite,
                processInfo.hProcess,
                processInfo.hThread,
                processInfo.dwProcessId,
                logger,
                options.SuppressWatcherClose,
                options.KeepConptySideHandles ? inputRead : null,
                options.KeepConptySideHandles ? outputWrite : null);

            return new ConPtyDiagnosticSpawn
            {
                Session = session,
                StartupFlags = startupInfo.dwFlags,
                StdInput = startupInfo.hStdInput.ToInt64(),
                StdOutput = startupInfo.hStdOutput.ToInt64(),
                StdError = startupInfo.hStdError.ToInt64(),
                CreationFlags = creationFlags,
                EnvironmentInherited = environmentBlock is null,
                CommandLine = commandLine,
                PseudoConsoleCols = cols,
                PseudoConsoleRows = rows,
                PseudoConsoleValid = pseudoConsoleValid,
                UpdateAttributeLastError = updateAttributeLastError,
            };
        }
        finally
        {
            if (attributeList != IntPtr.Zero)
            {
                ConPtyNative.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
        }
    }

    private static IntPtr AllocateAttributeList(IntPtr pseudoConsole, PtySpawnRequest request, ILogger logger, out int updateLastError)
    {
        updateLastError = 0;
        nint listSize = 0;
        ConPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);
        logger.WinAttributeListInitialized(listSize);
        IntPtr attributeList = Marshal.AllocHGlobal(listSize);
        if (!ConPtyNative.InitializeProcThreadAttributeList(attributeList, 1, 0, ref listSize))
        {
            int error = Marshal.GetLastPInvokeError();
            Marshal.FreeHGlobal(attributeList);
            ConPtyNative.ClosePseudoConsole(pseudoConsole);
            logger.WinAttributeListInitFailed(request.Command, error);
            throw new PtySpawnException($"InitializeProcThreadAttributeList failed for '{request.Command}' (error {error}).");
        }
        if (!ConPtyNative.UpdateProcThreadAttribute(
                attributeList,
                0,
                ConPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                pseudoConsole,
                IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
        {
            int error = Marshal.GetLastPInvokeError();
            ConPtyNative.DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(attributeList);
            ConPtyNative.ClosePseudoConsole(pseudoConsole);
            logger.WinAttributeListUpdateFailed(request.Command, error);
            throw new PtySpawnException($"UpdateProcThreadAttribute failed for '{request.Command}' (error {error}).");
        }
        updateLastError = Marshal.GetLastPInvokeError();
        logger.WinAttributeListUpdated(request.Command);
        return attributeList;
    }

    private static PtySpawnException SpawnFailure(PtySpawnRequest request, string stage, ILogger logger)
    {
        int error = Marshal.GetLastPInvokeError();
        logger.WinPipeCreateFailed(stage, request.Command, error);
        return new PtySpawnException($"{stage} failed for '{request.Command}' (error {error}).");
    }
}
