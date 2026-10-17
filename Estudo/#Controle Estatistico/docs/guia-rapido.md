# Guia rápido — QMS/CEP (versão de teste)

Versão 0.4.0-dev: login e perfis, cadastros, Definições e coleta de amostras.
As cartas CEP, o painel diário e os estudos (Cp/Cpk, Gage R&R, Kappa) chegam nas próximas fases.

> Ambiente de teste local. Os dados ficam em `data\qms.db` (SQLite) no próprio computador.
> Não é a instalação de produção (Windows Server + IIS + SQL Server), que será feita depois.

## 1. Pré-requisito

Python 3.12 instalado no Windows. Para conferir, rode `python --version` no PowerShell.

## 2. Primeira execução

1. Descompacte o pacote numa pasta, por exemplo `C:\QMS-teste`. Ele cria a subpasta `QMS-CEP-teste`; tudo fica dentro dela.
2. Dê dois cliques em **`iniciar.bat`**. Na primeira vez, ele:
   - cria o ambiente Python (`.venv`) e instala as bibliotecas (leva alguns minutos);
   - cria o arquivo `.env` com uma chave secreta aleatória;
   - cria o banco e abre o navegador em http://127.0.0.1:8000.
3. Com o `iniciar.bat` ainda aberto, dê dois cliques em **`criar_admin.bat`** e informe usuário, nome e senha.
   A senha precisa ter pelo menos 12 caracteres, com maiúscula, minúscula e número.
4. Opcional: dê dois cliques em **`dados_exemplo.bat`** para criar a hierarquia de exemplo
   (Montagem → Produto A → Linha 1 → Posto 10 → TL-001 → Altura).

   > Se preferir o PowerShell, primeiro entre na pasta do projeto (`cd C:\QMS-teste\QMS-CEP-teste`)
   > e depois rode `.venv\Scripts\python.exe scripts\create_admin.py`. Fora da pasta, o comando não é encontrado.
5. Entre em http://127.0.0.1:8000 com o usuário criado.

Nas próximas vezes, basta o `iniciar.bat`. Para parar o servidor, use Ctrl+C na janela dele.

## 3. Parametrizar

Faça nesta ordem:

1. **Usuários** (menu Usuários): crie as pessoas uma a uma e escolha o perfil.
   - **admin:** tudo.
   - **engenheiro:** cadastros e coleta.
   - **tecnico:** coleta.
   - **qualidade:** visualização e auditoria.
   - **leitor:** só visualiza.
2. **Cadastros** (menu Cadastros): Área → Produto → Linha → Posto → Tooling → Característica.
   - Para criar um item filho, clique no item pai.
   - Na característica, informe a unidade, LSL, nominal, USL, o tamanho do subgrupo e o tipo de carta (I-MR com n = 1, X̄-R com 2 a 10, X̄-S com 2 a 25).
   - Mudar a especificação gera uma nova revisão.
3. **Instrumentos** (link na tela de Cadastros): código, tipo, resolução e vencimento da calibração. Um instrumento vencido não aceita coleta.
4. **Definições** (menu Definições): faixas de bom, atenção e ruim para Cp, Cpk, Pp e Ppk, Gage R&R e Kappa, além das regras de carta e das frequências. Cada salvamento exige comentário e cria uma nova versão com histórico.

## 4. Subir amostras

- **Online:** menu **Coleta**. Escolha a característica, digite os n valores e informe instrumento, data/hora, turno e lote.
- **Arquivo:**
  1. Em Coleta, clique em **Baixar template XLSX** (ou CSV) e preencha uma linha por subgrupo.
  2. Clique em **Importar arquivo**. A prévia mostra cada linha como OK ou erro.
  3. Confirme. Só as linhas válidas são gravadas, e o mesmo arquivo não pode ser importado duas vezes.
- **Arquivo de exemplo:** `exemplos\coleta_exemplo_altura.csv` tem 25 subgrupos da característica de exemplo "Altura". Rode o `dados_exemplo.bat` antes de importá-lo.

## 5. Problemas comuns

| Sintoma | Solução |
|---|---|
| `python` não é reconhecido | Instale o Python 3.12 e marque "Add to PATH". |
| Esqueci a senha do admin | Crie outro admin com `create_admin.py`; depois, redefina a senha em Usuários. |
| Usuário bloqueado | O bloqueio dura 15 minutos, ou o admin pode redefinir a senha em Usuários. |
| Quero começar do zero | Pare o servidor, apague `data\qms.db` e rode o `iniciar.bat` de novo. |
| A porta 8000 está ocupada | Edite a última linha do `iniciar.bat` e troque `8000` por outra porta. |

## Admin padrao

No primeiro acesso em ambiente de desenvolvimento, use usuario admin / senha admin. O sistema exige a troca da senha antes de continuar. Essa conta padrao nunca deve ser usada em producao.
