using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SizeExplorer.Model;

namespace SizeExplorer.ViewModels
{
    class FileItemViewModel : INotifyPropertyChanged
    {
        private readonly File _file;
        private ImageSource _icon;

        public string Name { get { return _file.Name; } }
        public string Size { get { return _file.Size.ToSizeString(); } }
        public string LastWriteTime { get { return _file.LastWriteTime.ToDateTimeString(); } }
        //public ImageSource Icon
        //{
        //    get
        //    {
        //        var icon = System.Drawing.Icon.ExtractAssociatedIcon(_file.FullName);
        //        //var icon = new System.Drawing.Icon(@"C:\Windows\Installer\{EFAC02F7-FF56-4414-AC69-5A6B47997D0D}\F.ProductIcon.ico");
        //        return icon.im;
        //        //return null;
        //    }
        //}
        public ImageSource Icon
        {
            get
            {
                return _icon ?? (_icon = GetFileIconImageSource(_file.FullName, false, true));

        //        if (_icon == null && System.IO.File.Exists(_file.FullName))
        //        {
        //            using (var sysicon = System.Drawing.Icon.ExtractAssociatedIcon(_file.FullName))
        //            {
        //                _icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
        //                          sysicon.Handle,
        //                          Int32Rect.Empty,
        //                          System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()
        //                          );
        //            }
        //        }
            }
        }

        #region File Icon Extraction

        [StructLayout(LayoutKind.Sequential)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [Flags]
        enum SHGFI : uint
        {
            LargeIcon = 0x0,
            SmallIcon = 0x1,
            UseFileAttributes = 0x10,
            Icon = 0x100,
            Typename = 0x400
        }

        enum FileAttribute : uint
        {
            Directory = 0x10,
            Normal = 0x80
        }

        [DllImport("Shell32.dll")]
        private static extern IntPtr SHGetFileInfo(
            string path,
            [MarshalAs(UnmanagedType.U4)]
        FileAttribute attributes,
            ref SHFILEINFO info,
            uint sizeFileInfo,
            [MarshalAs(UnmanagedType.U4)]
        SHGFI flags);

        [DllImport("User32.dll")]
        private static extern int DestroyIcon(IntPtr hIcon);

        public static ImageSource GetFileIconImageSource(string path, bool large, bool file)
        {
            var shinfo = new SHFILEINFO();
            SHGFI flags = SHGFI.UseFileAttributes | SHGFI.Icon | (large ? SHGFI.LargeIcon : SHGFI.SmallIcon);
            SHGetFileInfo(path, file ? FileAttribute.Normal : FileAttribute.Directory, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
            ImageSource image = Imaging.CreateBitmapSourceFromHIcon(shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DestroyIcon(shinfo.hIcon);
            return image;
        }

        #endregion

        public FileItemViewModel(File file)
        {
            _file = file;
            _file.FileSizeChangeEvent += FileFileSizeChangeEvent;
        }

        void FileFileSizeChangeEvent(object sender, File.FileSizeChangeEventArgs e)
        {
            OnPropertyChanged("Size");
            OnPropertyChanged("LastWriteTime");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    static partial class Extension
    {
        public static string ToDateTimeString(this DateTime data)
        {
            return string.Format("{0} {1}", data.ToShortDateString(), data.ToLongTimeString());
        }
    }
}
