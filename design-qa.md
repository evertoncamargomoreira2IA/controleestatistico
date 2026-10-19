# QA visual das cartas CEP

final result: passed

Referência: `docs/referencias/frontend-cep-desejado.png` (1067 × 655). Implementação: `/cep-cartas?analysisId=1`, desktop 1280 × 720, captura da área da carta em `docs/manual/prints-sprint2/02-carta-x.png`. Comparação conjunta normalizada em `artifacts/cep-comparacao.png`, aberta e inspecionada; região gráfica examinada também na captura original.

## Comparação e escopo

Fundo branco, linha azul e pontos, nominal e média cinza, especificação vermelha contínua, controle vermelho tracejado e distribuição azul à direita foram reproduzidos. Tipografia e respiros da carta são legíveis; a legenda foi colocada abaixo para evitar reduzir a área gráfica no layout existente. A distribuição usa a mesma escala vertical. Carta R e tabela acessível foram acrescentadas conforme escopo funcional.

Os dados da referência são diferentes: o conjunto #2 de demonstração tem 25 trios repetidos, portanto linha e distribuição correspondem a esses dados, sem copiar valores da imagem. Cp/Cpk da referência não integra esta sprint; a análise é CEP 25 × 3 e não apresenta índices de capacidade. Os indicadores das regras ficam após a carta R, no estilo do sistema existente. Essas diferenças são de conteúdo e integração deliberadas, não erros de renderização.

## Verificações

- Fontes e textos legíveis, unidades e títulos consistentes, cores e estilos de limites distintos.
- Sem imagens artificiais: gráficos quantitativos desenhados por Chart.js, com dados reais do conjunto de demonstração salvo.
- Nenhum erro de console observado; três canvas renderizados. Tabela de conferência aberta e valores verificados.
- Tablet 768 × 1024 e celular 390 × 844: largura da página igual à largura disponível, sem rolagem horizontal do documento. A legenda envolve e as tabelas têm contêiner de rolagem.
- Identificado durante QA: reabrir análise antiga mantinha seletor na data atual, deixando opção vazia. Corrigido para a data original do snapshot, recapturado e verificado no conjunto #2. Autor incluído no resultado e no histórico.
- Fluxo de salvar, filtrar e reabrir verificado; reanalisar mantém uma única análise.

Sem achados P0/P1/P2 pendentes. Possível refinamento P3: aproximar a posição da legenda da referência em monitores largos, se o usuário preferir, preservando a legibilidade em telas menores.
