using System.Windows;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Views;

public partial class SocialPublishWindow : Window
{
    public SocialPublishWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
