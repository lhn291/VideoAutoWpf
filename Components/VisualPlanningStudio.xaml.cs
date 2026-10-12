using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;
using VideoAutoWpf.ViewModels;

namespace VideoAutoWpf.Components;

public partial class VisualPlanningStudio : UserControl
{
    public VisualPlanningStudio()
    {
        InitializeComponent();
    }

    private VisualPlanningViewModel? Vm => DataContext as VisualPlanningViewModel;

    private void CharacterCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is CharacterProfile ch && Vm != null)
        {
            Vm.SelectedCharacter = ch;
        }
    }

    private void LocationCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is LocationProfile loc && Vm != null)
        {
            Vm.SelectedLocation = loc;
        }
    }

    private void PreviewCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border b && b.Tag is ScenePreviewItem preview && Vm != null)
        {
            Vm.SelectedPreview = preview;
        }
    }
}
