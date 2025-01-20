using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace YAKL.Core
{
    public class YAKLService
    {
        private string GetKeeperRLRootPath()
        {
            return "c:\\Program Files (x86)\\Steam\\steamapps\\common\\KeeperRL\\";
        }

        private string GetKeeperRLModsPath()
        {
            return Path.Combine(GetKeeperRLRootPath(), "Mods");
        }

        private List<LocalMod> ParseLocalModsFromLocalModFolder()
        {
            var result = new List<LocalMod>();

            var modPath = GetKeeperRLModsPath();
            var directories = Directory.EnumerateDirectories(modPath);

            foreach (var dir in directories)
            {
                if (dir != "..")
                {
                    var lm = ParseFromFolder(dir);

                    result.Add(lm);
                }
            }

            return result;
        }

        private LocalMod ParseFromFolder(string path)
        {
            var lm = new LocalMod();
            lm.Name = Path.GetFileName(path);

            var yaklFile = Path.Combine(path, "yakl.txt");
            var descriptionFilePath = Path.Combine(path, "details.txt");
            lm.Description = System.IO.File.ReadAllText(descriptionFilePath);
            lm.PreviewPngPath = Path.Combine(path, "preview.png");

            if (File.Exists(yaklFile))
            {
                lm.FullDirectoryPath = path;
                lm.MinorVersion = int.Parse(System.IO.File.ReadAllText(yaklFile));
            }
            else
            {
                Console.WriteLine($"Yakl {yaklFile} not found");
            }

            return lm;
        }

        public Task UpdateMod(LocalMod lm)
        {
            System.IO.Directory.Delete(lm.FullDirectoryPath, true);
            return DownloadRepoDirToLocalDir(RepoOwner, Repo, lm.Name, lm.FullDirectoryPath);
        }

        public string RepoOwner { get; set; } = DefaultRepoOwner;
        public string Repo { get; set; } = DefaultRepo;
        public string RepoPath { get; set; } = DefaultRepoPath;

        public const string DefaultRepoOwner = "samuellsk";
        public const string DefaultRepo = "mods";
        public const string DefaultRepoPath = "RABBIT";

        public Task<List<LocalMod>> LoadLocalMods()
        {
            var ret = ParseLocalModsFromLocalModFolder();

            var downloadTasks = new List<Task>();

            //https://github.com/samuellsk/mods/tree/main/RABBIT
            foreach (var lm in ret)
            {
                if (lm.MinorVersion.HasValue)
                {
                    try
                    {
                        var downloadTask = GetFileFromGitHub(RepoOwner, Repo, lm.Name + "/yakl.txt").ContinueWith<GitHubHttpResult>((yaklFile, state) =>
                        {
                            GitHubHttpResult ghResult = yaklFile.Result;
                            if (ghResult.Result == HttpResultMy.Ok)
                            {
                                lm.SetNeedUpdateFromYaklFileContent(ghResult.Content);
                            }

                            return ghResult;
                        }, null);

                        downloadTasks.Add(downloadTask);
                    }
                    catch (Exception ex)
                    {
                    }
                }
                else
                {
                    Console.WriteLine($"Mod {lm.Name} is not yakl compatible");
                }
            }

            return Task.WhenAll(downloadTasks).ContinueWith<List<LocalMod>>((result) =>
            {
                return ret;
            });

            //var needToBeUpdated = ret.Where(i => i.NeedUpdate.HasValue && i.NeedUpdate.Value).ToList();

            //Console.WriteLine("These mods need to be updated " + string.Join(",", needToBeUpdated));

            //List<Task> downloadTasks = new List<Task>();

            //foreach (var lmToBeUpdated in needToBeUpdated)
            //{
            //    //delete

            //    System.IO.Directory.Delete(lmToBeUpdated.FullDirectoryPath, true);
            //    downloadTasks.Add(DownloadRepoDirToLocalDir(RepoOwner, Repo, lmToBeUpdated.Name, lmToBeUpdated.FullDirectoryPath));
            //    //download
            //}

            //await Task.WhenAll(downloadTasks);
        }

        public async Task DownloadRepoDirToLocalDir(string owner, string repo, string repoPath, string localPath)
        {
            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{repoPath}";

            // Set the User-Agent header
            _Client.DefaultRequestHeaders.Add("User-Agent", "CSharp-Downloader");

            try
            {
                // Fetch the contents of the directory
                HttpResponseMessage response = await _Client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                string jsonResponse = await response.Content.ReadAsStringAsync();

                // Parse the JSON response
                JArray items = JArray.Parse(jsonResponse);

                List<Task> downloadTasks = new List<Task>();

                foreach (var item in items)
                {
                    string fileName = item["name"].ToString();
                    string downloadUrl = item["download_url"]?.ToString();

                    // If it's a file, download it
                    if (item["type"].ToString() == "file" && !string.IsNullOrEmpty(downloadUrl))
                    {
                        string localFilePath = Path.Combine(localPath, fileName);
                        downloadTasks.Add(DownloadFileAsync(downloadUrl, localFilePath));
                    }
                    else if (item["type"].ToString() == "dir")
                    {
                        downloadTasks.Add(DownloadRepoDirToLocalDir(owner, repo, repoPath + "/" + fileName, Path.Combine(localPath, fileName)));
                    }
                }

                // Wait for all downloads to complete
                await Task.WhenAll(downloadTasks);

                Console.WriteLine("All files downloaded successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public async Task DownloadFileAsync(string downloadUrl, string localPath)
        {
            // Make the GET request to download the file
            HttpResponseMessage response = await _Client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            // Ensure the directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(localPath));

            // Write the file to local storage
            using (var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            Console.WriteLine($"Downloaded {localPath}");
        }

        private static readonly HttpClient _Client = new HttpClient();

        public class GitHubHttpResult
        {
            public byte[] Content { get; set; }
            public HttpResultMy Result { get; set; }
        }

        public enum HttpResultMy
        {
            Ok,
            NotFound
        }

        private static async Task<GitHubHttpResult> GetFileFromGitHub(string owner, string repo, string path)
        {
            //_Client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CSharp-App", "1.0"));
            //https://github.com/miki151/KeeperRLCommunityResources/blob/master/Mods/Alpha36/(A36)%20The%20Overlord/version_info
            //string url = $"https://api.github.com/repos/{owner}/{repo}/blob/main/{path}";

            string url = $"https://raw.githubusercontent.com/{owner}/{repo}/main/{path}";

            //https://raw.githubusercontent.com/samuellsk/mods/main/RABBIT/yakl.txt
            try
            {
                HttpResponseMessage response = _Client.Send(new HttpRequestMessage(HttpMethod.Get, url));
                response.EnsureSuccessStatusCode();
                var responseTest = await response.Content.ReadAsByteArrayAsync();

                return new GitHubHttpResult() { Result = HttpResultMy.Ok, Content = responseTest };
            }
            catch (HttpRequestException ex)
            {
                await new Task<GitHubHttpResult>(() => new GitHubHttpResult() { Result = HttpResultMy.NotFound });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return new GitHubHttpResult() { Result = HttpResultMy.NotFound };
        }
    }

    public class LocalMod
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string FullDirectoryPath { get; set; }
        public int? MinorVersion { get; set; } = null;
        public string KeeperRLVersion { get; set; }

        public bool? NeedUpdate { get; set; }

        public string PreviewPngPath { get; set; }

        public override string ToString()
        {
            return $"{Name} {MinorVersion} {NeedUpdate}";
        }

        internal void SetNeedUpdateFromYaklFileContent(byte[] content)
        {
            NeedUpdate = int.Parse(System.Text.UTF8Encoding.UTF8.GetString(content)) > MinorVersion;
        }
    }
}

//using System;
//using System.IO;
//using System.Net.Http;
//using System.Threading.Tasks;
//using Newtonsoft.Json.Linq;
//using System.Collections.Generic;

//class Program
//{
//    static async Task Main(string[] args)
//    {
//        string owner = "username"; // Replace with the repository owner's username
//        string repo = "repository"; // Replace with the repository name
//        string directoryPath = "path/to/directory"; // Replace with the path to the directory

//        await DownloadDirectoryAsync(owner, repo, directoryPath);
//    }

//    static async Task DownloadDirectoryAsync(string owner, string repo, string directoryPath)
//    {
//        using (HttpClient _Client = new HttpClient())
//        {
//            string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{directoryPath}";

//            // Set the User-Agent header
//            _Client.DefaultRequestHeaders.Add("User-Agent", "CSharp-Downloader");

//            try
//            {
//                // Fetch the contents of the directory
//                HttpResponseMessage response = await _Client.GetAsync(apiUrl);
//                response.EnsureSuccessStatusCode();
//                string jsonResponse = await response.Content.ReadAsStringAsync();

//                // Parse the JSON response
//                JArray items = JArray.Parse(jsonResponse);

//                List<Task> downloadTasks = new List<Task>();

//                foreach (var item in items)
//                {
//                    string fileName = item["name"].ToString();
//                    string downloadUrl = item["download_url"]?.ToString();

//                    // If it's a file, download it
//                    if (item["type"].ToString() == "file" && !string.IsNullOrEmpty(downloadUrl))
//                    {
//                        string localPath = Path.Combine(directoryPath, fileName);
//                        downloadTasks.Add(DownloadFileAsync(downloadUrl, localPath));
//                    }
//                }

//                // Wait for all downloads to complete
//                await Task.WhenAll(downloadTasks);

//                Console.WriteLine("All files downloaded successfully!");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error downloading directory: {ex.Message}");
//            }
//        }
//    }

//    static async Task DownloadFileAsync(string fileUrl, string localFilePath)
//    {
//        using (HttpClient _Client = new HttpClient())
//        {
//            // Make the GET request to download the file
//            HttpResponseMessage response = await _Client.GetAsync(fileUrl);
//            response.EnsureSuccessStatusCode();

//            // Ensure the directory exists
//            Directory.CreateDirectory(Path.GetDirectoryName(localFilePath));

//            // Write the file to local storage
//            using (var fileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
//            {
//                await response.Content.CopyToAsync(fileStream);
//            }

//            Console.WriteLine($"Downloaded {localFilePath}");
//        }
//    }
//}