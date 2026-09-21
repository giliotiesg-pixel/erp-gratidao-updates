package main

import (
 "archive/zip"
 "crypto/sha256"
 "encoding/hex"
 "encoding/json"
 "fmt"
 "io"
 "net/http"
 "os"
 "os/exec"
 "path/filepath"
 "runtime"
 "strconv"
 "strings"
 "time"
)

const appVersion = "0.1.1"
const updateManifestURL = "https://raw.githubusercontent.com/giliotiesg-pixel/erp-gratidao-updates/main/ui_fyne/versao.json"

type updateManifest struct {
 Version string `json:"version"`
 PackageURL string `json:"package_url"`
 SHA256 string `json:"sha256"`
 Notes string `json:"notes"`
}

func versionParts(v string) []int {
 v=strings.TrimSpace(strings.TrimPrefix(strings.ToLower(v),"v"))
 xs:=strings.Split(v,"."); out:=make([]int,len(xs))
 for i,x:=range xs { out[i],_=strconv.Atoi(x) }
 return out
}
func newerVersion(remote,local string) bool {
 a,b:=versionParts(remote),versionParts(local); n:=len(a);if len(b)>n{n=len(b)}
 for i:=0;i<n;i++ {x,y:=0,0;if i<len(a){x=a[i]};if i<len(b){y=b[i]};if x!=y{return x>y}}
 return false
}
func fetchUpdateManifest()(updateManifest,error){
 var m updateManifest
 c:=&http.Client{Timeout:12*time.Second};r,e:=c.Get(updateManifestURL);if e!=nil{return m,e};defer r.Body.Close()
 if r.StatusCode!=http.StatusOK{return m,fmt.Errorf("manifesto HTTP %d",r.StatusCode)}
 if e=json.NewDecoder(io.LimitReader(r.Body,1024*1024)).Decode(&m);e!=nil{return m,e}
 if strings.TrimSpace(m.Version)==""||strings.TrimSpace(m.PackageURL)==""||len(strings.TrimSpace(m.SHA256))!=64{return m,fmt.Errorf("manifesto de atualização inválido")}
 return m,nil
}
func backupBeforeUpdate()(string,error){
 exe,e:=os.Executable();if e!=nil{return "",e};root:=filepath.Dir(exe)
 db:=filepath.Join(root,"data","erp.sqlite");if _,e=os.Stat(db);e!=nil{return "",e}
 dir:=filepath.Join(root,"backup");if e=os.MkdirAll(dir,0755);e!=nil{return "",e}
 dst:=filepath.Join(dir,"erp_antes_atualizacao_"+time.Now().Format("20060102_150405")+".sqlite")
 in,e:=os.Open(db);if e!=nil{return "",e};defer in.Close();out,e:=os.Create(dst);if e!=nil{return "",e}
 _,e=io.Copy(out,in);ce:=out.Close();if e!=nil{return "",e};if ce!=nil{return "",ce};return dst,nil
}
func downloadUpdate(m updateManifest)(string,error){
 exe,e:=os.Executable();if e!=nil{return "",e};dir:=filepath.Join(filepath.Dir(exe),"update");if e=os.MkdirAll(dir,0755);e!=nil{return "",e}
 dst:=filepath.Join(dir,"ERP_Gratidao_Update.zip");r,e:=(&http.Client{Timeout:3*time.Minute}).Get(m.PackageURL);if e!=nil{return "",e};defer r.Body.Close()
 if r.StatusCode!=http.StatusOK{return "",fmt.Errorf("download HTTP %d",r.StatusCode)}
 f,e:=os.Create(dst);if e!=nil{return "",e};h:=sha256.New();_,e=io.Copy(io.MultiWriter(f,h),r.Body);ce:=f.Close();if e!=nil{return "",e};if ce!=nil{return "",ce}
 got:=hex.EncodeToString(h.Sum(nil));if !strings.EqualFold(got,strings.TrimSpace(m.SHA256)){os.Remove(dst);return "",fmt.Errorf("SHA-256 inválido")}
 return dst,nil
}
func prepareAndLaunchUpdate(zipPath string)error{
 if runtime.GOOS!="windows"{return fmt.Errorf("atualização automática disponível no Windows")}
 exe,e:=os.Executable();if e!=nil{return e};root:=filepath.Dir(exe);stage:=filepath.Join(root,"update","stage");os.RemoveAll(stage);if e=os.MkdirAll(stage,0755);e!=nil{return e}
 z,e:=zip.OpenReader(zipPath);if e!=nil{return fmt.Errorf("pacote ZIP inválido: %w",e)};defer z.Close()
 var newExe string
 for _,f:=range z.File {name:=filepath.Base(f.Name);if strings.EqualFold(filepath.Ext(name),".exe"){src,e:=f.Open();if e!=nil{return e};dst:=filepath.Join(stage,name);out,e:=os.Create(dst);if e!=nil{src.Close();return e};_,e=io.Copy(out,src);src.Close();out.Close();if e!=nil{return e};newExe=dst;break}}
 if newExe==""{return fmt.Errorf("executável não encontrado no pacote")}
 bat:=filepath.Join(root,"update","aplicar_atualizacao.cmd")
 script:=fmt.Sprintf("@echo off\r\ntimeout /t 2 /nobreak >nul\r\ncopy /y %q %q >nul\r\nstart \"\" %q\r\ndel \"%%~f0\"\r\n",newExe,exe,exe)
 if e=os.WriteFile(bat,[]byte(script),0644);e!=nil{return e}
 cmd:=exec.Command("cmd","/c","start","",bat);cmd.Dir=root
 if e=cmd.Start();e!=nil{return e};return nil
}
