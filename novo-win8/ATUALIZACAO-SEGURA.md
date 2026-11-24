# Canal oficial de atualização — Armazém Gratidão

- **Desenvolvimento:** código em main e alterações em branches/PRs. PRs compilam e testam, nunca publicam.
- **Homologação:** workflow único para Windows. Reserva build acima dos releases existentes; compila instalador, PDV e atualizador com mesma versão. Testes funcionais, de layout, reparo, backup, fiado, caixa e cadastro são obrigatórios. O ZIP passa por inspeção integral, lista de arquivos autorizados, versões internas e SHA-256.
- **Oficial:** publicação explícita por [release] ou despacho autorizado. Primeiro release rascunho, verificação dos digests dos dois anexos, depois publicação. O manifesto oficial é sincronizado sobre o HEAD atual, com novas tentativas sem force-push, e conferido remotamente. Nunca retroceder build nem substituir pacote publicado.
- **Cliente:** canal único atualizacao-novo-win8-pacote.json; validação de URL, versão e SHA-256, backup obrigatório, preflight, substituição com comparação de arquivos e rollback em falha.
- **Diagnóstico:** Sistema e Manutenção > Diagnóstico das atualizações. Log local do manifesto, build, download, backup e erro. Atualizador registra substituição/rollback em Arquivos/Logs.
- **Falha:** se testes falharem, não publicar. Se sincronização falhar após release validado, preservar pacote e manifesto anterior; reparar a publicação com o mesmo digest ou publicar build superior após testes. GitHub não oferece transação atômica entre releases e arquivos Git.
- **Operação:** não editar manualmente a build publicada, não reutilizar tags e não remover a proteção de backup. Não publicar versão de teste como oficial.
