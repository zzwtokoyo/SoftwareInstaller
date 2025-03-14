namespace SoftwareInstaller.RunTask
{
    public class InstallerTask
    {
        public string FilePath { get; set; }
        public string Arguments { get; set; }

        public InstallerTask(string filePath)
        {
            FilePath = filePath;
            Arguments = "/silent";
        }
    }
}
