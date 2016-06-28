using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ThreadState = System.Threading.ThreadState;

namespace SizeExplorer.Model
{   
    class Folder
    {
        public static IList<Folder> RootFolderList = new List<Folder>();

        private readonly Folder _root;
        private readonly Folder _parent;
        private readonly object _syncRootChildren;
        private readonly object _syncRootFiles;
        private IList<Folder> _children;
        private IList<File> _files;
        private long? _totalSize;
        private string _message;
        private static FileSystemWatcher _fileSystemWatcher;

        private readonly Thread _thread;
        public IList<File> Files
        {
            get
            {
                if (_files == null)
                {
                    lock (_root._syncRootFiles)
                    {
                        if (_files == null)
                        {
                            _files = new List<File>();
                            var files = Directory.GetFiles(FullName);
                            foreach (var file in files)
                                _files.Add(new File(file));
                        }
                    }
                }
                return _files;
            }
        }

        public string FullName { get; private set; }
        public string Name
        {
            get
            {
                string data;

                if (FullName.EndsWith(@"\"))
                    data = FullName.Substring(0, 2);
                else
                    data = FullName.Substring(FullName.LastIndexOf(@"\") + 1);

                return data;
            }
        }
        public long? TotalSize
        {
            get { return _totalSize; }
            set
            {
                if ( _totalSize != value )
                {
                    _totalSize = value;

                    if (_totalSize != null)
                    {
                        //Provided so that file part can be updated
                        foreach (var folder in _children)
                            folder.SetParentSize(_totalSize.Value);
                        foreach (var file in _files)
                            file.SetParentSize(_totalSize.Value);
                    }
                }
            }
        }
        public double? SizePart { get; private set; }
        public bool IsScanning { get; private set; }

        public string Message
        {
            get { return _message; }
            private set
            {
                if ( _message != value )
                {
                    _message = value;
                    InvokeMessageChangedEvent(new MessageChangedEventArgs());
                }
            }
        }
        public bool IsDrive { get { return FullName.EndsWith(@":\"); } }

        #region Event

        public class FolderStartScanningEventArgs : EventArgs
        {
            
        }

        public class FolderScannedEventArgs : EventArgs
        {
            
        }

        public class FileActionEventArgs : EventArgs
        {
            public string Message { get; private set; }

            public FileActionEventArgs(string message)
            {
                Message = message;
            }
        }

        public class SizePartChangedEventArgs : EventArgs
        {
            
        }

        public class SizeChangedEventArgs : EventArgs
        {
            
        }

        public class NameChangedEventArgs : EventArgs
        {

        }

        public class MessageChangedEventArgs : EventArgs
        {

        }

        public class FileEventArgs : EventArgs
        {
            public File File { get; private set; }
            public Folder Folder { get; private set; }

            public FileEventArgs(File file, Folder folder)
            {
                File = file;
                Folder = folder;
            }
        }

        public event EventHandler<FolderStartScanningEventArgs> FolderStartScanningEvent;
        public static event EventHandler<FolderScannedEventArgs> FolderScannedEvent;
        public static event EventHandler<FileActionEventArgs> FileActionEvent;
        public event EventHandler<SizePartChangedEventArgs> SizePartChangedEvent;
        public event EventHandler<SizeChangedEventArgs> SizeChangedEvent;
        public event EventHandler<NameChangedEventArgs> NameChangedEvent;
        public event EventHandler<MessageChangedEventArgs> MessageChangedEvent;
        public event EventHandler<FileEventArgs> FileCreatedEvent;
        public event EventHandler<FileEventArgs> FileDeletedEvent;

        public void InvokeFolderStartScanningEvent(FolderStartScanningEventArgs e)
        {
            var handler = FolderStartScanningEvent;
            if (handler != null)
                handler(this, e);
        }

        public static void InvokeFolderScannedEvent(FolderScannedEventArgs e)
        {
            var handler = FolderScannedEvent;
            if (handler != null) 
                handler(null, e);
        }

        public static void InvokeFileActionEvent(FileActionEventArgs e)
        {
            var handler = FileActionEvent;
            if (handler != null)
                handler(null, e);
        }

        public void InvokeSizePartChangedEvent(SizePartChangedEventArgs e)
        {
            var handler = SizePartChangedEvent;
            if (handler != null) 
                handler(this, e);
        }

        public void InvokeFileCreatedEvent(FileEventArgs e)
        {
            var handler = FileCreatedEvent;
            if (handler != null)
                handler(this, e);
        }

        public void InvokeMessageChangedEvent(MessageChangedEventArgs e)
        {
            var handler = MessageChangedEvent;
            if (handler != null)
                handler(this, e);
        }

        public void InvokeSizeChangedEvent(SizeChangedEventArgs e)
        {
            var handler = SizeChangedEvent;
            if (handler != null) 
                handler(this, e);
        }

        public void InvokeNameChangedEvent(NameChangedEventArgs e)
        {
            var handler = NameChangedEvent;
            if (handler != null) 
                handler(this, e);
        }

        public void InvokeFileDeletedEvent(FileEventArgs e)
        {
            var handler = FileDeletedEvent;
            if (handler != null) 
                handler(this, e);
        }


        #endregion

        //Factory
        private static Folder CreateDrive(string driveLetter, bool loadTreeAsync)
        {
            if (!driveLetter.EndsWith(@":\")) throw new InvalidOperationException();

            //Look for folder in the loaded static list (Repository)
            var folder = RootFolderList.FirstOrDefault(itm => string.Compare(itm.FullName, driveLetter) == 0);
            if (folder == null)
            {
                lock (RootFolderList) //Check-lock-check pattern
                {
                    folder = RootFolderList.FirstOrDefault(itm => string.Compare(itm.FullName, driveLetter) == 0);
                    if (folder == null)
                    {
                        folder = new Folder(driveLetter, loadTreeAsync);
                        RootFolderList.Add(folder);
                    }
                }
            }
            return folder;
        }

        private static Folder CreateFolder(Folder root, string folderPath, Folder parent)
        {
            if (folderPath.EndsWith(@":\")) throw new InvalidOperationException();

            return new Folder(folderPath, root, parent);
        }

        private Folder()
        {
            //InvokeFolderScannedEvent(new FolderScannedEventArgs());
        }

        /// <summary>
        /// Subfolder
        /// </summary>
        /// <param name="fullName"></param>
        /// <param name="root"></param>
        /// <param name="parent"></param>
        private Folder(string fullName, Folder root, Folder parent)
            : this()
        {
            _root = root;
            _parent = parent;
            FullName = fullName;

            if (IsDrive) throw new InvalidOperationException();
        }

        /// <summary>
        /// Drive
        /// </summary>
        /// <param name="driveLetter"></param>
        /// <param name="loadTreeAsync"></param>
        private Folder(string driveLetter, bool loadTreeAsync)
            : this()
        {
            _root = this;
            _parent = null;
            _syncRootChildren = new object();
            _syncRootFiles = new object();
            FullName = driveLetter;

            if (!IsDrive ) throw new InvalidOperationException();

            //If the folder is a drive, start a thread that loads subfolders
            try
            {
                //Start listening to events on the drive
                _fileSystemWatcher = new FileSystemWatcher();
                _fileSystemWatcher.Path = FullName;
                _fileSystemWatcher.Filter = "*.*";
                _fileSystemWatcher.IncludeSubdirectories = true;
                _fileSystemWatcher.EnableRaisingEvents = true;
                _fileSystemWatcher.Changed += FileSystemWatcherChanged;
                _fileSystemWatcher.Created += FileSystemWatcherCreated;
                //_fileSystemWatcher.Renamed += _fileSystemWatcher_Renamed;
                _fileSystemWatcher.Deleted += FileSystemWatcherDeleted;

                _thread = new Thread(LoadSizeEngine) { Name = string.Format("LoadDrive{0}", FullName.Substring(0, 1)), IsBackground = true, Priority = ThreadPriority.BelowNormal };
                if (loadTreeAsync)
                {
                    //Automatically starts a new scanner thread for each logical drive.
                    //Only do this for the C-drive, others have to be started manually

                    if (driveLetter.StartsWith("C"))
                        _thread.Start();
                }
            }
            catch(ArgumentException exp)
            {
                if (string.Compare(  exp.Message, string.Format( "The directory name {0} is invalid.", FullName )) == 0 )
                {
                    Message = exp.Message;
                    _fileSystemWatcher = null;
                }
                else
                    throw;
            }
        }

        public void StartScan()
        {
            if (!IsDrive) throw new InvalidOperationException(string.Format("Can only start a scanner in root folder (drive). Not in a subfolder like {0}.", FullName));
            if (_thread.ThreadState == (ThreadState.Background | ThreadState.Unstarted))
                _thread.Start();
        }

        void FileSystemWatcherChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                var fullName = e.FullPath;

                //TODO: Also check for access denied
                if (System.IO.File.Exists(fullName))
                {
                    var folder = FindFolder(fullName.ParentFolderNamePart().SubFolderNamePart());
                    if (folder != null)
                    {
                        var file = folder.Files.FirstOrDefault(itm => string.Compare(itm.FullName, fullName) == 0);
                        if (file != null)
                        {
                            var fileSizeChange = file.Update();
                            folder.ChangeSize(fileSizeChange);

                            InvokeFileActionEvent(new FileActionEventArgs(string.Format("File {0} changed size.", file.Name)));
                        }
                    }
                }
                else if (Directory.Exists(fullName))
                {
                    //TODO: Implement for folders
                    //throw new NotImplementedException();
                }
            }
            catch (UnauthorizedAccessException exp)
            {
                //The file that was changed could not be accessed
                //Swallow
            }
            //catch (System.IO.FileNotFoundException exp)
            //{
            //    //TODO: Thor a file removed event (Removing the file from the view if displayed there)               
            //}
            catch (Exception exp)
            {
                Debug.WriteLine(exp.Message);
                Trace.TraceWarning(exp.Message);
            }
        }

        private void ChangeSize(long fileSizeChange)
        {
            if (_totalSize == null || fileSizeChange == 0) return;

            TotalSize += fileSizeChange;
            //InvokeNameChangedEvent(new NameChangedEventArgs());
            InvokeSizeChangedEvent(new SizeChangedEventArgs());

            if (_parent != null)
                _parent.ChangeSize(fileSizeChange);
        }

        void FileSystemWatcherCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                var fullName = e.FullPath;

                if (System.IO.File.Exists(fullName))
                {
                    var folder = FindFolder(fullName.ParentFolderNamePart().SubFolderNamePart());
                    if (folder != null)
                    {
                        var newFile = new File(fullName);
                        folder.ChangeSize(newFile.Update());
                        folder.Files.Add(newFile);
                        folder.InvokeFileCreatedEvent(new FileEventArgs(newFile, folder));

                        InvokeFileActionEvent(new FileActionEventArgs(string.Format("File {0} was created.", newFile.Name)));
                    }
                }
                else if (Directory.Exists(fullName))
                {
                    //TODO: Implement creation of folders
                    //throw new NotImplementedException();
                }
            }
            catch (Exception exp)
            {
                Debug.WriteLine(exp.Message);
                Trace.TraceWarning(exp.Message);
            }
        }

        //void _fileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        //{
        //    try
        //    {
        //        //TODO: The file possibly also changed folders
        //        var folder = FindFolder(e.OldFullPath.ParentFolderNamePart().SubFolderNamePart());
        //        if (folder != null)
        //        {
        //            //var file = folder.FindFile(e.OldFullPath.ChildNamePart());
        //            //if (file != null)
        //            //{
        //            //    //TODO: Check where the file should be moved.
        //            //}
        //        }
        //    }
        //    catch (Exception exp)
        //    {                
        //        throw;
        //    }
        //}

        void FileSystemWatcherDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                var fullName = e.FullPath;

                var folder = FindFolder(fullName.ParentFolderNamePart().SubFolderNamePart());
                if (folder != null)
                {
                    //TODO: Check if this is a file or a folder
                    //TODO: Find file in collection
                    var file = folder.Files.FirstOrDefault(itm => string.Compare(itm.FullName, fullName) == 0);
                    if ( file != null )
                    {
                        folder.ChangeSize(file.Size);
                        folder.Files.Remove(file);
                        folder.InvokeFileDeletedEvent(new FileEventArgs(file, folder));

                        InvokeFileActionEvent(new FileActionEventArgs(string.Format("File {0} was deleted.", file.Name)));

                        return;
                    }

                    var subFolder = folder.Children.FirstOrDefault(itm => string.Compare(itm.FullName, fullName) == 0);
                    if ( subFolder != null )
                    {
                        Children.Remove(subFolder);

                        //TODO: Raise a folder removed event)
                        throw new NotImplementedException();
                        
                        return;
                    }

                    throw new InvalidOperationException(string.Format("Cannot find {0} as a file, nor a folder.", fullName));
                }
            }
            catch (Exception exp)
            {
                Debug.WriteLine(exp.Message);
                Trace.TraceWarning(exp.Message);
            }
        }

        public Folder FindFolder(string folderSubPath)
        {
            //Get the name part?
            var rootFolder = folderSubPath.RootFolderNamePart();
            var subPath = folderSubPath.SubFolderNamePart();

            var folder = Children.FirstOrDefault(itm => itm.Name == rootFolder);
            if (folder == null || subPath == null) return folder;
            return folder.FindFolder(subPath);
        }

        //public File FindFile(string fileName)
        //{
        //    //var files = GetFiles(false);
        //    //if (files == null) return null;

        //    return Files.FirstOrDefault(itm => itm.Name == fileName);
        //}

        //public void RemoveFile(string fileName)
        //{
        //    //var files = GetFiles(false);
        //    //if (files == null) return;

        //    var file = Files.FirstOrDefault(itm => itm.Name == fileName);
        //    if (file != null)
        //        Files.Remove(file);
        //}

        private void LoadSizeEngine()
        {
            //try
            //{
                LoadSize();
            //}
            //catch (Exception exp)
            //{
            //    Support.Toolkit.Support.Instance.RegisterIssueAsync(exp, false);
            //}
        }

        private void LoadSize()
        {
            try
            {
                IsScanning = true;
                InvokeFolderStartScanningEvent(new FolderStartScanningEventArgs());

                var size = Files.Sum(itm => itm.Size);

                foreach (var child in Children)
                {
                    child.LoadSize();
                    if (child.TotalSize != null)
                        size += child.TotalSize.Value;
                    //else
                    //    throw new InvalidOperationException();
                }
                TotalSize = size;
            }
            catch(UnauthorizedAccessException exp)
            {
                Message = exp.Message;
                //_totalSize = 0;
            }
            catch(IOException exp)
            {
                Message = exp.Message;
                //_totalSize = 0;                    
            }
            catch (Exception exp)
            {
                Message = exp.Message;
                throw;
            }
            finally
            {
                IsScanning = false;
                //InvokeNameChangedEvent(new NameChangedEventArgs());
                InvokeFolderScannedEvent(new FolderScannedEventArgs());
                InvokeSizeChangedEvent(new SizeChangedEventArgs());
            }
        }

        //public IList<Folder> GetChildren(bool load)
        public IList<Folder> Children
        {
            get
            {
                //Check-Lock-Check pattern
                if (_children == null)
                {
                    lock (_root._syncRootChildren)
                    {
                        if (_children == null)
                        {
                            try
                            {
                                _children = GetSubfolders(_root, FullName, this).ToList();
                            }
                            catch (IOException exp)
                            {
                                Message = exp.Message;
                                _children = new List<Folder>();
                            }
                            catch (UnauthorizedAccessException exp)
                            {
                                Message = exp.Message;
                                _children = new List<Folder>();
                            }
                            catch(Exception exp)
                            {
                                throw;
                            }
                        }
                    }
                }
                return _children;
            }
        }

        public static IEnumerable<Folder> GetLogicalDrives(bool loadTreeAsync)
        {
            var drives = Directory.GetLogicalDrives();
            foreach (var drive in drives)
                yield return Folder.CreateDrive(drive, loadTreeAsync);
        }

        private static IEnumerable<Folder> GetSubfolders(Folder root, string folder, Folder parent)
        {
            var directories = Directory.GetDirectories(folder);
            foreach (var directory in directories)
                yield return Folder.CreateFolder(root, directory, parent);
        }

        private void SetParentSize(long parentSize)
        {
            SizePart = _totalSize / (double)parentSize * 100;
            InvokeSizePartChangedEvent(new SizePartChangedEventArgs());
        }
    }

    static partial class Extension
    {
        /// <summary>
        /// Picks out the first part of a folder sub path.
        /// IE: "AAA\BBB\CCC" --> "AAA"
        /// If the part is "AAA" then "AAA" is returned.
        /// </summary>
        /// <param name="folderSubPath"></param>
        /// <returns></returns>
        public static string RootFolderNamePart(this string folderSubPath)
        {
            int pos = folderSubPath.IndexOf("\\");
            if (pos == -1) return folderSubPath;
            return folderSubPath.Substring(0, pos);
        }

        /// <summary>
        /// Picks out the sub part of a folder sub path.
        /// IE: "AAA\BBB\CCC" --> "BBB\CCC"
        /// If the part is "AAA" then null is returned.
        /// </summary>
        /// <param name="folderSubPath"></param>
        /// <returns></returns>
        public static string SubFolderNamePart(this string folderSubPath)
        {
            int pos = folderSubPath.IndexOf("\\");
            if (pos == -1) return null;
            return folderSubPath.Substring(pos + 1);
        }

        /// <summary>
        /// Picks out the parent part of a folder (of full file name) path.
        /// IE: "AAA\BBB\CCC" --> "AAA\BBB"
        /// </summary>
        /// <param name="folderSubPath"></param>
        /// <returns></returns>
        public static string ParentFolderNamePart(this string folderSubPath)
        {
            int pos = folderSubPath.LastIndexOf("\\");
            var data = folderSubPath.Substring(0, pos);
            if (data.EndsWith(":")) 
                pos++;
            return folderSubPath.Substring(0, pos);
        }

        /// <summary>
        /// Picks out the last part of the folder path.
        /// IE: "AAA\BBB\CCC" --> CCC"
        /// </summary>
        /// <param name="folderSubPath"></param>
        /// <returns></returns>
        public static string ChildNamePart(this string folderSubPath)
        {
            int pos = folderSubPath.LastIndexOf("\\");
            return folderSubPath.Substring(pos + 1);
        }
    }
}
