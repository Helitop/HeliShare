using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace HeliShare.Helpers
{
    public static class ShellHelper
    {
        private static readonly Guid AppUserModelIdGuid = new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}");
        private const uint AppUserModelIdPropertyId = 5;

        public static bool CreateShortcutForNotifications(string appId)
        {
            try
            {
                // Путь к ярлыку в меню Пуск
                string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "HeliShare.lnk");
                
                // Путь к текущему EXE
                string exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return false;

                // Создаем объект ярлыка
                IShellLinkW shortcut = (IShellLinkW)new CShellLink();
                
                // Настраиваем основные свойства
                shortcut.SetPath(exePath);
                shortcut.SetWorkingDirectory(Path.GetDirectoryName(exePath));
                shortcut.SetDescription("HeliShare File Transfer");

                // --- УСТАНОВКА AppUserModelID (AUMID) ---
                IPropertyStore propertyStore = (IPropertyStore)shortcut;
                PropVariant appIdVariant = new PropVariant();
                appIdVariant.SetString(appId);

                Guid key = AppUserModelIdGuid;
                propertyStore.SetValue(ref key, ref appIdVariant, AppUserModelIdPropertyId);
                propertyStore.Commit();

                // --- СОХРАНЕНИЕ ---
                IPersistFile persistFile = (IPersistFile)shortcut;
                persistFile.Save(startMenuPath, true);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SHORTCUT ERROR: {ex.Message}");
                return false;
            }
        }

        #region COM Interfaces & Structs
        
        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
        private class CShellLink { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out ushort pwHotkey);
            void SetHotkey(ushort wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out Guid pkey);
            void GetValue(ref Guid pkey, out PropVariant pv);
            void SetValue(ref Guid pkey, ref PropVariant pv, uint propId);
            void Commit();
        }

        // ИСПРАВЛЕННАЯ СТРУКТУРА: Работает и на x86, и на x64
        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr ptr; // IntPtr сам подстроится под разрядность системы (4 или 8 байт)

            public void SetString(string value)
            {
                vt = 31; // VT_LPWSTR
                ptr = Marshal.StringToCoTaskMemUni(value);
            }
        }
        #endregion
    }
}