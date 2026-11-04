using System.IO;
using System.Reflection;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace TraducaoScbr;

/// <summary>A versão do app, se ele roda pelo pacote da loja e as opções de linha de comando (para testes).</summary>
public static class AppInfo
{
    public const string Name = "Tradução SCBR";

    public static string Version { get; } = ReadVersion();

    /// <summary>Se o app roda instalado pelo MSIX (loja ou pacote de teste), com a identidade do pacote.</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    public static string DefaultDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TraducaoSCBR");

    /// <summary>Se o Windows abriu o app no login, pelo StartupTask do pacote.</summary>
    public static bool LaunchedAtLogin()
    {
        if (!IsPackaged)
        {
            return false;
        }

        try
        {
            return AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask;
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ReadVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "?" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static bool DetectPackaged()
    {
        try
        {
            return Package.Current?.Id is not null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }
}

/// <summary>
/// As opções de linha de comando, para testar o app sem mexer no jogo de verdade:
/// <c>--api http://localhost:5199</c> (o mock), <c>--dados &lt;pasta&gt;</c> (outra pasta de configurações),
/// <c>--pastas "&lt;pasta1&gt;;&lt;pasta2&gt;"</c> (só estas pastas, sem procurar o jogo),
/// <c>--bandeja</c> (abre sem a janela), <c>--captura &lt;arquivo.png&gt;</c> (grava a janela numa imagem e fecha) e
/// <c>--aba traducao|opcoes|ajuda</c> e <c>--canal PTU</c> (a aba e o canal que abrem, para as capturas).
/// A variável de ambiente TRADUCAO_SCBR_API também troca o endereço da API.
/// </summary>
public sealed record AppOptions(
    Uri? Api, string? DataFolder, IReadOnlyList<string>? OnlyFolders, bool Tray, string? Capture, string? Tab = null, string? Channel = null)
{
    public static AppOptions Parse(IReadOnlyList<string> args)
    {
        Uri? api = null;
        string? data = null, capture = null, tab = null, channel = null;
        List<string>? folders = null;
        var tray = false;
        for (var i = 0; i < args.Count; i++)
        {
            string? Next() => i + 1 < args.Count ? args[++i] : null;
            switch (args[i])
            {
                case "--api" when Next() is { } value && Uri.TryCreate(value, UriKind.Absolute, out var uri):
                    api = uri;
                    break;
                case "--dados":
                    data = Next();
                    break;
                case "--pastas":
                    folders = (Next() ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    break;
                case "--bandeja":
                    tray = true;
                    break;
                case "--captura":
                    capture = Next();
                    break;
                case "--aba":
                    tab = Next();
                    break;
                case "--canal":
                    channel = Next();
                    break;
            }
        }

        if (api is null && Environment.GetEnvironmentVariable("TRADUCAO_SCBR_API") is { Length: > 0 } env
            && Uri.TryCreate(env, UriKind.Absolute, out var fromEnv))
        {
            api = fromEnv;
        }

        return new AppOptions(api, data, folders, tray, capture, tab, channel);
    }
}
