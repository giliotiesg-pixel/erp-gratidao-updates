using System.Diagnostics;
using System.Net.Http.Json;
namespace AMZG.Core;
public sealed record UpdateManifest(string Version,string PackageUrl,string Sha256,bool Mandatory,string[]? Modules,string? Notes);
public static class AutomaticUpdater {
 public const string CurrentVersion="0.8.2";
 public const string ManifestUrl="https://raw.githubusercontent.com/giliotiesg-pixel/erp-gratidao-updates/main/amzg-manifest.json";
 static readonly HttpClient Http=new(){Timeout=TimeSpan.FromMinutes(5)};
 public static async Task<UpdateManifest?> CheckAsync(CancellationToken ct=default){
  try{
   var m=await Http.GetFromJsonAsync<UpdateManifest>(ManifestUrl,ct);
   if(m is null||!Version.TryParse(m.Version,out var remote)||!Version.TryParse(CurrentVersion,out var local)||remote<=local)return null;
   if(!Uri.TryCreate(m.PackageUrl,UriKind.Absolute,out var u)||u.Scheme!="https")return null;
   if(string.IsNullOrWhiteSpace(m.Sha256)||m.Sha256.Length!=64||!m.Sha256.All(Uri.IsHexDigit))return null;
   return m;
  }catch{return null;}
 }
 public static async Task<string> DownloadAndValidateAsync(UpdateManifest m,IProgress<int>? progress=null,CancellationToken ct=default){
  SafeStorage.Ensure();
  var ext=Path.GetExtension(new Uri(m.PackageUrl).AbsolutePath);
  if(!ext.Equals(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Formato de atualização não suportado.");
  var dst=Path.Combine(SafeStorage.UpdateRoot,$"AMZG-Setup-{m.Version}.exe");
  using var resp=await Http.GetAsync(m.PackageUrl,HttpCompletionOption.ResponseHeadersRead,ct);resp.EnsureSuccessStatusCode();
  var len=resp.Content.Headers.ContentLength??-1;await using var input=await resp.Content.ReadAsStreamAsync(ct);await using var output=File.Create(dst);
  var buf=new byte[81920];long total=0;int n;while((n=await input.ReadAsync(buf,ct))>0){await output.WriteAsync(buf.AsMemory(0,n),ct);total+=n;if(len>0)progress?.Report((int)(total*100/len));}
  await output.FlushAsync(ct);
  if(!string.Equals(SafeStorage.Sha256(dst),m.Sha256,StringComparison.OrdinalIgnoreCase)){File.Delete(dst);throw new InvalidDataException("SHA-256 da atualização é inválido.");}
  return dst;
 }
 public static void ApplyAndRestart(string installer){
  if(!File.Exists(installer))throw new FileNotFoundException("Instalador da atualização não encontrado.",installer);
  SafeStorage.BackupDatabase();
  Process.Start(new ProcessStartInfo(installer,"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS"){UseShellExecute=true,Verb="runas"});
  Environment.Exit(0);
 }
}