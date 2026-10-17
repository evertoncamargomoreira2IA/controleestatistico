# Sprint 1 — Coleta CEP

Entrega: conjunto de 25 amostras com três leituras, média e amplitude sem arredondamento intermediário, rascunho persistido, conclusão com 75 leituras e consulta somente leitura após conclusão. Entrada digitada, colagem do Excel e seleção explícita da base importada. Rastreio por instrumento, operador, data, turno, lote e revisão da característica.

Histórico transacional com snapshots antes/depois e controle de versão. Painel diário por data de criação em São Paulo, filtro de processo e progresso de preenchimento. O painel mostra o estado atual dos conjuntos, não uma fotografia histórica do encerramento de cada dia.

## Verificação

- 76 testes automatizados aprovados, zero falhas e zero ignorados. Incluem autorização, validação, persistência parcial, concorrência, conclusão, seleção da base, calendário local e 11 especificações.
- Migração SQLite do esquema anterior preservando medições existentes, verificada em teste automatizado.
- 21 verificações HTTP aprovadas: login admin/admin, sessão, CSRF, redirecionamento anônimo e rotas autenticadas.
- Navegador: criar, colar, salvar e reabrir cinco amostras; importar CSV sintético com 25 subgrupos válidos; selecionar 25 registros da base e concluir; conferir histórico e painel diário; verificar largura de tablet sem transbordamento da página.
- Método de uso Word, oito páginas, gerado a partir do procedimento Foxconn fornecido e prints reais. Renderização e verificação de layout, campos de página e preservação do arquivo de referência.
- Ficha Excel vazia 25 × 3 para validação do usuário: média e amplitude com três leituras numéricas, sem exibir zero como resultado de dados ausentes. Download em `/cep-coleta`; copiar B8:D32 para a colagem. Upload à base utiliza o template específico em `/coleta`.

## Cadastros e documentação

11 dispositivos carregados apenas na demonstração Development, conforme imagem fornecida. Tolerâncias assimétricas preservadas; IDs provisórios; revisões existentes não sobrescritas. Sem medições artificiais para esses dispositivos. Acesso local solicitado: admin/admin, sem relaxar a política geral nem habilitar essa exceção em produção.

Código consolidado na raiz C#. Protótipos e cópias antigas retirados da árvore atual, preservados no histórico Git. SDK, bancos, pacotes e evidências temporárias ignorados. Workflow de build e testes preparado para GitHub Actions. Skills atualizadas na raiz e em `skills/qms-cep-sprint-01`.

## Próxima sprint

Sprint 2: motor X̄-R, cartas, distribuição lateral, regras de tendência e limites conforme referência visual. Cp/Cpk e método para 33 médias exigem validação explícita na sprint correspondente. SQL Server, servidor corporativo, HTTPS, carga, backup e restauração permanecem para homologação.
