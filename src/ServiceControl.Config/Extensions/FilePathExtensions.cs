namespace ServiceControl.Config.Extensions
{
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using Path = System.IO.Path;

    public static class FilePathExtensions
    {
        static bool TryGetPathRoot(string path, out string root)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                root = string.Empty;
                return false;
            }

            try
            {
                root = Path.GetPathRoot(path);
                return true;
            }
            catch (System.ArgumentException)
            {
                root = string.Empty;
                return false;
            }
        }

        //Removes trailing spaces and periods as well as invalid file name characters 
        //The following document lists all the conventions around file/path naming
        //https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file#naming-conventions
        static string RemoveInvalidDirectoryNameCharacters(string name)
        {
            if (name == null)
            {
                return name;
            }

            name = Regex.Replace(name, @"//+", @"\");

            name = Regex.Replace(name, @"\\+", @"\");

            if (TryGetPathRoot(name, out string root))
            {
                root = Path.GetPathRoot(name);

                name = name.Remove(0, root.Length);
            }

            var segments = name.Split('\\');

            var nameBuilder = new StringBuilder();

            if (root != string.Empty)
            {
                nameBuilder.Append(root);
            }

            foreach (var segment in segments)
            {
                foreach (char character in segment)
                {
                    if (Path.GetInvalidFileNameChars().Contains(character))
                    {
                        continue;
                    }

                    nameBuilder.Append(character);
                }

                nameBuilder.Append("\\");
            }

            return nameBuilder.ToString().TrimEnd('\\');
        }

        public static string SanitizeFilePath(this string name)
        {
            name = RemoveInvalidDirectoryNameCharacters(name);

            return name;
        }
    }
}
