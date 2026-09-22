using System.Windows.Controls;

namespace VideoAutoWpf.Components;

public partial class ConsoleLogControl : UserControl
{
    public ConsoleLogControl()
    {
        InitializeComponent();
    }

    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.ScrollToEnd();
        }
    }
}
