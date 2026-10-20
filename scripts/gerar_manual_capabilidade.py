"""Guia Foxconn da Sprint 4, com reprodução real do Excel simulado."""
from pathlib import Path
helpers = Path(__file__).with_name('gerar_manual.py').read_text(encoding='utf-8').split("if 'Title' not in doc.styles:")[0]
exec(compile(helpers, str(Path(__file__).with_name('gerar_manual.py')), 'exec'))
TITLE='Método de uso de Cp e Cpk e arquivos simulados'
OUT=ROOT/'docs/manual/Metodo de Uso CP CPK Sprint 4.docx'
doc.settings.odd_and_even_pages_header_footer=False
for part in [section.header,section.footer]:
    paragraphs=list(part.paragraphs)
    for t in part.tables:
        for row in t.rows:
            for cell in row.cells:paragraphs.extend(cell.paragraphs)
    for p in paragraphs:
        for r in p.runs:
            r.text=r.text.replace('Método de uso da coleta CEP no QMS',TITLE).replace('17/10/2026','20/10/2026')
if 'Title' not in doc.styles:doc.styles.add_style('Title',WD_STYLE_TYPE.PARAGRAPH)
style=doc.styles['Title'];style.font.name='Arial';style.font.size=Pt(15);style.font.color.rgb=RGBColor.from_string(BLUE)
doc.add_paragraph(TITLE,style='Title')
table(['Controle','Identificação'],[
    ['Revisão e data','00 — validação · 20/10/2026'],
    ['Aplicação','QMS web — CEP 25 × 3 e Cp/Cpk 33 × 3'],
    ['Código e aprovação','A preencher pelo controle documental'],
],[1.4,5.6])
heading('1 FINALIDADE')
text('Orientar a coleta de 33 amostras com três leituras, a consulta dos índices Cp/Cpk sobre as médias e o teste de importação de arquivos simulados. Os simulados contêm dados fictícios de treinamento; não representam medições de produto. O CEP continua utilizando 25 amostras com três leituras.')
heading('2 ENTRAR E CRIAR O CONJUNTO')
text('Abrir http://127.0.0.1:5188. Na demonstração local, utilizar login admin e senha admin. No menu, escolher Coleta Cp/Cpk 33 × 3 ou Conjuntos CEP 25 × 3. Para conferir os valores deste guia, selecionar Altura no dispositivo TL-001, com nominal 10 mm, LSL 9,5 mm e USL 10,5 mm. Informar um título com a palavra SIMULADO e clicar em Criar rascunho.')
text('Na identificação da coleta, selecionar o instrumento INS-001 ativo e com calibração válida, informar a data e hora local da coleta, turno T1 e lote SIMULADO. Datas futuras e instrumentos incompatíveis são rejeitados. Os arquivos de leituras não contêm identificação de instrumento ou data: esses campos devem ser informados no sistema.')
heading('3 ENVIAR O ARQUIVO OU PREENCHER AS LEITURAS')
text('Baixar o Excel ou CSV simulado pelo botão da própria coleta. Abrir Origem dos dados e expandir Enviar arquivo de leituras CSV ou XLSX. Marcar Confirmo substituir a grade e selecionar o arquivo correspondente ao módulo. O envio preenche a grade; ainda é necessário salvar.')
table(['Módulo','Arquivo','Quantidade'],[
    ['CEP','Simulado_CEP_25x3.csv ou .xlsx','25 amostras / 75 leituras'],
    ['Cp/Cpk','Simulado_CP_CPK_33x3.csv ou .xlsx','33 amostras / 99 leituras'],
],[1,4.3,1.7])
text('O CSV usa ponto e vírgula. O Excel usa a primeira aba Leituras. Ambos exigem as colunas amostra, leitura1, leitura2 e leitura3. Não enviar a antiga ficha de colagem nesse campo. O envio à base em Coleta / Importar continua usando seu próprio template completo da característica.')
text('Alternativas: digitar, colar somente as três colunas de leituras sem cabeçalho ou selecionar amostras da base. Conferir a substituição da grade. A seleção da base preserva instrumento, data, lote, operador e revisão e não permite alterar esses dados ao vincular.')
page()
heading('4 SALVAR CONCLUIR E ANALISAR')
text('Conferir leituras e identificação. Aplicar identificação às linhas digitadas quando necessário. Salvar rascunho preserva o preenchimento parcial e registra histórico. Para concluir, preencher todas as amostras, marcar a conferência e clicar em Concluir conjunto. Um conjunto concluído fica somente para consulta; uma nova coleta exige outro conjunto.')
text('No Cp/Cpk, clicar em Abrir análise, selecionar o conjunto concluído e clicar em Calcular e salvar estudo. Engenharia, Qualidade e Administração podem calcular; Técnico coleta e consulta; Leitor consulta. No CEP, Abrir análise direciona às cartas X média e R. O mesmo conjunto não é utilizado nos dois módulos.')
heading('5 INTERPRETAR O RESULTADO')
text('Cada ponto do estudo Cp/Cpk é a média de três leituras. O centro é a média das 33 médias. A estimativa local de sigma utiliza as 32 amplitudes móveis entre médias consecutivas, divididas por 1,128. Preservar a ordem cronológica; não ordenar pelos valores. Cp = (USL − LSL) / (6 sigma). Cpk é o menor índice entre os lados superior e inferior.')
text('Os índices descrevem as médias dos trios. As 99 leituras originais permanecem disponíveis, inclusive sua contagem fora da especificação. As faixas numéricas e metas são as definições salvas no estudo. Bom não significa processo aprovado. Normalidade, independência e estabilidade exigem avaliação técnica. Pp/Ppk e normalidade pertencem à próxima sprint.')
text('Variação nula, quantidade incorreta, leituras não finitas e falta de um dos limites de especificação impedem o cálculo. Sinais na série de médias são apresentados para investigação. Resultados negativos de Cpk são preservados. Sem sinais nas regras verificadas não comprova estabilidade.')
heading('6 CONFERIR O TESTE SIMULADO')
table(['Conferência','CEP','Cp/Cpk'],[
    ['Média geral','10,000000','9,999091'],
    ['Variação','Amplitude média 0,080000','MR médio 0,060000'],
    ['Resultado principal','LIC X 9,918160 / LSC X 10,081840','Cp 3,133333 / Cpk 3,127636'],
],[1.5,2.7,2.8])
text('Os valores acima usam Altura / TL-001 e seus limites de demonstração. Outra característica pode produzir resultado diferente mesmo com as mesmas leituras. A aba Conferencia do Excel permite acompanhar as fórmulas; importar apenas a primeira aba é o comportamento do sistema.')
heading('7 PROGRESSO E HISTÓRICO')
text('Abrir Progresso Cp/Cpk, escolher a data de criação local e consultar os conjuntos. O painel mostra preenchimento e análises salvas; não representa meta de programação diária. Abrir o conjunto para consultar salvamentos e rastreabilidade. Em Estudos Cp/Cpk, o histórico conserva cada estudo, autor, data, versão do conjunto, método e definições. Uma reexecução idêntica reutiliza o registro; novas definições geram outro estudo.')
page()
heading('8 CONFERÊNCIA DO ARQUIVO EXCEL')
p=doc.add_paragraph();p.alignment=WD_ALIGN_PARAGRAPH.CENTER
p.add_run().add_picture(str(ROOT/'artifacts/simulados/CP_CPK_33x3.png'),width=Inches(7.02))
text('Figura 1 — Reprodução da aba Conferencia do arquivo simulado Cp/Cpk. A imagem mostra o arquivo entregue, não uma tela do sistema.')
heading('9 ROTEIRO DE ACEITE DO USUÁRIO')
text('Importar os dois arquivos em seus respectivos módulos, salvar, reabrir e conferir as quantidades. Concluir e analisar. Comparar os resultados com a tabela deste guia e com o Excel. Tentar enviar o arquivo de 33 amostras no CEP: o sistema deve rejeitar o excesso, sem cortar as últimas linhas. Verificar os históricos e o progresso diário.')
text('Registrar eventuais diferenças com o ID do conjunto, a característica, a mensagem apresentada e o arquivo utilizado. A captura das telas novas e o aceite visual permanecem pendentes. A validação visual da Sprint 3 foi adiada pelo solicitante; este guia não declara seu fechamento.')
heading('10 REFERÊNCIAS DO MÉTODO')
text('Método do projeto: cp-cpk-33means-mr2-v1. Memória de cálculo e casos de referência em docs/metodo-cp-cpk.md. Referências primárias: NIST, Individuals Control Charts, itl.nist.gov/div898/handbook/pmc/section3/pmc322.htm; NIST, What is Process Capability, itl.nist.gov/div898/handbook/pmc/section1/pmc16.htm. A aplicação do método à série das médias é uma decisão explícita deste projeto.')
doc.core_properties.title=TITLE;doc.core_properties.subject='Coleta, importação e estudos Cp/Cpk — Sprint 4'
doc.save(OUT);preserve_package_parts(OUT);print(OUT)
