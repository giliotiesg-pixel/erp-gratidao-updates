using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
      if(length<0||length>4096){Write(stream,413,"text/plain","Requisição muito grande.");return;}
      byte[] payload=new byte[length];int count=0;
      while(count<length){int read=stream.Read(payload,count,length-count);if(read<=0)throw new IOException("Conexão interrompida.");count+=read;}
      string json=Encoding.UTF8.GetString(payload),path=target.Split('?')[0];
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
      if(method=="GET"&&path=="/api/codigo"){
       Write(stream,200,"application/json; charset=utf-8",FindBarcode(Param(query,"codigo")));return;
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
      return SafeJson(new{ok=true,id=id,nome=name,codigo=barcode,quantidade=qty,mensagem="Produto cadastrado e integrado ao estoque."});
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
<title>Armazém Gratidão - Estoque</title><style>
*{box-sizing:border-box}body{margin:0;background:#e8f2ff;color:#17365f;font-family:Arial,sans-serif}.box{max-width:550px;margin:auto;padding:15px}
h1{margin:12px 0;font-size:24px;color:#134e9f}.card{background:white;border-radius:15px;padding:18px;margin:12px 0;box-shadow:0 2px 9px #c8d6e7}
label{display:block;font-weight:bold;margin:12px 0 6px}input,textarea,select,button{width:100%;font:inherit;font-size:18px;padding:13px;border-radius:9px;border:1px solid #a7b5c6}
button{border:0;background:#1453a3;color:white;font-weight:bold;margin-top:13px}button.red{background:#bd243d}
.item{padding:12px;border-bottom:1px solid #dbe5f1;cursor:pointer}.item b{display:block}.item small{display:block;margin-top:4px}.selected{background:#e1efff}
.info{padding:12px;border-radius:8px;background:#f2f7fd;margin-top:10px}.warn{color:#9f1c31;font-weight:bold}.ok{color:#14613a;font-weight:bold}
p{line-height:1.5}</style></head><body><main class='box'><h1>Armazém Gratidão</h1><p>Entrada de estoque pelo celular • mesma rede Wi-Fi</p>
<section class='card'><label>Buscar nome ou código de barras</label><input id='search' placeholder='Digite ou use o microfone do teclado' autocomplete='off'>
<button id='find'>Buscar produto</button><div id='results'></div></section>
<section class='card'><h3>Registrar entrada</h3><div id='chosen' class='info'>Selecione um produto acima.</div>
<label>Data da entrada</label><input id='date' type='date'>
<label>Quantidade a adicionar</label><input id='qty' type='number' min='0.001' step='0.001' inputmode='decimal' placeholder='Ex.: 12'>
<label>Descrição do recebimento</label><textarea id='reason' rows='3' placeholder='Ex.: Mercadoria recebida do fornecedor'></textarea>
<div id='after' class='info'>Quantidade após entrada: —</div>
<button id='save' class='red'>Conferir e salvar entrada</button><p id='msg'></p>
</section><p><small>Use o microfone do teclado Android para ditar nomes e quantidades. Não é necessário microfone no computador. O estoque somente será gravado após confirmação.</small></p></main>
<script>
(function(){
const token=new URLSearchParams(location.search).get('chave')||'';
const $=id=>document.getElementById(id);
let selected=null,locked=false;
$('date').value=new Date().toLocaleDateString('sv-SE');
const fmt=n=>Number(n).toLocaleString('pt-BR',{minimumFractionDigits:3,maximumFractionDigits:3});
function err(message,ok){$('msg').textContent=message;$('msg').className=ok?'ok':'warn'}
function preview(){if(selected){const q=parseFloat($('qty').value)||0;$('after').textContent='Quantidade após entrada: '+fmt(Number(selected.quantidade)+q)}}
$('qty').addEventListener('input',preview);
$('find').onclick=async function(){
 const q=$('search').value.trim();if(!q)return;
 try{const r=await fetch('/api/buscar?chave='+encodeURIComponent(token)+'&q='+encodeURIComponent(q),{cache:'no-store'});const d=await r.json();
 $('results').textContent='';
 if(!d.produtos||!d.produtos.length){$('results').textContent='Produto não encontrado.';return;}
 for(const p of d.produtos){
 const el=document.createElement('div');el.className='item';
 const name=document.createElement('b');name.textContent=p.nome;el.appendChild(name);
 const info=document.createElement('small');info.textContent='Código: '+p.codigo+' • Estoque: '+fmt(p.quantidade);el.appendChild(info);
 el.onclick=function(){selected=p;$('chosen').textContent=p.nome+' • Estoque atual: '+fmt(p.quantidade);preview();err('',false);};
 $('results').appendChild(el);
 }
 }catch(e){err('Falha de conexão com o computador. '+e.message,false);}
};
$('search').addEventListener('keydown',e=>{if(e.key==='Enter')$('find').click()});
$('save').onclick=async function(){
 if(locked)return;
 if(!selected){err('Selecione o produto.',false);return;}
 const q=parseFloat($('qty').value),description=$('reason').value.trim(),date=$('date').value;
 if(!isFinite(q)||q<=0||!description||!date){err('Informe data, quantidade positiva e descrição.',false);return;}
 if(!confirm('Confirmar a entrada de '+fmt(q)+' de '+selected.nome+'?\nEstoque atual: '+fmt(selected.quantidade)+'\nNovo: '+fmt(Number(selected.quantidade)+q)))return;
 locked=true;$('save').disabled=true;err('Salvando e registrando auditoria...',false);
 const id=(Date.now().toString(36)+'-'+Math.random().toString(36).slice(2)+'-'+Math.random().toString(36).slice(2));
 try{const response=await fetch('/api/entrada',{method:'POST',headers:{'Content-Type':'application/json','X-AMZG-Token':token},body:JSON.stringify({produto:selected.id,quantidade:String(q),descricao:description,data:date,operacao:id})});
 const d=await response.json();if(!d.ok){err(d.erro||'Não foi possível salvar.',false);return;}
 selected.quantidade=d.atual;preview();$('qty').value='';$('reason').value='';
 err('Salvo! '+d.produto+' | Anterior: '+fmt(d.anterior)+' + Entrada: '+fmt(d.adicionado)+' = '+fmt(d.atual),true);
 $('chosen').textContent=selected.nome+' • Estoque atual: '+fmt(selected.quantidade);
 }catch(e){err('Conexão interrompida. Antes de tentar novamente confira no computador se a entrada foi gravada, para evitar duplicação.',false)}
 finally{locked=false;$('save').disabled=false;}
};
})();
</script></body></html>";}
 }
}