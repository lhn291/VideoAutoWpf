using System.Windows;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Views;

public partial class AutomationDashboardWindow : Window
{
    public AutomationDashboardWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
