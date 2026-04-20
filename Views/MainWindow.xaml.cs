using System.Windows;
using YouTubeDownloader.ViewModels;

namespace YouTubeDownloader;

public partial class MainWindow : Window {
    //public MainWindow() {
    //    InitializeComponent();
    //    DataContext = new MainViewModel();
    //}
    public MainWindow(MainViewModel viewModel) {
        InitializeComponent();
        DataContext = viewModel;
    }
}