
using System;
using System.IO;
using System.Runtime.InteropServices;

class Program
{
    [DllImport("user32.dll")]
    static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    static extern void GlobalUnlock(IntPtr hMem);

    const uint CF_HDROP = 15; // File drop format
    const uint GMEM_MOVEABLE = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    struct DROPFILES
    {
        public int pFiles;
        public POINT pt;
        public bool fNC;
        public bool fWide;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT
    {
        public int x;
        public int y;
    }
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide file paths as arguments.");
            return;
        }
        string[] files = args;
        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                Console.WriteLine($"File not found: {file}");
                return;
            }
            else
            {
                Console.WriteLine($"File found: {file}");
            }
        }
        SetFileDrop(files);
    }

    static void SetFileDrop(string[] files)
    {
        // Open the clipboard
        if (!OpenClipboard(IntPtr.Zero))
        {
            Console.WriteLine("Failed to open clipboard.");
            return;
        }

        // Calculate size
        int numFiles = files.Length;
        int size = Marshal.SizeOf(typeof(DROPFILES)) + (numFiles * Marshal.SystemDefaultCharSize * 260) + 2; // +2 for null terminator
        IntPtr hGlobal = IntPtr.Zero;

        try
        {
            hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)size);
            if (hGlobal == IntPtr.Zero)
            {
                CloseClipboard();
                Console.WriteLine("Failed to allocate global memory.");
                return;
            }

            // Lock the memory
            DROPFILES dropFiles = new DROPFILES
            {
                pFiles = Marshal.SizeOf(typeof(DROPFILES)),
                pt = new POINT { x = 0, y = 0 },
                fNC = false,
                fWide = true
            };

            IntPtr pGlobal = GlobalLock(hGlobal);
            Marshal.StructureToPtr(dropFiles, pGlobal, false);

            // Copy file paths
            IntPtr filePathPtr = IntPtr.Add(pGlobal, Marshal.SizeOf(typeof(DROPFILES)));
            foreach (string file in files)
            {
                Marshal.Copy(file.ToCharArray(), 0, filePathPtr, file.Length);
                filePathPtr = IntPtr.Add(filePathPtr, file.Length * Marshal.SystemDefaultCharSize);
                Marshal.WriteByte(filePathPtr, 0); // Null-terminate each string
                filePathPtr = IntPtr.Add(filePathPtr, Marshal.SystemDefaultCharSize);
            }
            Marshal.WriteByte(filePathPtr, 0); // Null-terminate the list

            GlobalUnlock(hGlobal);

            if (OpenClipboard(IntPtr.Zero))
            {
                EmptyClipboard();
                if (SetClipboardData(CF_HDROP, hGlobal) != IntPtr.Zero)
                {
                    Console.WriteLine("Files copied to clipboard.");
                }
                CloseClipboard();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        finally
        {
            if (hGlobal != IntPtr.Zero)
            {
                GlobalUnlock(hGlobal);
            }
        }
    }
}
