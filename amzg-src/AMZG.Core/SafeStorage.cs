using System.Security.Cryptography;
namespace AMZG.Core;
public static class SafeStorage {
 public static string Root => @"C:\AMZG-Seguranca";
 public static string BackupRoot => Path.Combine(Root,"Backups");
 public static string UpdateRoot => Path.Combine(Root,"Atualizacoes");
 public static string VersionRoot => Path.Combine(Root,"Versoes");
 public static string LogRoot => Path.Combine(Root,"Logs");
 public static string RecoveryRoot => Path.Combine(Root,"Recuperacao");
 public static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"AMZG","Data");
 public static string DatabasePath => Path.Combine(DataRoot,"amzg.db");
 public static void Ensure(){foreach(var p in new[]{Root,BackupRoot,UpdateRoot,VersionRoot,LogRoot,RecoveryRoot,DataRoot})Directory.CreateDirectory(p);}
 public static string Sha256(string file){using var s=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();}
 public static string BackupDatabase(){Ensure();if(!File.Exists(DatabasePath))return "";var dir=Path.Combine(BackupRoot,DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(dir);var dst=Path.Combine(dir,"amzg.db");File.Copy(DatabasePath,dst,false);File.WriteAllText(Path.Combine(dir,"amzg.db.sha256"),Sha256(dst));return dst;}
}