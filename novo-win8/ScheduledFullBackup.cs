using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Globalization;
using System.Windows.Forms;
namespace ArmazemGratidao {
 internal static class ScheduledFullBackup {
  static readonly string Data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
  static readonly string App=Application.StartupPath;
  static string Db{get{return Path.Combine(Data,"armazem-gratidao.db");}}
  static string Sid{get{return WindowsIdentity.GetCurrent().User.Value;}}
  public static string Root(){
   string root=Path.Combine(App,"Arquivos","Backups","Diarios");
   try{Directory.CreateDirectory(root);return root;}
   catch(UnauthorizedAccessException){root=Path.Combine(Data,"Backups","Diarios");Directory.CreateDirectory(root);return root;}
  }
  static void Log(string message){
   try{File.AppendAllText(Path.Combine(Root(),"Historico-backups.txt"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" | "+message+Environment.NewLine);}catch{}
  }
  public static DateTime LatestSlot(DateTime now){
   if(now.Hour>=23)return now.Date.AddHours(23);
   if(now.Hour>=11)return now.Date.AddHours(11);
   return now.Date.AddDays(-1).AddHours(23);
  }
  public static int RunHeadless(){
   try{Run(false);return 0;}catch(Exception ex){Log("ERRO: "+ex.Message);return 1;}
  }
  // Tarefas interativas por usuário: funcionam com PDV fechado, sem armazenar senha.
  public static string RegisterWindowsTasks(){
   try{
    Root();
    string path=Application.ExecutablePath;
    string signature=path+"|11:00|23:00|v1";
    string marker=Path.Combine(Data,"agenda-backups-v1.txt");
    if(File.Exists(marker)&&File.ReadAllText(marker)==signature)return null;
    foreach(string hour in new[]{"11:00","23:00"}){
     string name="ArmazemGratidao-Backup-"+Sid+"-"+hour.Replace(":","");
     string taskCommand="\\\""+path+"\\\" --backup-diario";
     string args="/Create /F /SC DAILY /ST "+hour+" /TN \""+name+"\" /TR \""+taskCommand+"\" /IT /RL LIMITED";
     using(var proc=Process.Start(new ProcessStartInfo("schtasks.exe",args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true})){
      string output=proc.StandardOutput.ReadToEnd();string error=proc.StandardError.ReadToEnd();
      proc.WaitForExit();
      if(proc.ExitCode!=0)throw new Exception((error+" "+output).Trim());
     }
    }
    Directory.CreateDirectory(Data);File.WriteAllText(marker,signature);
    Log("Agendador Windows registrado: 11:00 e 23:00.");
    return null;
   }catch(Exception ex){Log("Falha ao configurar Agendador: "+ex.Message);return "Backup às 11h e 23h ativo com PDV aberto; Agendador Windows não registrado. Confira o histórico.";}
  }
  public static string Run(bool manual){
   if(!File.Exists(Db)){if(manual)throw new FileNotFoundException("Banco não encontrado.",Db);return null;}
   using(var mutex=new Mutex(false,"Local\\AMZG-FullBackup-"+Sid)){
    bool acquired=false;
    try{
     try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
     if(!acquired)return null;
     string root=Root();
     DateTime now=DateTime.Now,slot=LatestSlot(now);
     // Não inventar cópia histórica de horário em que o PC estava desligado.
     string key=slot.ToString("yyyy-MM-dd_HH-mm",CultureInfo.InvariantCulture);
     string day=Path.Combine(root,slot.ToString("yyyy-MM-dd"));
     if(!manual&&Directory.Exists(day)&&Directory.GetDirectories(day,key+"*").Any(x=>File.Exists(Path.Combine(x,"BACKUP-VALIDADO.txt"))))return null;
     string name=manual?now.ToString("yyyy-MM-dd_HH-mm-ss")+"-manual-"+Guid.NewGuid().ToString("N").Substring(0,6):key;
     if(!manual&&now-slot>TimeSpan.FromMinutes(5))name+="-recuperado-"+now.ToString("HH-mm-ss");
     if(manual)day=Path.Combine(root,now.ToString("yyyy-MM-dd"));
     Directory.CreateDirectory(day);
     string final=Path.Combine(day,name);
     if(Directory.Exists(final))final+="-"+Guid.NewGuid().ToString("N").Substring(0,6);
     string partial=final+".partial-"+Guid.NewGuid().ToString("N");
     try{
      Directory.CreateDirectory(partial);
      var files=new List<string>();
      var excludes=new List<string>{root,Path.Combine(App,"Arquivos","Backups"),Path.Combine(App,"Arquivos","Atualizacoes"),Path.Combine(App,"Arquivos","Rollback"),Path.Combine(Data,"Backups"),Path.Combine(Data,"Backup"),Path.Combine(Data,"Atualizacoes")};
      string configured=Path.Combine(Data,"backup-location.txt");
      if(File.Exists(configured)){string custom=File.ReadAllText(configured).Trim();if(Path.IsPathRooted(custom)&&!Same(custom,Data)&&!Same(custom,App))excludes.Add(custom);}
      string dbTarget=Path.Combine(partial,"Dados","armazem-gratidao.db");
      CopyDatabase(Db,dbTarget);files.Add(dbTarget);
      CopyTree(Data,Path.Combine(partial,"Dados"),excludes,Db,files);
      CopyTree(App,Path.Combine(partial,"Programa"),excludes,Db,files);
      string documents=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Armazem Gratidao");
      string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidao");
      if(Directory.Exists(documents))CopyTree(documents,Path.Combine(partial,"Documentos"),excludes,Db,files);
      if(Directory.Exists(legacy)&&!Same(legacy,Data))CopyTree(legacy,Path.Combine(partial,"ConfiguracoesLocais"),excludes,Db,files);
      if(!File.Exists(Path.Combine(partial,"Programa",Path.GetFileName(Application.ExecutablePath))))throw new Exception("Executável não incluído no backup.");
      var manifest=new List<string>();
      foreach(string file in files.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)){
       using(var stream=File.OpenRead(file))using(var sha=SHA256.Create()){
        manifest.Add(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()+"  "+file.Substring(partial.Length+1));
       }
      }
      File.WriteAllLines(Path.Combine(partial,"SHA256.txt"),manifest);
      File.WriteAllText(Path.Combine(partial,"BACKUP-VALIDADO.txt"),"Backup completo do Armazém Gratidão\nHorário previsto: "+(manual?"Manual":slot.ToString("dd/MM/yyyy HH:mm"))+"\nInício real: "+now.ToString("dd/MM/yyyy HH:mm:ss")+"\nConclusão: "+DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")+"\nBanco SQLite verificado; arquivos: "+files.Count+"\nBackups anteriores e pacotes temporários não são duplicados.");
      Directory.Move(partial,final);
      Log("OK: "+final);
      return final;
     }catch{try{if(Directory.Exists(partial))Directory.Delete(partial,true);}catch{}throw;}
    }catch(Exception ex){Log("ERRO: "+ex.Message);throw;}
    finally{if(acquired)mutex.ReleaseMutex();}
   }
  }
  static bool Same(string a,string b){return string.Equals(Path.GetFullPath(a).TrimEnd('\\'),Path.GetFullPath(b).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase);}
  static bool Under(string a,string b){return Same(a,b)||Path.GetFullPath(a).StartsWith(Path.GetFullPath(b).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);}
  static void CopyDatabase(string source,string target){
   Directory.CreateDirectory(Path.GetDirectoryName(target));
   using(var a=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=source,ReadOnly=true,FailIfMissing=true,Pooling=false}.ToString()))
   using(var b=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=target,Pooling=false}.ToString())){
    a.Open();b.Open();a.BackupDatabase(b,"main","main",-1,null,0);
   }
   using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=target,ReadOnly=true,FailIfMissing=true,Pooling=false}.ToString())){
    c.Open();using(var q=new SQLiteCommand("PRAGMA integrity_check",c))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new Exception("Banco de backup inválido: "+source);
   }
  }
  static void CopyTree(string source,string target,List<string> excluded,string mainDb,List<string> files){
   Directory.CreateDirectory(target);
   foreach(string directory in Directory.GetDirectories(source)){
    if(excluded.Any(x=>Under(directory,x))||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)continue;
    CopyTree(directory,Path.Combine(target,Path.GetFileName(directory)),excluded,mainDb,files);
   }
   foreach(string file in Directory.GetFiles(source)){
    string name=Path.GetFileName(file);
    if(Same(file,mainDb)||excluded.Any(x=>Under(file,x))||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
    if(name.EndsWith("-wal",StringComparison.OrdinalIgnoreCase)||name.EndsWith("-shm",StringComparison.OrdinalIgnoreCase)||name.EndsWith("-journal",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".partial",StringComparison.OrdinalIgnoreCase)||name.StartsWith("armazem-gratidao-antes-",StringComparison.OrdinalIgnoreCase)||name.StartsWith("armazem-gratidao-completo-",StringComparison.OrdinalIgnoreCase)||name.IndexOf(".antes-",StringComparison.OrdinalIgnoreCase)>=0||name.StartsWith("antes-integracao-",StringComparison.OrdinalIgnoreCase))continue;
    string destination=Path.Combine(target,name);
    if(new[]{".db",".sqlite",".sqlite3"}.Contains(Path.GetExtension(file).ToLowerInvariant()))CopyDatabase(file,destination);
    else{
     // Compartilhar leitura sem bloquear comprovantes e arquivos de configuração.
     bool copied=false;
     for(int attempt=0;attempt<3&&!copied;attempt++){
      var before=new FileInfo(file);long length=before.Length;DateTime modified=before.LastWriteTimeUtc;
      using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
      using(var output=new FileStream(destination,FileMode.Create,FileAccess.Write,FileShare.None))input.CopyTo(output);
      var after=new FileInfo(file);copied=after.Exists&&after.Length==length&&after.LastWriteTimeUtc==modified&&new FileInfo(destination).Length==length;
      if(!copied)Thread.Sleep(150);
     }
     if(!copied)throw new IOException("Arquivo mudou durante a cópia: "+file);
    }
    files.Add(destination);
   }
  }
 }
}
