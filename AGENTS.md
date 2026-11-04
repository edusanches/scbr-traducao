# Tradução SCBR: convenções para agentes

O app de bandeja do Windows, na Microsoft Store, que instala, mantém em dia e remove a tradução PT-BR da comunidade
para Star Citizen. A tradução vem da ferramenta `/tradutores` do site da Star Citizen Brasil (repositório privado
`edusanches/scbr`), pela API descrita em `docs/api.md`. O contexto do projeto está em
`C:\dev\organização\CONTEXTO-SCBR.md` e `C:\dev\organização\PLANO-SCBR.md`.

## Princípios

- Interface toda em **PT-BR**. Termos do jogo podem ficar em inglês (LIVE, PTU, build).
- **A pasta do jogo é da pessoa:** o app só mexe em `<canal>\data\Localization\portuguese_(brazil)` e na linha
  `g_language` do `user.cfg` (sempre com o `.bak` antes). Nunca na `g_languageAudio` nem em outras linhas.
- **Nunca teste no jogo de verdade.** Use `--pastas` com um jogo de mentira e `--dados` com uma pasta de teste (veja o
  README). Um teste com o pacote MSIX registrado acha o jogo real: dê aos canais de mentira nomes que o PC não tem
  (HOTFIX, TECH-PREVIEW) e só clique neles.
- Nada de telemetria: o app só fala com o scbr.space.

## Stack

.NET 10 (LTS), C# com `Nullable` e `TreatWarningsAsErrors`. O núcleo (`src/TraducaoScbr.Core`) não tem tela e é
testado com xUnit (`tests/TraducaoScbr.Core.Tests`). O app (`src/TraducaoScbr`) é WPF no estilo do launcher do jogo
(pedido do Eduardo): barra do topo própria (`WindowChrome`), botões e cartões com o canto cortado (`Chanfro`), fundo
espacial desenhado em código (`FundoEspacial`), fonte Bahnschrift e o azul da SCBR (`Tema.xaml`). Nada de arte, logo
ou fonte do jogo: o símbolo é o da Star Citizen Brasil. A bandeja é o `NotifyIcon` do WinForms. Empacotado em MSIX
(`packaging/`), com `runFullTrust` e o StartupTask.

## Comandos

```bash
dotnet build TraducaoScbr.slnx
dotnet test TraducaoScbr.slnx
dotnet run tools/mock-api.cs -- <pasta>            # a API de mentira
powershell -File packaging/build-msix.ps1          # o MSIX (artifacts/)
powershell -File packaging/build-msix.ps1 -SoLayout  # só a pasta, para Add-AppxPackage -Register
dotnet run tools/gerar-icones.cs                   # os ícones, do símbolo da SCBR
```

## Código

- Identificadores em inglês; comentários, mensagens e textos da tela em português.
- Regra nova vai no núcleo, com teste. A tela só mostra o estado do `TranslationService`.
- Os textos de erro já vêm prontos para a tela (`ApiException`, `InstallException`).
- Scripts `.ps1` com acento são gravados em UTF-8 **com BOM** (o PowerShell 5.1 lê sem BOM como ANSI).
- Mudou o contrato da API? O app aceita o formato antigo e o novo até as versões antigas saírem de circulação.

## Git e PRs

- Branch a partir da `main`; nunca commit direto na `main`.
- PRs pequenos, em português, com rótulo de tipo (`bug`, `enhancement`, `documentation`, `infra`) e um roteiro de
  teste para o Eduardo. O merge é sempre do Eduardo.
- O CI (GitHub Actions, Windows) compila, testa e gera o MSIX como artefato.
- **Nada vai para a loja sem o OK do Eduardo.** O passo a passo está em `docs/loja.md`.
