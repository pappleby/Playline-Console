namespace PlaylineConsole
{
    using System.IO;
    using Yarn.Compiler;

    public static class CreateProjectFileCommand
    {
        public static void CreateProjFile(string projectName, bool playlineExclusion)
        {
            Project proj = new Project();

            if (playlineExclusion)
            {
                proj.ExcludeFilePatterns = new[] { "**/builds/*" };
            }

            var path = $"./{projectName}.yarnproject";

            if (File.Exists(path))
            {
                Log.Error($"Unable to create a new project file as one already exists at \"{path}\"");
                return;
            }

            proj.SaveToFile(path);
            Log.Info($"new project file created at {path}");
        }
    }
}
