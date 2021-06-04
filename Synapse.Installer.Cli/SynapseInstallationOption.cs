using CommandLine;

namespace Synapse.Installer.Cli
{
    [Verb("install", isDefault: true)]
    public class SynapseInstallationOption
    {
        [Option(longName: "server", Required = true, HelpText = "Path to your SL Server")]
        [Value(index: 0, Required = true)]
        public string ServerPath { get; set; }

        [Option(longName: "latest", Required = false, HelpText = "Download & install latest release")]
        [Value(index: 1, Required = true)]
        public bool LatestRelease { get; set; }

        [Option(longName: "prerelease", Required = false, HelpText = "Whether it should download a pre-release")]
        [Value(index: 2, Required = true)]
        public bool PreRelease { get; set; }
    }
}
