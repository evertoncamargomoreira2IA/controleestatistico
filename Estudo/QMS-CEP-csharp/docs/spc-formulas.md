# Fórmulas do método X̄-R

Versão: `xbar-r-1.0.0`. Para k subgrupos de tamanho constante n e observações xᵢⱼ:

- Média do subgrupo: x̄ᵢ = Σxᵢⱼ/n; amplitude: Rᵢ = máximo − mínimo.
- X̄̄ = média das médias; R̄ = média das amplitudes.
- Carta X̄: LIC = X̄̄ − A₂R̄; LC = X̄̄; LSC = X̄̄ + A₂R̄.
- Carta R: LIC = D₃R̄; LC = R̄; LSC = D₄R̄.
- σ within = R̄/d₂. Cp = (USL−LSL)/(6σwithin); Cpu=(USL−X̄̄)/(3σwithin); Cpl=(X̄̄−LSL)/(3σwithin); Cpk=min(Cpu,Cpl).
- s global é o desvio-padrão amostral, raiz da soma dos desvios quadráticos dividida por N−1. Pp usa a largura de especificação sobre 6s; Ppu/Ppl/Ppk usam a média global e 3s.
- PPM observado = contagem real fora de [LSL,USL] / total × 1.000.000.

Constantes A₂, D₃, D₄ e d₂ usadas para n=2..10 estão em `src/constants.js`, em três casas decimais. Fonte declarada: “tabela padrão de constantes de cartas de controle (ex.: AIAG SPC Manual 2ª ed./Montgomery) — requer validação organizacional”. Essa referência é indicativa, não uma validação editorial da tabela. Validação normativa e organizacional pendente antes do uso produtivo.

Os resultados mantêm precisão integral em cálculos; arredondamento ocorre apenas na interface. σ zero torna os índices correspondentes indefinidos (null), sem divisão por zero. Exigem-se limites bilaterais LSL e USL.
