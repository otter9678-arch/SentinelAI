using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SentinelAI.Core
{
    /// <summary>
    /// Authenticode signature verification (wintrust) — needed because an
    /// unsigned exe in a user-writable path is a strong malware signal.
    /// </summary>
    public static class Authenticode
    {
        [DllImport("wintrust.dll", SetLastError = true)]
        private static extern long WinVerifyTrust(IntPtr hWnd, ref Guid pgActionID, ref WINTRUST_DATA pWVTData);

        private static Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pFile2;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        public static bool IsSigned(string filePath)
        {
            try
            {
                var fileInfo = new WINTRUST_FILE_INFO
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)),
                    pcwszFilePath = filePath
                };
                IntPtr pInfo = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
                Marshal.StructureToPtr(fileInfo, pInfo, false);

                var wtd = new WINTRUST_DATA
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)),
                    dwUIChoice = 2, // WTD_UI_NONE
                    fdwRevocationChecks = 0,
                    dwUnionChoice = 1, // WTD_CHOICE_FILE
                    pFile = pInfo
                };

                long result = WinVerifyTrust(IntPtr.Zero, ref WINTRUST_ACTION_GENERIC_VERIFY_V2, ref wtd);
                Marshal.FreeHGlobal(pInfo);
                return result == 0; // 0 = signed and trusted
            }
            catch { return false; }
        }
    }
}