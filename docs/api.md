# A API que o app lê

O site (repositório `edusanches/scbr`, PR "Publicar", edusanches/scbr#163) publica a tradução que os tradutores
fecham na ferramenta `/tradutores`, com a versão, o canal e o build da rodada. A página pública dela é "Star Citizen
em português", em <https://scbr.space/traducao>. O app lê duas rotas, públicas e sem login.

## `GET https://scbr.space/api/traducao/atual`

A tradução publicada agora. CORS liberado (`*`) e `Cache-Control: public, max-age=300`.

```json
{
  "versao": "4.10.3",
  "canal": "PTU",
  "build": 13055118,
  "publicadaEm": "2026-11-04T15:20:00-03:00",
  "tamanho": 11327670,
  "sha256": "2269ca8b0abc23c590a462258c027cb918cf9e68543625f921e7093c13b23572",
  "arquivo": "https://scbr.space/api/traducao/arquivo/<id>",
  "pagina": "https://scbr.space/traducao"
}
```

| Campo | Tipo | No app |
|---|---|---|
| `versao` | texto, obrigatório | O título da tradução publicada ("4.10.3 PTU"). |
| `canal` | `LIVE`, `PTU`, `EPTU`, `TECH-PREVIEW`, `HOTFIX` ou `null` | Junto da versão. Só informativo: o mesmo arquivo serve todos os canais (a equipe mantém no fim dele as chaves que saíram do PTU e o LIVE ainda usa). |
| `build` | número (ou texto com número) ou `null` | Comparado com o `RequestedP4ChangeNum` de cada canal: o jogo num build maior gera o aviso "mais novo que a tradução". Com `null`, o app não compara. |
| `publicadaEm` | data ISO 8601, opcional | "Publicada hoje às 15:20". |
| `tamanho` | número, obrigatório | O tamanho do `global.ini` em bytes, sem compressão. O download precisa bater. Até 64 MB. |
| `sha256` | 64 caracteres hexadecimais, obrigatório | O hash do `global.ini`. O download precisa bater, senão nada é instalado. É também como o app sabe se um canal está em dia. |
| `arquivo` | URL, obrigatória | De onde baixar o `global.ini`. Pode ser relativa à API. Só `https` (o `http` vale só em `localhost`, para o mock). |
| `pagina` | URL, opcional | O link "Ver no scbr.space". |

**404** (`{ "erro": "Nenhuma tradução publicada ainda." }`, com `max-age=60`, ou qualquer 404) quer dizer que ainda
não há tradução publicada: o app mostra
"Ainda não há tradução publicada". Outros erros e respostas que o app não entende aparecem na janela, e ele tenta de
novo na próxima conferência, com a última publicação que conhecia.

Campos novos são ignorados. Tirar ou renomear um campo obrigatório quebra as versões do app que já estão nos PCs:
nesse caso, publique antes uma versão do app que aceite os dois formatos.

## `GET <arquivo>`

O `global.ini` (UTF-8 com BOM, como o jogo exige), `Content-Type: text/plain; charset=utf-8`,
`Content-Disposition: attachment; filename="global.ini"` e `Cache-Control: public, max-age=31536000, immutable`: o id
é de uma publicação só e o conteúdo nunca muda. Com `Accept-Encoding: gzip` vem em gzip (~2,5 MB em vez de 11 MB),
e o app descompacta antes de conferir o tamanho e o sha256 do arquivo aberto.

O ETag é por variante: `"<sha256>"` no arquivo aberto e `"<sha256>-gz"` no gzip, com 304 para o `If-None-Match`
igual. O app não usa o `If-None-Match`: ele só baixa quando o sha256 publicado muda.

Só as 5 publicações mais novas guardam o arquivo: uma mais antiga responde 410 (e um id que não existe, 404). O app
avisa que a tradução saiu do ar e baixa a atual na próxima conferência.

## Como o app chama

- `User-Agent: TraducaoSCBR/<versão> (+https://scbr.space/traducao)`. Nenhum dado da pessoa vai no pedido.
- Uma conferência ao abrir o app (45 s depois do login, quando ele inicia com o Windows), a cada 3 horas, ao voltar
  da suspensão, quando a internet volta e no "Conferir agora".
- O download só acontece quando o sha256 publicado não é o do arquivo que o app já tem guardado.
- Para testar sem o site: `tools/mock-api.cs` (veja o README).
