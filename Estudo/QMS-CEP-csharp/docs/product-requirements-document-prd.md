# PRD — Plataforma QMS/CEP (versão servidor)

Status: rascunho para validação com o usuário · Data: 2026-10-15

Este documento detalha a necessidade descrita pelo usuário e a divide em módulos e scripts implementáveis. Os arquivos de referência (procedimento de Controle de Tooling, planilha de Gage R&R e planilha de Kappa) foram usados **apenas para entender o processo**; seus valores e critérios não são adotados como regra. Todo critério de aceitação será configurável em **Definições**.

Os pontos marcados **[DECIDIR]** precisam de confirmação antes da fase correspondente.

---

## 1. Objetivo

Aplicação web instalável em servidor interno para:

1. Coletar medições de características de controle (por tooling/dispositivo) de forma rastreável.
2. Acompanhar cartas CEP diárias e a lista de índices Cp/Cpk (Pp/Ppk) num painel principal.
3. Executar estudos interativos no estilo Minitab: capabilidade (histograma, curva, estatísticas), Gage R&R e Kappa.
4. Manter histórico e trilha de auditoria de tudo que foi coletado, calculado e alterado.
5. Permitir que um administrador parametrize o que é "bom", "atenção" ou "ruim" para cada tipo de estudo.

Fora do escopo inicial: integração automática com MES/ERP, leitura direta do cabo do relógio comparador, envio por Microsoft Teams. Ficam como fases futuras (seção 9).

---

## 2. Perfis e controle de acesso

| Perfil | Pode fazer |
|---|---|
| **Administrador** | Tudo, incluindo usuários, perfis, Definições e cadastros-mestre. |
| **Engenheiro** | Cadastros de característica/tooling, parâmetros de estudo, limites de controle, análises, comentários e ações. |
| **Técnico** | Coletar amostras (online ou upload), ver cartas e estudos, registrar comentário/ação imediata. |
| **Qualidade** | Ver tudo, executar e aprovar estudos de MSA (Gage R&R, Kappa), registrar disposição. |
| **Leitor** | Apenas visualizar painel, cartas e relatórios. |

Requisitos:

- Tela de login com usuário e senha; senha armazenada com hash forte (bcrypt/argon2), nunca em texto.
- Sessão com expiração por inatividade; bloqueio após N tentativas falhas (N em Definições).
- Permissões verificadas **no servidor** em cada rota, não só escondendo botões.
- Usuário pode ter escopo restrito (ex.: só uma Área ou Linha). **[DECIDIR]** se isso é necessário na fase 1.
- Login corporativo (AD/Entra ID) fica como evolução. **[DECIDIR]** se é obrigatório desde o início.

---

## 3. Modelo de dados (conceitual)

Hierarquia de rastreabilidade, conforme o processo de tooling:

```
Área → Produto → Linha → Posto → Tooling (código, descrição) → Característica
                                             ↘ Instrumento (ID relógio/gage, calibração)
```

Entidades principais:

- **Característica**: nome, unidade, LSL, alvo/nominal, USL, tipo (contínua/atributo), tamanho do subgrupo, tipo de carta, revisão.
- **Limites de controle**: valores calculados ou fixados, base usada, data de cálculo, validade e quem aprovou. Mantém histórico de revisões.
- **Coleta / Amostra**: característica, instrumento, operador, data/hora, turno, lote, valores, origem (online/upload/importação), subgrupo.
- **Estudo de capabilidade**: dados usados, parâmetros, resultados, versão do método, autor, status.
- **Estudo Gage R&R**: peças × avaliadores × repetições, método, resultados.
- **Estudo Kappa**: itens com padrão × avaliadores × ensaios, resultados.
- **Ocorrência/sinal**: regra violada, ponto, data, status, comentário, ação imediata, ação definitiva, responsável, conclusão.
- **Definições**: critérios e faixas por tipo de estudo, regras de carta ativas, frequências.
- **Usuário, Perfil, Permissão**.
- **Auditoria** (append-only): ator, instante, entidade, ação, valores antes/depois, origem.

---

## 4. Telas e funcionalidades

### 4.1 Login
Usuário, senha, "esqueci a senha" (fluxo via administrador na fase 1), mensagem de erro genérica.

### 4.2 Painel principal (dashboard)
- Filtros: Área, Produto, Linha, Posto, Tooling, período (padrão: hoje).
- **Cartas CEP do dia**: miniaturas por característica, com o último ponto e um indicador de sinal. Clique abre a carta completa.
- **Lista de Cp/Cpk**: tabela com característica, tooling, n, Cp, Cpk, Pp, Ppk, data do último estudo e status colorido conforme Definições (bom/atenção/ruim). Ordenável e exportável.
- Pendências: sinais abertos sem ação, coletas atrasadas, limites de controle vencidos, MSA vencido.

### 4.3 Caixa de parâmetros da execução
Painel reutilizado em todos os estudos, com:
- Característica (ou entrada manual de LSL/alvo/USL/unidade).
- Tamanho do subgrupo e tipo de carta.
- Período e filtros dos dados.
- Método de σ: within (R̄/d₂, S̄/c₄ ou MR̄/d₂) e overall (s amostral).
- Opções: transformação/teste de normalidade, número de classes do histograma, mostrar limites de especificação.
- Botão **Executar**, com resultado reproduzível (parâmetros salvos junto ao resultado).

### 4.4 Cartas CEP
- Tipos: X̄-R (n 2–10), X̄-S (n > 10), I-MR (n = 1).
- Limites de controle calculados a partir dos dados ou **fixados** a partir de uma base aprovada (ex.: base de NPI), com data de validade e lembrete de recálculo.
- Regras de sinal configuráveis em Definições: ponto fora de 3σ, sequência de N pontos de um lado da linha central, tendência de N pontos subindo/descendo, proximidade de X% do limite de especificação, e as regras Nelson/Western Electric que forem habilitadas.
- Pontos com sinal ficam destacados. Clique no ponto mostra detalhes e permite comentário/ação.
- Especificação e limites de controle sempre visualmente distintos.

### 4.5 Estudo de capabilidade (Cp/Cpk) estilo Minitab, interativo
- Histograma das amostras com curvas normais within e overall sobrepostas, linhas LSL/alvo/USL.
- Estatísticas: N, média, desvio within e overall, mín./máx., Cp, Cpk, Cpu, Cpl, Pp, Ppk, Ppu, Ppl, PPM observado e esperado (within/overall).
- Gráfico de probabilidade normal com teste Anderson-Darling e p-valor.
- Mini carta de controle e gráfico das últimas amostras (estilo "Capability Sixpack").
- Zoom, hover com valores, exportação PNG/PDF/Excel.
- Avisos automáticos: processo instável, normalidade rejeitada, amostra pequena, MSA ausente ou reprovado.

### 4.6 Gage R&R (contínuo)
- Montagem do estudo: peças, avaliadores, repetições, tolerância, instrumento.
- Entrada online em grade ou upload/template.
- Métodos: médias e amplitudes, e ANOVA (com e sem interação).
- Resultados: EV, AV, interação, R&R, PV, TV, %Contribuição, %Variação do estudo, %Tolerância, ndc.
- Gráficos: componentes de variação, carta R por avaliador, carta X̄ por avaliador, medição por peça, medição por avaliador, interação peça × avaliador.
- Classificação conforme critérios das Definições.

### 4.7 Kappa (atributos)
- Montagem: itens com valor de referência (padrão), avaliadores, ensaios, ordem **aleatória** gerada pelo sistema e identificação cega dos itens.
- Entrada online ou upload/template.
- Resultados: concordância e Kappa dentro do avaliador, entre avaliadores e de cada avaliador vs padrão; efetividade, taxa de "deixar passar" e taxa de alarme falso; intervalos de confiança.
- Gráficos de concordância por avaliador.
- Classificação conforme Definições.

### 4.8 Coleta de amostras
Três formas, todas com a mesma validação:
1. **Online, uma a uma** (ou por subgrupo), com seleção Área → Produto → Linha → Posto → Tooling, ID do instrumento e código do tooling.
2. **Upload** de CSV/XLSX.
3. **Gerar template** de planilha já com cabeçalho, característica e campos de rastreio, para preencher e carregar.

Validação: valores numéricos, tamanho do subgrupo, campos de rastreio obrigatórios, duplicidade (idempotência), valores fora de faixa plausível. Upload mostra prévia com erros por linha antes de gravar.

### 4.9 Histórico e auditoria
- Histórico por característica: coletas, estudos, revisões de limites, sinais e ações.
- Trilha de auditoria somente-anexação, filtrável por usuário, entidade e período.
- Estudos são imutáveis depois de aprovados; uma correção gera nova versão.

### 4.10 Definições (parametrização do que é bom ou ruim)
- Faixas por índice: Cp, Cpk, Pp, Ppk (ex.: ruim < x ≤ atenção < y ≤ bom), por padrão global com possibilidade de exceção por produto/cliente.
- Gage R&R: %R&R (estudo e tolerância), ndc mínimo.
- Kappa: Kappa mínimo, efetividade mínima, taxa máxima de "deixar passar" e de alarme falso.
- Regras de carta ativas e seus parâmetros (N pontos, % da especificação).
- Frequências: coleta, estudo de capabilidade, recálculo de limites, validade de MSA.
- Segurança: tentativas de login, expiração de sessão.
- Toda alteração em Definições é auditada e versionada; cada resultado registra a versão das Definições usada.

### 4.11 Ocorrências e ações (fase 2)
Sinal de carta ou índice ruim gera ocorrência com comentário, ação imediata (técnico), ação definitiva e responsável (engenheiro), anexos e conclusão. Notificação por e-mail.

### 4.12 Relatórios e exportação
Relatório PDF de estudo (capabilidade, Gage R&R, Kappa), exportação Excel dos dados e do resultado.

---

## 5. Requisitos não funcionais

- Interface em português, responsiva para desktop e tablet.
- Cálculos estatísticos em módulo separado da interface, determinísticos, versionados e cobertos por testes com conjuntos de referência (resultados conferidos com Minitab ou cálculo manual documentado).
- Não afirmar conformidade certificada com AIAG/IATF/ISO; constantes e métodos documentados em `docs/spc-formulas.md` e `docs/msa-formulas.md`.
- Servidor: HTTPS, segredos fora do código, backup do banco, logs de aplicação.
- Desempenho alvo: painel abre em menos de 2 s com até 500 características ativas. **[DECIDIR]** volume real esperado.

---

## 6. Arquitetura proposta

O usuário pediu HTML ou Python para hospedar em servidor, e esta máquina não tem Node. A proposta:

| Camada | Escolha | Motivo |
|---|---|---|
| Backend | **Python 3.12 + FastAPI** | Estatística madura (numpy, scipy, pandas, statsmodels), API e páginas no mesmo serviço. |
| Interface | HTML renderizado no servidor (Jinja2) + **HTMX** + **Plotly.js** | Gráficos interativos tipo Minitab sem build de frontend. |
| Banco | **SQLAlchemy + Alembic**; SQLite em desenvolvimento, **SQL Server** em produção | Mesmo código nos dois ambientes; SQL Server alinhado à arquitetura-alvo. **[DECIDIR]** banco do servidor. |
| Autenticação | Sessão com cookie seguro, hash argon2/bcrypt, RBAC próprio | Simples para fase 1; AD/Entra ID depois. |
| Arquivos | openpyxl / pandas para template e upload | Compatível com Excel. |
| Relatórios | WeasyPrint ou exportação via navegador **[DECIDIR]** | |
| Execução | Uvicorn como serviço Windows/Linux atrás de IIS ou Nginx | **[DECIDIR]** sistema operacional do servidor. |

Desvio registrado em relação à arquitetura-alvo da skill (.NET + React): o usuário pediu explicitamente Python/HTML. O motor estatístico isolado em `qms/stats` facilita migrar ou expor como serviço no futuro.

O protótipo estático atual (`index.html`, `src/*.js`) passa a ser referência de interface e de testes; os cálculos serão portados para Python e conferidos com os mesmos casos.

---

## 7. Divisão em scripts

```
app/
  main.py                 # cria o FastAPI, monta rotas, middlewares, arquivos estáticos
  config.py               # configurações por ambiente (.env), sem segredos no código
  db.py                   # engine/sessão SQLAlchemy
  models/                 # tabelas: hierarquia, característica, coleta, estudos, definições, usuários, auditoria
  schemas/                # validação de entrada/saída (Pydantic)
  auth/
    security.py           # hash de senha, sessão, bloqueio por tentativas
    rbac.py               # perfis, permissões, dependências de rota
  services/
    audit.py              # grava trilha append-only
    settings_service.py   # lê/versiona Definições e classifica resultados (bom/atenção/ruim)
    import_service.py     # upload CSV/XLSX, prévia, validação, idempotência
    template_service.py   # gera planilhas-modelo para coleta, Gage R&R e Kappa
    report_service.py     # PDF/Excel dos estudos
  stats/                  # MOTOR ESTATÍSTICO PURO, sem web nem banco
    constants.py          # A2, D3, D4, d2, c4, K1, K2, K3 com fonte e versão
    descriptive.py        # média, desvios, quantis, histograma
    control_charts.py     # X̄-R, X̄-S, I-MR, limites calculados/fixados
    rules.py              # regras de sinal configuráveis
    capability.py         # Cp/Cpk/Pp/Ppk, PPM observado/esperado
    normality.py          # Anderson-Darling, probabilidade normal
    gage_rr.py            # médias e amplitudes + ANOVA
    kappa.py              # Fleiss/Cohen, efetividade, miss, alarme falso, ICs
  routes/
    auth_routes.py        # login/logout
    dashboard.py          # painel principal
    master_data.py        # cadastros
    collection.py         # coleta online, upload, template
    spc.py                # cartas
    capability.py         # estudo Cp/Cpk
    msa_gage.py           # Gage R&R
    msa_kappa.py          # Kappa
    history.py            # histórico e auditoria
    settings.py           # Definições
    users.py              # usuários e perfis
  templates/              # páginas Jinja2 (base, login, dashboard, cada módulo)
  static/                 # CSS, Plotly.js local, JS de gráficos
migrations/               # Alembic
tests/
  stats/                  # casos de referência para cada cálculo
  api/                    # permissões, validação, fluxos principais
scripts/
  create_admin.py         # cria o primeiro administrador
  seed_demo.py            # dados de demonstração
docs/                     # PRD, arquitetura, fórmulas, regras, deploy, manual
```

---

## 8. Fases de entrega

| Fase | Entrega | Pronto quando |
|---|---|---|
| **F0 Base** | Esqueleto FastAPI, banco, migrações, login, perfis, auditoria, layout | Login funciona, rotas protegidas por perfil, auditoria grava login. |
| **F1 Cadastros e Definições** | Hierarquia, características, instrumentos, usuários, tela de Definições | Admin cadastra tudo e define faixas bom/atenção/ruim. |
| **F2 Coleta** | Coleta online, upload com prévia, geração de template | Dados entram pelas três vias com a mesma validação e rastreio. |
| **F3 CEP e Painel** | Cartas X̄-R/X̄-S/I-MR, regras configuráveis, painel diário, lista Cp/Cpk | Painel mostra cartas do dia e a lista colorida. |
| **F4 Capabilidade** | Estudo interativo com histograma, normalidade, sixpack, export | Resultado bate com casos de referência. |
| **F5 Gage R&R** | Estudo completo, gráficos, classificação | Resultado bate com caso de referência. |
| **F6 Kappa** | Estudo completo com ordem aleatória e cega | Resultado bate com caso de referência. |
| **F7 Ocorrências, relatórios e deploy** | Ocorrências/ações, e-mail, PDF, guia de instalação | Instalado no servidor de homologação. |

Cada fase termina com testes executados e documentação atualizada.

---

## 9. Evoluções futuras

Login corporativo (AD/Entra ID), integração direta com o relógio comparador, notificações no Teams, integração MES/ERP, multi-site, PDCA completo, MSA de linearidade e estabilidade, Cm/Cmk.

---

## 10. Decisões pendentes

1. Sistema operacional e servidor web onde será instalado (Windows/IIS ou Linux/Nginx).
2. Banco de produção (SQL Server disponível? ou PostgreSQL/SQLite).
3. Login local na fase 1 é suficiente, ou precisa de AD desde o início?
4. Escopo de acesso por Área/Linha já na fase 1?
5. Volume esperado (características ativas, coletas por dia).
6. Formato preferido de relatório (PDF, Excel ou ambos).
