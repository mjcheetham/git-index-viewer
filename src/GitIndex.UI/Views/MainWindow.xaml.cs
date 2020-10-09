using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GitIndex.UI.Views
{
    public class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}