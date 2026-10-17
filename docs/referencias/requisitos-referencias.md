# Referências fornecidas pelo usuário em 17/10/2026

## Tela desejada para análise CEP

Imagem: `frontend-cep-desejado.png`. Carta com 25 médias em azul, distribuição lateral, nominal e média global em cinza, LSL/USL em vermelho contínuo, LIC/LSC em vermelho tracejado e legenda explícita. Indicadores abaixo: Cp acima de 1,67, Cpk acima de 1,33, sete pontos consecutivos crescentes acima do nominal e sete decrescentes abaixo do nominal. A lógica exata das sequências, o limiar estrito e a classificação devem ser documentados e testados antes da entrega. Manter critérios configuráveis.

A imagem orienta a próxima tela; os números nela exibidos não são resultados de medições desta sprint. A carta R complementar continua no escopo X̄-R. Cp/Cpk completos pertencem à sprint de capacidade; os 33 subgrupos de três leituras e o tratamento das médias exigem definição explícita do método.

## Especificações de teste

Fonte: `especificacoes-tooling.png`. Valores em mm. Os identificadores abaixo são provisórios para demonstração. Linha e posto reais não foram informados; usa-se Linha de teste / Posto de validação.

| Produto | Código | Descrição | Nominal | Tolerância inferior | Tolerância superior | LSL | USL |
|---|---|---|---:|---:|---:|---:|---:|
| Athena - Lebi | AT-001 | Feed Shaft Gear Insert Press Fixture | 326,95 | 0,30 | 0,30 | 326,65 | 327,25 |
| Athena - Lebi | AT-002 | Feed Shaft Pulley Insert Press Fixture | 4,68 | 0,10 | 0,10 | 4,58 | 4,78 |
| Athena - Lebi | AT-003 | Belt Tension - Paper Motor | -5,25 | 0,75 | 0,75 | -6,00 | -4,50 |
| Athena - Lebi | AT-004 | X-Bias Press Fixture | 49,39 | 0,10 | 0,10 | 49,29 | 49,49 |
| Athena - Lebi | AT-005 | Out Put Clutch Press Fixture | 1,11 | 0,10 | 0,10 | 1,01 | 1,21 |
| Peak Mech - Sayan | PK-001 | Gear Feedroller 24T Press | 64,13 | 0,20 | 0,20 | 63,93 | 64,33 |
| Peak Mech - Sayan | PK-002 | Pulley Cluster and Encoder Ring Press | 4,73 | 0,40 | 0,10 | 4,33 | 4,83 |
| Peak Mech - Sayan | PK-003 | Gear Right Feedroller 17T Press | 317,28 | 0,10 | 0,30 | 317,18 | 317,58 |
| Peak Mech - Sayan | PK-004 | Output Gear 42T Press | 10,09 | 0,20 | 0,20 | 9,89 | 10,29 |
| Peak Mech - Sayan | PK-005 | Pick Encoder Disk and Hub Press | 40,45 | 0,30 | 0,30 | 40,15 | 40,75 |
| Peak Mech - Sayan | PK-006 | Gear Feedroller 24T Pick & Press | 48,63 | 0,20 | 0,20 | 48,43 | 48,83 |

As especificações são pré-carregadas no ambiente Development. Medições de demonstração usadas nos prints pertencem apenas à característica genérica Altura / TL-001, separada desses cadastros. O seed preserva revisões existentes, sem sobrescrever alterações de engenharia.
