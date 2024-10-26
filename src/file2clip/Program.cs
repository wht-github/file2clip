
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
    [DllImport("user32.dll")]
    static extern IntPtr GetClipboardData(uint uFormat);

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
        files = files.Select(file => Path.GetFullPath(file).ToLowerInvariant()).ToArray();
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
        // int numFiles = files.Length;
        int size = 0;
        foreach (string file in files)
        {
            var file_chars = file.ToCharArray();
            size += (file_chars.Length + 1) * Marshal.SystemDefaultCharSize;
        }
        size += Marshal.SizeOf(typeof(DROPFILES)) + Marshal.SystemDefaultCharSize * 2; // +2 for null terminator
        if (size % 1024 != 0)
        {
            size += 1024 - (size % 1024);
        }
        size *= 2;
        // Console.WriteLine($"Size: {size}");
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
            DROPFILES dropFiles = new()
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
                var file_chars = file.ToCharArray();
                // Console.WriteLine($"Copying file path: {new string(file_chars)}");
                Marshal.Copy(file_chars, 0, filePathPtr, file_chars.Length);
                filePathPtr = IntPtr.Add(filePathPtr, file_chars.Length * Marshal.SystemDefaultCharSize);
                Marshal.WriteByte(filePathPtr, (byte)'\0'); // Null-terminate each string
                filePathPtr = IntPtr.Add(filePathPtr, Marshal.SystemDefaultCharSize);
            }
            Marshal.WriteByte(filePathPtr, (byte)'\0'); // Null-terminate the list

            GlobalUnlock(hGlobal);
            if (EmptyClipboard())
            {
                Console.WriteLine("Clipboard cleared.");
            }
            if (OpenClipboard(IntPtr.Zero))
            {
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
                // Console.WriteLine("Freeing global memory.");
                GlobalUnlock(hGlobal);
            }
        }
    }
}
