namespace TimeReport.Web.Configuration;

public static class WebPaths
{
    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    public static void EnsurePrivateDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(fullPath);
            }
            else
            {
                Directory.CreateDirectory(fullPath, PrivateDirectoryMode);
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(fullPath, PrivateDirectoryMode);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    public static string UserDirectory(string dataDirectory, string userId)
    {
        if (!Guid.TryParseExact(userId, "N", out _))
        {
            throw new InvalidOperationException("The user identifier is invalid.");
        }

        return Path.Combine(Path.GetFullPath(dataDirectory), "users", userId);
    }

    public static void DeleteUserDirectory(string dataDirectory, string userId)
    {
        var usersRoot = Path.GetFullPath(Path.Combine(dataDirectory, "users"));
        var target = Path.GetFullPath(UserDirectory(dataDirectory, userId));
        var prefix = usersRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!target.StartsWith(prefix, comparison))
        {
            throw new InvalidOperationException("Refusing to delete a path outside the user data directory.");
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }

    public static bool UserDirectoryExists(string dataDirectory, string userId) =>
        Directory.Exists(UserDirectory(dataDirectory, userId));
}
