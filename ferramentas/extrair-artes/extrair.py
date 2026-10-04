# -*- coding: utf-8 -*-
"""
Extrai a arte do jogo em HTML como imagens PNG com fundo transparente.

Como funciona: a arte do HTML e feita de CSS. Para cada peca, o script monta
uma pagina com o CSS original e o cenario original, esconde tudo menos aquela
peca, tira uma foto com um navegador sem janela e recorta a imagem no
contorno da peca. Como usa o CSS e o cenario originais, a peca sai igual ao
que aparecia no jogo.

Alem das imagens, grava manifesto.json com a posicao de cada peca dentro do
cenario de 900x530. A Unity usa isso para remontar a cena no mesmo lugar.

Uso:  python extrair.py
"""
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

from PIL import Image

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.normpath(os.path.join(AQUI, "..", ".."))
HTML_ORIGINAL = os.path.join(RAIZ, "index.html")
HTML_NOVAS = os.path.join(AQUI, "artes-novas.html")
SAIDA = os.path.join(RAIZ, "unity", "Assets", "Art", "Cenario")

LARGURA, ALTURA = 900, 530      # tamanho do cenario no jogo HTML
ESCALA = 2                      # foto em 2x para a arte ficar nitida na Unity

CENARIOS = ["supermercado", "eletronicos", "banco", "online", "telefonia"]

# Textos que sao do ambiente, nao do caso: no HTML eles eram fixos por cenario,
# entao entram na imagem. Copiados do objeto SCENARIOS do index.html.
TEXTOS = {
    "supermercado": ("SUPERMERCADO", "FISCALIZAÇÃO DE PREÇOS", "CORREDOR DE PRODUTOS"),
    "eletronicos": ("ELETRÔNICOS", "GARANTIA E ASSISTÊNCIA", "MOSTRUÁRIO DA LOJA"),
    "banco": ("BANCO", "CRÉDITO RESPONSÁVEL", "TERMINAIS BANCÁRIOS"),
    "online": ("COMPRAS ONLINE", "OFERTAS E ENTREGAS", "CENTRO DE ENTREGAS"),
    "telefonia": ("TELEFONIA", "PLANOS E SERVIÇOS", "MOSTRUÁRIO DE PLANOS"),
}

# Cenario original, copiado do index.html. Textos variaveis ficam vazios:
# na Unity eles viram texto por cima da imagem.
CENARIO_HTML = """
<div class="office" id="office" {cenario_attr}>
  <div class="wall-pattern"></div>
  <div class="room-light"></div>
  <div class="window">
    <div class="sun"></div><div class="cloud"></div>
    <div class="city"><i class="tower tower-a"></i><i class="tower tower-b"></i><i class="tower tower-c"></i><i class="tower tower-d"></i><i class="tower tower-e"></i></div>
  </div>
  <div class="scene-decor" data-label="{decor}"><i></i><i></i><i></i><i></i></div>
  <div class="procon-sign"><span>{rotulo}</span><small>{subtitulo}</small></div>
  <div class="notice-board"><i></i><i></i></div>
  <div class="wall-clock"></div>
  <div class="floor"></div>
  <div class="file-cabinet"><i></i><i></i><i></i></div>
  <div class="pixel-plant"></div>
  <div class="character consumer owner" id="owner" data-variant="{variante}">
    <span class="character-shadow"></span>
    <div class="head"></div><div class="hair"></div>
    <span class="eye left"></span><span class="eye right"></span><span class="mouth"></span>
    <div class="body"><span class="collar"></span></div><div class="legs"></div>
  </div>
  <div class="speech-ping" id="speechPing"></div>
  <div class="character agent inspector" id="inspector">
    <span class="character-shadow"></span>
    <div class="head"></div><div class="hair"></div>
    <span class="eye left"></span><span class="eye right"></span><span class="mouth"></span>
    <div class="body"><span class="collar"></span></div><span class="agent-badge"></span><div class="legs"></div>
  </div>
  <div class="counter"></div><div class="counter-top"></div>
  <div class="computer"></div><div class="papers"></div>
  <div class="keyboard"></div><div class="coffee-mug"></div>
</div>
"""

# nome do arquivo, seletor da peca, cenario, variante do personagem
# O que aparecia durante o jogo. Com um cenario ativo o HTML esconde janela,
# mural, planta, computador, teclado, caneca e papeis (linhas 1448 e 1544).
PECAS_ORIGINAIS = [
    ("relogio", ".wall-clock", "supermercado", "0"),
    ("piso", ".floor", "supermercado", "0"),
    ("tampo", ".counter-top", "supermercado", "0"),
    ("fiscal", "#inspector", "supermercado", "0"),
]
for v in ("0", "1", "2", "3"):
    PECAS_ORIGINAIS.append(("responsavel_" + v, "#owner", "supermercado", v))
for c in CENARIOS:
    PECAS_ORIGINAIS.append(("parede_" + c, ".wall-pattern", c, "0"))
    PECAS_ORIGINAIS.append(("decor_" + c, ".scene-decor", c, "0"))
    PECAS_ORIGINAIS.append(("placa_" + c, ".procon-sign", c, "0"))
    PECAS_ORIGINAIS.append(("balcao_" + c, ".counter", c, "0"))

# Pecas que existem no CSS mas o jogo nao mostrava durante a partida.
# Sao fotografadas sem cenario ativo, e forcadas a aparecer.
PECAS_EXTRAS = [
    ("extra_janela", ".window"),
    ("extra_mural", ".notice-board"),
    ("extra_planta", ".pixel-plant"),
    ("extra_computador", ".computer"),
    ("extra_teclado", ".keyboard"),
    ("extra_caneca", ".coffee-mug"),
    ("extra_papeis", ".papers"),
    ("extra_arquivo", ".file-cabinet"),
    ("extra_luz", ".room-light"),
]

# Pecas do HTML adaptadas para receber texto do caso por cima.
# nome, seletor, CSS que limpa o que o HTML escrevia dentro dela.
PECAS_LIMPAS = [
    ("terminal", ".computer", ".computer::before { content: '' !important; }"),
]

# Pecas desenhadas no artes-novas.html, no mesmo estilo do jogo.
PECAS_NOVAS = ["etiqueta", "cartaz", "balanca", "caixa", "arroz", "oleo", "embalagem", "documento",
               "celular", "fone", "notebook", "cartao", "balao"]

ISOLAMENTO = """
*, *::before, *::after { animation: none !important; transition: none !important; }
html, body { margin: 0 !important; padding: 0 !important; background: transparent !important; }
.office {
  position: relative !important; width: %dpx !important; height: %dpx !important; min-height: 0 !important;
  margin: 0 !important; background: transparent !important; box-shadow: none !important;
  border-color: transparent !important; overflow: visible !important;
}
.office::before, .office::after { display: none !important; }
.office > *:not(%s) { visibility: hidden !important; }
/* Sem letras dentro das imagens: texto pequeno reduzido vira borrao. A Unity
   escreve placa, balcao e gondola por cima, nitido. A placa mantem o texto
   invisivel so para guardar o tamanho original. */
.counter::before { display: none !important; }
.scene-decor::after { display: none !important; }
.procon-sign span, .procon-sign small { color: transparent !important; text-shadow: none !important; }
"""


def achar_navegador():
    candidatos = [
        r"C:\Program Files\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    ]
    for caminho in candidatos:
        if os.path.exists(caminho):
            return caminho
    sys.exit("Nenhum navegador encontrado para tirar as fotos.")


def css_original():
    texto = io.open(HTML_ORIGINAL, encoding="utf-8").read()
    blocos = re.findall(r"<style>(.*?)</style>", texto, re.S)
    if not blocos:
        sys.exit("Nao achei o CSS dentro do index.html.")
    return "\n".join(blocos)


def fotografar(navegador, html, pasta_temp, nome):
    pagina = os.path.join(pasta_temp, nome + ".html")
    foto = os.path.join(pasta_temp, nome + ".png")
    io.open(pagina, "w", encoding="utf-8").write(html)
    subprocess.run([
        navegador, "--headless=new", "--disable-gpu", "--hide-scrollbars",
        "--default-background-color=00000000",
        "--force-device-scale-factor=%d" % ESCALA,
        "--window-size=%d,%d" % (LARGURA, ALTURA),
        "--screenshot=" + foto,
        "file:///" + pagina.replace("\\", "/"),
    ], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=60)
    return foto


def recortar(foto, destino):
    """Corta no contorno da peca. Devolve a caixa em pixels do cenario de 900x530."""
    imagem = Image.open(foto).convert("RGBA")
    caixa = imagem.getchannel("A").getbbox()
    if caixa is None:
        return None
    imagem.crop(caixa).save(destino)
    x0, y0, x1, y1 = caixa
    return {"x": x0 / ESCALA, "y": y0 / ESCALA, "w": (x1 - x0) / ESCALA, "h": (y1 - y0) / ESCALA}


def main():
    navegador = achar_navegador()
    css = css_original()
    os.makedirs(SAIDA, exist_ok=True)
    pasta_temp = tempfile.mkdtemp(prefix="artes-procon-")
    manifesto = {"cenario": {"largura": LARGURA, "altura": ALTURA}, "pecas": {}}
    vazias = []

    try:
        for nome, seletor, cenario, variante in PECAS_ORIGINAIS:
            html = ("<!doctype html><html><head><meta charset='utf-8'><style>" + css +
                    (ISOLAMENTO % (LARGURA, ALTURA, seletor)) + "</style></head><body>" +
                    CENARIO_HTML.format(cenario_attr='data-scenario="%s"' % cenario, variante=variante,
                                        rotulo=TEXTOS[cenario][0], subtitulo=TEXTOS[cenario][1],
                                        decor=TEXTOS[cenario][2]) +
                    "</body></html>")
            foto = fotografar(navegador, html, pasta_temp, nome)
            caixa = recortar(foto, os.path.join(SAIDA, nome + ".png"))
            if caixa is None:
                vazias.append(nome)
                continue
            manifesto["pecas"][nome] = caixa
            print("  ok  %-18s %4dx%-4d em (%d, %d)" % (nome, caixa["w"], caixa["h"], caixa["x"], caixa["y"]))

        for nome, seletor in PECAS_EXTRAS:
            forcar = "%s { display: block !important; }" % seletor
            html = ("<!doctype html><html><head><meta charset='utf-8'><style>" + css +
                    (ISOLAMENTO % (LARGURA, ALTURA, seletor)) + forcar + "</style></head><body>" +
                    CENARIO_HTML.format(cenario_attr="", variante="0", rotulo="", subtitulo="", decor="") + "</body></html>")
            foto = fotografar(navegador, html, pasta_temp, nome)
            caixa = recortar(foto, os.path.join(SAIDA, nome + ".png"))
            if caixa is None:
                vazias.append(nome)
                continue
            manifesto["pecas"][nome] = caixa
            print("  ok  %-18s %4dx%-4d em (%d, %d)  (nao aparecia no jogo)" % (nome, caixa["w"], caixa["h"], caixa["x"], caixa["y"]))

        for nome, seletor, limpeza in PECAS_LIMPAS:
            forcar = "%s { display: block !important; }" % seletor
            html = ("<!doctype html><html><head><meta charset='utf-8'><style>" + css +
                    (ISOLAMENTO % (LARGURA, ALTURA, seletor)) + forcar + limpeza + "</style></head><body>" +
                    CENARIO_HTML.format(cenario_attr="", variante="0", rotulo="", subtitulo="", decor="") +
                    "</body></html>")
            foto = fotografar(navegador, html, pasta_temp, nome)
            caixa = recortar(foto, os.path.join(SAIDA, nome + ".png"))
            if caixa is None:
                vazias.append(nome)
                continue
            manifesto["pecas"][nome] = caixa
            print("  ok  %-18s %4dx%-4d  (do HTML, com a tela limpa)" % (nome, caixa["w"], caixa["h"]))

        if os.path.exists(HTML_NOVAS):
            novas = io.open(HTML_NOVAS, encoding="utf-8").read()
            for nome in PECAS_NOVAS:
                html = novas.replace("/*ALVO*/", "#" + nome)
                foto = fotografar(navegador, html, pasta_temp, "nova_" + nome)
                caixa = recortar(foto, os.path.join(SAIDA, nome + ".png"))
                if caixa is None:
                    vazias.append(nome)
                    continue
                manifesto["pecas"][nome] = caixa
                print("  ok  %-18s %4dx%-4d  (arte nova)" % (nome, caixa["w"], caixa["h"]))
    finally:
        shutil.rmtree(pasta_temp, ignore_errors=True)

    # Lista em vez de dicionario: e o formato que a Unity le sem biblioteca extra.
    lista = [dict(nome=nome, **caixa) for nome, caixa in manifesto["pecas"].items()]
    saida = {"largura": LARGURA, "altura": ALTURA, "pecas": lista}
    io.open(os.path.join(SAIDA, "manifesto.json"), "w", encoding="utf-8").write(
        json.dumps(saida, ensure_ascii=False, indent=1))

    print("\n%d pecas extraidas para %s" % (len(manifesto["pecas"]), SAIDA))
    if vazias:
        print("Saíram vazias (nada visivel para fotografar): " + ", ".join(vazias))


if __name__ == "__main__":
    main()
