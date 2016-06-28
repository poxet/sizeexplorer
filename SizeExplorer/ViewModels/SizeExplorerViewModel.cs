using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SizeExplorer.Model;

namespace SizeExplorer.ViewModels
{
    class SizeExplorerViewModel : INotifyPropertyChanged
	{
	    private readonly ReadOnlyCollection<FolderViewModel> _folders;
        private int _fileScanCounter;
        private int _folderScanCounter;

        public string ScanStatusText { get; private set; }
        public string FileEventText { get; private set; }
        public ReadOnlyCollection<FolderViewModel> Folders { get { return _folders; } }

        #region Event


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }


        #endregion
        #region Constructors
        

        public SizeExplorerViewModel()
            : this(false)
        {

        }

        public SizeExplorerViewModel(bool loadTreeAsync)
        {
            File.FileScannedEvent += FileFileScannedEvent;
            Folder.FolderScannedEvent += FolderFolderScannedEvent;
            Folder.FileActionEvent += FolderFileActionEvent;

            var folders = new List<FolderViewModel>();

            var drives = Folder.GetLogicalDrives(loadTreeAsync);
            foreach (var drive in drives)
                folders.Add(new FolderViewModel(drive));

            //var sortedFolders = (IList<FolderViewModel>)(from item in folders orderby item.SizePart select item);

            //TODO: What if a new drive is attached, or removed? (Is there an event for that)
            _folders = new ReadOnlyCollection<FolderViewModel>(folders);
        }


        #endregion

        private void FileFileScannedEvent(object sender, File.FileScannedEventArgs e)
        {
            _fileScanCounter++;
            if (_fileScanCounter % 100 == 0)
                UpdateScanStatusText();
        }

        private void FolderFolderScannedEvent(object sender, Folder.FolderScannedEventArgs e)
        {
            _folderScanCounter++;
            //UpdateScanStatusText();
        }

        private void FolderFileActionEvent(object sender, Folder.FileActionEventArgs e)
        {
            FileEventText = e.Message;
            OnPropertyChanged("FileEventText");
        }

        private void UpdateScanStatusText()
        {
            ScanStatusText = string.Format("{0} file and {1} folders scanned.", _fileScanCounter, _folderScanCounter);
            OnPropertyChanged("ScanStatusText");            
        }
	}
}
