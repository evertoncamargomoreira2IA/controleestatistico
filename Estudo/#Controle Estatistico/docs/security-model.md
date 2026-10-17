# Modelo de segurança F0

## Perfis e permissões

| Perfil | Permissões F0 |
|---|---|
| admin | todas |
| engenheiro | view, collection.create, studies.run, masterdata.manage |
| tecnico | view, collection.create |
| qualidade | view, studies.run, msa.approve |
| leitor | view |

As permissões são verificadas no servidor por `require_permission`; usuários são administrados individualmente.

## Sessão e senha

Cookie assinado por Starlette SessionMiddleware, HttpOnly, SameSite=Lax, com Secure configurável e expiração por inatividade. Argon2 armazena senhas. Limite de falhas, tempo de bloqueio e tamanho mínimo vêm da configuração. Mensagens de login são genéricas. POST exige token CSRF guardado na sessão.

## Auditoria e pendências de produção

`audit_log` registra eventos de autenticação e alterações de usuários com instante UTC, ator, entidade, antes/depois quando aplicável, IP e origem. A aplicação não oferece edição/exclusão da trilha; em produção, imponha também permissões de banco que neguem UPDATE/DELETE. HTTPS, chave forte fora do código, cookie Secure, backup/restore, política de retenção e revisão de segurança ainda precisam ser definidos para produção.

## Admin padrao

Em dev, development ou local, a inicializacao cria admin com senha admin apenas se nao houver usuarios. A troca da senha e obrigatoria no primeiro acesso. Nunca use essa credencial em producao.
