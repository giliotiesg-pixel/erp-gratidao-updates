using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Imaging;
using QRCoder;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace ArmazemGratidao {
 // Servidor local e temporario, ativado expressamente pelo operador.
 // O desktop permanece Windows Forms nativo; somente o celular utiliza o navegador.
 internal sealed class MobileStockServer : IDisposable {
  readonly string database;
  readonly TcpListener listener;
  readonly string secret;
  readonly HashSet<string> completed=new HashSet<string>(StringComparer.Ordinal);
  readonly object writeLock=new object();
  volatile bool running;
  Thread worker;
  public string Token {get{return secret;}}
  public const int Port=8765;
  public MobileStockServer(string db){
   database=db;
   byte[] bytes=new byte[18];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
   secret=BitConverter.ToString(bytes).Replace("-","");
   listener=new TcpListener(IPAddress.Any,Port);
  }
  public void Start(){
   if(!File.Exists(database))throw new Exception("Banco de dados não encontrado.");
   listener.Start(8);running=true;
   worker=new Thread(Loop){IsBackground=true,Name="AMZG Mobile Stock"};worker.Start();
  }
  public void Dispose(){running=false;try{listener.Stop();}catch{}}
  public static string LanAddress(){
   try{
    foreach(var address in Dns.GetHostAddresses(Dns.GetHostName())){
     if(address.AddressFamily!=AddressFamily.InterNetwork||IPAddress.IsLoopback(address))continue;
     byte[] b=address.GetAddressBytes();
     if(b[0]==10||(b[0]==192&&b[1]==168)||(b[0]==172&&b[1]>=16&&b[1]<=31))return address.ToString();
    }
   }catch{}
   return null;
  }
  void Loop(){
   while(running){
    try{var socket=listener.AcceptTcpClient();ThreadPool.QueueUserWorkItem(_=>Handle(socket));}
    catch(SocketException){if(!running)break;}
    catch(ObjectDisposedException){break;}
    catch{}
   }
  }
  static string UrlDecode(string s){return Uri.UnescapeDataString((s??"").Replace("+"," "));}
  static string Param(string query,string key){
   foreach(string item in (query??"").Split('&')){
    var pair=item.Split(new[]{'='},2);
    if(pair.Length==2&&pair[0]==key)return UrlDecode(pair[1]);
   }
   return "";
  }
  static string SafeJson(object obj){return new JavaScriptSerializer().Serialize(obj);}
  static Dictionary<string,object> ParseObject(string json){
   return new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string,object>;
  }
  static string Field(Dictionary<string,object> data,string key){
   return data!=null&&data.ContainsKey(key)?Convert.ToString(data[key],CultureInfo.InvariantCulture):"";
  }
  static decimal DecimalValue(object value){
   if(value==null||value==DBNull.Value)return 0m;
   return Convert.ToDecimal(value,CultureInfo.InvariantCulture);
  }
  static HashSet<string> Columns(SQLiteConnection conn,string table){
   var cols=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var cmd=new SQLiteCommand("PRAGMA table_info("+table+")",conn))
   using(var reader=cmd.ExecuteReader())while(reader.Read())cols.Add(Convert.ToString(reader["name"]));
   return cols;
  }
  static bool Table(SQLiteConnection conn,string name){
   using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name",conn)){
    cmd.Parameters.AddWithValue("@name",name);return Convert.ToInt32(cmd.ExecuteScalar())>0;
   }
  }
  static string ActivePredicate(HashSet<string> cols){
   string active=cols.Contains("active")?"active":cols.Contains("is_active")?"is_active":cols.Contains("ativo")?"ativo":null;
   return active==null?"1=1":"COALESCE(\""+active+"\",1)<>0";
  }
  SQLiteConnection Open(bool readOnly){
   var db=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=database,FailIfMissing=true,ReadOnly=readOnly}.ToString());
   db.Open();return db;
  }
  void Handle(TcpClient socket){
   using(socket){
    socket.ReceiveTimeout=12000;socket.SendTimeout=12000;
    try{
     using(var stream=socket.GetStream()){
      var header=new MemoryStream();int prev=0,ch;
      while(header.Length<16384&&(ch=stream.ReadByte())!=-1){
       header.WriteByte((byte)ch);prev=((prev<<8)|(byte)ch);
       if(prev==0x0D0A0D0A)break;
      }
      if(prev!=0x0D0A0D0A){Write(stream,400,"text/plain; charset=utf-8","Requisição inválida.");return;}
      string[] headers=Encoding.UTF8.GetString(header.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);
      string[] first=headers[0].Split(' ');
      if(first.Length<2){Write(stream,400,"text/plain","Requisição inválida.");return;}
      string method=first[0],target=first[1];
      int length=0;string suppliedToken="";
      foreach(string h in headers){
       if(h.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))int.TryParse(h.Substring(15).Trim(),out length);
       if(h.StartsWith("X-AMZG-Token:",StringComparison.OrdinalIgnoreCase))suppliedToken=h.Substring(13).Trim();
      }
      string path=target.Split('?')[0];
      int maxLength=path=="/api/ler-foto"?2500000:4096;
      if(length<0||length>maxLength){Write(stream,413,"application/json; charset=utf-8",SafeJson(new{ok=false,erro="Foto muito grande. Aproxime o código ou reduza a foto."}));return;}
      byte[] payload=new byte[length];int count=0;
      while(count<length){int read=stream.Read(payload,count,length-count);if(read<=0)throw new IOException("Conexão interrompida.");count+=read;}
      string json=Encoding.UTF8.GetString(payload);
      string query=target.Contains("?")?target.Substring(target.IndexOf('?')+1):"";
      string presented=method=="POST"?suppliedToken:Param(query,"chave");
      if(!ConstantEquals(presented,secret)){Write(stream,403,"application/json; charset=utf-8",SafeJson(new{erro="Acesso não autorizado. Confira o endereço e a chave."}));return;}
      if(method=="GET"&&path=="/"){Write(stream,200,"text/html; charset=utf-8",MobilePage());return;}
      if(method=="GET"&&path=="/api/buscar"){
       Write(stream,200,"application/json; charset=utf-8",Search(Param(query,"q")));return;
      }
      if(method=="POST"&&path=="/api/entrada"){
       Write(stream,200,"application/json; charset=utf-8",AddStock(json));return;
      }
      if(method=="GET"&&path=="/api/qr-produto"){
       Write(stream,200,"application/json; charset=utf-8",ProductQr(Param(query,"id")));return;
      }
      if(method=="GET"&&path=="/api/produto"){
       Write(stream,200,"application/json; charset=utf-8",FindStockProduct(Param(query,"id")));return;
      }
      if(method=="GET"&&path=="/api/codigo"){
       Write(stream,200,"application/json; charset=utf-8",FindBarcode(Param(query,"codigo")));return;
      }
      if(method=="POST"&&path=="/api/ler-foto"){
       Write(stream,200,"application/json; charset=utf-8",ReadBarcodePhoto(json));return;
      }
      if(method=="POST"&&path=="/api/cadastrar"){
       Write(stream,200,"application/json; charset=utf-8",RegisterProduct(json));return;
      }
      Write(stream,404,"application/json; charset=utf-8",SafeJson(new{erro="Rota não encontrada."}));
     }
    }catch(Exception){
     // Falhas de rede sao encerradas sem afetar o PDV.
    }
   }
  }
  static bool ConstantEquals(string a,string b){
   byte[] x=Encoding.UTF8.GetBytes(a??""),y=Encoding.UTF8.GetBytes(b??"");
   if(x.Length!=y.Length)return false;
   int diff=0;for(int i=0;i<x.Length;i++)diff|=x[i]^y[i];
   return diff==0;
  }
  static void Write(NetworkStream stream,int code,string type,string body){
   byte[] data=Encoding.UTF8.GetBytes(body);
   string status=code==200?"OK":code==403?"Forbidden":code==404?"Not Found":code==413?"Payload Too Large":"Bad Request";
   byte[] head=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+status+"\r\nContent-Type: "+type+"\r\nContent-Length: "+data.Length+"\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n");
   stream.Write(head,0,head.Length);stream.Write(data,0,data.Length);
  }
  string Search(string input){
   string term=(input??"").Trim();
   if(term.Length==0)return SafeJson(new{produtos=new object[0]});
   if(term.Length>120)throw new Exception("Busca muito longa.");
   using(var conn=Open(true)){
    var cols=Columns(conn,"products");
    string active=ActivePredicate(cols);
    string sql="SELECT id,name,COALESCE(barcode,'') AS barcode,COALESCE(stock,0) AS stock FROM products WHERE "+active+" AND (name LIKE @term OR barcode=@exact OR barcode LIKE @term) ORDER BY CASE WHEN barcode=@exact THEN 0 ELSE 1 END,name COLLATE NOCASE LIMIT 12";
    var result=new List<object>();
    using(var cmd=new SQLiteCommand(sql,conn)){
     cmd.Parameters.AddWithValue("@term","%"+term+"%");cmd.Parameters.AddWithValue("@exact",term);
     using(var reader=cmd.ExecuteReader())while(reader.Read()){
      result.Add(new{id=Convert.ToInt64(reader["id"]),nome=Convert.ToString(reader["name"]),codigo=Convert.ToString(reader["barcode"]),quantidade=DecimalValue(reader["stock"])});
     }
    }
    return SafeJson(new{produtos=result});
   }
  }
  // A imagem é decodificada localmente no desktop, sem rede externa nem camera HTTPS.
  string ReadBarcodePhoto(string payload){
   try{
    var data=ParseObject(payload);
    string image=Field(data,"foto");
    int comma=image.IndexOf(',');
    if(comma>=0)image=image.Substring(comma+1);
    if(image.Length==0||image.Length>2200000)throw new Exception("Foto ausente ou muito grande.");
    byte[] bytes=Convert.FromBase64String(image);
    if(bytes.Length>1700000)throw new Exception("Foto ultrapassa o limite de 1,7 MB.");
    using(var ms=new MemoryStream(bytes))
    using(var bitmap=new Bitmap(ms)){
     if(bitmap.Width<160||bitmap.Height<90||((long)bitmap.Width*bitmap.Height)>18000000)
      throw new Exception("Foto com tamanho inválido.");
     var reader=new ZXing.BarcodeReader {AutoRotate=true,Options=new ZXing.Common.DecodingOptions{TryHarder=true}};
     var result=reader.Decode(bitmap);
     if(result==null){
      // Inversão da imagem pode auxiliar em embalagens com impressão invertida.
      using(var reversed=(Bitmap)bitmap.Clone()){
       reversed.RotateFlip(RotateFlipType.Rotate180FlipNone);
       result=reader.Decode(reversed);
      }
     }
     if(result==null)return SafeJson(new{ok=false,erro="Código não localizado. Centralize as barras, deixe as linhas nítidas e tente outra foto."});
     string code=(result.Text??"").Trim();
     if(code.StartsWith("AMZG-PRODUTO:",StringComparison.OrdinalIgnoreCase)){
      long qrId;
      if(!long.TryParse(code.Substring("AMZG-PRODUTO:".Length),out qrId)||qrId<=0)throw new Exception("QR Code de produto inválido.");
      return SafeJson(new{ok=true,tipo="produto",id=qrId,formato="QR_CODE"});
     }
     if(code.Length<5||code.Length>48)throw new Exception("Código reconhecido fora do formato esperado.");
     return SafeJson(new{ok=true,tipo="barras",codigo=code,formato=Convert.ToString(result.BarcodeFormat)});
    }
   }catch(Exception ex){return SafeJson(new{ok=false,erro="Não foi possível ler a foto: "+ex.Message});}
  }

  // O QR contém o ID permanente do banco; o nome fica impresso junto à etiqueta.
  // A geração é automática e sob demanda: não modifica IDs nem quantidades.
  string ProductQr(string raw){
   try{
    long id;if(!long.TryParse(raw,out id)||id<=0)throw new Exception("Código interno inválido.");
    using(var conn=Open(true)){
     var cols=Columns(conn,"products");
     string sql="SELECT id,name FROM products WHERE id=@id AND "+ActivePredicate(cols);
     using(var cmd=new SQLiteCommand(sql,conn)){
      cmd.Parameters.AddWithValue("@id",id);
      using(var r=cmd.ExecuteReader()){
       if(!r.Read())throw new Exception("Produto não encontrado ou inativo.");
       string name=Convert.ToString(r["name"]);
       string value="AMZG-PRODUTO:"+id.ToString(CultureInfo.InvariantCulture);
       using(var generator=new QRCodeGenerator())
       using(var data=generator.CreateQrCode(value,QRCodeGenerator.ECCLevel.Q))
       using(var qr=new QRCode(data))
       using(var bitmap=qr.GetGraphic(7))
       using(var stream=new MemoryStream()){
        bitmap.Save(stream,ImageFormat.Png);
        return SafeJson(new{ok=true,id=id,nome=name,conteudo=value,imagem="data:image/png;base64,"+Convert.ToBase64String(stream.ToArray())});
       }
      }
     }
    }
   }catch(Exception ex){return SafeJson(new{ok=false,erro=ex.Message});}
  }
  string FindStockProduct(string raw){
   try{
    long id;if(!long.TryParse(raw,out id)||id<=0)throw new Exception("Identificador inválido.");
    using(var conn=Open(true)){
     var cols=Columns(conn,"products");
     using(var cmd=new SQLiteCommand("SELECT id,name,COALESCE(barcode,'') AS barcode,COALESCE(stock,0) AS stock FROM products WHERE id=@id AND "+ActivePredicate(cols),conn)){
      cmd.Parameters.AddWithValue("@id",id);
      using(var r=cmd.ExecuteReader()){
       if(!r.Read())throw new Exception("Produto excluído ou não cadastrado.");
       return SafeJson(new{ok=true,produto=new{id=id,nome=Convert.ToString(r["name"]),codigo=Convert.ToString(r["barcode"]),quantidade=DecimalValue(r["stock"])}});
      }
     }
    }
   }catch(Exception ex){return SafeJson(new{ok=false,erro=ex.Message});}
  }
  string FindBarcode(string raw){
   try{
    string code=(raw??"").Trim();
    if(code.Length<5||code.Length>48)throw new Exception("Código de barras inválido.");
    using(var conn=Open(true)){
     using(var q=new SQLiteCommand("SELECT id,name,COALESCE(stock,0) AS stock FROM products WHERE TRIM(COALESCE(barcode,''))=@code LIMIT 1",conn)){
      q.Parameters.AddWithValue("@code",code);
      using(var rd=q.ExecuteReader()){
       if(rd.Read())return SafeJson(new{existe=true,id=Convert.ToInt64(rd["id"]),nome=Convert.ToString(rd["name"]),quantidade=DecimalValue(rd["stock"])});
      }
     }
    }
    return SafeJson(new{existe=false});
   }catch(Exception ex){return SafeJson(new{erro=ex.Message});}
  }
  string RegisterProduct(string json){
   try{
    var data=ParseObject(json);
    string barcode=Field(data,"codigo").Trim(),name=Field(data,"nome").Trim();
    string op=Field(data,"operacao").Trim();
    if(barcode.Length<8||barcode.Length>32)throw new Exception("Código de barras precisa ter de 8 a 32 caracteres.");
    foreach(char ch in barcode)if(!(ch>='0'&&ch<='9'))throw new Exception("Código de barras deve conter somente números.");
    if(name.Length<3||name.Length>180)throw new Exception("Informe a descrição do produto, de 3 a 180 caracteres.");
    decimal cost,price,qty;
    if(!decimal.TryParse(Field(data,"custo"),NumberStyles.Number,CultureInfo.InvariantCulture,out cost)||cost<0||cost>9999999)throw new Exception("Custo inválido.");
    if(!decimal.TryParse(Field(data,"preco"),NumberStyles.Number,CultureInfo.InvariantCulture,out price)||price<0||price>9999999)throw new Exception("Preço inválido.");
    if(!decimal.TryParse(Field(data,"quantidade"),NumberStyles.Number,CultureInfo.InvariantCulture,out qty)||qty<0||qty>10000000)throw new Exception("Quantidade inicial inválida.");
    if(op.Length<10||op.Length>90)throw new Exception("Identificador da operação inválido.");
    lock(writeLock){
     // Não altera produto existente. O operador deve usar Entrada de estoque para somar quantidades.
     using(var conn=Open(false)){
      if(!Table(conn,"products")||!Table(conn,"stock_audit_log"))throw new Exception("O banco não possui as tabelas de produtos e auditoria exigidas.");
      using(var check=new SQLiteCommand("SELECT id FROM products WHERE TRIM(COALESCE(barcode,''))=@code LIMIT 1",conn)){
       check.Parameters.AddWithValue("@code",barcode);
       object existing=check.ExecuteScalar();if(existing!=null)throw new Exception("Este código de barras já está cadastrado. Use Entrada de estoque; nenhum cadastro foi duplicado.");
      }
      var cols=Columns(conn,"products");
      foreach(string column in new[]{"id","barcode","name","price","cost","stock"})if(!cols.Contains(column))throw new Exception("Estrutura de produtos incompatível: falta "+column+".");
      string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidao","backups");
      Directory.CreateDirectory(directory);
      string backup=Path.Combine(directory,"cadastro-celular-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
      using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup,FailIfMissing=false}.ToString())){
       dest.Open();conn.BackupDatabase(dest,"main","main",-1,null,0);
      }
      long id;
      using(var tx=conn.BeginTransaction()){
       using(var insert=new SQLiteCommand("INSERT INTO products(barcode,name,price,cost,stock) VALUES(@code,@name,@price,@cost,@stock)",conn,tx)){
        insert.Parameters.AddWithValue("@code",barcode);
        insert.Parameters.AddWithValue("@name",name);
        insert.Parameters.AddWithValue("@price",price);
        insert.Parameters.AddWithValue("@cost",cost);
        insert.Parameters.AddWithValue("@stock",qty);
        if(insert.ExecuteNonQuery()!=1)throw new Exception("Não foi possível inserir o cadastro.");
       }
       using(var last=new SQLiteCommand("SELECT last_insert_rowid()",conn,tx))id=Convert.ToInt64(last.ExecuteScalar());
       if(qty>0){
        using(var audit=new SQLiteCommand("INSERT INTO stock_audit_log(product_id,product_name,barcode,event_type,quantity_before,quantity_after,quantity_delta,reference_type,reference_id,reason) VALUES(@id,@name,@code,'CADASTRO_INICIAL',0,@qty,@qty,'PRODUTO',@id,@reason)",conn,tx)){
         audit.Parameters.AddWithValue("@id",id);audit.Parameters.AddWithValue("@name",name);audit.Parameters.AddWithValue("@code",barcode);audit.Parameters.AddWithValue("@qty",qty);
         audit.Parameters.AddWithValue("@reason","Cadastro inicial pelo celular; operação "+op);
         if(audit.ExecuteNonQuery()!=1)throw new Exception("Falha na auditoria da quantidade inicial.");
        }
       }
       tx.Commit();
      }
      return SafeJson(new{ok=true,id=id,nome=name,codigo=barcode,quantidade=qty,qrDisponivel=true,mensagem="Produto cadastrado e integrado ao estoque. QR Code já disponível."});
     }
    }
   }catch(Exception ex){return SafeJson(new{ok=false,erro=ex.Message});}
  }
  string AddStock(string json){
   try{
    var data=ParseObject(json);
    long id;decimal qty;
    if(!long.TryParse(Field(data,"produto"),out id)||id<=0)throw new Exception("Selecione um produto válido.");
    if(!decimal.TryParse(Field(data,"quantidade"),NumberStyles.Number,CultureInfo.InvariantCulture,out qty)||qty<=0||qty>10000000)throw new Exception("Informe uma quantidade positiva válida.");
    string description=Field(data,"descricao").Trim(),date=Field(data,"data").Trim(),requestId=Field(data,"operacao").Trim();
    if(description.Length<3||description.Length>300)throw new Exception("Informe uma descrição da entrada (3 a 300 caracteres).");
    DateTime parsed;
    if(!DateTime.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed))throw new Exception("Data da entrada inválida.");
    if(Math.Abs((parsed.Date-DateTime.Today).TotalDays)>365)throw new Exception("Confira a data da entrada.");
    if(requestId.Length<10||requestId.Length>90)throw new Exception("Identificador da operação inválido.");
    lock(writeLock){
     if(completed.Contains(requestId))return SafeJson(new{ok=true,repetida=true,mensagem="Esta entrada já foi registrada. Não foi somada outra vez."});
     using(var conn=Open(false)){
      if(!Table(conn,"runtime_operation_context")||!Table(conn,"stock_audit_log"))throw new Exception("Banco sem tabelas de proteção e auditoria. Nenhum estoque alterado.");
      var cols=Columns(conn,"products");
      string active=ActivePredicate(cols);
      using(var check=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE id=@id AND "+active,conn)){
       check.Parameters.AddWithValue("@id",id);
       if(Convert.ToInt32(check.ExecuteScalar())!=1)throw new Exception("Produto não encontrado ou inativo.");
      }
      string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidao","backups");
      Directory.CreateDirectory(directory);
      string backup=Path.Combine(directory,"estoque-celular-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".db");
      using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup,FailIfMissing=false}.ToString())){
       dest.Open();conn.BackupDatabase(dest,"main","main",-1,null,0);
      }
      decimal before=0,after=0;string name="",barcode="";
      using(var tx=conn.BeginTransaction()){
       using(var read=new SQLiteCommand("SELECT name,barcode,stock FROM products WHERE id=@id AND "+active,conn,tx)){
        read.Parameters.AddWithValue("@id",id);
        using(var reader=read.ExecuteReader()){
         if(!reader.Read())throw new Exception("Produto não está ativo.");
         name=Convert.ToString(reader["name"]);barcode=Convert.ToString(reader["barcode"]);before=DecimalValue(reader["stock"]);
        }
       }
       after=checked(before+qty);
       int oldAuth;string oldOp;
       using(var context=new SQLiteCommand("SELECT authorized,operation FROM runtime_operation_context WHERE id=1",conn,tx))
       using(var reader=context.ExecuteReader()){
        if(!reader.Read())throw new Exception("Proteção do estoque não configurada.");
        oldAuth=Convert.ToInt32(reader["authorized"]);oldOp=Convert.ToString(reader["operation"]);
       }
       using(var q=new SQLiteCommand("UPDATE runtime_operation_context SET authorized=1,operation='ENTRADA_MANUAL_AUDITADA' WHERE id=1",conn,tx))q.ExecuteNonQuery();
       using(var update=new SQLiteCommand("UPDATE products SET stock=@next WHERE id=@id AND stock=@before AND "+active,conn,tx)){
        update.Parameters.AddWithValue("@next",after);update.Parameters.AddWithValue("@id",id);update.Parameters.AddWithValue("@before",before);
        if(update.ExecuteNonQuery()!=1)throw new Exception("Estoque alterado simultaneamente. Refaça a conferência.");
       }
       string note="Entrada via celular | Data: "+date+" | "+description+" | Operação: "+requestId;
       using(var audit=new SQLiteCommand("INSERT INTO stock_audit_log(product_id,product_name,barcode,event_type,quantity_before,quantity_after,quantity_delta,reference_type,reason) VALUES(@id,@name,@code,'ENTRADA_MANUAL',@before,@after,@qty,'ESTOQUE',@reason)",conn,tx)){
        audit.Parameters.AddWithValue("@id",id);audit.Parameters.AddWithValue("@name",name);audit.Parameters.AddWithValue("@code",barcode);
        audit.Parameters.AddWithValue("@before",before);audit.Parameters.AddWithValue("@after",after);audit.Parameters.AddWithValue("@qty",qty);audit.Parameters.AddWithValue("@reason",note);
        if(audit.ExecuteNonQuery()!=1)throw new Exception("Falha ao gravar auditoria.");
       }
       using(var q=new SQLiteCommand("UPDATE runtime_operation_context SET authorized=@auth,operation=@op WHERE id=1",conn,tx)){
        q.Parameters.AddWithValue("@auth",oldAuth);q.Parameters.AddWithValue("@op",oldOp);q.ExecuteNonQuery();
       }
       tx.Commit();
      }
      completed.Add(requestId);
      if(completed.Count>2000)completed.Clear();
      return SafeJson(new{ok=true,produto=name,anterior=before,adicionado=qty,atual=after,mensagem="Entrada registrada com backup e auditoria."});
     }
    }
   }catch(Exception ex){return SafeJson(new{ok=false,erro=ex.Message});}
  }
  static string MobilePage(){return @"<!doctype html><html lang='pt-BR'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>
<title>Armazém Gratidão - Coletor</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#e8f2ff;color:#17365f;font-family:Arial,sans-serif}
.box{max-width:600px;margin:auto;padding:13px}h1{font-size:24px;color:#134e9f;margin:12px 0}h2{font-size:20px}
.card{background:white;border-radius:15px;padding:17px;margin:12px 0;box-shadow:0 2px 9px #c8d6e7}
label{display:block;font-weight:bold;margin:12px 0 6px}
input,textarea,button{width:100%;font:inherit;font-size:18px;padding:12px;border-radius:9px;border:1px solid #a7b5c6}
button{border:0;background:#1453a3;color:white;font-weight:bold;margin-top:12px;cursor:pointer}
button.red{background:#bd243d}button.secondary{background:#59738f}
nav{display:flex;gap:8px}nav button{font-size:15px}
.item{padding:12px;border-bottom:1px solid #dbe5f1;cursor:pointer}.item b,.item small{display:block}.item small{margin-top:4px}
.info{padding:12px;border-radius:8px;background:#f2f7fd;margin-top:10px;white-space:pre-wrap}
.warn{color:#9f1c31;font-weight:bold}.ok{color:#14613a;font-weight:bold}.hidden{display:none}
p{line-height:1.5}.hint{font-size:13px;color:#455a76}
</style></head><body><main class='box'>
<h1>Armazém Gratidão</h1><p>Coletor de produtos pelo celular • mesma rede Wi-Fi</p>
<nav><button id='tabRegister'>Cadastrar produto</button><button id='tabEntry' class='secondary'>Entrada de estoque</button></nav>
<section class='card' id='registerPanel'>
<h2>Cadastrar com código de barras</h2>
<label>Código de barras</label><input id='barcode' inputmode='numeric' autocomplete='off' placeholder='Leia com câmera ou digite'>
<input id='photo' class='hidden' type='file' accept='image/*' capture='environment'>
<button id='scan'>Bipar com a câmera</button>
<p class='hint'>O celular abre a câmera para fotografar o código. O código é reconhecido pelo computador, sem biblioteca de internet. Aproxime as barras e tire uma foto nítida. Se necessário, digite os números.</p>
<button id='check' class='secondary'>Conferir código no cadastro</button>
<div id='barcodeStatus' class='info'>Aponte para o código antes de cadastrar.</div>
<label>Descrição do produto</label><input id='productName' maxlength='180' placeholder='Ex.: Arroz Camil 5 kg'>
<label>Custo de compra (R$)</label><input id='cost' type='number' inputmode='decimal' step='0.01' min='0' placeholder='Ex.: 19.50'>
<label>Preço de venda (R$)</label><input id='price' type='number' inputmode='decimal' step='0.01' min='0' placeholder='Ex.: 24.99'>
<label>Quantidade inicial</label><input id='initialQty' type='number' inputmode='decimal' step='0.001' min='0' value='0'>
<button id='register' class='red'>Conferir e salvar produto</button><p id='registerMsg'></p>
</section>
<section class='card hidden' id='entryPanel'>
<h2>Entrada de produto existente</h2>
<label>Buscar nome ou código</label><input id='search' placeholder='Digite o nome ou código'>
<button id='find'>Buscar produto</button><div id='results'></div>
<div id='chosen' class='info'>Selecione um produto acima.</div>
<label>Data da entrada</label><input id='date' type='date'>
<label>Quantidade a adicionar</label><input id='qty' type='number' min='0.001' step='0.001' inputmode='decimal'>
<label>Descrição do recebimento</label><textarea id='reason' rows='3' placeholder='Ex.: Mercadoria recebida do fornecedor'></textarea>
<div id='after' class='info'>Quantidade após entrada: —</div>
<button id='save' class='red'>Conferir e salvar entrada</button><p id='msg'></p>
</section>
<p class='hint'>O cadastro vai para a mesma tabela usada por Produtos e Estoque no computador. As quantidades existentes nunca serão substituídas por um novo cadastro.</p>
</main>

<script>
(function(){
const token=new URLSearchParams(location.search).get('chave')||'';
const $=id=>document.getElementById(id);
const fmt=n=>Number(n).toLocaleString('pt-BR',{minimumFractionDigits:3,maximumFractionDigits:3});
let selected=null,locked=false,barcodeChecked='';
$('date').value=new Date().toLocaleDateString('sv-SE');
function notice(id,message,ok){$(id).textContent=message;$(id).className=ok?'ok':'warn'}
function tab(registered){$('registerPanel').classList.toggle('hidden',!registered);$('entryPanel').classList.toggle('hidden',registered)}
$('tabRegister').onclick=()=>tab(true);$('tabEntry').onclick=()=>tab(false);
function preview(){if(selected){let qty=parseFloat($('qty').value)||0;$('after').textContent='Quantidade após entrada: '+fmt(Number(selected.quantidade)+qty)}}
$('qty').addEventListener('input',preview);
function requestId(){return Date.now().toString(36)+'-'+Math.random().toString(36).slice(2)+'-'+Math.random().toString(36).slice(2)}
async function checkBarcode(){
 const code=$('barcode').value.trim();barcodeChecked='';
 if(!code){$('barcodeStatus').textContent='Leia ou informe um código.';return}
 try{
 const response=await fetch('/api/codigo?chave='+encodeURIComponent(token)+'&codigo='+encodeURIComponent(code),{cache:'no-store'});
 const d=await response.json();if(d.erro)throw Error(d.erro);
 barcodeChecked=code;
 if(d.existe){
 $('barcodeStatus').textContent='JÁ CADASTRADO: '+d.nome+' | Estoque: '+fmt(d.quantidade)+'. Use Entrada de estoque, não cadastre novamente.';
 notice('registerMsg','O código já existe. Nenhum produto foi criado.',false);
 }else{$('barcodeStatus').textContent='Código disponível para novo cadastro: '+code;notice('registerMsg','',false)}
 }catch(e){$('barcodeStatus').textContent='Não foi possível conferir: '+e.message}
}
$('barcode').addEventListener('input',()=>{barcodeChecked='';$('barcodeStatus').textContent='Confira o código antes de salvar.'});
$('check').onclick=checkBarcode;
$('scan').onclick=()=>{$('photo').value='';$('photo').click()};
$('photo').addEventListener('change',async function(){
 const file=this.files&&this.files[0];if(!file)return;
 $('barcodeStatus').textContent='Lendo a foto no computador...';
 const url=URL.createObjectURL(file);
 try{
  const img=new Image();
  await new Promise((resolve,reject)=>{img.onload=resolve;img.onerror=reject;img.src=url});
  const max=1900,scale=Math.min(1,max/Math.max(img.naturalWidth||img.width,img.naturalHeight||img.height));
  const canvas=document.createElement('canvas');
  canvas.width=Math.max(1,Math.round(img.naturalWidth*scale));
  canvas.height=Math.max(1,Math.round(img.naturalHeight*scale));
  const context=canvas.getContext('2d');
  context.drawImage(img,0,0,canvas.width,canvas.height);
  const photo=canvas.toDataURL('image/jpeg',0.86);
  const response=await fetch('/api/ler-foto',{method:'POST',headers:{'Content-Type':'application/json','X-AMZG-Token':token},
    body:JSON.stringify({foto:photo})});
  const data=await response.json();
  if(!data.ok)throw Error(data.erro||'Não foi possível localizar o código de barras.');
  $('barcode').value=data.codigo;
  notice('registerMsg','Código reconhecido: '+data.codigo+' ('+data.formato+').',true);
  await checkBarcode();
 }catch(e){
  $('barcodeStatus').textContent='Leitura não concluída: '+e.message;
  notice('registerMsg','Use boa iluminação, mantenha as barras completas e nítidas, e fotografe de perto. Também é possível digitar o código.',false);
 }finally{URL.revokeObjectURL(url)}
});
$('register').onclick=async function(){
 if(locked)return;
 const code=$('barcode').value.trim(),name=$('productName').value.trim();
 const cost=Number($('cost').value),price=Number($('price').value),qty=Number($('initialQty').value);
 if(!/^[0-9]{8,32}$/.test(code)){notice('registerMsg','Informe um código numérico válido (8 a 32 dígitos).',false);return}
 if(!name||name.length<3||!$('cost').value||!$('price').value||!$('initialQty').value||![cost,price,qty].every(Number.isFinite)||Math.min(cost,price,qty)<0){
 notice('registerMsg','Confira descrição, custo, preço e quantidade inicial.',false);return}
 if(barcodeChecked!==code){notice('registerMsg','Clique em Conferir código no cadastro antes de salvar.',false);return}
 if($('barcodeStatus').textContent.startsWith('JÁ CADASTRADO')){notice('registerMsg','Produto existente. Use Entrada de estoque.',false);return}
 if(!confirm('Cadastrar '+name+'?\nCódigo: '+code+'\nCusto: R$ '+cost.toFixed(2)+'\nPreço: R$ '+price.toFixed(2)+'\nQuantidade inicial: '+fmt(qty)))return;
 locked=true;$('register').disabled=true;
 try{
 const response=await fetch('/api/cadastrar',{method:'POST',headers:{'Content-Type':'application/json','X-AMZG-Token':token},body:JSON.stringify({codigo:code,nome:name,custo:String(cost),preco:String(price),quantidade:String(qty),operacao:requestId()})});
 const data=await response.json();
 if(!data.ok)throw Error(data.erro||'Erro ao salvar cadastro.');
 notice('registerMsg','CADASTRADO! '+data.nome+' • Código: '+data.codigo+' • Quantidade: '+fmt(data.quantidade),true);
 $('barcode').value='';$('productName').value='';$('cost').value='';$('price').value='';$('initialQty').value='0';barcodeChecked='';
 $('barcodeStatus').textContent='Pronto para bipar o próximo produto.';
 }catch(e){notice('registerMsg','Cadastro não concluído: '+e.message,false)}
 finally{locked=false;$('register').disabled=false}
};
$('find').onclick=async function(){
 const q=$('search').value.trim();if(!q)return;
 try{
 const response=await fetch('/api/buscar?chave='+encodeURIComponent(token)+'&q='+encodeURIComponent(q),{cache:'no-store'});
 const data=await response.json();$('results').textContent='';
 if(!data.produtos||!data.produtos.length){$('results').textContent='Produto não encontrado.';return}
 for(const p of data.produtos){
 const el=document.createElement('div');el.className='item';const name=document.createElement('b');name.textContent=p.nome;el.appendChild(name);
 const info=document.createElement('small');info.textContent='Código: '+p.codigo+' • Estoque: '+fmt(p.quantidade);el.appendChild(info);
 el.onclick=()=>{selected=p;$('chosen').textContent=p.nome+' • Estoque atual: '+fmt(p.quantidade);preview();notice('msg','',false)};
 $('results').appendChild(el);
 }
 }catch(e){notice('msg','Falha na conexão: '+e.message,false)}
};
$('search').addEventListener('keydown',e=>{if(e.key==='Enter')$('find').click()});
$('save').onclick=async function(){
 if(locked)return;if(!selected){notice('msg','Selecione o produto.',false);return}
 const qty=parseFloat($('qty').value),description=$('reason').value.trim(),date=$('date').value;
 if(!isFinite(qty)||qty<=0||!description||!date){notice('msg','Informe data, quantidade e descrição.',false);return}
 if(!confirm('Entrada de '+fmt(qty)+' de '+selected.nome+'?\nAnterior: '+fmt(selected.quantidade)+'\nNovo: '+fmt(Number(selected.quantidade)+qty)))return;
 locked=true;$('save').disabled=true;notice('msg','Salvando com auditoria...',false);
 try{
 const response=await fetch('/api/entrada',{method:'POST',headers:{'Content-Type':'application/json','X-AMZG-Token':token},
 body:JSON.stringify({produto:selected.id,quantidade:String(qty),descricao:description,data:date,operacao:requestId()})});
 const data=await response.json();if(!data.ok)throw Error(data.erro||'Erro ao salvar.');
 selected.quantidade=data.atual;$('qty').value='';$('reason').value='';preview();
 $('chosen').textContent=selected.nome+' • Estoque atual: '+fmt(selected.quantidade);
 notice('msg','Salvo! '+data.produto+' | Anterior: '+fmt(data.anterior)+' + Entrada: '+fmt(data.adicionado)+' = '+fmt(data.atual),true);
 }catch(e){notice('msg','Verifique no computador antes de repetir: '+e.message,false)}
 finally{locked=false;$('save').disabled=false}
};
})();
</script></body></html>";}
 }
}