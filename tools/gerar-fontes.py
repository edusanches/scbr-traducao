"""Gera as fontes do app a partir das do site (src/app/fonts do repositório edusanches/scbr).

O site usa a Exo 2 (títulos e texto) e a Montserrat (menu, botões, rótulos e a marca) em woff2 variável, que o WPF
não lê. Este script tira de cada uma os pesos que o app usa, como TTF estático, com um nome de família por peso
("Montserrat SemiBold"), para o XAML escolher pelo nome. As duas são da SIL Open Font License 1.1: os arquivos OFL vão
junto.

Uso, da raiz deste repositório (precisa do fonttools e do brotli: pip install fonttools brotli):

    python tools/gerar-fontes.py C:\\dev\\scbr\\src\\app\\fonts
"""

import shutil
import sys
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

# (arquivo do site, família, pesos)
FONTES = [
    ("exo2-latin.woff2", "Exo 2", [(400, "Regular"), (600, "SemiBold")]),
    ("montserrat-latin.woff2", "Montserrat", [(600, "SemiBold"), (700, "Bold")]),
]
LICENCAS = ["OFL-exo2.txt", "OFL-montserrat.txt"]


def gerar(origem: Path, destino: Path) -> None:
    destino.mkdir(parents=True, exist_ok=True)
    for arquivo, familia, pesos in FONTES:
        for peso, nome_peso in pesos:
            fonte = TTFont(origem / arquivo)
            estatica = instantiateVariableFont(fonte, {"wght": peso})
            estatica.flavor = None
            # Um nome de família por peso, sem os nomes tipográficos (16 e 17): o WPF acha a fonte pelo nome exato.
            familia_peso = familia if nome_peso == "Regular" else f"{familia} {nome_peso}"
            nomes = estatica["name"]
            for registro in list(nomes.names):
                if registro.nameID in (16, 17, 21, 22):
                    nomes.removeNames(nameID=registro.nameID)
            nomes.setName(familia_peso, 1, 3, 1, 0x409)
            nomes.setName("Regular", 2, 3, 1, 0x409)
            nomes.setName(f"{familia_peso} Regular", 4, 3, 1, 0x409)
            nomes.setName(f"{familia_peso.replace(' ', '')}-Regular", 6, 3, 1, 0x409)
            nomes.setName(f"{familia_peso};Regular;TraducaoSCBR", 3, 3, 1, 0x409)
            estatica["OS/2"].usWeightClass = peso
            estatica["OS/2"].fsSelection = (estatica["OS/2"].fsSelection & ~0b1100001) | 0b1000000
            estatica["head"].macStyle = 0
            saida = destino / f"{familia.replace(' ', '')}-{nome_peso}.ttf"
            estatica.save(saida)
            print(f"{saida.name}: \"{familia_peso}\", peso {peso}, {saida.stat().st_size} bytes")
    for licenca in LICENCAS:
        shutil.copyfile(origem / licenca, destino / licenca)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("Uso: python tools/gerar-fontes.py <pasta src/app/fonts do site>")
    gerar(Path(sys.argv[1]), Path(__file__).resolve().parent.parent / "src" / "TraducaoScbr" / "Fontes")
