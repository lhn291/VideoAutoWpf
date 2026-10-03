using System.Windows;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Views;

public partial class ScriptGeneratorWindow : Window
{
    public ScriptGeneratorViewModel ViewModel { get; }
    public ScriptWorkspace? ResultWorkspace => ViewModel.ResultWorkspace;

    public ScriptGeneratorWindow(
        GeminiScriptService geminiService, 
        GoogleTtsService? ttsService = null, 
        BgmService? bgmService = null,
        CreateVideoFromAnalyticsRequest? analyticsRequest = null)
    {
        InitializeComponent();
        WindowState = WindowState.Maximized;
        ViewModel = new ScriptGeneratorViewModel(geminiService, ttsService, bgmService);
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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ViewModel.Cleanup();
    }
}
