"""Complemento Foxconn da Sprint 2, com o mesmo template e helpers da coleta."""
from pathlib import Path
helpers = Path(__file__).with_name('gerar_manual.py').read_text(encoding='utf-8').split("if 'Title' not in doc.styles:")[0]
exec(compile(helpers, str(Path(__file__).with_name('gerar_manual.py')), 'exec'))
TITLE = 'Método de uso das cartas CEP no QMS'
OUT = ROOT/'docs/manual/Metodo de Uso Cartas CEP Sprint 2.docx'
PRINTS = ROOT/'docs/manual/prints-sprint2'
for part in [section.header, section.footer]:
    paragraphs = list(part.paragraphs)
    for t in part.tables:
        for row in t.rows:
            for cell in row.cells: paragraphs.extend(cell.paragraphs)
    for p in paragraphs:
        for r in p.runs:
            if 'Método de uso da coleta CEP no QMS' in r.text:
                r.text = r.text.replace('Método de uso da coleta CEP no QMS', TITLE)
            if '17/10/2026' in r.text: r.text=r.text.replace('17/10/2026','19/10/2026')
if 'Title' not in doc.styles: doc.styles.add_style('Title', WD_STYLE_TYPE.PARAGRAPH)
style=doc.styles['Title']; style.font.name='Arial'; style.font.size=Pt(15); style.font.color.rgb=RGBColor.from_string(BLUE)
doc.add_paragraph(TITLE, style='Title')
table(['Controle','Identificação'], [
    ['Revisão e data','00 — minuta · 19/10/2026'],
    ['Código e aprovação','A preencher pelo controle documental'],
    ['Aplicação','QMS web · Sprint 2 · complementar ao método de coleta da Sprint 1'],
], [1.4,5.6])
heading('1 FINALIDADE E ACESSO')
text('Gerar e consultar cartas X̄-R de conjuntos concluídos com 25 amostras e três leituras por amostra. Preservar as 75 leituras, a revisão, os resultados e a versão do método em cada análise salva. A coleta e sua importação permanecem descritas no procedimento da Sprint 1.')
text('Acessar Cartas CEP no menu. Informar a data de criação do conjunto, clicar em Buscar conjuntos e selecionar a coleta. Engenharia, Qualidade e Administração podem clicar em Analisar e salvar. Os demais perfis com consulta podem abrir os resultados existentes.')
figure('01-analise.png','Figura 1 — Seleção e identificação da análise salva.',crop=(265,0,1240,635),width=6.8)
text('Também é possível abrir as cartas pelo conjunto concluído. Rascunhos não podem ser analisados. Na demonstração local, o acesso solicitado é admin/admin. O acesso corporativo será definido na homologação.')
page()
heading('2 INTERPRETAR A CARTA DE MÉDIAS')
text('Cada ponto azul representa a média das três leituras de uma amostra. A distribuição lateral agrupa as 25 médias em seis classes, na mesma escala vertical. Pontos envolvidos em sinais são destacados em vermelho; o detalhe da regra aparece abaixo das cartas.')
figure('02-carta-x.png','Figura 2 — Carta X̄ com distribuição lateral e legenda.',crop=(270,30,1227,565),width=7.0)
table(['Linha','Interpretação'],[
    ['Média global cinza tracejada','Centro calculado a partir das 25 médias.'],
    ['Nominal cinza contínua','Valor de referência preservado da característica.'],
    ['LIC e LSC vermelhos tracejados','Limites de controle estimados a partir deste conjunto.'],
    ['LSL e USL vermelhos contínuos','Limites de especificação individuais cadastrados. Não são limites de controle.'],
],[2.1,4.9])
text('Médias dentro da especificação não garantem que todas as leituras individuais estejam dentro dela. Conferir as contagens das 75 leituras e das 25 médias na seção Resultado das regras. Limites ausentes não são inventados.')
text('No exemplo, os subgrupos de demonstração repetem os mesmos valores. A linha horizontal representa esses dados salvos, sem substituí-los por medições artificiais.')
page()
heading('3 INTERPRETAR A AMPLITUDE E OS SINAIS')
figure('03-carta-r.png','Figura 3 — Carta R e início do resultado das regras.',crop=(270,110,1227,705),width=7.0)
text('A amplitude é a maior leitura menos a menor, dentro da mesma amostra. A linha central da carta R é a média das 25 amplitudes. Os limites são LIC = 0 e LSC = 2,574 × amplitude média. Limites dimensionais de especificação não se aplicam à carta R.')
text('A carta X̄ usa média global ± 1,023 × amplitude média. Os cálculos mantêm a precisão disponível; o arredondamento ocorre apenas na apresentação. Os limites desta análise não são limites fixos aprovados para acompanhamento futuro.')
text('São avaliadas médias ou amplitudes estritamente fora de LIC/LSC. Para as regras próprias do projeto, sete médias devem ser estritamente crescentes e todas acima do nominal, ou estritamente decrescentes e todas abaixo do nominal. Empates e valores iguais ao nominal interrompem a sequência. Janelas sobrepostas são listadas separadamente.')
text('Sem nominal, as regras de sete pontos ficam não avaliadas. Amplitude média zero gera aviso para investigar resolução do instrumento e coleta. Ausência de sinais não comprova estabilidade nem libera produto.',bold=True)
page()
heading('4 HISTÓRICO E CONFERÊNCIA')
figure('04-historico.png','Figura 4 — Resultado das regras e consulta do histórico.',crop=(265,0,1240,710),width=6.8)
text('Em Histórico de análises, filtrar pelo conjunto ou processo e clicar em Abrir análise. Recarregar a página mantém os resultados salvos. Analisar novamente a mesma versão abre o registro existente, sem duplicar nem substituir o histórico.')
text('Valores e limites para conferência apresenta as 25 médias, amplitudes e limites. O link Consultar leituras e histórico da coleta abre o rastreio original. A análise preserva usuário, data/hora de São Paulo, revisão, versão do conjunto e método xbar-r-25x3-v1.')
text('Ao encontrar sinal, conferir rastreio, instrumento e condições do processo, registrar a investigação no fluxo da organização e manter o resultado original. Não excluir amostras para remover um sinal. Programação, metas, ações e aprovação de limites serão integradas no CEP operacional.')
heading('5 REFERÊNCIAS E CONTROLE DA REVISÃO')
text('Método X̄-R: NIST Engineering Statistics Handbook, seção 6.3.1.1, www.itl.nist.gov/div898/handbook/pmc/section3/pmc311.htm. Constantes n = 3: A₂ = 1,023; D₃ = 0; D₄ = 2,574. As regras de sete pontos são requisitos específicos deste projeto. Cp/Cpk será tratado em procedimento e sprint próprios.')
text('Rev. 00 — emissão da minuta da Sprint 2. Código, revisão técnica e aprovação: a preencher. Kappa e Gage R&R foram retirados do escopo vigente.')
doc.core_properties.title=TITLE
doc.core_properties.subject='Método de uso das cartas CEP — Sprint 2'
doc.save(OUT)
preserve_package_parts(OUT)
print(OUT)
