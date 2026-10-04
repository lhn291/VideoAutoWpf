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

        viewModel.RequestSwitchTab = (tabIndex) =>
        {
            Dispatcher.Invoke(() =>
            {
                switch (tabIndex)
                {
                    case 0: Tab0.IsChecked = true; break;
                    case 1: TabTopic.IsChecked = true; break;
                    case 2: Tab1.IsChecked = true; break;
                    case 3: Tab2.IsChecked = true; break;
                    case 4: Tab3.IsChecked = true; break;
                }
            });
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

    private void TopicSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is YouTubeAnalyticsViewModel vm)
        {
            if (vm.SearchTopChannelsByTopicCommand.CanExecute(null))
            {
                vm.SearchTopChannelsByTopicCommand.Execute(null);
            }
        }
    }

    private void ChannelSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is YouTubeAnalyticsViewModel vm)
        {
            if (vm.AnalyzeChannelCommand.CanExecute(null))
            {
                vm.AnalyzeChannelCommand.Execute(null);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
