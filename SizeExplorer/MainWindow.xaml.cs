using System.ComponentModel;
using System.Windows;
using SizeExplorer.ViewModels;

namespace SizeExplorer
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            var loadTreeAsync = !DesignerProperties.GetIsInDesignMode(this);
            var sizeExplorerViewModel = new SizeExplorerViewModel(loadTreeAsync);

            InitializeComponent();

            DataContext = sizeExplorerViewModel;
        }

        private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
        }
    }
}
