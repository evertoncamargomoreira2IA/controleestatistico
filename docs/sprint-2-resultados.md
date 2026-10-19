# Sprint 2 — Cartas CEP

Data: 19/10/2026. Entrega para validação local: cartas X̄-R de 25 amostras com três leituras, distribuição lateral das médias, limites estimados e sinais. Kappa e Gage R&R retirados do escopo e das superfícies ativas do produto. Históricos preservados.

## Resultado

Página `/cep-cartas` com seleção de conjunto concluído, execução permitida à Engenharia, Qualidade e Administração, salvamento e reabertura. Consulta pelos demais perfis com View. Cada resultado preserva entrada completa, rastreio, revisão, versão do conjunto e do método, usuário e data local; reanálise da mesma versão abre o registro existente. Criação e auditoria compartilham transação; análises são somente inclusão.

Cartas brancas com série azul, nominal e centro cinza, limites de controle vermelhos tracejados e especificações contínuas, conforme referência. Sinais destacados e listados por posição. Carta R separada, distribuição lateral de seis classes, tabela das médias/amplitudes para conferência e acessibilidade. Contagem de leituras fora da especificação separada da contagem de médias. Avisos de ausência de nominal e variação nula.

Painel diário indica análises salvas e conjuntos completos aguardando análise. A data é de criação do conjunto, com estado atual. Programação e cumprimento de metas ainda pertencem à Sprint 3.

## Verificação

- 83 testes automatizados aprovados, zero falhas e zero ignorados na suíte da sprint. Retirada da permissão MSA verificada também por testes focados de perfis após o ajuste.
- 25 verificações HTTP aprovadas, incluindo login, CSRF, bloqueio anônimo, acesso às cartas e ausência dos módulos retirados nos menus e definições.
- Referência aritmética: 25 trios [9,10,11] geram X̄̄=10, R̄=2, LIC X̄=7,954, LSC X̄=12,046 e LSC R=5,148. Testes de deslocamento, pico de amplitude, tendências crescentes/decrescentes, empates, nominal ausente, leituras negativas, igualdade à especificação, contagem individual e de médias, números inválidos e overflow.
- Serviço: rascunho rejeitado, autorização de escrita e consulta, idempotência, reprodução integral do JSON e auditoria; alteração de resultado salvo rejeitada.
- Migração desde o esquema anterior preserva medições existentes. Na demonstração, cópia SQLite anterior à migração mantida em artifacts ignorado.
- Navegador: login admin/admin, análise do conjunto concluído #2, filtro do histórico, reabertura após recarregar e reanálise sem duplicação; tabela de valores conferida; nenhum erro de console observado.
- Responsividade verificada em 768 e 390 pixels: largura de rolagem igual à largura da página. Tabelas usam contêiner com rolagem quando necessário.
- Manual Foxconn complementar em quatro páginas, com prints reais e layout renderizado para conferência.

## Limites e continuidade

O método versionado usa regras fixas exibidas na tela; as definições gerais editáveis ainda não alteram essa análise. Limites estimados nos mesmos 25 subgrupos, sem aprovação ou reutilização automática como limites fixos. Ausência de sinais não é aprovação de produto nem comprovação de estabilidade. Cp/Cpk, programação, investigação com ações no sistema, limites aprovados e homologação SQL Server permanecem nas próximas etapas. As especificações dos dispositivos foram preservadas; nenhum dado de medição foi inventado para eles.

SDK, banco e evidências temporárias não são enviados ao GitHub. A dependência de gráficos é vendorizada com licença e executa localmente. Os procedimentos anteriores continuam como registros históricos; o complemento desta sprint descreve as novas cartas.
