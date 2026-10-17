# Sprint 0 — preparação e verificação da base

Data: 17/10/2026. Status: concluída para desenvolvimento local. Não representa homologação corporativa.

## Base de trabalho

- Origem: `C:/Users/evertonmoreira/Desktop/Claude/#Controle Estatistico/csharp`.
- Continuação: `C:/Users/evertonmoreira/Documents/ChatGPT/Controle Estatistico`.
- Original preservada. Não foram copiados bancos, binários, caches ou histórico Git da origem.
- SDK oficial .NET 10.0.402 instalado localmente em `.tools/dotnet`; sem necessidade de instalação global. Pacotes restaurados em `.nuget/packages`.
- Solução: Domain, Application, Infrastructure, Statistics, Web e Qms.Tests.

## Evidências executadas

| Verificação | Resultado |
|---|---|
| Restauração dos seis projetos | Sucesso |
| Compilação Release | Sucesso, zero erros, dois avisos |
| Suíte xUnit | 43 aprovados, zero falhas, zero ignorados |
| Inicialização da aplicação | Sucesso em Development, localhost:5187 |
| Migrações e seed em SQLite vazio | Sucesso; banco de teste separado |
| Testes HTTP | 18 aprovados |

Testes unitários e de serviços cobrem usuários, permissões, cadastros, instrumentos, definições, validação, coleta, importação e auditoria. Não são testes completos de interface nem testes estatísticos dos módulos ainda não implementados.

As 18 verificações HTTP cobriram página de login; bloqueio de seis rotas para anônimos; rejeição de POST sem token antiforgery; login de demonstração; exigência e execução de troca de senha; acesso administrativo às seis rotas; presença de login e alteração de senha no histórico.

Banco HTTP: `artifacts/sprint0-smoke.db`, separado do banco de desenvolvimento. A senha de demonstração foi substituída por senha aleatória no teste, não exibida nem persistida em texto. A instância temporária foi encerrada após a verificação.

## Melhorias na verificação

- Removido teste que apenas executava `Assert.True(true)`.
- Adicionados três casos reais de turno inválido, conferindo rejeição, ausência de medições persistidas e ausência de auditoria de sucesso.
- Adicionados dois casos para impedir alteração e exclusão de auditoria, verificando a permanência do evento original no banco.
- Scripts: `scripts/verificar.ps1` e `scripts/testar-http.ps1`.
- Evidências locais ignoradas pelo Git: `artifacts/build.binlog`, `artifacts/TestResults/sprint0.trx`, `artifacts/http-smoke.json`.

## Como reproduzir

1. Executar `./scripts/verificar.ps1 -Restore` com acesso ao NuGet na primeira execução; depois basta `./scripts/verificar.ps1`.
2. Para HTTP, iniciar a aplicação com SDK local, ambiente Development, content root de `src/Qms.Web`, URL `http://127.0.0.1:5187` e connection string apontando para um NOVO arquivo SQLite descartável em `artifacts`.
3. Executar `./scripts/testar-http.ps1`. O script pressupõe admin de desenvolvimento recém-criado e troca sua senha; não reutilizar contra banco real ou de produção.
4. Encerrar a instância temporária. Para repetir o cenário de primeiro acesso, usar outro banco descartável.

Neste ambiente, downloads, compilação e comunicação HTTP local precisaram de execução autorizada fora das restrições de rede/processos do sandbox. A compilação restrita falhou sem diagnóstico do compilador; a execução autorizada concluiu normalmente.

## Avisos e limites conhecidos

- CS0649: campo `ExternalLogin.message` não recebe atribuição no template de login externo.
- xUnit2013: teste existente de instrumento verifica quantidade com Assert.Equal em vez de Assert.Single.
- Smoke em HTTP local emitiu aviso de porta HTTPS não configurada. HTTPS corporativo ainda será configurado e testado na homologação.
- Não foram executados testes visuais no navegador, de carga, SQL Server, IIS ou restauração de backup.
- Auditoria é protegida contra alterações pelo DbContext da aplicação; proteção contra acesso administrativo direto ao banco exige controles corporativos adicionais.
- Não há conjuntos CEP 25 × 3 nem estudos de capabilidade implementados nesta sprint.

## Próximo aceite — Sprint 1

Entregar conjunto CEP com 25 amostras de três leituras, rascunho, entrada pelo frontend e seleção da base, persistência das leituras originais e histórico do conjunto. Mostrar progresso de preenchimento (amostras completas/25) sem confundi-lo com programação diária. O planejamento e o quadro diário completo entram na Sprint 3, conforme `roteiro-sprints.md`.
