# Auditoria estatica e plano de validacao - build 584

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

## Compatibilidade Windows 8 original
O Windows 8 original vem com .NET Framework 4.5; nao admite .NET Framework 4.8.
A build 583 gerada para .NET Framework 4.8 causou erro de Metodo nao encontrado: System.Array.Empty() no Windows 8.
A build 584 passa a mirar .NET Framework 4.5, incluindo atualizador e dependencias SQLite selecionadas para esse runtime.
Necessario testar no Windows 8 64 bits real (o runner Windows Server nao reproduz a versao antiga do .NET).

## Revisao geral solicitada - 18/11/2026 (nao certificada)
O usuario solicitou revalidar **todos** os pedidos anteriores e republicar somente apos
compilacao, testes funcionais e instalador aprovados. O status de uma alteracao
no GitHub nao prova que ela esteja instalada ou funcional.

### Ultimo estado confirmado no GitHub Actions
- Compilacao do commit 1333cf4b: **success** (run 44223513646).
- Instalador/reparo do mesmo commit: **failure** (run 44223513736).
- Etapa que falhou: teste visual do PDV (run 44223513736, job 134530958949).
- Mensagem: "O cadastro de produtos nao esta com titulo e tamanho compactos."
- Publicacao de release: nao executada por causa da falha.
- Nao instalar ou anunciar atualizacao como pronta antes de liberar todos os gates.

### Matriz de revisao obrigatoria (pendente ate teste real)
1. Todas as janelas: maximizar/restaurar, dimensoes adaptadas ao monitor,
   campos sem alongamento, cabeçalhos visiveis, barras de rolagem e botoes acessiveis.
2. Produtos: cadastro e edicao com TODOS os campos e pesquisas existentes,
   custo real, preco sugerido, lucro/margem, salvar, pesquisa por codigo e descricao,
   QR individual e consulta online opcional.
3. PDV Windows: pesquisa/bipagem, carrinho, estoque, pagamentos divididos,
   Pix, dinheiro/troco, cartao, fiado, cliente, taxas, cupom e historico.
4. Vendas: dia/anteriores, edicao, exclusao, auditoria, importacao direta das
   anotacoes retroativas sem re-finalizar e recalculo de totais.
5. Estoque: baixa atomica, entradas autorizadas, historico, peso, QR e
   sincronizacao com produtos e celular.
6. Clientes e Fiado: saldo aberto por cliente, credito autorizado,
   pagamentos parciais, reabertura de baixa, estornos auditados e nenhuma
   criacao de credito ficticio.
7. Caixa: abertura, troco, sangria, fechamento, edicao autorizada e
   consulta de caixas anteriores.
8. Cascos retornaveis: registro por cliente/venda, devolucao e saldo.
9. Lucro/dizimo: custo, margem, 10% do lucro, reserva e devolucoes.
10. Metas de vendas: indicadores, projecao mensal e tabela sem sobreposicao.
11. Valores monetarios: exatamente duas casas na apresentacao, sem
    modificar precos cadastrados; quantidades/pesos mantem precisao propria.
12. PDV celular: QR no carrinho, autenticacao, venda finalizada diretamente
    no banco do computador, idempotencia, estoque e historico sincronizados.
    **Nao declarar finalizacao movel pronta antes do teste ponta a ponta.**
13. Atualizacao: backup SQLite completo com integridade verificada antes de
    substituir, rollback, hash SHA-256, release valido, instalador unico,
    reparo e logs, preservando banco real e versoes anteriores.
14. Compatibilidade: Windows 8/8.1/10/11, resolucoes diferentes,
    controles DPI e operacao com teclado/mouse.
15. Regressao: usar banco de teste descartavel; testar telas, SQL, instalacao,
    backup/restauracao e atualizacao antes de liberar release.

### Regras de aceite
- Cada item exige evidencias de teste; codigo salvo = implementado,
  compilacao = apenas compilado, teste = validado, release = publicavel.
- Falhas devem ser corrigidas na causa, sem desativar testes para esconder
  defeitos. Atualizar verificacoes antigas somente se contradizem requisito
  atual, mantendo testes de nao sobreposicao e legibilidade.
- Nunca sobrescrever dados de producao para testar.
