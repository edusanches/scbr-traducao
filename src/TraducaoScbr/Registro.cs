using System.Globalization;
using System.IO;

namespace TraducaoScbr;

/// <summary>
/// O registro do app (registro.txt na pasta de dados), para entender um problema com a pessoa. Fica com ~512 KB:
/// o anterior vira registro.old.txt.
/// </summary>
public static class Registro
{
    private static readonly Lock Gate = new();
    private static string? _path;

    public static void Start(string dataFolder)
    {
        Directory.CreateDirectory(dataFolder);
        _path = Path.Combine(dataFolder, "registro.txt");
        Write($"{AppInfo.Name} {AppInfo.Version} aberto ({(AppInfo.IsPackaged ? "pacote" : "sem pacote")})");
    }

    public static void Write(string message)
    {
        if (_path is null)
        {
            return;
        }

        lock (Gate)
        {
            try
            {
                var info = new FileInfo(_path);
                if (info.Exists && info.Length > 512 * 1024)
                {
                    File.Move(_path, Path.ChangeExtension(_path, ".old.txt"), overwrite: true);
                }

                File.AppendAllText(_path, $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
