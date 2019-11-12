using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MSBuildForUnity.Tasks
{
    /// <summary>
    /// Generates a Unity asset id (formatted guid) from an asset name (text).
    /// Since Unity asset ids are guids(128 bit), use an MD5 hash (also 128 bits) formatted as a hex string.
    /// </summary>
    public sealed class GenerateAssetId : Task
    {
        [Required]
        public string AssetName { get; set; }

        [Output]
        public String AssetId { get; set; }

        public override bool Execute()
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(this.AssetName);
            using (var md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                this.AssetId = string.Join(string.Empty, hashBytes.Select(b => b.ToString("x2")));
            }

            return true;
        }
    }
}
