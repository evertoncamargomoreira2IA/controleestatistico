# Fórmulas do método X̄-R

## Implementação da Sprint 2

Método `xbar-r-25x3-v1`, exclusivamente 25 subgrupos de três leituras finitas, com posições únicas de 1 a 25. Constantes fixas n=3: A₂=1,023; D₃=0; D₄=2,574. Referência do método: [NIST 6.3.1.1](https://www.itl.nist.gov/div898/handbook/pmc/section3/pmc311.htm); valores de A₂/D₄ também conferidos no [manual NIOSH, páginas PDF 59 e 94](https://stacks.cdc.gov/view/cdc/178780/cdc_178780_DS1.pdf).

Sinais: média estritamente fora de LIC/LSC na carta X̄, amplitude estritamente fora dos limites na R. Regras próprias solicitadas: sete médias estritamente crescentes, todas acima do nominal, ou sete estritamente decrescentes, todas abaixo do nominal. Empates e igualdade ao nominal interrompem a sequência; janelas sobrepostas contam separadamente. Sem nominal, essas regras não são avaliadas. Amplitude média zero é advertência explícita, sem cálculo de índices de capacidade.

Contagens de especificação são separadas para 75 leituras e 25 médias; igualdade aos limites é aceita. A distribuição lateral mostra 25 médias em seis classes, compartilhando a escala vertical da carta. Os limites são estimados dentro do próprio conjunto, para análise inicial, sem aprovação nem aplicação automática a conjuntos futuros.

As definições gerais editáveis de cartas ainda não controlam este método fixo. A integração, parametrização e aprovação dos limites são parte do CEP operacional. A análise salva preserva entrada e resultados JSON, versão do método, revisão e usuário. Nenhum Cp/Cpk é calculado nesta implementação.

## Referência para capacidade futura

Versão: `xbar-r-1.0.0`. Para k subgrupos de tamanho constante n e observações xᵢⱼ:

- Média do subgrupo: x̄ᵢ = Σxᵢⱼ/n; amplitude: Rᵢ = máximo − mínimo.
- X̄̄ = média das médias; R̄ = média das amplitudes.
- Carta X̄: LIC = X̄̄ − A₂R̄; LC = X̄̄; LSC = X̄̄ + A₂R̄.
- Carta R: LIC = D₃R̄; LC = R̄; LSC = D₄R̄.
- σ within = R̄/d₂. Cp = (USL−LSL)/(6σwithin); Cpu=(USL−X̄̄)/(3σwithin); Cpl=(X̄̄−LSL)/(3σwithin); Cpk=min(Cpu,Cpl).
- s global é o desvio-padrão amostral, raiz da soma dos desvios quadráticos dividida por N−1. Pp usa a largura de especificação sobre 6s; Ppu/Ppl/Ppk usam a média global e 3s.
- PPM observado = contagem real fora de [LSL,USL] / total × 1.000.000.

Constantes A₂, D₃, D₄ e d₂ usadas para n=2..10 estão em `reference/constants.js`, em três casas decimais. Fonte declarada: “tabela padrão de constantes de cartas de controle (ex.: AIAG SPC Manual 2ª ed./Montgomery) — requer validação organizacional”. Essa referência é indicativa, não uma validação editorial da tabela. Validação normativa e organizacional pendente antes do uso produtivo.

Os resultados mantêm precisão integral em cálculos; arredondamento ocorre apenas na interface. σ zero torna os índices correspondentes indefinidos (null), sem divisão por zero. Exigem-se limites bilaterais LSL e USL.
