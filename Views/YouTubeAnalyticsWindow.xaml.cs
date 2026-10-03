using System.Windows;
using System.Windows.Controls;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Views;

public partial class YouTubeAnalyticsWindow : Window
{
    public YouTubeAnalyticsWindow(YouTubeAnalyticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestCloseWithAction = () =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tagStr && int.TryParse(tagStr, out var tabIndex))
        {
            if (DataContext is YouTubeAnalyticsViewModel vm)
            {
                vm.SelectedTab = tabIndex;
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
