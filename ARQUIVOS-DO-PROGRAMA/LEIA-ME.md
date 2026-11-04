# ARMAZÉM GRATIDÃO NOVO — CENTRAL DO PROJETO

Este diretório é o ponto de entrada para continuar a manutenção do programa **já instalado**, sem recriá-lo.

## Identificação inequívoca
- Aplicativo: **Armazém Gratidão Novo** (Windows nativo, C# WinForms, SQLite).
- Código-fonte: [`novo-win8/`](../novo-win8/).
- Projeto: [`novo-win8/ArmazemGratidao.csproj`](../novo-win8/ArmazemGratidao.csproj).
- Formulário principal e verificador de atualizações: [`novo-win8/Program.cs`](../novo-win8/Program.cs).
- Workflow correto: [`compilar-amzg-novo-win8.yml`](../.github/workflows/compilar-amzg-novo-win8.yml).
- Instalador: [`novo-win8/instalador.iss`](../novo-win8/instalador.iss).
- Backup anterior à instalação: [`novo-win8/backup-antes-instalacao.ps1`](../novo-win8/backup-antes-instalacao.ps1).
- Releases reconhecidos pelo atualizador: tags `amzg-novo-win8-build-N`.
- **Build 467**: versão confirmada instalada em 04/11/2026; não confundir com outros projetos/repositórios ou com o AMZG 0.8.3.
- Data desta central: 04/11/2026. Build atual deve ser confirmada a cada atualização, nunca presumida.

## Documentos
- [REQUISITOS-E-ALTERACOES.md](REQUISITOS-E-ALTERACOES.md): pedidos, regras de preservação e telas.
- [ATUALIZACAO-E-BACKUP.md](ATUALIZACAO-E-BACKUP.md): procedimento seguro de atualização.
- [COMANDOS-E-RECUPERACAO.md](COMANDOS-E-RECUPERACAO.md): recuperar código em outro computador.
- [INVENTARIO-DE-ARQUIVOS.md](INVENTARIO-DE-ARQUIVOS.md): mapa de arquivos e dados.

## Regra de segurança
**Não substituir o programa instalado por outro projeto.** Não sobrescrever ou publicar bancos de dados, vendas, fiados, dados pessoais, senhas ou backups no GitHub. Validar backup, compilação e compatibilidade antes de publicar. O código estar no GitHub não significa que uma versão nova foi liberada.
