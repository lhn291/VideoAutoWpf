using System.Windows;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Views;

public partial class ScriptGeneratorWindow : Window
{
    public ScriptGeneratorViewModel ViewModel { get; }
    public ScriptWorkspace? ResultWorkspace => ViewModel.ResultWorkspace;

    public ScriptGeneratorWindow(GeminiScriptService geminiService, GoogleTtsService? ttsService = null, BgmService? bgmService = null)
    {
        InitializeComponent();
        ViewModel = new ScriptGeneratorViewModel(geminiService, ttsService, bgmService);
        ViewModel.RequestClose = (dialogResult) =>
        {
            DialogResult = dialogResult;
            Close();
        };
        DataContext = ViewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ViewModel.Cleanup();
    }
}
