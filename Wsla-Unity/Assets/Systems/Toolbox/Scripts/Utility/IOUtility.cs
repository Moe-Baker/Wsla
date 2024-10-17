using System.IO;

namespace Toolbox
{
    public static class IOUtility
    {
        public static bool EnsureDirectoryExists(string path)
        {
            if (Path.HasExtension(path)) //Is File Path
            {
                if (File.Exists(path))
                    return false;

                path = Path.GetDirectoryName(path);

                Directory.CreateDirectory(path);
                return true;
            }
            else //Is Directory Path
            {
                if (Directory.Exists(path))
                    return false;

                Directory.CreateDirectory(path);
                return true;
            }
        }
    }
}