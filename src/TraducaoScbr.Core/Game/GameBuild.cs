namespace TraducaoScbr.Core.Game;

/// <summary>
/// O build de um canal instalado, lido do build_manifest.id. O número que vale é o <see cref="ChangeNumber"/>
/// (RequestedP4ChangeNum); o Branch e a Version ficam só para o registro.
/// </summary>
public sealed record GameBuild(long ChangeNumber, string? Branch, string? Version, string? BuildDate);

/// <summary>Um canal do jogo achado no PC: LIVE, PTU, EPTU, HOTFIX, TECH-PREVIEW...</summary>
public sealed record GameChannel(string Name, string Path, GameBuild? Build);
