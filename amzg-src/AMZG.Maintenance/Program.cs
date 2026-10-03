using System.IO.Compression;
using AMZG.Core;
namespace AMZG.Maintenance;
internal static class Program {
 static readonly HashSet<string> Blocked=new(StringComparer.OrdinalIgnoreCase){".db",".sqlite",".sqlite3"};
 [STAThread] static int Main(string[] args){try{SafeStorage.Ensure();if(args.Length<2||args[0]!="--apply-update")return 2;var package=Path.GetFullPath(args[1]);int pid=args.Length>2&&int.TryParse(args[2],out var p)?p:0;if(pid>0){try{System.Diagnostics.Process.GetProcessById(pid).WaitForExit(30000);}catch{}}
  if(!File.Exists(package))throw new FileNotFoundException("Pacote de atualização não encontrado.",package);
  var install=AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);var rollback=Path.Combine(SafeStorage.VersionRoot,DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(rollback);SafeStorage.BackupDatabase();
  var temp=Path.Combine(SafeStorage.UpdateRoot,"apply-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
  try{ZipFile.ExtractToDirectory(package,temp);foreach(var src in Directory.EnumerateFiles(temp,"*",SearchOption.AllDirectories)){var rel=Path.GetRelativePath(temp,src);var ext=Path.GetExtension(src);if(Blocked.Contains(ext))throw new InvalidDataException("Atualização bloqueada: pacote contém banco de dados.");var dst=Path.GetFullPath(Path.Combine(install,rel));if(!dst.StartsWith(Path.GetFullPath(install)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Caminho inválido no pacote.");if(File.Exists(dst)){var bak=Path.Combine(rollback,rel);Directory.CreateDirectory(Path.GetDirectoryName(bak)!);File.Copy(dst,bak,true);}Directory.CreateDirectory(Path.GetDirectoryName(dst)!);File.Copy(src,dst,true);}
   var exe=Path.Combine(install,"AMZG.exe");if(File.Exists(exe))System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=true});return 0;
  }catch{foreach(var bak in Directory.Exists(rollback)?Directory.EnumerateFiles(rollback,"*",SearchOption.AllDirectories):Array.Empty<string>()){var rel=Path.GetRelativePath(rollback,bak);var dst=Path.Combine(install,rel);Directory.CreateDirectory(Path.GetDirectoryName(dst)!);File.Copy(bak,dst,true);}throw;}finally{try{Directory.Delete(temp,true);}catch{}}
 }catch(Exception ex){try{SafeStorage.Ensure();File.AppendAllText(Path.Combine(SafeStorage.LogRoot,"update-error.log"),$"[{DateTime.Now:O}] {ex}\n");}catch{}return 1;}}
}