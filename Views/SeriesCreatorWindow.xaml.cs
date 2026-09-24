using System.Windows;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Views;

public partial class SeriesCreatorWindow : Window
{
    public SeriesCreatorViewModel ViewModel { get; }
    public SeriesProject? ResultSeriesProject => ViewModel.ResultSeriesProject;

    public SeriesCreatorWindow(GeminiScriptService geminiService)
    {
        InitializeComponent();
        ViewModel = new SeriesCreatorViewModel(geminiService);
        ViewModel.RequestClose = (dialogResult) =>
        {
            DialogResult = dialogResult;
            Close();
        };
        DataContext = ViewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
