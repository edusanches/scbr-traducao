# Envio à Microsoft Store, passo a passo

Para o Eduardo. **Nada vai para a loja sem o OK dele.** O cadastro no Partner Center é dele, de pessoa física e sem
taxa. Os textos prontos para a listagem estão no fim.

## Antes de enviar

1. **O site no ar com a API** (PR "Publicar", edusanches/scbr#163) e **a primeira tradução publicada** pela
   ferramenta. Sem publicação, o app mostra "Ainda não há tradução publicada" e não tem o que instalar.
2. **A política de privacidade citando o app:** [docs/privacidade.md](privacidade.md) tem o texto sugerido para a
   `/privacidade` do site. A loja pede o endereço dela.
3. **A troca combinada com a equipe de tradução**, que distribui hoje pelos `.cmd` do sc-tradutor-ptbr: o app adota
   a tradução que eles instalaram e troca pela publicada no site.

## 1. Conta de desenvolvedor (uma vez)

1. Abra <https://storedeveloper.microsoft.com> e clique em **Get started for free**. Só essa entrada é grátis; pelo
   Partner Center direto, aparece o cadastro antigo, com taxa.
2. Escolha **Individual developer** e entre com a **conta Microsoft pessoal** (a conta de trabalho não serve para
   pessoa física).
3. Faça a **verificação de identidade** com um documento oficial com foto e uma selfie, de preferência pelo celular,
   com boa luz e o documento original.
4. Confira os dados do perfil. O **nome do editor** aparece na loja embaixo do nome do app: se der para escolher, use
   algo como "Comunidade SCBR", **sem "Star Citizen"**; numa conta de pessoa física ele pode vir do seu nome.
5. Clique em **Go to Partner Center dashboard** e escolha a mesma conta. A página "Apps & Games" pode levar uns
   minutos para aparecer (ou abra <https://aka.ms/submitwindowsapp>).

## 2. Reservar o nome

Partner Center > **Apps e jogos** > **Novo produto** > **App MSIX ou PWA** > nome **Tradução SCBR** > verificar a
disponibilidade > **Reservar nome do produto**.

## 3. Copiar a identidade do produto

No app reservado, **Gerenciamento do produto > Identidade do produto**. Copie três valores (não são segredo):

| No Partner Center | No build | Exemplo |
|---|---|---|
| `Package/Identity/Name` | `-IdentityName` / variável `LOJA_IDENTITY_NAME` | `12345ComunidadeSCBR.TraducaoSCBR` |
| `Package/Identity/Publisher` | `-Publisher` / `LOJA_PUBLISHER` | `CN=00000000-0000-0000-0000-000000000000` |
| `Package/Properties/PublisherDisplayName` | `-PublisherDisplayName` / `LOJA_PUBLISHER_DISPLAY_NAME` | `Comunidade SCBR` |

## 4. Gerar o pacote com a identidade da loja

**Pelo CI (recomendado):** no GitHub, **Settings > Secrets and variables > Actions > Variables**, crie as três
variáveis da tabela. Rode o workflow **CI** na `main` (Actions > CI > Run workflow) e baixe o artefato
`TraducaoSCBR-msix`, que tem o `TraducaoSCBR_<versão>_x64.msix`.

**Ou no PC,** com o SDK do .NET 10:

```powershell
powershell -File packaging/build-msix.ps1 -IdentityName "<Name>" -Publisher "<Publisher>" -PublisherDisplayName "<PublisherDisplayName>"
```

O pacote sai sem assinatura, como a loja quer: ela assina. Cada envio precisa de uma versão maior que a anterior
(`<Version>` em `Directory.Build.props`; o último número do MSIX fica sempre 0).

## 5. Criar o envio

No app, **Iniciar o envio** e preencha cada seção:

1. **Preços e disponibilidade:** gratuito; mercados, todos (para quem joga de fora do Brasil também); visibilidade
   pública. Em "Publicação", escolha publicar manualmente se quiser escolher a hora depois da certificação.
2. **Propriedades:**
   - Categoria: **Utilitários e ferramentas**.
   - Política de privacidade: `https://scbr.space/privacidade`.
   - Site: `https://scbr.space/traducao`.
   - Contato de suporte: o convite do Discord da SCBR ou um e-mail da comunidade.
   - "Este produto acessa, coleta ou transmite informações pessoais?": **Não** (o app não manda dados da pessoa).
3. **Classificações etárias:** responda o questionário (IARC). O app não tem violência, conversa entre pessoas,
   compras nem localização: sai com a classificação livre.
4. **Pacotes:** suba o `.msix` (família de dispositivos: Desktop). O Partner Center confere a identidade e a versão.
5. **Listagens da loja:** só **Português (Brasil)**, com os textos de baixo. Capturas de tela (1920 × 1080), em
   `docs/loja`: [`captura-1.png`](loja/captura-1.png) (em dia), [`captura-2.png`](loja/captura-2.png) (o PTU num build
   mais novo que a tradução), [`captura-3.png`](loja/captura-3.png) (as opções) e
   [`captura-4.png`](loja/captura-4.png) (a primeira vez).
   Logotipo 1:1, se pedir: [`docs/loja/logo-1080.png`](loja/logo-1080.png) ou [`logo-300.png`](loja/logo-300.png).
6. **Opções de envio:**
   - Notas para a certificação: o texto de baixo (os testadores não têm o Star Citizen instalado).
   - Funcionalidade restrita **runFullTrust**: a justificativa de baixo.
7. **Enviar para a Store.** A certificação leva de algumas horas a uns três dias úteis. O Partner Center avisa por
   e-mail; com publicação manual, clique em **Publicar agora** quando quiser.

## 6. Atualizações

Aumente a `<Version>` em `Directory.Build.props` (por exemplo, 1.0.1) num PR, gere o pacote pelo CI e, no Partner
Center, **Atualizar** o envio, suba o novo `.msix` na seção Pacotes e envie. A loja atualiza o app nos PCs sozinha e
baixa só o que mudou.

## Textos para a listagem

**Nome:** Tradução SCBR

**Descrição:**

> A Tradução SCBR instala e mantém em dia a tradução para o português do Brasil feita pela comunidade para Star
> Citizen.
>
> O app acha o jogo no seu PC (LIVE, PTU e os outros canais), mostra o build de cada canal e instala com um clique a
> tradução publicada pela equipe de tradução da comunidade Star Citizen Brasil (scbr.space). Quando sai uma tradução
> nova, ele atualiza sozinho e avisa. Quando o jogo recebe uma atualização mais nova que a tradução, ele avisa que os
> textos novos podem aparecer em inglês até a próxima tradução.
>
> Para tirar a tradução de um canal, é um clique também, e o jogo volta ao inglês.
>
> O app fica na bandeja, perto do relógio, e pode iniciar com o Windows. Ele não pede login, não coleta dados pessoais
> e só se conecta ao scbr.space, para conferir e baixar a tradução.
>
> Antes de desinstalar o app, remova a tradução dos canais pelo próprio app: o Windows não deixa o app limpar a pasta
> do jogo depois de desinstalado.
>
> A Tradução SCBR é um projeto de fãs, sem vínculo com a Cloud Imperium Games ou a Roberts Space Industries. Star
> Citizen® é marca registrada da Cloud Imperium Rights LLC.

**Recursos do produto:**

- Acha o Star Citizen sozinho, em qualquer disco
- Instala a tradução PT-BR em cada canal com um clique
- Atualiza sozinho quando sai tradução nova
- Avisa quando o jogo está mais novo que a tradução
- Remove a tradução de um canal com um clique
- Fica na bandeja e pode iniciar com o Windows
- Sem login, sem anúncios e sem coleta de dados

**Palavras-chave:** tradução, português, PT-BR, Star Citizen, localização, Brasil, legendas

**O que há de novo nesta versão (1.0.0):** Primeira versão.

**Direitos autorais e marcas:** © 2026 comunidade Star Citizen Brasil (scbr.space). Star Citizen® é marca
registrada da Cloud Imperium Rights LLC; este app não tem vínculo com a Cloud Imperium Games.

**Notas para a certificação:**

> The app installs a community Brazilian Portuguese translation (a text file, global.ini) into the folder of the PC
> game Star Citizen. Without the game installed, the app shows "Não achei o Star Citizen neste PC" and a button to
> pick the game folder. To test without the game: create C:\Teste\StarCitizen\LIVE and put an empty file named
> Data.p4k in it; in the app, click "Adicionar pasta do jogo…" (or "Escolher a pasta do jogo…") and pick
> C:\Teste\StarCitizen. The LIVE channel appears; "Instalar" writes
> C:\Teste\StarCitizen\LIVE\data\Localization\portuguese_(brazil)\global.ini and a user.cfg with
> "g_language = portuguese_(brazil)"; "Remover" undoes it. The translation is downloaded from
> https://scbr.space/api/traducao/atual. The app lives in the notification area (tray) and, on first run, enables its
> startup task to check for new translations at sign-in; the user can turn it off in the app window. No account or
> login is needed.

**Justificativa do runFullTrust:**

> Tradução SCBR is a desktop app (WPF, .NET 10). It needs runFullTrust to write the translation file into the Star
> Citizen installation folder chosen by the user (by default C:\Program Files\Roberts Space Industries\StarCitizen\
> <channel>), which is outside the app package, to edit that game's user.cfg, and to show an icon in the Windows
> notification area.
