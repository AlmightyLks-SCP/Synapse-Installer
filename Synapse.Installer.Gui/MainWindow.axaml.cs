using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Synapse.Installer.Gui.ViewModels;

namespace Synapse.Installer.Gui
{
    public partial class MainWindow : Window
    {
        private InstallerViewModel _installerViewModel;

        public MainWindow()
        {
            _installerViewModel = new InstallerViewModel(this);
            DataContext = _installerViewModel;
            Initialized += _installerViewModel.OnLoad;

            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
