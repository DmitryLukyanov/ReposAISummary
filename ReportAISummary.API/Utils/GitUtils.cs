using LibGit2Sharp;

namespace ReportAISummary.API.Utils
{
    public sealed class GitUtils
    {
        public static void PrepareServices(IServiceCollection services)
        {
            services.AddScoped<GitUtils>();
        }

        public string CloneRepo(string repo, string workingPath)
        {
            var effectiveWorkingDirectory = Path.GetFullPath(
                Path.Combine(
                    [
                    workingPath,
                    .. new Uri(repo).Segments[1..]
                    ]));

            var cloneOptions = new CloneOptions()
            {
                BranchName = "master", // move to config
                Checkout = true,
            };

            //if (!string.IsNullOrEmpty(passwordOrPat))
            //{
            //    cloneOptions.CredentialsProvider = (_url, _user, _types) =>
            //        new UsernamePasswordCredentials { Username = username ?? "git", Password = passwordOrPat };
            //}

            var dotGitDirectory = Repository.Clone(repo, workdirPath: effectiveWorkingDirectory, cloneOptions);

            string[] segments = dotGitDirectory.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            return Path.Combine(segments[..^1]); // path without .git
        }
    }
}
