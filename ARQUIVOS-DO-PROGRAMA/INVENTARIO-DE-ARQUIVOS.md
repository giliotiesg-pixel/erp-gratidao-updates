# INVENTÁRIO DE ARQUIVOS

## Código-fonte principal (pasta `novo-win8/`)
- `Program.cs`: formulário principal, Menu Principal, Início/Indicadores, atualizador, backup e navegação.
- `PdvForm.cs`: PDV.
- `ConsultaForm.cs`: vendas/consultas.
- `FiadoClientesForm.cs`: fiado e clientes.
- `ProdutosForm.cs`: produtos/estoque.
- `CascosForm.cs`: cascos retornáveis.
- `ModuloConsultaForm.cs`: módulos de gestão.
- `LucroForm.cs`: lucro.
- `PaymentFees.cs`: taxas de pagamento.
- `MobileStockServer.cs`: integração de estoque pelo celular.
- `ArmazemGratidao.csproj` e `packages.config`: projeto e dependências.
- `instalador.iss`: instalador.
- `backup-antes-instalacao.ps1`: backup executado pelo instalador.

## Automação
- `.github/workflows/compilar-amzg-novo-win8.yml`: compilação e publicação da família de builds reconhecida pelo aplicativo.

## Dados privados no computador (NÃO versionar)
O código atual define `%LOCALAPPDATA%\ArmazemGratidaoNovo` como diretório de dados e `armazem-gratidao.db` como banco principal. A pasta de backup pode ser configurada em `backup-location.txt`. Confirmar outros arquivos de configuração e anexos antes de considerar backup completo.

## O que guardar para uso em outro computador
1. Código-fonte e documentação: GitHub.
2. Instalador validado: Release oficial correspondente à versão.
3. Banco, vendas, fiado e configurações: backup privado e verificado, nunca no GitHub público.
4. Histórico de solicitações: este diretório de documentação e PRs.

## Regras
Não misturar com `amzg-src/`, `amzg/` ou `erp/`, que representam projetos/linhas diferentes. Não inferir que uma build foi publicada a partir de um commit ou artefato de teste.
