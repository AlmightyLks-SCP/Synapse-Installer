using CommandLine;
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

namespace Synapse.Installer.Cli
{
    class Program
    {
        private static List<GitHubRelease> _releases;
        private static HttpClient _client;
        private static string _localSynapseReleasesFilePath;
        private static SynapseInstallationOption _synapseInstallationOption;
        private static bool _running;

        static void Main(string[] args)
        {
            Init();
            //Console.WriteLine(string.Join(" ", args));

            var result = Parser.Default.ParseArguments<SynapseInstallationOption>(args);
            result.WithNotParsed(stuff => Console.WriteLine("Please check your input and try again"));
            result.WithParsed(async installOptions => await ProcessParsing(installOptions));

            while (_running) ;
        }
        private static void Init()
        {
            _running = false;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", InstallerConstants.HttpUserAgent);
            _client.DefaultRequestHeaders.Add("Accept", InstallerConstants.HttpAccept);

            _releases = new List<GitHubRelease>();

            _localSynapseReleasesFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"Local-GitHubReleases.json");
        }
        private static async Task ProcessParsing(SynapseInstallationOption option)
        {
            _synapseInstallationOption = option;
            //Console.WriteLine($"{option.ServerPath} | {option.LatestRelease} | {option.PreRelease}");

            LoadGitHubReleases();
            await DownloadGitHubRelease(_releases.OrderByDescending(_ => _.CreatedAt).First(), option.ServerPath);
        }
        private static async Task DownloadGitHubRelease(GitHubRelease release, string serverPath)
        {
            _running = true;
            try
            {
                Console.WriteLine("Determining release...");
                var synapseAsset = release.Assets.Find(_ => _.Name == "Synapse2.zip");
                if (synapseAsset == null)
                {
                    Console.WriteLine("Invalid release");
                    return;
                }

                Console.WriteLine($"Downloading {release.TagName}...");
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
                Console.WriteLine($"Extracting...");
                ZipFile.ExtractToDirectory("Synapse2.zip", "Temp");

                Console.WriteLine($"Replacing Assembly-CSharp.dll...");
                //Replace Assembly-CSharp.dll
                string slManagedPath = Path.Combine(serverPath, "SCPSL_Data", "Managed");
                File.Copy(
                    Path.Combine("Temp", "Assembly-CSharp.dll"),
                    Path.Combine(slManagedPath, "Assembly-CSharp.dll"),
                    true
                    );

                Console.WriteLine(@$"Creating Synapse folder...");
                //Create AppData Synapse folder
                string synapseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Synapse");
                if (!Directory.Exists(synapseFolder))
                {
                    Directory.CreateDirectory(synapseFolder);
                }

                Console.WriteLine(@$"Copying Synapse.dll...");
                //Copy / Replace Synapse.dll
                File.Copy(
                    Path.Combine("Temp", "Synapse", "Synapse.dll"),
                    Path.Combine(synapseFolder, "Synapse.dll"),
                    true
                    );

                Console.WriteLine(@$"Creating Synapse\dependencies folder...");
                //Create AppData Synapse Dependencies folder
                if (Directory.Exists(Path.Combine(synapseFolder, "dependencies")))
                {
                    Directory.Delete(Path.Combine(synapseFolder, "dependencies"), true);
                }
                Directory.CreateDirectory(Path.Combine(synapseFolder, "dependencies"));

                Console.WriteLine(@$"Copying over dependencies...");
                //Copy / Replace dependencies over
                foreach (var dependencyFile in Directory.GetFiles(Path.Combine("Temp", "Synapse", "dependencies")))
                {
                    var fullPathDependencyFile = Path.Combine(Directory.GetCurrentDirectory(), dependencyFile);
                    var fileName = Path.GetFileName(fullPathDependencyFile);
                    var expectedFilePath = Path.Combine(synapseFolder, "dependencies", fileName);
                    Console.WriteLine(@$"Copying {fileName}...");
                    File.Copy(fullPathDependencyFile, expectedFilePath, true);
                }

                Console.WriteLine(@$"Deleting Synapse2.zip...");
                if (File.Exists("Synapse2.zip"))
                {
                    File.Delete("Synapse2.zip");
                }
                Console.WriteLine(@$"Deleting Temp folder...");
                if (Directory.Exists("Temp"))
                {
                    Directory.Delete("Temp", true);
                }
                Console.WriteLine(@$"Done!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Something went wrong!{Environment.NewLine}{e}");
            }
            _running = false;
        }
        private static void LoadGitHubReleases()
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
        private static void LoadLocalGitHubReleasesFile()
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
                    //Only releases with a "Synapse2.zip"
                    _releases.Clear();
                    foreach (var release in localReleases.GitHubReleases.Where(_ => _.Assets.Any(_ => _.Name == "Synapse2.zip")))
                    {
                        if (_synapseInstallationOption.PreRelease)
                        {
                            _releases.Add(release);
                        }
                        else if (!release.PreRelease)
                        {
                            _releases.Add(release);
                        }
                    }
                }
            }
            catch
            {

            }
        }
        private static void FetchGitHubReleases()
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
                    _releases.Clear();

                    //Only releases with a "Synapse2.zip"
                    foreach (var release in releases.Where(_ => _.Assets.Any(_ => _.Name == "Synapse2.zip")))
                    {
                        if (_synapseInstallationOption.PreRelease)
                        {
                            _releases.Add(release);
                        }
                        else if (!release.PreRelease)
                        {
                            _releases.Add(release);
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
        private static void SaveLocally()
        {
            if (_releases.Count == 0)
            {
                return;
            }
            GitHubReleasesLocal gitHubReleasesLocal = new GitHubReleasesLocal()
            {
                GitHubReleases = _releases,
                DateTime = DateTime.Now
            };
            var json = JsonSerializer.Serialize(gitHubReleasesLocal, new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(_localSynapseReleasesFilePath, json);
        }
    }
}
