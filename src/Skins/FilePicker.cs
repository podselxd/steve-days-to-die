using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SteveDaysToDie.Skins
{
    /// <summary>Windows' own "Open" window, to choose a skin picture (no extra library needed).</summary>
    static class FilePicker
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class OpenFileName
        {
            public int lStructSize = Marshal.SizeOf(typeof(OpenFileName));
            public IntPtr hwndOwner = IntPtr.Zero;
            public IntPtr hInstance = IntPtr.Zero;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData = IntPtr.Zero;
            public IntPtr lpfnHook = IntPtr.Zero;
            public string lpTemplateName;
            public IntPtr pvReserved = IntPtr.Zero;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        const int OFN_NOCHANGEDIR = 0x8, OFN_PATHMUSTEXIST = 0x800, OFN_FILEMUSTEXIST = 0x1000, OFN_EXPLORER = 0x80000;

        /// <summary>Shows the window and returns the chosen file, or null if the player cancelled.</summary>
        public static string ChoosePng(string title)
        {
            try
            {
                string start = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(start)) start = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                var ofn = new OpenFileName
                {
                    hwndOwner = GetActiveWindow(),
                    lpstrFilter = "Skins de Minecraft (*.png)\0*.png\0\0",
                    lpstrFile = new string('\0', 2048),
                    nMaxFile = 2048,
                    lpstrFileTitle = new string('\0', 260),
                    nMaxFileTitle = 260,
                    lpstrInitialDir = start,
                    lpstrTitle = title,
                    Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                };
                if (!GetOpenFileName(ofn)) return null;
                string path = ofn.lpstrFile;
                int z = path.IndexOf('\0');
                if (z >= 0) path = path.Substring(0, z);
                return string.IsNullOrEmpty(path) ? null : path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SVD] File window: " + ex.Message);
                return null;
            }
        }
    }
}
