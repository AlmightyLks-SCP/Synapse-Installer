using Avalonia.Controls;
using Avalonia.Interactivity;
using Synapse.Installer.Gui.Commands;
using Synapse.Installer.Gui.Services;
using Synapse.Installer.Shared.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Synapse.Installer.Gui.ViewModels
{
    public class InstallerViewModel : ViewModelBase
    {
        public RelayCommand InstallCommand { get; }
        public RelayCommand ServerPathCommand { get; }
        public SynapseService SynapseService { get; }

        private GitHubRelease _selectedRelease;
        public GitHubRelease SelectedRelease
        {
            get => _selectedRelease;
            set
            {
                if (_selectedRelease != value)
                {
                    _selectedRelease = value;
                    OnPropertyChanged();
                    InstallCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _serverPath;
        public string ServerPath
        {
            get => _serverPath;
            set
            {
                if (_serverPath != value)
                {
                    _serverPath = value;
                    OnPropertyChanged();
                    InstallCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _installationProgress;
        public string InstallationProgress
        {
            get => _installationProgress;
            set
            {
                if (_installationProgress != value)
                {
                    _installationProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _includePreRelease;
        public bool IncludePreRelease
        {
            get => _includePreRelease;
            set
            {
                if (_includePreRelease != value)
                {
                    _includePreRelease = value;
                    SynapseService.LoadGitHubReleases();
                    OnPropertyChanged();
                }
            }
        }

        private Window _window;

        public InstallerViewModel(Window window)
        {
            InstallationProgress = string.Empty;
            _window = window;
            SynapseService = new SynapseService(this);
            InstallCommand = new RelayCommand(async () => await OnInstallButtonClicked(), ValidateInput);
            ServerPathCommand = new RelayCommand(async () => await OnSelectServerPath(), () => true);
            _serverPath = string.Empty;
        }

        public void OnLoad(object sender, EventArgs e)
        {
            SynapseService.LoadGitHubReleases();
        }
        public async Task OnInstallButtonClicked()
        {
            await SynapseService.DownloadGitHubRelease(SelectedRelease, ServerPath);
        }
        public async Task OnSelectServerPath()
        {
            ServerPath = await SynapseService.SelectServerPath(_window);
        }
        private bool ValidateInput()
        {
            bool result = true;

            result &= SelectedRelease != null;
            result &= !string.IsNullOrWhiteSpace(ServerPath);

            try
            {
                //If given directory contains the server file
                var fileNames = Directory.GetFiles(ServerPath)
                    .Select(path => Path.GetFileName(path));
                result &= fileNames.Contains("SCPSL.exe");
            }
            catch
            {
                result = false;
            }

            return result;
        }
    }
}
