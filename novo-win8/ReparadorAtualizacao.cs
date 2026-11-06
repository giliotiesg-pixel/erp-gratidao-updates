using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
  string installDir=@"C:\ERP-Gratidao";
  static readonly string[,] files={
   {"AMZG.Payload.Program","ArmazemGratidaoNovo.exe"},
   {"AMZG.Payload.Config","ArmazemGratidaoNovo.exe.config"},
   {"AMZG.Payload.Qr","QRCoder.dll"},
   {"AMZG.Payload.Barcode","zxing.dll"},
   {"AMZG.Payload.SQLite","System.Data.SQLite.dll"},
   {"AMZG.Payload.Native","x64/SQLite.Interop.dll"},
   {"AMZG.Payload.Updater","AtualizadorGratidao.exe"}
  };
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
     await Task.Run(()=>Repair(target));
     SetStatus("Reparo concluido. O programa ja pode ser aberto normalmente.",100);
     MessageBox.Show(this,"Atualizador corrigido. O Armazem Gratidao nao foi desinstalado e nenhum dado foi apagado. Abra o programa e use Verificar Atualizacao normalmente.","Reparo concluido",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }catch(Exception ex){
     SetStatus("Reparo bloqueado. Confira a mensagem antes de tentar novamente.",0);
     MessageBox.Show(this,ex.Message,"Reparo nao concluido",MessageBoxButtons.OK,MessageBoxIcon.Error);
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
  void Repair(string install){
   install=Path.GetFullPath(install);
   string app=Path.Combine(install,"ArmazemGratidaoNovo.exe");
   if(!File.Exists(app))throw new Exception("Programa nao encontrado em "+install+". Escolha a pasta correta. Nenhum arquivo foi alterado.");
   if(Process.GetProcessesByName("ArmazemGratidaoNovo").Any())
    throw new Exception("Feche todas as janelas do Armazem Gratidao antes do reparo. Nenhum arquivo foi alterado.");
   string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
   string database=Path.Combine(data,"armazem-gratidao.db");
   if(!File.Exists(database))throw new Exception("Banco de dados nao encontrado. Reparo bloqueado para preservar suas vendas e fiado.");
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