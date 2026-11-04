# Texto para a política de privacidade do site

A loja pede a política de privacidade do app, e ela é a do site: <https://scbr.space/privacidade>
(`src/app/privacidade/page.tsx` no repositório do site). Antes do envio à loja, ela precisa citar o app. Sugestão de
seção nova, no tom das outras (e a data "Atualizada em" muda junto):

> **O app Tradução SCBR**
>
> O app Tradução SCBR, na Microsoft Store, instala e atualiza a tradução do jogo no seu PC. Ele consulta o
> scbr.space para saber qual é a tradução publicada (a versão, o canal, o build e o sha256 do arquivo) e baixa o
> arquivo dela. O app não pede login, não manda dados pessoais e não tem telemetria: o pedido leva só o que qualquer
> acesso a um site leva, como o endereço IP e o nome do app.
>
> No seu PC, o app lê as pastas do Star Citizen (o `build_manifest.id` e o `user.cfg` de cada canal) e o log do RSI
> Launcher, só para achar onde o jogo está instalado e qual é a versão dele. Ele grava a tradução e a linha do idioma
> no `user.cfg` só dos canais em que você instalar. As configurações do app, o registro dele e a cópia da tradução
> baixada ficam na pasta do app no Windows e saem quando você o desinstala. Nada disso é enviado para a gente.

Se o site guardar algo dos acessos à API (logs com o IP, por exemplo), vale dizer por quanto tempo, como nas outras
seções.
