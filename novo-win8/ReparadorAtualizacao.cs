using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Forms;

namespace ArmazemGratidaoReparo {
 static class Entry {
  [STAThread] static void Main(){
   Application.EnableVisualStyles();
   Application.SetCompatibleTextRenderingDefault(false);
   Application.Run(new RepairWindow());
  }
 }
 sealed class RepairWindow:Form {
  readonly TextBox installation=new TextBox{Left=18,Top=79,Width=475,ReadOnly=true};
  readonly Label status=new Label{Left=18,Top=129,Width=590,Height=68,
   Text="Este reparador corrige o Verificar Atualizacao sem desinstalar o Armazem Gratidao. Faz backup antes de substituir arquivos.",
   ForeColor=Color.FromArgb(24,70,145),Font=new Font("Segoe UI",10)};
  readonly Button repair=new Button{Left=18,Top=210,Width=275,Height=49,Text="REPARAR E ATUALIZAR",
   BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold)};
  readonly Button cancel=new Button{Left=312,Top=210,Width=120,Height=49,Text="FECHAR"};
  readonly Button browse=new Button{Left=504,Top=77,Width=93,Height=28,Text="Localizar"};
  readonly ProgressBar progress=new ProgressBar{Left=18,Top=279,Width=579,Height=22,Minimum=0,Maximum=100};
  string installDir=FindInstallationDirectory();
  static readonly string[,] files={
   {"AMZG.Payload.Program","ArmazemGratidaoNovo.exe"},
   {"AMZG.Payload.Qr","QRCoder.dll"},
   {"AMZG.Payload.Barcode","zxing.dll"},
   {"AMZG.Payload.SQLite","System.Data.SQLite.dll"},
   {"AMZG.Payload.Native","x64/SQLite.Interop.dll"},
   {"AMZG.Payload.Updater","AtualizadorGratidao.exe"}
  };
  static string FindInstallationDirectory(){
   string defaultDir=@"C:\ERP-Gratidao";
   var paths=new List<string>();
   // O instalador Inno guarda a pasta efetivamente escolhida pelo usuario.
   try{
    foreach(RegistryKey root in new[]{Registry.LocalMachine,Registry.CurrentUser}){
     foreach(string viewPath in new[]{
      @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{8B8E23C2-9EB0-4E9C-9B7A-65B9A1E3AB70}_is1",
      @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{8B8E23C2-9EB0-4E9C-9B7A-65B9A1E3AB70}_is1"
     }){
      using(var key=root.OpenSubKey(viewPath)){
       string value=key==null?"":Convert.ToString(key.GetValue("InstallLocation"));
       if(!string.IsNullOrWhiteSpace(value))paths.Add(value.Trim('"'));
      }
     }
    }
   }catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
   paths.Add(defaultDir);
   paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Armazem Gratidao Novo"));
   paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Armazem Gratidao Novo"));
   foreach(string path in paths.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)){
    try{if(File.Exists(Path.Combine(path,"ArmazemGratidaoNovo.exe")))return Path.GetFullPath(path);}
    catch(ArgumentException){}catch(NotSupportedException){}
   }
   return defaultDir;
  }
  static string FindUserDataDirectory(){
   string local=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
   if(File.Exists(Path.Combine(local,"armazem-gratidao.db")))return local;
   // Windows pode elevar o reparador com uma conta de administrador diferente.
   // Procura SOMENTE a pasta oficial do ERP, sem tocar em arquivos de outros apps.
   var matches=new List<string>();
   try{
    string users=Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    if(!string.IsNullOrWhiteSpace(users)&&Directory.Exists(users)){
     foreach(string profile in Directory.GetDirectories(users)){
      string candidate=Path.Combine(profile,"AppData","Local","ArmazemGratidaoNovo");
      try{if(File.Exists(Path.Combine(candidate,"armazem-gratidao.db")))matches.Add(candidate);}
      catch(UnauthorizedAccessException){}
     }
    }
   }catch(UnauthorizedAccessException){}catch(IOException){}
   if(matches.Count==1)return matches[0];
   // Em caso de multiplos usuarios, o reparador nao seleciona banco de terceiros.
   return local;
  }
  string SelectDataDirectory(){
   string detected=FindUserDataDirectory();
   if(File.Exists(Path.Combine(detected,"armazem-gratidao.db")))return detected;
   MessageBox.Show(this,
    "Nao encontrei o banco de dados na pasta padrao deste usuario. Isso pode acontecer se o reparador foi aberto como outra conta do Windows."+
    Environment.NewLine+"Escolha a pasta ArmazemGratidaoNovo que contem armazem-gratidao.db."+
    Environment.NewLine+"Nenhum dado sera apagado.",
    "Localizar dados sem alterar o programa",MessageBoxButtons.OK,MessageBoxIcon.Information);
   using(var dialog=new FolderBrowserDialog{
    Description="Selecione a pasta de dados que contem armazem-gratidao.db",
    SelectedPath=Directory.Exists(detected)?detected:Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}){
    if(dialog.ShowDialog(this)!=DialogResult.OK)return null;
    if(!File.Exists(Path.Combine(dialog.SelectedPath,"armazem-gratidao.db"))){
     MessageBox.Show(this,"Essa pasta nao contem o banco armazem-gratidao.db. Reparo cancelado sem alterar arquivos.",
       "Dados nao encontrados",MessageBoxButtons.OK,MessageBoxIcon.Warning);
     return null;
    }
    return dialog.SelectedPath;
   }
  }
  public RepairWindow(){
   Text="Armazem Gratidao - Reparar Atualizacao";StartPosition=FormStartPosition.CenterScreen;
   Size=new Size(640,365);MinimumSize=new Size(640,365);MaximumSize=new Size(640,365);
   BackColor=Color.FromArgb(239,247,255);
   Controls.Add(new Label{Text="ARMAZÉM GRATIDÃO  |  REPARAR ATUALIZAÇÃO",Left=18,Top=17,
    Width=580,Height=36,Font=new Font("Segoe UI",14,FontStyle.Bold),ForeColor=Color.FromArgb(178,28,46)});
   Controls.Add(new Label{Text="Pasta do programa:",Left=18,Top=59,Width=300,Height=21});
   installation.Text=installDir;Controls.Add(installation);
   Controls.Add(browse);Controls.Add(status);Controls.Add(repair);Controls.Add(cancel);Controls.Add(progress);
   browse.Click+=(s,e)=>{
    using(var dialog=new FolderBrowserDialog{Description="Escolha a pasta onde o Armazem Gratidao esta instalado",
     SelectedPath=Directory.Exists(installDir)?installDir:Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}){
     if(dialog.ShowDialog(this)==DialogResult.OK){installDir=dialog.SelectedPath;installation.Text=installDir;}
    }
   };
   cancel.Click+=(s,e)=>Close();
   repair.Click+=async (s,e)=>{
    repair.Enabled=false;browse.Enabled=false;cancel.Enabled=false;
    try{
     string target=installDir;
     if(!File.Exists(Path.Combine(target,"ArmazemGratidaoNovo.exe"))){
      MessageBox.Show(this,
       "O programa nao esta nesta pasta: "+target+Environment.NewLine+
       "Clique em Localizar e selecione a pasta onde existe ArmazemGratidaoNovo.exe."+
       Environment.NewLine+"O reparador nao alterou nenhum arquivo.",
       "Localizar instalacao",MessageBoxButtons.OK,MessageBoxIcon.Warning);
      return;
     }
     if(Process.GetProcessesByName("ArmazemGratidaoNovo").Any()){
      MessageBox.Show(this,
       "O Armazem Gratidao ainda esta em execucao. Feche o programa completamente antes de reparar. Se continuar, confira no Gerenciador de Tarefas.",
       "Feche o programa antes do reparo",MessageBoxButtons.OK,MessageBoxIcon.Warning);
      return;
     }
     string data=SelectDataDirectory();
     if(data==null)return;
     await Task.Run(()=>Repair(target,data));
     SetStatus("Reparo concluido. O programa ja pode ser aberto normalmente.",100);
     MessageBox.Show(this,"Atualizador corrigido. O Armazem Gratidao nao foi desinstalado e nenhum dado foi apagado. Abra o programa e use Verificar Atualizacao normalmente.","Reparo concluido",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }catch(Exception ex){
     SetStatus("Reparo bloqueado. Confira a mensagem antes de tentar novamente.",0);
     MessageBox.Show(this,ex.Message+Environment.NewLine+Environment.NewLine+
      "Instalacao nao foi desinstalada. Nao exclua nenhuma pasta.",
      "Reparo nao concluido",MessageBoxButtons.OK,MessageBoxIcon.Error);
    }finally{repair.Enabled=true;browse.Enabled=true;cancel.Enabled=true;}
   };
  }
  void SetStatus(string s,int pct){
   if(IsDisposed)return;
   if(InvokeRequired){BeginInvoke(new Action<string,int>(SetStatus),s,pct);return;}
   status.Text=s;progress.Value=Math.Max(0,Math.Min(100,pct));
  }
  static string BackupRoot(string data){
   string config=Path.Combine(data,"backup-location.txt");
   string root=File.Exists(config)?File.ReadAllText(config).Trim():Path.Combine(data,"Backups");
   if(string.IsNullOrWhiteSpace(root)||!Path.IsPathRooted(root))throw new Exception("Pasta de backup configurada e invalida.");
   Directory.CreateDirectory(root);
   return Path.GetFullPath(root);
  }
  static void CheckCopy(string source,string target){
   if(!File.Exists(target)||new FileInfo(target).Length!=new FileInfo(source).Length)
    throw new Exception("O backup nao preservou o arquivo: "+Path.GetFileName(source));
   using(var a=SHA256.Create())
   using(var left=File.OpenRead(source))
   using(var right=File.OpenRead(target)){
    if(!a.ComputeHash(left).SequenceEqual(a.ComputeHash(right)))
     throw new Exception("Verificacao do backup falhou: "+Path.GetFileName(source));
   }
  }
  static string MakeBackup(string data,string app,string root){
   string tag=DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
   string baseDir=Path.Combine(root,"ReparoAtualizacao-"+tag);
   string backupData=Path.Combine(baseDir,"Dados");
   string backupApp=Path.Combine(baseDir,"ArquivosAntigos");
   Directory.CreateDirectory(backupData);Directory.CreateDirectory(backupApp);
   string rootData=Path.GetFullPath(data).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   string skip=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   string updates=Path.GetFullPath(Path.Combine(data,"Atualizacoes")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   var pending=new Stack<string>();pending.Push(data);
   int total=0;
   while(pending.Count>0){
    string folder=pending.Pop();
    foreach(string sub in Directory.GetDirectories(folder)){
     string full=Path.GetFullPath(sub).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
     if(full.StartsWith(skip,StringComparison.OrdinalIgnoreCase)||
       full.StartsWith(updates,StringComparison.OrdinalIgnoreCase))continue;
     pending.Push(sub);
    }
    foreach(string file in Directory.GetFiles(folder)){
     string source=Path.GetFullPath(file);
     string relative=source.Substring(rootData.Length);
     string target=Path.Combine(backupData,relative);
     Directory.CreateDirectory(Path.GetDirectoryName(target));
     File.Copy(source,target,true);CheckCopy(source,target);total++;
    }
   }
   if(total==0||!File.Exists(Path.Combine(backupData,"armazem-gratidao.db")))
    throw new Exception("Backup completo de vendas e estoque nao foi confirmado. Reparo cancelado.");
   return baseDir;
  }
  void Repair(string install,string data){
   install=Path.GetFullPath(install);
   string app=Path.Combine(install,"ArmazemGratidaoNovo.exe");
   if(!File.Exists(app))throw new Exception("Programa nao encontrado em "+install+". Escolha a pasta correta. Nenhum arquivo foi alterado.");
   if(Process.GetProcessesByName("ArmazemGratidaoNovo").Any())
    throw new Exception("Feche todas as janelas do Armazem Gratidao antes do reparo. Nenhum arquivo foi alterado.");
   string database=Path.Combine(data,"armazem-gratidao.db");
   if(!File.Exists(database))throw new Exception("Banco de dados nao localizado em: "+data+". Reparo bloqueado, sem perda de vendas e fiado.");
   SetStatus("Conferindo dados e preparando backup completo...",15);
   string root=BackupRoot(data);
   string backup=MakeBackup(data,app,root);
   string previous=Path.Combine(backup,"ArquivosAntigos");
   string stage=Path.Combine(Path.GetTempPath(),"amzg-reparo-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(stage);
   var targets=new List<string>();
   var created=new List<string>();
   try{
    SetStatus("Conferindo arquivos do reparo...",37);
    var assembly=Assembly.GetExecutingAssembly();
    for(int i=0;i<files.GetLength(0);i++){
     string resource=files[i,0],relative=files[i,1];
     string target=Path.Combine(stage,relative.Replace('/',Path.DirectorySeparatorChar));
     Directory.CreateDirectory(Path.GetDirectoryName(target));
     using(var source=assembly.GetManifestResourceStream(resource)){
      if(source==null)throw new Exception("Reparador incompleto: "+relative);
      using(var output=File.Create(target))source.CopyTo(output);
     }
     if(new FileInfo(target).Length<=0)throw new Exception("Arquivo do reparador invalido: "+relative);
     targets.Add(relative);
    }
    SetStatus("Guardando versao anterior para recuperacao...",57);
    foreach(string name in targets){
     string source=Path.Combine(install,name.Replace('/',Path.DirectorySeparatorChar));
     string saved=Path.Combine(previous,name.Replace('/',Path.DirectorySeparatorChar));
     Directory.CreateDirectory(Path.GetDirectoryName(saved));
     if(File.Exists(source)){File.Copy(source,saved,true);CheckCopy(source,saved);}
     else created.Add(source);
    }
    SetStatus("Corrigindo atualizador e arquivos do programa...",78);
    try{
     foreach(string name in targets.Where(x=>!x.Equals("ArmazemGratidaoNovo.exe",StringComparison.OrdinalIgnoreCase))
      .Concat(targets.Where(x=>x.Equals("ArmazemGratidaoNovo.exe",StringComparison.OrdinalIgnoreCase)))){
      string source=Path.Combine(stage,name.Replace('/',Path.DirectorySeparatorChar));
      string target=Path.Combine(install,name.Replace('/',Path.DirectorySeparatorChar));
      Directory.CreateDirectory(Path.GetDirectoryName(target));
      File.Copy(source,target,true);CheckCopy(source,target);
     }
    }catch(Exception ex){
     var failures=new List<string>();
     foreach(string name in targets){
      string target=Path.Combine(install,name.Replace('/',Path.DirectorySeparatorChar));
      string saved=Path.Combine(previous,name.Replace('/',Path.DirectorySeparatorChar));
      try{if(File.Exists(saved))File.Copy(saved,target,true);}catch(Exception restore){failures.Add(name+": "+restore.Message);}
     }
     foreach(string item in created)try{if(File.Exists(item))File.Delete(item);}catch{}
     throw new Exception("A substituicao falhou: "+ex.Message+". Arquivos anteriores restaurados. Backup: "+backup+
      (failures.Count>0?". Restauracao manual necessaria: "+string.Join("; ",failures):""),ex);
    }
    SetStatus("Verificando integridade dos arquivos e dos backups...",95);
    if(!File.Exists(Path.Combine(backup,"Dados","armazem-gratidao.db")))
     throw new Exception("Pasta de backup ficou incompleta.");
    SetStatus("Atualizacao reparada. Backups preservados em "+backup,100);
   }finally{
    try{Directory.Delete(stage,true);}catch{}
   }
  }
 }
}