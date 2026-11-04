using Windows.ApplicationModel;

namespace TraducaoScbr;

/// <summary>
/// O início com o Windows, pelo StartupTask do pacote (TaskId "TraducaoScbrAoIniciar" no AppxManifest.xml). Só
/// existe no app instalado pelo MSIX; rodando sem pacote, fica indisponível.
/// </summary>
public static class InicioComWindows
{
    public const string TaskId = "TraducaoScbrAoIniciar";

    /// <summary>O estado atual, ou null sem pacote.</summary>
    public static async Task<StartupTaskState?> GetStateAsync()
    {
        if (!AppInfo.IsPackaged)
        {
            return null;
        }

        try
        {
            return (await StartupTask.GetAsync(TaskId)).State;
        }
        catch (Exception e) when (e is ArgumentException or System.Runtime.InteropServices.COMException)
        {
            Registro.Write($"StartupTask indisponível: {e.Message}");
            return null;
        }
    }

    /// <summary>Liga o início com o Windows. Se a pessoa desligou nas Configurações, só ela liga de novo por lá.</summary>
    public static async Task<StartupTaskState?> EnableAsync()
    {
        if (!AppInfo.IsPackaged)
        {
            return null;
        }

        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            return task.State == StartupTaskState.Disabled ? await task.RequestEnableAsync() : task.State;
        }
        catch (Exception e) when (e is ArgumentException or System.Runtime.InteropServices.COMException)
        {
            Registro.Write($"Não deu para ligar o StartupTask: {e.Message}");
            return null;
        }
    }

    public static async Task<StartupTaskState?> DisableAsync()
    {
        if (!AppInfo.IsPackaged)
        {
            return null;
        }

        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (task.State == StartupTaskState.Enabled)
            {
                task.Disable();
            }

            return task.State;
        }
        catch (Exception e) when (e is ArgumentException or System.Runtime.InteropServices.COMException)
        {
            Registro.Write($"Não deu para desligar o StartupTask: {e.Message}");
            return null;
        }
    }

    public static bool IsOn(StartupTaskState? state) => state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

    /// <summary>A explicação para a tela quando o Windows não deixa o app mudar a escolha.</summary>
    public static string? Note(StartupTaskState? state) => state switch
    {
        null when !AppInfo.IsPackaged => "Só no app instalado pela Microsoft Store.",
        StartupTaskState.DisabledByUser => "Desligado nas Configurações do Windows. Para ligar, use Configurações > Aplicativos > Inicialização.",
        StartupTaskState.DisabledByPolicy => "Desligado por uma política do Windows neste PC.",
        StartupTaskState.EnabledByPolicy => "Ligado por uma política do Windows neste PC.",
        _ => null,
    };
}
