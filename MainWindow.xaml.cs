using System.Windows;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf;

/// <summary>
/// Cửa sổ chính MainWindow quản lý điều hướng và sự kiện kéo thả toàn cục
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                if (DataContext is MainViewModel vm)
                {
                    await vm.HandleDroppedFilesAsync(files);
                }
            }
        }
    }
}