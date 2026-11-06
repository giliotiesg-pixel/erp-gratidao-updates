# Auditoria estatica e plano de validacao - build 583

Data: 2026-11-06. Referencia: codigo-fonte `novo-win8/`.
**Escopo**: revisao estatica das fontes disponiveis, scripts de compilacao e
atualizacao; nao constitui certificacao funcional completa.

## Causas e ajustes encontrados
- A build 582 executava backup e procura de vendas historicas na thread de
  interface WinForms ao abrir; o backup integral tambem bloqueava a interface
  ao confirmar a instalacao. Reestruturado para tarefa em segundo plano e
  `SemaphoreSlim` no backup obrigatorio.
- `Application.Idle` verificava repetidamente todos os botoes, formularios e
  grids, mesmo em repouso. Verificacao limitada a intervalos de 1,5 segundo.
- Backup integral gerava SQLite validado e fazia outra copia identica.
  Eliminada a duplicacao; mantido `PRAGMA integrity_check`.
- Busca automatica de vendas historicas agora no maximo uma vez por dia.
- Script geral apontava para `AMZG.App/AMZG.App.csproj`, ausente. Substituido
  por builder do projeto real em `novo-win8/`.
- Projeto WinForms dependia de `BuildVersion.cs` e icone ausentes na fonte
  consultada. Ambos passam a ser gerados pela rotina de compilacao.
- `packages.config` nao listava ZXing.Net, referenciado no projeto. Corrigido.
- Aplicativo e atualizador tinham projeto C# mas sem `OutputPath` definido.
  Ajustado para saida x64 consistente.
- A tela de suporte usava uma pasta separada e link historico; agora abre a
  central `Arquivos` da pasta de instalacao.
- Instalador conserva AppId anterior e usa a mesma fonte para instalar e
  reparar; conserva local de instalacao existente e nao inclui banco real.
- Atualizador grava logs e rollback na pasta central, quando disponivel.

## Fontes inspecionadas
`Program.cs`, `PdvForm.cs`, `ProdutosForm.cs`,
`FiadoClientesForm.cs`, `ConsultaForm.cs`, `ModuloConsultaForm.cs`,
`CascosForm.cs`, `LucroForm.cs`, `PaymentFees.cs`,
`MobileStockServer.cs`, `ConsultaPrecoForm.cs`,
`AtualizadorGratidao.cs`, `ZipCompat.cs`, `ReparadorAtualizacao.cs`,
arquivos .csproj, instalador Inno e scripts de backup e GitHub.

## Itens que ainda exigem teste em Windows
1. Compilacao real em GitHub Actions (projetos .NET 4.8 x64 e Inno Setup).
2. Instalacao nova no Windows 8.1 e Windows 10, com e sem permissao admin.
3. Reparo usando atalho local e banco de producao preservado.
4. Backup completo em banco SQLite de TESTE com transacoes e fiados;
   abrir a copia restaurada, consultar `PRAGMA integrity_check` e contagem.
5. Simular arquivo bloqueado/sem espaco/permissao; garantir rollback e
   nenhuma perda de dados.
6. Validar upgrade ZIP, versao instalada, SHA-256 do manifesto e log.
7. Iniciar o PDV varias vezes em PC de baixo desempenho; verificar uso de
   CPU/disco/memoria, responsividade das telas e backup automatico diario.
8. Registrar vendas, formas de pagamento, estoque, fiado parcial, caixa,
   lucro, relatorios e cascos usando banco **descartavel**.
9. Verificar importacao historica, vendas anteriores e reabertura apos
   atualizar; auditar comandos SQL de escrita e migracao.
10. Garantir que dados e fotos particulares nunca entrem nos artefatos GitHub.

## Pendencias nao declaradas como corrigidas
- Nao ha execucao Windows ou testes de ponta a ponta verificados nesta etapa.
- A seguranca de pastas de backups em PCs multiusuario precisa de politica ACL.
- O atualizador usa analisador ZIP32 proprio: validar arquivos maiores e
  rejeicao de ZIP malicioso; a lista de arquivos permitidos e SHA-256 ajudam.
- A rotina de envio/recebimento mobile precisa de testes reais em rede local.
- A auditoria estatica nao prova integridade de todos os casos de negocio;
  qualquer migracao SQLite precisa de testes sobre copia antes de producao.
