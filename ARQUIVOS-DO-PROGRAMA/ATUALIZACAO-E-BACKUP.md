# ATUALIZAÇÃO AUTOMÁTICA E BACKUP

## Como o programa detecta novas versões
1. `novo-win8/Program.cs` consulta a API de Releases do repositório `giliotiesg-pixel/erp-gratidao-updates`.
2. Ignora releases draft/prerelease; compara tags `amzg-novo-win8-build-N` com `Assembly.GetExecutingAssembly().GetName().Version.Build`.
3. Só oferece atualização se N for **maior** que a build instalada.
4. O workflow `.github/workflows/compilar-amzg-novo-win8.yml` gera `1.0.${{ github.run_number }}.0` e publica Release **somente em `main`**, quando as etapas anteriores têm sucesso.
5. Build de branch/PR não é atualização disponível para o usuário. Compilação verde não garante funcionamento completo.

## Processo obrigatório antes de liberar
1. Identificar versão real instalada e último Release da série `amzg-novo-win8-build-`.
2. Confirmar que mudanças são exclusivamente as pedidas; revisar diff.
3. Executar testes de compilação, interface e fluxos existentes, especialmente vendas, fiado, estoque, caixa e persistência de dados.
4. Verificar que o instalador faz **backup completo**, incluindo banco SQLite consistente (com WAL quando aplicável), configurações e arquivos de dados necessários, na pasta configurada; validar restauração. Se não houver essa garantia, **não publicar**.
5. Mesclar somente versão revisada e aprovada na `main`; aguardar workflow de release e conferir EXE e tag publicados.
6. No computador instalado, usar **Verificar atualização**; preservar pasta de dados e configurações. Testar após atualizar.

## Atenção
O script de backup existente deve ser auditado quanto à cobertura de todos os dados e configurações; a presença do script não comprova, por si só, backup completo. Nunca afirmar que a atualização está disponível antes de confirmar Release superior à build instalada.

## Proteção de dados
Não incluir `.db`, `.db-wal`, `.db-shm`, cópias de clientes, vendas, CPF, credenciais ou arquivos de configuração privados em commits públicos. Fazer cópias em local privado com controle de acesso.
