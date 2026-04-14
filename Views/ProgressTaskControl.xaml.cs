using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YouTubeDownloader.Models;
using YouTubeDownloader.Models.ProgressTasks;

namespace YouTubeDownloader.Views
{
    /// <summary>
    /// Interaction logic for ProgressTaskControl.xaml
    /// </summary>
    public partial class ProgressTaskControl : UserControl
    {
        public static readonly DependencyProperty TaskProperty = DependencyProperty.Register(nameof(Task), typeof(ProgressTask), typeof(ProgressTaskControl), new PropertyMetadata(null, OnTaskChanged));

        public static readonly DependencyProperty CancelCommandProperty = DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(ProgressTaskControl));

        public ProgressTask? Task {
            get => (ProgressTask?)GetValue(TaskProperty);
            set => SetValue(TaskProperty, value);
        }

        public ICommand? CancelCommand {
            get => (ICommand?)GetValue(CancelCommandProperty);
            set => SetValue(CancelCommandProperty, value);
        }

        public ProgressTaskControl()
        {
            InitializeComponent();
        }

        private static void OnTaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is ProgressTaskControl control && e.NewValue is ProgressTask task) {
                control.DataContext = task;
            }
        }
    }
}
