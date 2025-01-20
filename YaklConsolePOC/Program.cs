using System.Net.Http.Headers;
using YAKL.Core;

namespace YaklConsolePOC
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var yaklService = new YAKLService();

            var mods = await yaklService.LoadLocalMods();

            await yaklService.UpdateMod(mods[2]);

            //await Task.CompletedTask;

            //await GetFileFromGitHub(owner, repo, path);
        }
    }
}