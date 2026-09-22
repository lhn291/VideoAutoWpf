using System.Windows;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Views;

public partial class ScriptGeneratorWindow : Window
{
    public ScriptGeneratorViewModel ViewModel { get; }
    public ScriptWorkspace? ResultWorkspace => ViewModel.ResultWorkspace;

    public ScriptGeneratorWindow(GeminiScriptService geminiService)
    {
        InitializeComponent();
        ViewModel = new ScriptGeneratorViewModel(geminiService);
        ViewModel.RequestClose = (dialogResult) =>
        {
            DialogResult = dialogResult;
            Close();
        };
        DataContext = ViewModel;
    }
}
