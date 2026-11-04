using System.Runtime.InteropServices;
using System.Security;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace TraducaoScbr;

/// <summary>
/// As notificações do Windows. No app instalado pelo pacote, é a notificação nativa (fica na central de
/// notificações e abre o app no clique). Sem pacote, o Windows não aceita a notificação nativa e o app usa o balão
/// da bandeja.
/// </summary>
public static class Notificacoes
{
    /// <summary>Mostra a notificação nativa. Falso quando não dá (sem pacote): aí vale o balão da bandeja.</summary>
    public static bool TryShow(string title, string text, string tag)
    {
        if (!AppInfo.IsPackaged)
        {
            return false;
        }

        try
        {
            var xml = new XmlDocument();
            xml.LoadXml(
                "<toast launch=\"abrir\"><visual><binding template=\"ToastGeneric\">"
                + $"<text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(text)}</text>"
                + "</binding></visual></toast>");
            var toast = new ToastNotification(xml)
            {
                // A mesma etiqueta troca a notificação anterior do mesmo assunto, em vez de empilhar.
                Tag = Tag(tag),
                Group = "avisos",
            };
            var notifier = ToastNotificationManager.CreateToastNotifier();
            if (notifier.Setting != NotificationSetting.Enabled)
            {
                // A pessoa (ou uma política) desligou as notificações: o app respeita, sem balão.
                Registro.Write($"Notificações desligadas no Windows: {notifier.Setting}");
                return true;
            }

            notifier.Show(toast);
            return true;
        }
        catch (Exception e) when (e is COMException or ArgumentException or UnauthorizedAccessException or InvalidOperationException)
        {
            Registro.Write($"Notificação nativa falhou: {e.Message}");
            return false;
        }
    }

    /// <summary>Quantas notificações do app estão na central (para conferir nos testes).</summary>
    public static int InCenter()
    {
        try
        {
            return AppInfo.IsPackaged ? ToastNotificationManager.History.GetHistory().Count : 0;
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            return 0;
        }
    }

    // A etiqueta do Windows aceita até 64 caracteres.
    private static string Tag(string key)
    {
        var tag = key.Split('|')[0];
        return tag.Length > 64 ? tag[..64] : tag;
    }
}
