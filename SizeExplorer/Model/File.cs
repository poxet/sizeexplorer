using System;
using System.IO;

namespace SizeExplorer.Model
{
    class File
    {
        private long? _size;
        private DateTime? _lastWriteTime;

        public string FullName { get; private set; }
        public DateTime LastWriteTime
        {
            get
            {
                if (_lastWriteTime == null) throw new InvalidOperationException();
                return _lastWriteTime.Value;
            }
        }
        public string Name
        {
            get
            {
                var pos = FullName.LastIndexOf(@"\") + 1;
                return FullName.Substring(pos);
            }
        }
        public long Size
        {
            get
            {
                if (_size == null) throw new InvalidOperationException();
                return _size.Value;
            }
        }
        public double? SizePart { get; private set; } 

        #region Event

        public class FileScannedEventArgs : EventArgs
        {

        }

        public class FileSizeChangeEventArgs : EventArgs
        {
            public long SizeChange { get; private set; }
            public DateTime LastWriteTime { get; private set; }

            public FileSizeChangeEventArgs(long sizeChange, DateTime lastWriteTime)
            {
                SizeChange = sizeChange;
                LastWriteTime = lastWriteTime;
            }
        }

        public static event EventHandler<FileScannedEventArgs> FileScannedEvent;
        public event EventHandler<FileSizeChangeEventArgs> FileSizeChangeEvent;

        public static void InvokeFileScannedEvent(FileScannedEventArgs e)
        {
            var handler = FileScannedEvent;
            if (handler != null)
                handler(null, e);
        }

        public void InvokeFileSizeChangeEvent(FileSizeChangeEventArgs e)
        {
            var handler = FileSizeChangeEvent;
            if (handler != null)
                handler(this, e);
        }

        #endregion

        public File(string fullName)
        {
            FullName = fullName;

            //Perform update on the main file information
            Update();

            InvokeFileScannedEvent(new FileScannedEventArgs());
        }

        public long Update()
        {
            try
            {
                var isUpdating = false;
                long? prevSize = null;
                long sizeChange = 0;

                //Retrieve file information
                var fi = new FileInfo(FullName);

                //First time, ore no change
                if (_size == null || _size.Value != fi.Length)
                {
                    if (_size != null)
                        isUpdating = true;

                    prevSize = _size;
                    _size = fi.Length;
                }


                if (_lastWriteTime == null || DateTime.Compare(_lastWriteTime.Value, fi.LastWriteTime) != 0)
                {
                    if (_lastWriteTime != null)
                        isUpdating = true;

                    _lastWriteTime = fi.LastWriteTime;
                }

                if (isUpdating)
                {
                    sizeChange = _size.Value - (prevSize != null ? prevSize.Value : 0);
                    InvokeFileSizeChangeEvent(new FileSizeChangeEventArgs(sizeChange, _lastWriteTime.Value));
                }


                return sizeChange;
            }
            catch (PathTooLongException)
            {
                //TODO: Perhaps long files are hidden in here.
                _size = 0;
                return 0;
            }
            catch (FileNotFoundException)
            {
                _size = 0;
                return 0;
            }
        }

        public void SetParentSize(long parentSize)
        {
            SizePart = _size / (double)parentSize * 100;
        }
    }
}

