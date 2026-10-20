# Método de Cp/Cpk sobre médias de três leituras

Decisão de implementação da Sprint 4, em 20/10/2026. Método versionado: `cp-cpk-33means-mr2-v1`.

## Unidade de análise

O requisito do projeto é considerar 33 médias, cada uma obtida de três leituras. As 99 leituras permanecem no conjunto e no snapshot do estudo. O resultado descreve a série dessas médias; não é um índice de capabilidade das 99 medições individuais. Os limites de especificação cadastrados são aplicados às médias, conforme o requisito. A adequação desse critério à especificação do produto depende de validação técnica do processo.

Cada média é tratada como uma observação individual na série temporal. A estimativa local usa amplitudes móveis de dois pontos consecutivos. Esta é uma adaptação explícita do método I-MR à unidade de análise escolhida pelo projeto, não uma recomendação do NIST para substituir medições individuais por médias.

## Fórmulas

Para i = 1…33, média mᵢ = (xᵢ₁ + xᵢ₂ + xᵢ₃) / 3.

- Centro: média das 33 médias.
- MRᵢ = |mᵢ − mᵢ₋₁|, i = 2…33; são 32 amplitudes móveis.
- σ local das médias = média das 32 amplitudes móveis / 1,128.
- Cp = (USL − LSL) / (6σ).
- Cpu = (USL − centro) / (3σ).
- Cpl = (centro − LSL) / (3σ).
- Cpk = mínimo entre Cpu e Cpl. Cpk negativo é preservado.

A ordem das posições define a sequência temporal. Na conclusão do conjunto, datas decrescentes são rejeitadas. Datas iguais são permitidas, pois o registro no frontend tem resolução de minuto; a sequência das posições deve corresponder à coleta. Não ordenar pelas grandezas medidas.

Não usar o desvio padrão global das 33 médias sob o nome de sigma local. Pp/Ppk, normalidade e PPM não pertencem a esta implementação. Não estimar a dispersão das médias dividindo a dispersão das leituras por √3: o método escolhido utiliza diretamente a série observada de médias.

## Validação e interpretação

Exigir 33 posições únicas, 99 números finitos e os dois limites de especificação finitos, com LSL < USL. Variação das médias nula ou operações fora da faixa numérica impedem salvar um índice. Cp/Cpk unilateral não foi implementado nesta sprint; apresentar erro claro em vez de inventar Cp.

As faixas Bom/Atenção/Baixo e suas metas são obtidas das definições vigentes e preservadas no estudo. Elas classificam o valor numérico e não aprovam o processo. A referência visual do usuário pede Cp 1,67 e Cpk 1,33; é possível ajustar esses valores em Definições, sem alterar silenciosamente metas já cadastradas. Alteração de definições gera outra versão de estudo; o resultado anterior é imutável.

Verificações fixas informativas: média além de três sigmas, MR além de 3,267 × MR médio, sete médias estritamente no mesmo lado do centro e sete em tendência estrita. Essas verificações são próprias do método versionado de Cp/Cpk; os interruptores das regras CEP não as alteram. Sem sinais não comprova estabilidade. Não são abertas ocorrências CEP para estudos Cp/Cpk nesta sprint.

Os índices são preliminares. Normalidade, independência e estabilidade da série exigem avaliação técnica; o tamanho de 33 observações foi definido pelo usuário e não representa homologação de suficiência estatística. Não há intervalo de confiança nem liberação automática de produto.

## Referências primárias

- [NIST — Individuals Control Charts](https://www.itl.nist.gov/div898/handbook/pmc/section3/pmc322.htm): MR de observações consecutivas e constante 1,128.
- [NIST — What is Process Capability?](https://itl.nist.gov/div898/handbook/pmc/section1/pmc16.htm): fórmulas Cp/Cpk e hipóteses de interpretação.

## Conferência independente

Caso analítico de teste: 33 médias alternando 9,9 e 10,1, começando em 9,9; LSL 9,5 e USL 10,5. Cada trio é formado por média − 0,02; média; média + 0,02. MR médio = 0,2; σ = 0,177304964539; centro = 9,996969696970; Cp = 0,94; Cpk = 0,934303030303.

Arquivo simulado entregue: médias alternam 9,97 e 10,03, começando em 9,97; trios com deslocamentos −0,04; 0; +0,04. Centro = 9,999090909091; MR médio = 0,06; σ = 0,053191489362; Cp = 3,133333333333; Cpk = 3,127636363636. O Excel contém fórmulas independentes para conferência; testes importam os arquivos CSV e XLSX reais e comparam os resultados.
