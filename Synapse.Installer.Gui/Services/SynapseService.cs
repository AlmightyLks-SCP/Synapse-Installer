using Avalonia.Controls;
using Synapse.Installer.Gui.ViewModels;
using Synapse.Installer.Shared;
using Synapse.Installer.Shared.Model;
using Synapse.Installer.Shared.Models.Serialization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Synapse.Installer.Gui.Services
{
    public class SynapseService
    {
        public ObservableCollection<GitHubRelease> Releases { get; private set; }

        private HttpClient _client;
        private string _localSynapseReleasesFilePath;

        private OpenFolderDialog _openFolderDialog;
        private InstallerViewModel _installerViewModel;

        public SynapseService(InstallerViewModel installerViewModel)
        {
            _installerViewModel = installerViewModel;

            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", InstallerConstants.HttpUserAgent);
            _client.DefaultRequestHeaders.Add("Accept", InstallerConstants.HttpAccept);

            Releases = new ObservableCollection<GitHubRelease>();

            _localSynapseReleasesFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"Local-GitHubReleases.json");
            _openFolderDialog = new OpenFolderDialog();
            _openFolderDialog.Directory = Directory.GetCurrentDirectory();
        }

        public async Task DownloadGitHubRelease(GitHubRelease release, string serverPath)
        {
            try
            {
                _installerViewModel.InstallationProgress = "Determining release...";
                var synapseAsset = release.Assets.Find(_ => _.Name == "Synapse2.zip");
                if (synapseAsset == null)
                {
                    _installerViewModel.InstallationProgress = "Invalid release";
                    return;
                }

                _installerViewModel.InstallationProgress = $"Downloading {release.TagName}...";
                //Download Synapse2.zip
                var response = await _client.GetAsync(synapseAsset.BrowserDownloadUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync("Synapse2.zip", bytes);
                if (!Directory.Exists("Temp"))
                {
                    Directory.CreateDirectory("Temp");
                }
                _installerViewModel.InstallationProgress = $"Extracting...";
                ZipFile.ExtractToDirectory("Synapse2.zip", "Temp");

                _installerViewModel.InstallationProgress = $"Replacing Assembly-CSharp.dll...";
                //Replace Assembly-CSharp.dll
                string slManagedPath = Path.Combine(serverPath, "SCPSL_Data", "Managed");
                File.Copy(
                    Path.Combine("Temp", "Assembly-CSharp.dll"),
                    Path.Combine(slManagedPath, "Assembly-CSharp.dll"),
                    true
                    );

                _installerViewModel.InstallationProgress = @$"Creating AppData\Roaming\Synapse folder...";
                //Create AppData Synapse folder
                string synapseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Synapse");
                if (!Directory.Exists(synapseFolder))
                {
                    Directory.CreateDirectory(synapseFolder);
                }

                //_installerViewModel.InstallationProgress = @$"Copying Synapse.dll...";
                //Copy / Replace Synapse.dll
                File.Copy(
                    Path.Combine("Temp", "Synapse", "Synapse.dll"),
                    Path.Combine(synapseFolder, "Synapse.dll"),
                    true
                    );

                //_installerViewModel.InstallationProgress = @$"Creating AppData\Roaming\Synapse\dependencies folder...";
                //Create AppData Synapse Dependencies folder
                if (!Directory.Exists(Path.Combine(synapseFolder, "dependencies")))
                {
                    Directory.CreateDirectory(Path.Combine(synapseFolder, "dependencies"));
                }

                //_installerViewModel.InstallationProgress = @$"Copying over dependencies...";
                //Copy / Replace dependencies over
                foreach (var dependencyFile in Directory.GetFiles(Path.Combine("Temp", "Synapse", "dependencies")))
                {
                    var fullPathDependencyFile = Path.Combine(Directory.GetCurrentDirectory(), dependencyFile);
                    var fileName = Path.GetFileName(fullPathDependencyFile);
                    var expectedFilePath = Path.Combine(synapseFolder, "dependencies", fileName);
                    _installerViewModel.InstallationProgress = @$"Copying {fileName}...";
                    File.Copy(fullPathDependencyFile, expectedFilePath, true);
                }

                _installerViewModel.InstallationProgress = @$"Deleting Synapse2.zip...";
                if (File.Exists("Synapse2.zip"))
                {
                    File.Delete("Synapse2.zip");
                }
                _installerViewModel.InstallationProgress = @$"Deleting Temp folder...";
                if (Directory.Exists("Temp"))
                {
                    Directory.Delete("Temp", true);
                }
                _installerViewModel.InstallationProgress = @$"Done!";
            }
            catch (Exception e)
            {
                _installerViewModel.InstallationProgress = $"Something went wrong!{Environment.NewLine}{e}";
            }
        }
        public async Task<string> SelectServerPath(Window window)
        {
            return await _openFolderDialog.ShowAsync(window);
        }
        public void LoadGitHubReleases()
        {
            if (File.Exists(_localSynapseReleasesFilePath))
            {
                LoadLocalGitHubReleasesFile();
            }
            else
            {
                FetchGitHubReleases();
            }
        }
        private void LoadLocalGitHubReleasesFile()
        {
            if (!File.Exists(_localSynapseReleasesFilePath))
            {
                return;
            }

            try
            {
                string jsonContent = File.ReadAllText(_localSynapseReleasesFilePath);
                GitHubReleasesLocal localReleases = JsonSerializer.Deserialize<GitHubReleasesLocal>(jsonContent);

                //If info is older than two minutes, delete the file and try re-fetching it
                if ((DateTime.Now - localReleases.DateTime) > TimeSpan.FromMinutes(5))
                {
                    File.Delete(_localSynapseReleasesFilePath);
                    FetchGitHubReleases();
                }
                else
                {
                    Releases.Clear();
                    //Only releases with a "Synapse2.zip"
                    foreach (var release in localReleases.GitHubReleases.Where(_ => _.Assets.Any(_ => _.Name == "Synapse2.zip")))
                    {
                        if (_installerViewModel.IncludePreRelease)
                        {
                            Releases.Add(release);
                        }
                        else if (!release.PreRelease)
                        {
                            Releases.Add(release);
                        }
                    }
                }
            }
            catch
            {

            }
        }
        private void FetchGitHubReleases()
        {
            try
            {
                var response = _client.GetAsync(InstallerConstants.SynapseApiEndpoint)
                    .GetAwaiter()
                    .GetResult();

                if (response.IsSuccessStatusCode)
                {

                    string jsonStr = response.Content.ReadAsStringAsync()
                        .GetAwaiter()
                        .GetResult();

                    var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(jsonStr);
                    Releases.Clear();

                    //Only releases with a "Synapse2.zip"
                    foreach (var release in releases.Where(_ => _.Assets.Any(_ => _.Name == "Synapse2.zip")))
                    {
                        if (_installerViewModel.IncludePreRelease)
                        {
                            Releases.Add(release);
                        }
                        else if (!release.PreRelease)
                        {
                            Releases.Add(release);
                        }
                    }
                    SaveLocally();
                }
                else //If rate-limited, opt for local file
                {
                    LoadLocalGitHubReleasesFile();
                }
            }
            catch
            {

            }
        }
        private void SaveLocally()
        {
            if (Releases.Count == 0)
            {
                return;
            }
            GitHubReleasesLocal gitHubReleasesLocal = new GitHubReleasesLocal()
            {
                GitHubReleases = Releases.ToList(),
                DateTime = DateTime.Now
            };
            var json = JsonSerializer.Serialize(gitHubReleasesLocal, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(_localSynapseReleasesFilePath, json);
        }
    }
}
