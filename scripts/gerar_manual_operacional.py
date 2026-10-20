"""Minuta Sprint 3. Prints e teste visual dependem da retomada do navegador."""
from pathlib import Path
helpers = Path(__file__).with_name('gerar_manual.py').read_text(encoding='utf-8').split("if 'Title' not in doc.styles:")[0]
exec(compile(helpers, str(Path(__file__).with_name('gerar_manual.py')), 'exec'))
TITLE='Método de uso do CEP operacional'
OUT=ROOT/'docs/manual/Metodo de Uso CEP Operacional Sprint 3.docx'
for part in [section.header,section.footer]:
    paragraphs=list(part.paragraphs)
    for t in part.tables:
        for row in t.rows:
            for cell in row.cells: paragraphs.extend(cell.paragraphs)
    for p in paragraphs:
        for r in p.runs:
            if 'Método de uso da coleta CEP no QMS' in r.text:
                r.text=r.text.replace('Método de uso da coleta CEP no QMS',TITLE)
            if '17/10/2026' in r.text:r.text=r.text.replace('17/10/2026','20/10/2026')
if 'Title' not in doc.styles:doc.styles.add_style('Title',WD_STYLE_TYPE.PARAGRAPH)
style=doc.styles['Title'];style.font.name='Arial';style.font.size=Pt(15);style.font.color.rgb=RGBColor.from_string(BLUE)
doc.add_paragraph(TITLE,style='Title')
table(['Controle','Identificação'],[
    ['Revisão e data','00 — minuta · 20/10/2026'],
    ['Situação','Aguardando validação visual e inclusão dos prints da Sprint 3'],
    ['Código e aprovação','A preencher pelo controle documental'],
    ['Aplicação','QMS web — planejamento, limites de controle e investigação'],
],[1.4,5.6])
heading('1 FINALIDADE E RESPONSABILIDADES')
text('Programar conjuntos CEP de 25 amostras com três leituras, acompanhar o atendimento da meta e tratar as ocorrências das análises. Este complemento utiliza os métodos de coleta e cartas das Sprints 1 e 2, preservando seu histórico. As telas novas ainda precisam de validação visual; esta minuta não contém prints antigos apresentados como se fossem desta sprint.')
table(['Perfil','Operações'],[
    ['Administração','Programar, coletar, analisar, aprovar ou revogar limites e tratar ocorrências.'],
    ['Engenharia','Programar, coletar, analisar e tratar ocorrências.'],
    ['Qualidade','Programar, analisar, aprovar ou revogar limites e tratar ocorrências.'],
    ['Técnico','Coletar, vincular conjuntos e tratar ocorrências.'],
    ['Leitor','Consultar programação, limites, análises, ocorrências e históricos.'],
],[1.4,5.6])
heading('2 PROGRAMAR O DIA')
text('Acessar CEP operacional no menu. Informar a Data planejada e clicar em Consultar programação. O filtro localiza processo, título ou responsável. Sem programação, o cumprimento da meta é apresentado como indisponível, com a indicação Sem meta definida.')
text('Expandir Nova programação. Selecionar a característica ativa, informar título, turno T1, T2 ou T3, responsável ativo, meta de 1 a 100 conjuntos e prazo na mesma data planejada. O horário é de São Paulo. Salvar programação e conferir a mensagem de registro.')
text('Criar a coleta em Conjuntos CEP 25 × 3. Na seção Vincular uma coleta à programação, selecionar a programação e informar o ID do conjunto. Conferir característica, revisão e turno. O conjunto só pode contar em uma programação e a quantidade de vínculos não pode ultrapassar a meta.')
page()
heading('3 ACOMPANHAR METAS E PENDÊNCIAS')
table(['Situação','Condição'],[
    ['Planejada','Programação sem conjunto vinculado e ainda no prazo.'],
    ['Em coleta','Existe conjunto vinculado e a meta ainda não foi preenchida.'],
    ['Em análise','Meta de coleta preenchida sem todas as análises, ou investigação aberta.'],
    ['Atrasada','Prazo ultrapassado sem cumprir todas as condições de conclusão.'],
    ['Concluída','Meta de coletas válidas atingida, conjuntos analisados e nenhuma ocorrência aberta.'],
    ['Cancelada','Cancelamento registrado por perfil autorizado, com motivo e histórico preservados.'],
],[1.3,5.7])
text('O percentual de cumprimento compara conjuntos completos válidos com a meta, considerando somente programações não canceladas no filtro. Ele é diferente do percentual de preenchimento das 75 leituras de cada coleta. Conclusão operacional não equivale a aprovação estatística ou liberação de produto.')
text('Se uma coleta for preenchida com turno ou revisão incompatível, ela não conta como completa para a programação. A data do painel é a data planejada e o estado mostrado é o atual. O histórico registra cada vínculo ou cancelamento; não se trata de uma fotografia congelada do encerramento do dia.')
heading('4 APROVAR E REVOGAR LIMITES')
text('Qualidade ou Administração acessa Limites aprovados, expande Aprovar limites de uma análise e informa o ID da análise de base, validade em dias e justificativa técnica. A validade inicial é sugerida pelas definições e pode ser ajustada de 1 a 3650 dias.')
text('A base deve usar limites estimados, ter variação intramostra positiva, não apresentar sinais nas regras avaliadas nem leituras fora da especificação e não possuir investigação aberta. A característica deve estar ativa na mesma revisão. Aprovar e registrar decisão preserva o resultado original e o usuário responsável.')
text('Na página Cartas CEP, selecionar o conjunto e a Origem dos limites. Uma aprovação só se aplica à mesma característica e revisão, a uma coleta diferente da base e a leituras realizadas durante sua vigência. Aprovação vencida, revogada ou incompatível é rejeitada pelo servidor. O resultado preserva a origem e as linhas centrais da base aprovada.')
text('Para interromper o uso de limites, expandir Revogar limites aprovados, escolher o registro, informar o motivo e registrar a revogação. Resultados já salvos permanecem consultáveis. Uma nova versão de limites requer nova base e nova decisão, sem editar a aprovação anterior.')
page()
heading('5 REGRAS E INVESTIGAÇÃO DE OCORRÊNCIAS')
text('Novas análises preservam a versão ativa das definições: fora dos limites em X̄ e R, N médias do mesmo lado da linha central, N médias estritamente crescentes ou decrescentes e proximidade da especificação. Para sequências configuráveis, N varia de 2 a 25. As duas regras próprias de sete médias crescentes acima do nominal ou decrescentes abaixo do nominal continuam fixas.')
text('A proximidade compara a distância da média ao centro da especificação com a metade da faixa LSL–USL. Por exemplo, 95% identifica a faixa próxima às extremidades. Essa regra só é avaliada com os dois limites cadastrados. Controle e especificação continuam separados.')
text('Sinais, leituras fora da especificação ou amplitude média zero em novas análises operacionais abrem uma ocorrência por análise. O responsável inicial é quem executou a análise, com prazo inicial de 24 horas. Conferir e ajustar o responsável e o prazo conforme a rotina do processo.')
text('Acessar Ocorrências e ações, disponível para todas as datas. Clicar em Tratar ocorrência. Informar responsável, prazo, situação, causa, ação executada e verificação de eficácia. Para Encerrada, causa, ação e verificação são obrigatórias. Para ocorrência aberta ou em investigação, a atualização deve definir prazo futuro. Salvar investigação e conferir a mensagem.')
text('Uma ocorrência encerrada pode ser reaberta com nova atualização, mantendo o histórico. A análise não é recalculada nem tem seus sinais apagados ao encerrar a ação. Reanalisar com as mesmas regras e limites abre o registro existente; trocar a versão das regras ou a base cria outra análise com rastreio próprio.')
heading('6 CONSULTAR HISTÓRICO E CORRIGIR REGISTROS')
text('Usar os botões de histórico da programação, dos limites e da ocorrência. Cada evento apresenta data local, autor e registros antes/depois. Ao receber aviso de alteração simultânea, recarregar e conferir a versão atual antes de editar novamente.')
text('Vínculos não são apagados. Para corrigir uma programação que não possa mais ser atendida pelos conjuntos vinculados, cancelar com justificativa e registrar a nova programação. Não excluir amostras, alterar datas ou remover sinais para contornar uma rejeição.')
heading('7 CONTROLE DA MINUTA')
text('Rev. 00 — implementação da Sprint 3. Pendência de fechamento: validar os fluxos no navegador, registrar os prints reais e atualizar esta minuta. Código oficial, revisão técnica e aprovação: a preencher. A demonstração local usa admin/admin; a homologação corporativa continua em etapa própria.')
doc.core_properties.title=TITLE
doc.core_properties.subject='CEP operacional — minuta da Sprint 3'
doc.save(OUT);preserve_package_parts(OUT);print(OUT)
