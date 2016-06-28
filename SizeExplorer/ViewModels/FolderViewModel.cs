using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SizeExplorer.Model;
using Cursors = System.Windows.Input.Cursors;

namespace SizeExplorer.ViewModels
{
    class FolderViewModel : TreeViewItemViewModel
    {
        private readonly Folder _folder;
        private ObservableCollection<FileItemViewModel> _files;
        private ImageSource _icon;

        public string FullName { get { return _folder.FullName; } }
        public string Name { get { return _folder.Name; } }
        public string SizeInformation { get { return _folder.TotalSize != null ? _folder.TotalSize.Value.ToSizeString() : ""; } }
        public string Message { get { return _folder.Message; } }
        public SolidColorBrush Foreground
        {
            get 
            {
                if ( _folder.Message != null )
                    return new SolidColorBrush(Colors.Red);

                if ( _folder.IsScanning)
                    return new SolidColorBrush(Colors.DarkGreen);

                if ( _folder.TotalSize == null)
                    return new SolidColorBrush(Colors.LightGray);
                return new SolidColorBrush(Colors.Black);
            }
        }
        public ImageSource Icon { get { return _icon ?? (_icon = FileItemViewModel.GetFileIconImageSource(FullName, false,false)); } }
        public Visibility SizePartVisability
        {
            get 
            {
                if (_folder.IsScanning)
                    return Visibility.Visible; //Make the bar move

                if (_folder.Message != null)
                    return Visibility.Hidden;

                if (_folder.SizePart == null)
                    return Visibility.Hidden;
                return Visibility.Visible;
            }
        }
        public int SizePart
        {
            get
            {
                if (_folder.SizePart == null)
                    return 0;
                return (int)_folder.SizePart.Value;
            } 
            set
            {
                throw new InvalidOperationException();
            }
        }
        public bool IsScanning { get { return _folder.IsScanning; } }
        public string SizePartInfo
        {
            get
            {
                if (_folder.IsScanning)
                    return "Scanning..."; //Currently scanning the folder (or subfolder)

                if ( _folder.Message != null)
                    return ""; //Error message, show nothing.

                if (_folder.TotalSize == null)
                    return ""; //Not yet scanned
                
                if (_folder.SizePart == null)
                    return "Waiting for siblings..."; //Waiting for other sister folders, so that the part can be calculated

                return string.Format("{0}%", _folder.SizePart.Value.ToString("0"));
            }
        }
        
        public ObservableCollection<FileItemViewModel> Files
        {
            get
            {
                if (_files == null)
                {
                    _files = new ObservableCollection<FileItemViewModel>();
                    foreach (var file in _folder.Files)
                        _files.Add(new FileItemViewModel(file));
                }

                return _files;
            }
        }

        public FolderViewModel(Folder folder)
            : this(folder, null)
        {

        }

        public FolderViewModel(Folder folder, FolderViewModel parent)
            :base(parent,true)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            _folder = folder;
            _folder.NameChangedEvent += FolderNameChangedEvent;
            _folder.FolderStartScanningEvent += FolderStartScanningEvent;
            _folder.SizePartChangedEvent += FolderSizePartChangedEvent;
            _folder.SizeChangedEvent += FolderSizeChangedEvent;
            _folder.MessageChangedEvent += FolderMessageChangedEvent;
            _folder.FileCreatedEvent += FolderFileCreatedEvent;
            _folder.FileDeletedEvent += FolderFileDeletedEvent;
        }

        private readonly Dispatcher _dispatcher;

        public delegate void FolderFileCreatedEventCallback(object sender, Folder.FileEventArgs e);
        void FolderFileCreatedEvent(object sender, Folder.FileEventArgs e)
        {
            if ( _dispatcher.CheckAccess() )
            {
                var file = new FileItemViewModel(e.File);
                if (Files != null)
                    Files.Add(file);
            }
            else
                _dispatcher.Invoke(new FolderFileCreatedEventCallback(FolderFileCreatedEvent), new object[] {sender, e});
        }

        public delegate void FolderFileDeletedEventCallback(object sender, Folder.FileEventArgs e);
        void FolderFileDeletedEvent(object sender, Folder.FileEventArgs e)
        {
            if (_dispatcher.CheckAccess())
            {
                foreach (var file in Files)
                {
                    if (string.Compare(file.Name, e.File.Name) == 0)
                    {
                        Files.Remove(file);
                        return;
                    }
                }
            }
            else
                _dispatcher.Invoke(new FolderFileDeletedEventCallback(FolderFileDeletedEvent), new object[] { sender, e });
        }

        void FolderNameChangedEvent(object sender, Folder.NameChangedEventArgs e)
        {
            OnPropertyChanged("Name");
        }

        private void FolderStartScanningEvent(object sender, Folder.FolderStartScanningEventArgs e)
        {
            UpdateAllProperties();
            //OnPropertyChanged("Foreground");
            //OnPropertyChanged("SizePartVisability");
            //OnPropertyChanged("SizePartInfo");
            ////TODO: Also change to a progress bar
        }

        void FolderSizePartChangedEvent(object sender, Folder.SizePartChangedEventArgs e)
        {
            //When the size part changed, all siblings have been calculated and can be re-sorted within the parent folder

            //if (_files != null)
            //    _files = (ObservableCollection<FileItemViewModel>) (from item in _files orderby item.Size select item);
            UpdateAllProperties();
            //OnPropertyChanged("SizePart");
            //OnPropertyChanged("SizePartInfo");
            //OnPropertyChanged("SizePartVisability");
        }

        void FolderSizeChangedEvent(object sender, Folder.SizeChangedEventArgs e)
        {
            //ReloadChildren(); //Resort the children to a folder when the parents size changed
            UpdateAllProperties();
        }

        void FolderMessageChangedEvent(object sender, Folder.MessageChangedEventArgs e)
        {
            UpdateAllProperties();
            //OnPropertyChanged("Message");
            //OnPropertyChanged("Foreground");
            //OnPropertyChanged("SizeInformation");
            //OnPropertyChanged("SizePart");
            //OnPropertyChanged("SizePartInfo");
            //OnPropertyChanged("SizePartVisability");
        }

        private void UpdateAllProperties()
        {
            OnPropertyChanged("Message");
            OnPropertyChanged("Foreground");
            OnPropertyChanged("SizeInformation");
            OnPropertyChanged("SizePart");
            OnPropertyChanged("SizePartInfo");
            OnPropertyChanged("SizePartVisability");
            OnPropertyChanged("IsScanning");            
        }

        public delegate void ReloadChildrenEventCallback();
        protected void ReloadChildren()
        {
            //TODO: Check if to fire delegate
            if (_dispatcher.CheckAccess())
            {
                if (Children.Count > 1)
                {
                    //Children.Sort
                    //TODO: Also check if already sorted
                    //Children.Clear();
                    //LoadChildren();
                    //SortChildren(TODO);
                    SortChildren(new FolderSizeComparer());
                }
            }
            else
                _dispatcher.Invoke(new ReloadChildrenEventCallback(ReloadChildren), new object[] {});
                
        }

        protected override void LoadChildren()
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                if (_folder.IsDrive)
                {
                    //TODO: If this is a network drive, ask the user to confirm scanner
                    _folder.StartScan();
                }

                //TODO: Sort children (When the folder is completley scanned, resort!)
                var sortedChildren = (from item in _folder.Children orderby item.TotalSize descending select item);

                foreach (var folder in sortedChildren) //_folder.Children)
                    Children.Add(new FolderViewModel(folder, this));                
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void InvokeCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var handler = CollectionChanged;
            if (handler != null) 
                handler(this, e);
        }
    }

    static partial class Extension
    {
        public static string ToSizeString(this long data)
        {
            if (data < 1024)
                return string.Format("{0} byte", data);
            if (data / 1024 < 1024)
                return string.Format("{0} KB", ((double)data / 1024).ToString("0.00"));
            if (data / 1024 / 1024 < 1024)
                return string.Format("{0} MB", ((double)data / 1024 / 1024).ToString("0.00"));
            if (data / 1024 / 1024 / 1024 < 1024)
                return string.Format("{0} GB", ((double)data / 1024 / 1024 / 1024).ToString("0.00"));
            return string.Format("{0} TB", ((double)data / 1024 / 1024 / 1024 / 1024).ToString("0.00"));
        }
    }

    internal class FolderSizeComparer : IComparer<TreeViewItemViewModel>
    {
        public int Compare(TreeViewItemViewModel x, TreeViewItemViewModel y)
        {
            if ( x is FolderViewModel && y is FolderViewModel)
            {
                var xx = (FolderViewModel) x;
                var yy = (FolderViewModel) y;

                return xx.SizePart - yy.SizePart;
            }
            throw new InvalidOperationException();
        }
    }
}
