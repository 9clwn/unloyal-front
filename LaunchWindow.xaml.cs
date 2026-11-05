using System.Windows;

namespace UnloyalFront.UI;

public partial class LaunchWindow : Window
{
    public LaunchWindow() => InitializeComponent();

    public void Update(string message, int percent)
    {
        StatusText.Text = message;
        Bar.Value = Math.Clamp(percent, 0, 100);
    }
}
