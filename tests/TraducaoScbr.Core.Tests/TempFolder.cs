using System.Text;

namespace TraducaoScbr.Core.Tests;

/// <summary>Uma pasta temporária com um jogo de mentira, apagada no fim do teste.</summary>
internal sealed class TempFolder : IDisposable
{
    public const string LiveManifest = """
        {
            "Data": {
                "Branch": "sc-alpha-4.10.2",
                "BuildDateStamp": "Mon Oct 12 2026",
                "BuildId": "2cae0dfc-a941-4dcc-b82a-543b08b52d97",
                "BuildTimeStamp": "02:20:01 PM CST",
                "Config": "shipping",
                "Platform": "pc",
                "RequestedP4ChangeNum": "12913470",
                "Shelved_Change": "",
                "Tag": "public",
                "Version": "4.10.197.2878"
            }
        }
        """;

    public const string PtuManifest = """
        {
            "Data": {
                "Branch": "sc-alpha-4.10.0",
                "BuildDateStamp": "Tue Nov 03 2026",
                "RequestedP4ChangeNum": "13093654",
                "Tag": "public",
                "Version": "4.10.199.51990"
            }
        }
        """;

    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "traducao-scbr-testes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    /// <summary>Um canal com o Data.p4k vazio e, se vier, o build_manifest.id.</summary>
    public string Channel(string library, string name, string? manifest = null)
    {
        var folder = Combine(library, name);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(System.IO.Path.Combine(folder, "Data.p4k"), []);
        if (manifest is not null)
        {
            File.WriteAllText(System.IO.Path.Combine(folder, "build_manifest.id"), manifest);
        }

        return folder;
    }

    public string Write(string relative, string content, bool bom = false)
    {
        var path = Combine(relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var body = Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(path, bom ? [0xEF, 0xBB, 0xBF, .. body] : body);
        return path;
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
