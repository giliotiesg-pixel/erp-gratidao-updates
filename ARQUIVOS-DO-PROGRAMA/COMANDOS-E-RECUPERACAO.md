# COMANDOS E RECUPERAÇÃO EM OUTRO COMPUTADOR

## Obter o projeto
No PowerShell (com Git instalado):

```powershell
git clone https://github.com/giliotiesg-pixel/erp-gratidao-updates.git
cd erp-gratidao-updates
git pull origin main
```

Para consultar alterações ainda em revisão:

```powershell
git fetch origin
git switch feature/novo-win8-menu-inicial-build467
```

**Não instalar a versão de testes por cima da Build 467.** A ramificação de testes não representa um Release aprovado.

## Arquivos para abrir
- Solução/projeto: `novo-win8/ArmazemGratidao.csproj`
- Entrada do aplicativo: `novo-win8/Program.cs`
- Workflow: `.github/workflows/compilar-amzg-novo-win8.yml`
- Instalador: `novo-win8/instalador.iss`

## Compilar
Preferir o workflow oficial do GitHub Actions para restaurar dependências, validar rotinas e compilar. Para execução local, usar Visual Studio/MSBuild compatível com .NET Framework 4.8, dependências NuGet do projeto e Inno Setup. Não executar instalação/atualização em máquina com dados reais sem backup testado.

## Consultar releases
```powershell
Start-Process "https://github.com/giliotiesg-pixel/erp-gratidao-updates/releases"
Start-Process "https://github.com/giliotiesg-pixel/erp-gratidao-updates/actions/workflows/compilar-amzg-novo-win8.yml"
```

## Recuperar dados em outro local
O código-fonte **não contém** os dados de vendas/fiado do computador. Para migrar os dados, usar backup privado, validado e consistente do SQLite, incluindo as configurações necessárias, com o aplicativo fechado. Não copiar apenas o `.db` de uma sessão ativa sem tratar transações WAL. Nunca colocar backup de clientes no GitHub público.

## Para continuar o desenvolvimento
Basta informar: "Continue o Armazém Gratidão Novo a partir de `novo-win8` e leia `ARQUIVOS-DO-PROGRAMA/LEIA-ME.md`". As instruções e o histórico técnico do projeto ficam neste repositório.
