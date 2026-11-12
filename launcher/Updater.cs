using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace SteveVsZombiesLauncher
{
    /// <summary>Asks the project's GitHub page for its newest version and downloads its zip.</summary>
    static class Updater
    {
        const string Repo = "podselxd/steve-days-to-die";
        public const string Page = "https://github.com/" + Repo;

        public class Release
        {
            public Version Version;
            public string Tag;
            public string ZipUrl;
            public long Size;
        }

        static HttpWebRequest Request(string url)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "SteveVsZombiesLauncher";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 30000;
            return req;
        }

        /// <summary>The newest release that carries the mod's zip, or null (no internet, nothing published).</summary>
        public static Release Newest()
        {
            var req = Request("https://api.github.com/repos/" + Repo + "/releases?per_page=15");
            req.Accept = "application/vnd.github+json";
            string json;
            using (var resp = req.GetResponse())
            using (var r = new StreamReader(resp.GetResponseStream())) json = r.ReadToEnd();

            var list = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json) as object[];
            Release best = null;
            if (list == null) return null;
            foreach (var item in list)
            {
                var rel = item as IDictionary;
                if (rel == null || true.Equals(rel["draft"])) continue;
                string tag = rel["tag_name"] as string;
                var v = GameSetup.Parse(tag);
                if (v == null || (best != null && v <= best.Version)) continue;
                if (!(rel["assets"] is object[] assets)) continue;
                foreach (var a in assets)
                {
                    var asset = a as IDictionary;
                    string name = asset?["name"] as string;
                    if (name == null || !name.StartsWith("SteveVsZombies-", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    best = new Release
                    {
                        Version = v,
                        Tag = tag,
                        ZipUrl = asset["browser_download_url"] as string,
                        Size = Convert.ToInt64(asset["size"] ?? 0),
                    };
                    break;
                }
            }
            return best;
        }

        /// <summary>Downloads the release's zip into memory, reporting progress 0..1.</summary>
        public static MemoryStream Download(Release rel, Action<double> progress)
        {
            var req = Request(rel.ZipUrl);
            var ms = new MemoryStream();
            using (var resp = req.GetResponse())
            using (var s = resp.GetResponseStream())
            {
                long total = resp.ContentLength > 0 ? resp.ContentLength : rel.Size;
                var buf = new byte[81920];
                int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0)
                {
                    ms.Write(buf, 0, n);
                    if (total > 0) progress?.Invoke(Math.Min(1.0, ms.Length / (double)total));
                }
            }
            ms.Position = 0;
            return ms;
        }
    }
}
