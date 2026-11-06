using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArmazemGratidaoAtualizador {
 static class Program {
  [STAThread] static void Main(string[] args) {
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   Application.Run(new AtualizadorWindow(args));
  }
 }
 sealed class AtualizadorWindow : Form {
  readonly string[] args;
  readonly Label message=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,
   Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=Color.FromArgb(22,75,160)};
  readonly ProgressBar progress=new ProgressBar{Dock=DockStyle.Bottom,Minimum=0,Maximum=100,Value=0,Height=30};
  readonly Button close=new Button{Text="Fechar",Dock=DockStyle.Bottom,Height=40,Visible=false};
  public AtualizadorWindow(string[] arguments){
   args=arguments;
   Text="Armazém Gratidão | Atualizador Automático";
   Width=630;Height=235;MinimumSize=new Size(530,205);
   StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(236,244,254);
   Controls.Add(message);Controls.Add(progress);Controls.Add(close);
   close.Click+=(s,e)=>Close();
  }
  void WriteLog(string text){
   try{
    if(args.Length<4)return;
    string directory=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[3])),"Arquivos","Logs");
    Directory.CreateDirectory(directory);
    string name=Path.Combine(directory,"atualizador-"+DateTime.Now.ToString("yyyyMMdd")+".log");
    lock(logLock)File.AppendAllText(name,DateTime.Now.ToString("o")+" "+text+Environment.NewLine);
   }catch(IOException){}catch(UnauthorizedAccessException){}
  }
  static readonly object logLock=new object();
  void Report(string text,int percent){
   if(IsDisposed)return;
   if(InvokeRequired){BeginInvoke(new Action<string,int>(Report),text,percent);return;}
   message.Text=text;
   WriteLog(text);
   progress.Value=Math.Max(0,Math.Min(100,percent));
  }
  protected override async void OnShown(EventArgs e){
   base.OnShown(e);
   try {
    await Task.Run(()=>Apply());
    Report("Atualização concluída. Abrindo o Armazém Gratidão...",100);
    await Task.Delay(700);
    Close();
   }catch(Exception ex){
    WriteLog("ERRO: "+ex.ToString());
    Report("Não foi possível atualizar. A versão anterior foi preservada ou restaurada.",0);
    MessageBox.Show(this,ex.Message,"Atualização não concluída",MessageBoxButtons.OK,MessageBoxIcon.Error);
    close.Visible=true;
   }
  }
  static bool Allowed(string path){
   return new[]{
    "ArmazemGratidaoNovo.exe","ArmazemGratidaoNovo.exe.config",
    "QRCoder.dll","System.Data.SQLite.dll","zxing.dll","x64/SQLite.Interop.dll"
   }.Contains(path,StringComparer.OrdinalIgnoreCase);
  }
  void Apply(){
   if(args.Length!=6||args[0]!="--apply-zip")
    throw new Exception("Atualizador não pode ser executado diretamente. Use Verificar Atualização no Armazém Gratidão.");
   string zip=Path.GetFullPath(args[1]),expected=args[2],app=Path.GetFullPath(args[3]),
    backup=Path.GetFullPath(args[4]);
   int parentPid;
   if(!int.TryParse(args[5],out parentPid))throw new Exception("Identificador do programa inválido.");
   string dir=Path.GetDirectoryName(app);
   if(!File.Exists(zip)||!File.Exists(Directory.Exists(backup)?Path.Combine(backup,"Dados","armazem-gratidao.db"):backup))
    throw new Exception("Pacote ou backup completo não encontrado. Nenhum arquivo será substituído.");
   if(expected.Length!=64||!expected.All(Uri.IsHexDigit))throw new Exception("SHA-256 inválido.");
   Report("Conferindo integridade do pacote de atualização...",10);
   string actual;
   using(var file=File.OpenRead(zip))using(var sha=SHA256.Create())
    actual=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
   if(!string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase))
    throw new Exception("SHA-256 divergente. Atualização bloqueada.");
   Report("Aguardando o programa fechar para atualizar...",20);
   try{
    var process=Process.GetProcessById(parentPid);
    if(!process.WaitForExit(60000))throw new Exception("O Armazém Gratidão continua aberto. Feche todas as janelas e tente novamente.");
   }catch(ArgumentException){} // O processo já foi encerrado.
   string staging=Path.Combine(Path.GetTempPath(),"amzg-stage-"+Guid.NewGuid().ToString("N"));
   string rollbackRoot=Path.Combine(dir,"Arquivos","Rollback");
   if(!Directory.Exists(rollbackRoot))
    rollbackRoot=Path.Combine(Path.GetDirectoryName(zip),"RollbackArquivos");
   string rollback=Path.Combine(rollbackRoot,
    DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,5));
   Directory.CreateDirectory(staging);Directory.CreateDirectory(rollback);
   var targets=new List<string>();
   var created=new List<string>();
   try{
    Report("Preparando arquivos com segurança...",35);
    targets.AddRange(ZipCompat.Extract(zip,staging,Allowed));
    if(!targets.Contains("ArmazemGratidaoNovo.exe",StringComparer.OrdinalIgnoreCase))
     throw new Exception("Pacote incompleto: executável não encontrado.");
    foreach(string name in targets){
     string dst=Path.Combine(dir,name.Replace('/',Path.DirectorySeparatorChar));
     string previous=Path.Combine(rollback,name.Replace('/',Path.DirectorySeparatorChar));
     Directory.CreateDirectory(Path.GetDirectoryName(previous));
     if(File.Exists(dst))File.Copy(dst,previous,true);
     else created.Add(dst);
    }
    Report("Atualizando os arquivos do programa...",65);
    try{
     foreach(string name in targets.Where(x=>!x.Equals("ArmazemGratidaoNovo.exe",StringComparison.OrdinalIgnoreCase))
      .Concat(targets.Where(x=>x.Equals("ArmazemGratidaoNovo.exe",StringComparison.OrdinalIgnoreCase)))){
      string dst=Path.Combine(dir,name.Replace('/',Path.DirectorySeparatorChar));
      Directory.CreateDirectory(Path.GetDirectoryName(dst));
      File.Copy(Path.Combine(staging,name.Replace('/',Path.DirectorySeparatorChar)),dst,true);
     }
    }catch(Exception failure){
     Report("Revertendo arquivos para a versão anterior...",75);
     var restoreErrors=new List<string>();
     foreach(string name in targets){
      string old=Path.Combine(rollback,name.Replace('/',Path.DirectorySeparatorChar));
      string dst=Path.Combine(dir,name.Replace('/',Path.DirectorySeparatorChar));
      try{if(File.Exists(old))File.Copy(old,dst,true);}catch(Exception ex){restoreErrors.Add(name+": "+ex.Message);}
     }
     foreach(string added in created)try{if(File.Exists(added))File.Delete(added);}catch{}
     throw new Exception("Falhou a substituição: "+failure.Message+
      (restoreErrors.Count>0?"\nRestaurar manualmente: "+string.Join("; ",restoreErrors):"\nArquivos anteriores restaurados.")+
      "\nCópias dos arquivos: "+rollback,failure);
    }
    Report("Reiniciando o Armazém Gratidão...",90);
    try{
     Process.Start(new ProcessStartInfo(app){WorkingDirectory=dir,UseShellExecute=true});
    }catch(Exception ex){
     throw new Exception("Arquivos atualizados, mas não foi possível iniciar automaticamente. Abra o atalho do Armazém Gratidão. "+ex.Message);
    }
   }finally{
    try{Directory.Delete(staging,true);}catch{}
   }
  }
 }
}