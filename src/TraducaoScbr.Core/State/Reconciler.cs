using TraducaoScbr.Core.Game;
using TraducaoScbr.Core.Translation;

namespace TraducaoScbr.Core.State;

public enum ChannelStatus
{
    /// <summary>Sem a tradução.</summary>
    NotInstalled,

    /// <summary>Com a tradução publicada agora, escolhida no user.cfg.</summary>
    UpToDate,

    /// <summary>Com outra tradução (antiga, ou a de outro lugar).</summary>
    Outdated,

    /// <summary>Com o arquivo da tradução publicada, mas o user.cfg não escolhe o português.</summary>
    Inactive,

    /// <summary>Com uma tradução, sem saber qual é a publicada (o site não respondeu).</summary>
    Unknown,
}

/// <summary>Um canal com o estado da tradução, como aparece na tela.</summary>
/// <param name="Wanted">Se a pessoa quer a tradução nele.</param>
/// <param name="InstalledPublication">A publicação instalada, quando o app sabe qual é.</param>
/// <param name="GameAhead">Se o jogo está num build mais novo que a tradução publicada.</param>
public sealed record ChannelState(
    GameChannel Channel,
    ChannelInspection Inspection,
    bool Wanted,
    ChannelStatus Status,
    PublicationRecord? InstalledPublication,
    bool GameAhead);

public enum NoticeKind
{
    Info,
    Warning,
}

/// <summary>Um aviso para a bandeja. A chave evita mostrar o mesmo aviso duas vezes.</summary>
public sealed record Notice(string Key, string Title, string Text, NoticeKind Kind);

/// <summary>As regras do app, sem disco nem rede: o estado de cada canal, o que atualizar sozinho e o que avisar.</summary>
public static class Reconciler
{
    public static ChannelState Evaluate(GameChannel channel, ChannelInspection inspection, ChannelRecord? record, Publication? current)
    {
        // Sem registro, quem já tem a tradução escolhida no user.cfg (pelos .cmd da equipe, por exemplo) quer a
        // tradução: o app passa a cuidar dela.
        var wanted = record?.Wanted ?? (inspection.Installed && inspection.LanguageActive);

        PublicationRecord? installed = null;
        if (inspection.Sha256 is { } sha)
        {
            if (current is not null && sha.Equals(current.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                installed = current.ToRecord();
            }
            else if (record?.Installed is { } previous && sha.Equals(previous.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                installed = previous;
            }
        }

        var status = (inspection.Installed, current) switch
        {
            (false, _) => ChannelStatus.NotInstalled,
            (true, null) => inspection.LanguageActive ? ChannelStatus.Unknown : ChannelStatus.Inactive,
            (true, { } pub) when !pub.Sha256.Equals(inspection.Sha256, StringComparison.OrdinalIgnoreCase) => ChannelStatus.Outdated,
            _ => inspection.LanguageActive ? ChannelStatus.UpToDate : ChannelStatus.Inactive,
        };

        var ahead = current?.Build is { } published && channel.Build is { } build && build.ChangeNumber > published;
        return new ChannelState(channel, inspection, wanted, status, installed, ahead);
    }

    /// <summary>
    /// Os canais que o app atualiza sozinho: os que a pessoa quer, com outra tradução ou sem o arquivo (o launcher
    /// pode apagar a pasta numa atualização). Um user.cfg mexido à mão fica como a pessoa deixou.
    /// </summary>
    public static IReadOnlyList<ChannelState> ToUpdate(IEnumerable<ChannelState> states, Publication? current, bool autoUpdate) =>
        current is null || !autoUpdate
            ? []
            : states.Where(s => s.Wanted && s.Status is ChannelStatus.Outdated or ChannelStatus.NotInstalled).ToList();

    /// <summary>Os avisos de uma conferência, sem os que já foram mostrados.</summary>
    /// <param name="previous">A publicação vista na conferência anterior (null na primeira).</param>
    /// <param name="updated">Os canais que o app acabou de atualizar sozinho.</param>
    /// <param name="failed">Os canais que o app tentou atualizar e não conseguiu, com o motivo.</param>
    public static IReadOnlyList<Notice> Notices(
        Publication? current,
        PublicationRecord? previous,
        IReadOnlyList<ChannelState> states,
        IReadOnlyList<GameChannel> updated,
        IReadOnlyList<(GameChannel Channel, string Error)> failed,
        IReadOnlyCollection<string> alreadyNotified)
    {
        var notices = new List<Notice>();
        if (current is not null)
        {
            if (updated.Count > 0)
            {
                var names = Names(updated);
                notices.Add(new Notice(
                    $"atualizada|{current.Sha256}|{string.Join(",", updated.Select(c => c.Path.ToUpperInvariant()).Order())}",
                    "Tradução atualizada",
                    $"A tradução {current.Description} foi instalada {Em(names)}.",
                    NoticeKind.Info));
            }

            var isNew = previous is not null && !previous.Sha256.Equals(current.Sha256, StringComparison.OrdinalIgnoreCase);
            var waiting = states
                .Where(s => s.Wanted && s.Status == ChannelStatus.Outdated && !failed.Any(f => SamePath(f.Channel, s.Channel)))
                .Select(s => s.Channel)
                .ToList();
            if (isNew && waiting.Count > 0)
            {
                notices.Add(new Notice(
                    $"nova|{current.Sha256}",
                    "Saiu tradução nova",
                    $"A tradução {current.Description} está pronta. Abra o app para atualizar {Os(Names(waiting))}.",
                    NoticeKind.Info));
            }

            foreach (var (channel, error) in failed)
            {
                notices.Add(new Notice(
                    $"falha|{channel.Path.ToUpperInvariant()}|{current.Sha256}",
                    $"Não deu para atualizar o {channel.Name}",
                    error,
                    NoticeKind.Warning));
            }

            foreach (var state in states.Where(s => s.Wanted && s.GameAhead && s.Inspection.Installed))
            {
                var build = state.Channel.Build!.ChangeNumber;
                notices.Add(new Notice(
                    $"jogo-mais-novo|{state.Channel.Path.ToUpperInvariant()}|{build}|{current.Sha256}",
                    $"O {state.Channel.Name} está mais novo que a tradução",
                    $"O jogo está no build {build} e a tradução publicada é do {current.Build}. Os textos novos aparecem em inglês até a próxima tradução.",
                    NoticeKind.Warning));
            }
        }

        return notices.Where(n => !alreadyNotified.Contains(n.Key)).ToList();
    }

    /// <summary>"LIVE", "LIVE e PTU", "LIVE, PTU e EPTU".</summary>
    public static string Names(IReadOnlyList<GameChannel> channels)
    {
        var names = channels.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return names.Count switch
        {
            0 => "",
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " e " + names[^1],
        };
    }

    private static string Em(string names) => names.Contains(" e ", StringComparison.Ordinal) ? $"no {names.Replace(", ", ", no ").Replace(" e ", " e no ")}" : $"no {names}";

    private static string Os(string names) => names.Contains(" e ", StringComparison.Ordinal) ? $"o {names.Replace(", ", ", o ").Replace(" e ", " e o ")}" : $"o {names}";

    private static bool SamePath(GameChannel a, GameChannel b) => string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
}
