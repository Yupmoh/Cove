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

        ushort cols = (ushort)Math.Clamp(request.Cols, PtyConstants.MinCols, PtyConstants.MaxCols);
        ushort rows = (ushort)Math.Clamp(request.Rows, PtyConstants.MinRows, PtyConstants.MaxRows);

        if (!ConPtyNative.CreatePipe(out SafeFileHandle inputRead, out SafeFileHandle inputWrite, IntPtr.Zero, 0))
            throw SpawnFailure(request, "CreatePipe(input)");
        if (!ConPtyNative.CreatePipe(out SafeFileHandle outputRead, out SafeFileHandle outputWrite, IntPtr.Zero, 0))
        {
            inputRead.Dispose();
            inputWrite.Dispose();
            throw SpawnFailure(request, "CreatePipe(output)");
        }

        var size = new ConPtyNative.Coord { X = (short)cols, Y = (short)rows };
        int hr = ConPtyNative.CreatePseudoConsole(size, inputRead, outputWrite, 0, out IntPtr pseudoConsole);
        inputRead.Dispose();
        outputWrite.Dispose();
        if (hr != 0)
        {
            inputWrite.Dispose();
            outputRead.Dispose();
            _logger.LogError("conpty CreatePseudoConsole failed for '{Command}' (hr 0x{Hr:X8}).", request.Command, hr);
            throw new PtySpawnException($"CreatePseudoConsole failed for '{request.Command}' (hr 0x{hr:X8}).");
        }

        IntPtr attributeList = IntPtr.Zero;
        try
        {
            attributeList = AllocateAttributeList(pseudoConsole, request);

            var startupInfo = new ConPtyNative.StartupInfoEx
            {
                cb = Unsafe.SizeOf<ConPtyNative.StartupInfoEx>(),
                lpAttributeList = attributeList,
            };

            var argv = new List<string>(1 + request.Args.Count) { request.Command };
            argv.AddRange(request.Args);
            string commandLine = WindowsCommandLine.Build(argv);
            char[] environmentBlock = WindowsEnvironmentBlock.BuildBlock(request.Environment);
            uint creationFlags = ConPtyNative.EXTENDED_STARTUPINFO_PRESENT | ConPtyNative.CREATE_UNICODE_ENVIRONMENT;

            bool created;
            ConPtyNative.ProcessInformation processInfo;
            unsafe
            {
                fixed (char* environmentPointer = environmentBlock)
                {
                    created = ConPtyNative.CreateProcessW(
                        null,
                        commandLine,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        creationFlags,
                        (IntPtr)environmentPointer,
                        request.WorkingDirectory,
                        ref startupInfo,
                        out processInfo);
                }
            }

            if (!created)
            {
                int error = Marshal.GetLastPInvokeError();
                ConPtyNative.ClosePseudoConsole(pseudoConsole);
                inputWrite.Dispose();
                outputRead.Dispose();
                _logger.LogError("conpty CreateProcessW failed for '{Command}' (error {Error}).", request.Command, error);
                throw new PtySpawnException($"CreateProcessW failed for '{request.Command}' (error {error}).");
            }

            long id = Interlocked.Increment(ref _nextSessionId);
            return new WindowsPtySession(
                id,
                pseudoConsole,
                outputRead,
                inputWrite,
                processInfo.hProcess,
                processInfo.hThread,
                processInfo.dwProcessId,
                _logger);
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

    private static IntPtr AllocateAttributeList(IntPtr pseudoConsole, PtySpawnRequest request)
    {
        nint listSize = 0;
        ConPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref listSize);
        IntPtr attributeList = Marshal.AllocHGlobal(listSize);
        if (!ConPtyNative.InitializeProcThreadAttributeList(attributeList, 1, 0, ref listSize))
        {
            int error = Marshal.GetLastPInvokeError();
            Marshal.FreeHGlobal(attributeList);
            ConPtyNative.ClosePseudoConsole(pseudoConsole);
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
            throw new PtySpawnException($"UpdateProcThreadAttribute failed for '{request.Command}' (error {error}).");
        }
        return attributeList;
    }

    private PtySpawnException SpawnFailure(PtySpawnRequest request, string stage)
    {
        int error = Marshal.GetLastPInvokeError();
        _logger.LogError("conpty {Stage} failed for '{Command}' (error {Error}).", stage, request.Command, error);
        return new PtySpawnException($"{stage} failed for '{request.Command}' (error {error}).");
    }
}
