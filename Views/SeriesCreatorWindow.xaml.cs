using System.Windows;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Views;

public partial class SeriesCreatorWindow : Window
{
    public SeriesCreatorViewModel ViewModel { get; }
    public SeriesProject? ResultSeriesProject => ViewModel.ResultSeriesProject;

    public SeriesCreatorWindow(GeminiScriptService geminiService, YouTubeAnalyticsService? ytService, CreateVideoFromAnalyticsRequest? analyticsRequest = null)
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
        ViewModel = new SeriesCreatorViewModel(geminiService, ytService);
        ViewModel.RequestClose = (dialogResult) =>
        {
            DialogResult = dialogResult;
            Close();
        };

        if (analyticsRequest != null)
        {
            ViewModel.InitFromAnalyticsRequest(analyticsRequest);
        }

        DataContext = ViewModel;
    }

    public SeriesCreatorWindow(GeminiScriptService geminiService, CreateVideoFromAnalyticsRequest? analyticsRequest = null)
        : this(geminiService, null, analyticsRequest)
    {
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
