using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TanukiBCL.Roles;

public static class NGWordFileDialog
{
    // A separate STA thread keeps Unity rendering while Windows owns the modal dialog.
    public static Task<string?> Pick(bool save, string title, string initialPath, string filter)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows file dialogs are required.");
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var process = Process.GetCurrentProcess();
        IntPtr owner = process.MainWindowHandle;
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Show(save, title, initialPath, filter, owner)); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "Tanuki NG word file dialog" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
    private static string? Show(bool save, string title, string initialPath, string filter, IntPtr owner)
    {
        IntPtr file = Marshal.AllocHGlobal(32768 * 2);
        try
        {
            byte[] initial = Encoding.Unicode.GetBytes((save ? Path.GetFileName(initialPath) : "") + '\0');
            Marshal.Copy(initial, 0, file, initial.Length);
            var data = new OpenFileName {
                Size = Marshal.SizeOf<OpenFileName>(), Owner = owner, Filter = filter, FilterIndex = 1,
                File = file, MaxFile = 32768, InitialDirectory = Path.GetDirectoryName(initialPath),
                Title = title, DefaultExtension = "txt",
                Flags = 0x00080000 | 0x00000800 | 0x00000008 | 0x00800000 | 0x02000000 |
                    (save ? 0x00000002 : 0x00001000)
            };
            if (save ? GetSaveFileNameW(data) : GetOpenFileNameW(data)) return Marshal.PtrToStringUni(file);
            uint error = CommDlgExtendedError();
            if (error != 0) throw new IOException("File dialog error: 0x" + error.ToString("X"));
            return null;
        }
        finally { Marshal.FreeHGlobal(file); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class OpenFileName
    {
        public int Size;
        public IntPtr Owner, Instance;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Filter;
        public IntPtr CustomFilter;
        public int MaxCustomFilter, FilterIndex;
        public IntPtr File;
        public int MaxFile;
        public IntPtr FileTitle;
        public int MaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string? InitialDirectory, Title;
        public int Flags;
        public short FileOffset, FileExtension;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DefaultExtension;
        public IntPtr CustomData, Hook, TemplateName, Reserved;
        public int ReservedValue, FlagsEx;
    }
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileNameW([In, Out] OpenFileName data);
    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileNameW([In, Out] OpenFileName data);
    [DllImport("comdlg32.dll", ExactSpelling = true)]
    private static extern uint CommDlgExtendedError();
}
