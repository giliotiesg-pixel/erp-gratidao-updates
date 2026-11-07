# Armazem Gratidao Novo - instalacao, reparo e atualizacoes em um lugar

## Resultado pretendido
Um unico instalador: `Armazem-Gratidao-Novo-Setup.exe`.
A instalacao guarda uma copia dele como `Reparar-Armazem-Gratidao.exe`.
O atualizador grafico `AtualizadorGratidao.exe` acompanha o mesmo instalador.
O aplicativo continua nativo C# WinForms + SQLite (.NET Framework 4.5 (compativel com Windows 8), x64).

## Organizacao
Novas instalacoes utilizam `C:\ERP-Gratidao\` por padrao.
Instalacoes existentes preservam a pasta anterior registrada pelo Windows,
para evitar criar um segundo PDV por engano.

```text
<Pasta do programa>/
  ArmazemGratidaoNovo.exe
  AtualizadorGratidao.exe
  Reparar-Armazem-Gratidao.exe   (copia do mesmo instalador)
  QRCoder.dll
  zxing.dll
  System.Data.SQLite.dll
  x64/SQLite.Interop.dll
  Arquivos/
    Atualizacoes/               (pacotes ZIP baixados)
    Backups/                    (padrao novo; respeitar pasta antiga configurada)
    Logs/                       (historico de falhas do atualizador)
    Rollback/                   (arquivos anteriores para restauracao)
    LEIA-ME.txt
```

**Dados ativos**: continuam em `%LOCALAPPDATA%\ArmazemGratidaoNovo\armazem-gratidao.db`.
**Nao mover nem sobrescrever o banco de producao** para reorganizar pastas.
O programa continua respeitando `backup-location.txt` do perfil do usuario
se uma pasta de backup ja tiver sido selecionada.

## Uso
- Abrir o aplicativo: atalho **Armazem Gratidao Novo**.
- Reparar arquivos: atalho **Reparar Armazem Gratidao** (usa o mesmo instalador).
- Abrir a central: botao **Central de manutencao** no menu principal.
- Atualizacao normal: **Verificar Atualizacao**. O manifesto especifico do
  Windows 8/10 e `atualizacao-novo-win8-pacote.json`.
- Toda atualizacao tem ZIP validado por SHA-256, copia SQLite consistente e
  backup dos arquivos de dados. Sem backup valido, nao modifica os binarios.
- Instalacao/reparo em pasta protegida requer a autorizacao UAC normal do Windows.
  O programa solicita a autorizacao; nao tenta contornar seguranca do sistema.

## GitHub
O codigo e desenvolvido em `novo-win8/`.
A automacao `.github/workflows/amzg-novo-unificado.yml` gera:
1. instalador unico com reparador integrado;
2. ZIP incremental para o atualizador independente;
3. manifesto proposto e SHA-256 dos pacotes.

Releases sao **manuais**: primeiro compilar e testar em Windows,
depois executar workflow na branch `main` com `publicar_release=true`.
O workflow so publica o manifesto **depois** de confirmar os arquivos no Release.
Nunca adicionar `.db`, historico de vendas, dados de clientes ou backups ao GitHub.

## Seguranca de dados e limitacoes
- Instalador, backup e atualizacao nunca devem apagar a base em AppData.
- Backup e arquivos anteriores de reparo devem permanecer acessiveis.
- O instalador e o patch **ainda exigem testes funcionais no Windows**
  antes de publicar build 583.
- A compilacao da branch de trabalho **nao substitui** a build 582 instalada.
- Em maquinas com varios usuarios Windows, revisar permissoes da pasta
  compartilhada de backups antes de guardar dados confidenciais nela.
