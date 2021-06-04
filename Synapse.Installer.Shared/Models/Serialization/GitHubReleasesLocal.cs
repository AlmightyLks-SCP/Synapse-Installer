using Synapse.Installer.Shared.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synapse.Installer.Shared.Models.Serialization
{
    public class GitHubReleasesLocal
    {
        public DateTime DateTime { get; set; }
        public List<GitHubRelease> GitHubReleases { get; set; }
    }
}
