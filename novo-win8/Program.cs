using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace ArmazemGratidao {
 static class Program {
  [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Idle+=(sender,args)=>ButtonTextVisibility.CheckOpenForms(); Application.Run(new MainForm()); }
 }
 static class ButtonTextVisibility {
  static readonly System.Collections.Generic.Dictionary<Button,string> inspected=new System.Collections.Generic.Dictionary<Button,string>();
  public static void CheckOpenForms(){
   foreach(Form form in Application.OpenForms)Check(form);
  }
  static void Check(Control parent){
   foreach(Control child in parent.Controls){
    var button=child as Button;
    if(button!=null && !string.IsNullOrWhiteSpace(button.Text)){
     string signature=button.Text+"|"+button.Font.SizeInPoints+"|"+button.Width;
     string previous;
     if(!inspected.TryGetValue(button,out previous) || previous!=signature){
      var size=TextRenderer.MeasureText(button.Text,button.Font,new Size(1000,1000),TextFormatFlags.SingleLine);
      int desired=size.Width+24;
      if(button.Image!=null)desired+=button.Image.Width+8;
      if(button.Width<desired && button.Dock==DockStyle.None && button.Parent is FlowLayoutPanel){
       button.AutoSize=false;button.Width=desired;
      }
      button.AutoEllipsis=true;
      button.TextAlign=ContentAlignment.MiddleCenter;
      button.UseMnemonic=false;
      inspected[button]=button.Text+"|"+button.Font.SizeInPoints+"|"+button.Width;
     }
    }
    if(child.HasChildren)Check(child);
   }
  }
 }
 public sealed class MainForm : Form {
  readonly string dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidaoNovo");
  readonly string dbPath;
  int InstalledBuild { get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.Build; } }
  readonly FlowLayoutPanel sideMenu=new FlowLayoutPanel {Dock=DockStyle.Left,Width=245,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(8),BackColor=Color.FromArgb(20,69,147)};
  readonly Panel header=new Panel {Dock=DockStyle.Top,Height=55,BackColor=Color.FromArgb(18,75,163)};
  readonly Button menuToggle=new Button {Text="☰  Menu",Width=120,Height=38,BackColor=Color.White,ForeColor=Color.FromArgb(20,69,147)};
  readonly DataGridView grid=new DataGridView { Dock=DockStyle.Fill,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,AllowUserToAddRows=false };
  readonly Label status=new Label { Dock=DockStyle.Bottom,Height=34,TextAlign=ContentAlignment.MiddleLeft };
  public MainForm() {
   dbPath=Path.Combine(dataDir,"armazem-gratidao.db");
   Text="Armazém Gratidão Novo";Width=1200;Height=760;MinimumSize=new Size(950,600);Font=new Font("Segoe UI",10,FontStyle.Bold);BackColor=Color.FromArgb(245,247,251);
   var bar=sideMenu;
   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(20,69,147);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",10,FontStyle.Bold);grid.ColumnHeadersHeight=38;grid.RowTemplate.Height=32;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(241,245,252);
   status.BackColor=Color.FromArgb(234,238,247);status.ForeColor=Color.FromArgb(30,40,55);status.Padding=new Padding(12,0,0,0);
   menuToggle.Left=10;menuToggle.Top=8;menuToggle.Click+=(s,e)=>{sideMenu.Visible=!sideMenu.Visible;};header.Controls.Add(menuToggle);
   var brand=new Label {Text="ARMAZÉM GRATIDÃO",ForeColor=Color.White,Font=new Font("Segoe UI",16,FontStyle.Bold),AutoSize=true,Left=150,Top=12};header.Controls.Add(brand);
   AddSection(bar,"PRINCIPAL");
   AddButton(bar,"Início",ShowHome);
   AddButton(bar,"Verificar atualização",async (s,e)=>await CheckUpdates());
   AddSection(bar,"DADOS E RECUPERAÇÃO");
   AddButton(bar,"Importar banco",ImportDatabase);
   AddButton(bar,"Backup do banco",BackupDatabase);
   AddButton(bar,"Verificar banco",CheckDatabase);
   AddButton(bar,"Localizar vendas",CompareSales);
   AddButton(bar,"Preparar recuperação",PrepareRecoveredSales);
   AddButton(bar,"Aplicar recuperação",ApplyRecovery);
   AddButton(bar,"Conferir valores",CheckSaleDifferences);
   AddSection(bar,"OPERAÇÃO");
   AddButton(bar,"PDV - Nova Venda",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de vender.");return;}using(var pdv=new PdvForm(dbPath))pdv.ShowDialog(this);});
   AddButton(bar,"Vendas",(s,e)=>OpenConsult(false));
   AddButton(bar,"Itens",(s,e)=>ShowTable("sale_items","SELECT sale_id,name,quantity,unit_price,total FROM sale_items ORDER BY sale_id DESC LIMIT 5000"));
   AddButton(bar,"Fiado",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new FiadoClientesForm(dbPath))form.ShowDialog(this);});
   AddButton(bar,"Produtos",(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new ProdutosForm(dbPath))form.ShowDialog(this);});
   AddSection(bar,"GESTÃO");
   foreach(var module in new[]{"Estoque","Clientes","Compras","Caixa","Financeiro","Relatorios","Sistema"}) AddButton(bar,module,(s,e)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new ModuloConsultaForm(dbPath,module))form.ShowDialog(this);});
   Controls.Add(grid);Controls.Add(status);Controls.Add(bar);Controls.Add(header);Directory.CreateDirectory(dataDir);
   status.Text=File.Exists(dbPath)?"Banco encontrado. Selecione Vendas ou Fiado.":"Importe uma copia do banco SQLite.";
   ShowHome(this,EventArgs.Empty);
  }
  async Task CheckUpdates(){
   try{
    status.Text="Consultando versão publicada...";
    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
    string json;
    using(var web=new WebClient()){
     web.Headers[HttpRequestHeader.UserAgent]="ArmazemGratidaoNovo-Windows";
     web.Headers[HttpRequestHeader.Accept]="application/vnd.github+json";
     json=await web.DownloadStringTaskAsync(new Uri("https://api.github.com/repos/giliotiesg-pixel/erp-gratidao-updates/releases/latest"));
    }
    var release=new JavaScriptSerializer().DeserializeObject(json) as System.Collections.Generic.Dictionary<string,object>;
    if(release==null)throw new Exception("Resposta de atualização inválida.");
    string tag=Convert.ToString(release["tag_name"]);
    const string prefix="amzg-novo-win8-build-";
    int remoteBuild;
    if(!tag.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!int.TryParse(tag.Substring(prefix.Length),out remoteBuild))throw new Exception("Versão publicada não reconhecida.");
    if(remoteBuild<=InstalledBuild){status.Text="Versão atualizada (build "+InstalledBuild+").";MessageBox.Show("Você já está na versão mais recente disponível.\\nVersão instalada: build "+InstalledBuild);return;}
    var assets=release["assets"] as object[];if(assets==null)throw new Exception("Instalador não encontrado.");
    System.Collections.Generic.Dictionary<string,object> asset=null;
    foreach(object obj in assets){
     var item=obj as System.Collections.Generic.Dictionary<string,object>;
     if(item!=null&&Convert.ToString(item["name"])=="Armazem-Gratidao-Novo-Windows8-Setup.exe"){asset=item;break;}
    }
    if(asset==null)throw new Exception("O release não contém o instalador Windows.");
    string digest=asset.ContainsKey("digest")?Convert.ToString(asset["digest"]):"";
    if(!digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)||digest.Length!=71)throw new Exception("Atualização bloqueada: SHA-256 não publicado.");
    string url=Convert.ToString(asset["browser_download_url"]);
    if(!url.StartsWith("https://github.com/giliotiesg-pixel/erp-gratidao-updates/releases/download/",StringComparison.OrdinalIgnoreCase))throw new Exception("Endereço de atualização inválido.");
    if(MessageBox.Show("Nova versão: build "+remoteBuild+"\\nVersão atual: build "+InstalledBuild+"\\n\\nBaixar, conferir e iniciar a instalação?\\nO programa será fechado após iniciar o instalador.","Atualização do Armazém Gratidão",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){status.Text="Atualização cancelada.";return;}
    string dir=Path.Combine(Path.GetTempPath(),"ArmazemGratidaoNovo-Atualizacoes");
    Directory.CreateDirectory(dir);
    string installer=Path.Combine(dir,"Armazem-Gratidao-Novo-build-"+remoteBuild+"-Setup.exe");
    status.Text="Baixando atualização build "+remoteBuild+"...";
    using(var web=new WebClient())await web.DownloadFileTaskAsync(new Uri(url),installer);
    string actual;
    using(var stream=File.OpenRead(installer))using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    if(!string.Equals(actual,digest.Substring(7),StringComparison.OrdinalIgnoreCase)){File.Delete(installer);throw new Exception("SHA-256 inválido. Instalador rejeitado.");}
    if(File.Exists(dbPath)){
     string backup=Path.Combine(dataDir,"armazem-gratidao-antes-atualizacao-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db");
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
      source.Open();dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);
     }
    }
    Process.Start(new ProcessStartInfo(installer){UseShellExecute=true});
    Close();
   }catch(Exception ex){status.Text="Não foi possível atualizar.";MessageBox.Show(ex.Message,"Atualização não iniciada",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
  void ShowHome(object sender,EventArgs e){
   grid.Visible=false;
   var old=Controls["homePanel"];if(old!=null){Controls.Remove(old);old.Dispose();}
   var home=new FlowLayoutPanel {Name="homePanel",Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Padding=new Padding(22),BackColor=Color.FromArgb(245,247,251)};
   var title=new Label {Text="INÍCIO  •  ARMAZÉM GRATIDÃO",Width=940,Height=58,Font=new Font("Segoe UI",19,FontStyle.Bold),ForeColor=Color.FromArgb(20,69,147)};
   home.Controls.Add(title);
   var date=new Label {Text=DateTime.Now.ToString("dddd, dd/MM/yyyy  HH:mm"),Width=940,Height=36,Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=Color.FromArgb(160,20,40)};
   home.Controls.Add(date);
   var refresh=new Button {Text="Atualizar indicadores",Width=200,Height=40,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(20,69,147),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(8)};
   refresh.FlatAppearance.BorderSize=0;refresh.Click+=ShowHome;home.Controls.Add(refresh);
   home.Controls.Add(new Label {Text="",Width=680,Height=40});
   var metrics=new System.Collections.Generic.List<Tuple<string,string>>();
   if(File.Exists(dbPath)){
    try{
     using(var db=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
      db.Open();
      using(var q=new SQLiteCommand("SELECT COUNT(*) FROM sales WHERE substr(created_at,1,10)=@day AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",db)){
       q.Parameters.AddWithValue("@day",DateTime.Today.ToString("yyyy-MM-dd"));
       metrics.Add(Tuple.Create("VENDAS DO DIA",Convert.ToString(q.ExecuteScalar())));
      }
      using(var q=new SQLiteCommand("SELECT COALESCE(SUM(gross_total),0) FROM sales WHERE substr(created_at,1,10)=@day AND UPPER(COALESCE(status,'')) NOT LIKE '%EXCLUID%'",db)){
       q.Parameters.AddWithValue("@day",DateTime.Today.ToString("yyyy-MM-dd"));
       metrics.Add(Tuple.Create("TOTAL VENDIDO HOJE","R$ "+Convert.ToDecimal(q.ExecuteScalar()).ToString("N2")));
      }
      using(var q=new SQLiteCommand("SELECT COUNT(*) FROM products",db))
       metrics.Add(Tuple.Create("PRODUTOS CADASTRADOS",Convert.ToString(q.ExecuteScalar())));
     }
    }catch(Exception ex){metrics.Clear();metrics.Add(Tuple.Create("INDICADORES INDISPONÍVEIS","Verifique o banco: "+ex.Message));}
   }else metrics.Add(Tuple.Create("BANCO NÃO IMPORTADO","Use o botão Importar banco"));
   foreach(var metric in metrics){
    var panel=new Panel {Width=285,Height=103,BackColor=Color.White,Margin=new Padding(7,4,7,14)};
    panel.Controls.Add(new Label {Text=metric.Item1,Dock=DockStyle.Top,Height=34,Padding=new Padding(12,8,0,0),ForeColor=Color.FromArgb(160,20,40),Font=new Font("Segoe UI",9,FontStyle.Bold)});
    panel.Controls.Add(new Label {Text=metric.Item2,Dock=DockStyle.Bottom,Height=60,Padding=new Padding(12,8,0,0),ForeColor=Color.FromArgb(20,69,147),Font=new Font("Segoe UI",15,FontStyle.Bold),AutoEllipsis=true});
    home.Controls.Add(panel);
   }
   home.Controls.Add(new Label {Text="ACESSO RÁPIDO",Width=940,Height=35,ForeColor=Color.FromArgb(20,69,147),Font=new Font("Segoe UI",12,FontStyle.Bold)});

   var actions=new System.Collections.Generic.List<Tuple<string,EventHandler>>{
    Tuple.Create("PDV  •  Nova venda",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe o banco antes de vender.");return;}using(var form=new PdvForm(dbPath))form.ShowDialog(this);})),
    Tuple.Create("Vendas",(EventHandler)((s,a)=>OpenConsult(false))),
    Tuple.Create("Fiado",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new FiadoClientesForm(dbPath))form.ShowDialog(this);})),
    Tuple.Create("Produtos",(EventHandler)((s,a)=>{if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new ProdutosForm(dbPath))form.ShowDialog(this);})),
    Tuple.Create("Estoque",(EventHandler)((s,a)=>OpenHomeModule("Estoque"))),
    Tuple.Create("Clientes",(EventHandler)((s,a)=>OpenHomeModule("Clientes"))),
    Tuple.Create("Compras",(EventHandler)((s,a)=>OpenHomeModule("Compras"))),
    Tuple.Create("Caixa",(EventHandler)((s,a)=>OpenHomeModule("Caixa"))),
    Tuple.Create("Financeiro",(EventHandler)((s,a)=>OpenHomeModule("Financeiro"))),
    Tuple.Create("Relatórios",(EventHandler)((s,a)=>OpenHomeModule("Relatorios"))),
    Tuple.Create("Sistema",(EventHandler)((s,a)=>OpenHomeModule("Sistema"))),
    Tuple.Create("Backup do banco",(EventHandler)BackupDatabase),
    Tuple.Create("Verificar atualização",(EventHandler)(async (s,a)=>await CheckUpdates()))
   };
   foreach(var action in actions){
    var button=new Button {Text=action.Item1,Width=208,Height=78,Margin=new Padding(8),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(20,69,147),Font=new Font("Segoe UI",11,FontStyle.Bold)};
    button.FlatAppearance.BorderColor=Color.FromArgb(213,221,236);button.Click+=action.Item2;home.Controls.Add(button);
   }
   Controls.Add(home);home.BringToFront();header.BringToFront();sideMenu.BringToFront();status.BringToFront();status.Text="Início • Acesso rápido aos módulos. Banco de dados preservado.";
  }
  void OpenHomeModule(string module){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   using(var form=new ModuloConsultaForm(dbPath,module))form.ShowDialog(this);
  }
  void AddSection(Control parent,string title){parent.Controls.Add(new Label {Text=title,Width=175,Height=29,TextAlign=ContentAlignment.BottomLeft,ForeColor=Color.FromArgb(188,209,245),Font=new Font("Segoe UI",8,FontStyle.Bold),Padding=new Padding(7,0,0,4),Margin=new Padding(3,11,3,2)});}
  void AddButton(Control parent,string title,EventHandler click) {var b=new Button {Text=title,Width=175,Height=40,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(20,69,147),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,0,0),Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(3,3,3,3)};b.FlatAppearance.BorderSize=0;b.Click+=click;parent.Controls.Add(b);}
  void BackupDatabase(object sender,EventArgs e) {
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   using(var dlg=new SaveFileDialog {Title="Salvar copia de seguranca SQLite",Filter="Banco SQLite (*.db)|*.db",FileName="armazem-gratidao-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".db"}) {
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try {
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Escolha um destino diferente do banco em uso.");
     if(File.Exists(dlg.FileName))throw new Exception("Escolha um nome de arquivo novo para nao sobrescrever backups anteriores.");
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,FailIfMissing=false}.ToString())) {
      source.Open();dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);
     }
     using(var check=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())) {
      check.Open();using(var cmd=new SQLiteCommand("PRAGMA integrity_check",check))if(Convert.ToString(cmd.ExecuteScalar())!="ok")throw new Exception("Backup criado, mas a verificacao de integridade falhou.");
     }
     status.Text="Backup SQLite criado e validado: "+dlg.FileName;
     MessageBox.Show("Backup concluido e verificado. O banco em uso nao foi modificado.");
    }catch(Exception ex){MessageBox.Show(ex.Message,"Backup nao concluido",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void CheckDatabase(object sender,EventArgs e) {
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}
   try {
    using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
     conn.Open();
     string result;using(var cmd=new SQLiteCommand("PRAGMA integrity_check",conn))result=Convert.ToString(cmd.ExecuteScalar());
     if(result!="ok")throw new Exception("Integridade SQLite: "+result);
     var counts=new System.Text.StringBuilder("Integridade SQLite: OK"+Environment.NewLine);
     foreach(var table in new[]{"sales","sale_items","credit_payments","products"}){
      using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM "+table,conn))counts.AppendLine(table+": "+cmd.ExecuteScalar());
     }
     status.Text="Integridade do banco: OK. Consultas realizadas sem alteracoes.";
     MessageBox.Show(counts.ToString(),"Verificacao do banco");
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Problema na verificacao",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
  static string SalesSummary(string path) {
   using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=path,ReadOnly=true,FailIfMissing=true}.ToString())){
    conn.Open();
    using(var cmd=new SQLiteCommand("SELECT COUNT(*),COALESCE(MAX(id),0),COALESCE(MAX(created_at),'SEM DATA') FROM sales",conn))
    using(var reader=cmd.ExecuteReader()){
     if(!reader.Read())return "Nenhuma venda encontrada.";
     return "Quantidade: "+reader.GetValue(0)+" | Último número: "+reader.GetValue(1)+" | Data mais recente: "+reader.GetValue(2);
    }
   }
  }
  void CheckSaleDifferences(object sender,EventArgs e){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro um banco.");return;}
   using(var dlg=new OpenFileDialog {Title="Selecione o banco original para conferir valores das vendas",Filter="Bancos SQLite|*.db;*.sqlite;*.sqlite3|Todos os arquivos|*.*"}){
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try{
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Selecione um banco diferente do importado.");
     var imported=new System.Collections.Generic.Dictionary<long,decimal>();
     using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
      conn.Open();using(var cmd=new SQLiteCommand("SELECT id,gross_total FROM sales",conn))using(var reader=cmd.ExecuteReader())
       while(reader.Read())imported[Convert.ToInt64(reader.GetValue(0))]=Convert.ToDecimal(reader.GetValue(1));
     }
     var differences=new System.Collections.Generic.List<string>();
     long compared=0;
     using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
      conn.Open();using(var cmd=new SQLiteCommand("SELECT id,gross_total FROM sales ORDER BY id",conn))using(var reader=cmd.ExecuteReader()){
       while(reader.Read()){
        long id=Convert.ToInt64(reader.GetValue(0));
        decimal original=Convert.ToDecimal(reader.GetValue(1));
        decimal current;
        if(!imported.TryGetValue(id,out current))continue;
        compared++;
        if(Math.Abs(original-current)>0.005m)differences.Add(id+";"+original.ToString(System.Globalization.CultureInfo.InvariantCulture)+";"+current.ToString(System.Globalization.CultureInfo.InvariantCulture));
       }
      }
     }
     string msg="Vendas presentes nos dois bancos: "+compared+Environment.NewLine+
      "Vendas com total diferente: "+differences.Count+Environment.NewLine+
      "Comparação somente de leitura; não altera banco, vendas ou estoque.";
     if(differences.Count==0){MessageBox.Show(msg,"Conferir valores");return;}
     if(MessageBox.Show(msg+Environment.NewLine+"Salvar relatório CSV?","Conferir valores",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
     using(var save=new SaveFileDialog {Title="Salvar diferenças de valores",Filter="CSV (*.csv)|*.csv",FileName="divergencias-vendas-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"}){
      if(save.ShowDialog()!=DialogResult.OK)return;
      if(File.Exists(save.FileName))throw new Exception("Escolha um nome novo para não sobrescrever relatório anterior.");
      using(var writer=new StreamWriter(save.FileName,false,new System.Text.UTF8Encoding(true))){
       writer.WriteLine("numero_venda;valor_original;valor_importado");
       foreach(string line in differences)writer.WriteLine(line);
      }
      MessageBox.Show("Relatório salvo: "+save.FileName);
     }
    }catch(Exception ex){MessageBox.Show(ex.Message,"Conferência não concluída",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void ApplyRecovery(object sender,EventArgs e){
   MessageBox.Show("Aplicacao automatica temporariamente bloqueada: a validacao anterior comparava apenas contagens e poderia substituir registros alterados, inclusive do Fiado. O banco ativo foi preservado. Use Preparar recuperacao para gerar e conferir uma copia de teste.","Protecao contra perda de dados",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   return;

   if(!File.Exists(dbPath)){MessageBox.Show("Banco atual não encontrado.");return;}
   using(var pick=new OpenFileDialog {Title="Selecione o banco de TESTE gerado em Preparar recuperação",Filter="Banco SQLite (*.db)|*.db"}){
    if(pick.ShowDialog()!=DialogResult.OK)return;
    if(Path.GetFullPath(pick.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Selecione um banco de teste diferente do banco atual.");return;}
    try{
     int currentCount,testCount,extra;
     using(var current=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var candidate=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=pick.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
      current.Open();candidate.Open();
      foreach(var conn in new[]{current,candidate}){
       using(var check=new SQLiteCommand("PRAGMA integrity_check",conn))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Integridade SQLite inválida.");
      }
      using(var q=new SQLiteCommand("SELECT COUNT(*) FROM sales",current))currentCount=Convert.ToInt32(q.ExecuteScalar());
      using(var q=new SQLiteCommand("SELECT COUNT(*) FROM sales",candidate))testCount=Convert.ToInt32(q.ExecuteScalar());
      var currentIds=new System.Collections.Generic.HashSet<long>();
      using(var q=new SQLiteCommand("SELECT id FROM sales",current))using(var reader=q.ExecuteReader())while(reader.Read())currentIds.Add(Convert.ToInt64(reader.GetValue(0)));
      extra=0;
      using(var q=new SQLiteCommand("SELECT id FROM sales",candidate))using(var reader=q.ExecuteReader())while(reader.Read())if(!currentIds.Contains(Convert.ToInt64(reader.GetValue(0))))extra++;
      if(extra==0){MessageBox.Show("Nenhuma venda nova identificada. Banco atual preservado.");return;}
      if(testCount!=currentCount+extra)throw new Exception("O banco de teste não contém todas as vendas atuais. Recuperação bloqueada.");
      foreach(var table in new[]{"sales","sale_items","sale_payments","credit_payments","products"}){
       using(var a=new SQLiteCommand("SELECT COUNT(*) FROM "+table,current))
       using(var b=new SQLiteCommand("SELECT COUNT(*) FROM "+table,candidate)){
        long x=Convert.ToInt64(a.ExecuteScalar()),y=Convert.ToInt64(b.ExecuteScalar());
        if(y<x)throw new Exception("Banco de teste perdeu registros em "+table+". Aplicação bloqueada.");
        if(table=="products" && x!=y)throw new Exception("Quantidade de produtos diferente. Aplicação bloqueada.");
       }
      }
     }
     if(MessageBox.Show("Foram identificadas "+extra+" vendas adicionais no banco de teste."+Environment.NewLine+
      "O programa fará um BACKUP do banco atual e depois substituirá a cópia de trabalho pelo banco de teste."+Environment.NewLine+
      "Faça isso somente com o caixa parado e depois de conferir os dados. Deseja continuar?",
      "CONFIRMAR RECUPERAÇÃO",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
     string backup=dbPath+".antes-recuperacao-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".db";
     using(var current=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
     using(var target=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup,FailIfMissing=false}.ToString())){
      current.Open();target.Open();current.BackupDatabase(target,"main","main",-1,null,0);
     }
     string staged=Path.Combine(dataDir,"recuperacao-"+Guid.NewGuid().ToString("N")+".db");
     try{
      using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=pick.FileName,ReadOnly=true,FailIfMissing=true}.ToString()))
      using(var target=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=staged,FailIfMissing=false}.ToString())){
       source.Open();target.Open();source.BackupDatabase(target,"main","main",-1,null,0);
      }
      using(var verify=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=staged,ReadOnly=true,FailIfMissing=true}.ToString())){
       verify.Open();using(var q=new SQLiteCommand("PRAGMA integrity_check",verify))if(Convert.ToString(q.ExecuteScalar())!="ok")throw new Exception("Falha na verificação final da cópia.");
      }
      File.Replace(staged,dbPath,null);
      status.Text="Recuperação aplicada. "+extra+" vendas adicionais. Backup: "+backup;
      MessageBox.Show("Recuperação aplicada. Vendas adicionais: "+extra+Environment.NewLine+"Backup do banco anterior: "+backup+Environment.NewLine+"Confira Vendas e Fiado antes de continuar a operar.","Recuperação concluída");
     }finally{if(File.Exists(staged))File.Delete(staged);}
    }catch(Exception ex){MessageBox.Show("Recuperação não aplicada: "+ex.Message,"Recuperação bloqueada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void PrepareRecoveredSales(object sender,EventArgs e){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco atual.");return;}
   using(var pick=new OpenFileDialog {Title="Selecione o banco SQLite com vendas recuperadas",Filter="Banco SQLite (*.db)|*.db"}){
    if(pick.ShowDialog()!=DialogResult.OK)return;
    if(Path.GetFullPath(pick.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Selecione outro banco.");return;}
    using(var save=new SaveFileDialog {Title="Salvar banco de teste com vendas recuperadas",Filter="Banco SQLite (*.db)|*.db",FileName="armazem-recuperado-teste-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".db"}){
     if(save.ShowDialog()!=DialogResult.OK)return;
     if(File.Exists(save.FileName)){MessageBox.Show("Escolha um arquivo novo, sem sobrescrever.");return;}
     if(Path.GetFullPath(save.FileName).Equals(Path.GetFullPath(pick.FileName),StringComparison.OrdinalIgnoreCase)){MessageBox.Show("O destino não pode ser a origem.");return;}
     try{
      int imported=0,items=0,credits=0,payments=0;
      using(var original=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString()))
      using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=save.FileName,FailIfMissing=false}.ToString()))
      using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=pick.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
       original.Open();dest.Open();source.Open();
       using(var cmd=new SQLiteCommand("PRAGMA integrity_check",source))if(Convert.ToString(cmd.ExecuteScalar())!="ok")throw new Exception("Banco de recuperação com erro de integridade.");
       original.BackupDatabase(dest,"main","main",-1,null,0);
       var absent=new System.Collections.Generic.HashSet<long>();
       using(var cmd=new SQLiteCommand("SELECT id FROM sales",source))using(var reader=cmd.ExecuteReader()){
        while(reader.Read()){
         long id=Convert.ToInt64(reader.GetValue(0));
         using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sales WHERE id=@id",dest)){
          check.Parameters.AddWithValue("@id",id);
          if(Convert.ToInt32(check.ExecuteScalar())==0)absent.Add(id);
         }
        }
       }
       if(absent.Count==0){MessageBox.Show("Nenhuma venda ausente encontrada. Banco de teste criado sem alterações.");return;}
       if(MessageBox.Show("Foram identificadas "+absent.Count+" vendas ausentes. Criar uma CÓPIA de teste com essas vendas, itens e pagamentos? O estoque não será movimentado e o banco em uso ficará intacto.","Preparar recuperação",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
       using(var tx=dest.BeginTransaction()){
        foreach(string table in new[]{"sales","sale_items","sale_payments","credit_payments"}){
         var columns=new System.Collections.Generic.List<string>();
         using(var schema=new SQLiteCommand("PRAGMA table_info("+table+")",source))using(var reader=schema.ExecuteReader()){
          while(reader.Read())columns.Add(Convert.ToString(reader["name"]));
         }
         if(columns.Count==0)throw new Exception("Tabela ausente no arquivo de recuperação: "+table);
         var target=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
         using(var schema=new SQLiteCommand("PRAGMA table_info("+table+")",dest,tx))using(var reader=schema.ExecuteReader()){
          while(reader.Read())target.Add(Convert.ToString(reader["name"]));
         }
         foreach(string col in columns)if(!target.Contains(col))throw new Exception("Coluna incompatível em "+table+": "+col);
         string quoted=string.Join(",",columns.ConvertAll(x=>"\""+x.Replace("\"","\"\"")+"\"").ToArray());
         using(var query=new SQLiteCommand("SELECT "+quoted+" FROM "+table,source))
         using(var reader=query.ExecuteReader()){
          while(reader.Read()){
           long saleId=Convert.ToInt64(reader[table=="sales"?"id":"sale_id"]);
           if(!absent.Contains(saleId))continue;
           long rowId=Convert.ToInt64(reader["id"]);
           using(var check=new SQLiteCommand("SELECT COUNT(*) FROM "+table+" WHERE id=@id",dest,tx)){
            check.Parameters.AddWithValue("@id",rowId);
            if(Convert.ToInt32(check.ExecuteScalar())!=0)throw new Exception("Conflito de identificador em "+table+" ID "+rowId+". Nenhuma alteração será salva.");
           }
           string placeholders=string.Join(",",columns.ConvertAll(x=>"@"+x).ToArray());
           using(var insert=new SQLiteCommand("INSERT INTO "+table+" ("+quoted+") VALUES ("+placeholders+")",dest,tx)){
            foreach(string col in columns)insert.Parameters.AddWithValue("@"+col,reader[col]==DBNull.Value?(object)DBNull.Value:reader[col]);
            insert.ExecuteNonQuery();
           }
           if(table=="sales")imported++;
           if(table=="sale_items")items++;
           if(table=="credit_payments")credits++;
           if(table=="sale_payments")payments++;
          }
         }
        }
        tx.Commit();
       }
       using(var check=new SQLiteCommand("PRAGMA integrity_check",dest))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Falha na integridade do banco de teste.");
      }
      MessageBox.Show("BANCO DE TESTE CRIADO"+Environment.NewLine+"Vendas: "+imported+Environment.NewLine+"Itens: "+items+Environment.NewLine+"Pagamentos Fiado: "+credits+Environment.NewLine+"Outros pagamentos: "+payments+Environment.NewLine+Environment.NewLine+"Arquivo: "+save.FileName+Environment.NewLine+"Banco original e estoque preservados.","Recuperação preparada");
     }catch(Exception ex){MessageBox.Show(ex.Message+Environment.NewLine+"O banco original não foi modificado. Se existir um arquivo de teste incompleto, descarte-o.","Recuperação interrompida",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
   }
  }
  void CompareSales(object sender,EventArgs e){
   if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro um banco para comparar.");return;}
   using(var dlg=new OpenFileDialog {Title="Selecione o banco original para localizar vendas posteriores (somente leitura)",Filter="Bancos SQLite|*.db;*.sqlite;*.sqlite3|Todos os arquivos|*.*"}){
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try{
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Escolha o banco original, diferente do banco importado.");
     string imported=SalesSummary(dbPath),original=SalesSummary(dlg.FileName);
     var known=new System.Collections.Generic.HashSet<long>();
     using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true,FailIfMissing=true}.ToString())){
      c.Open();using(var q=new SQLiteCommand("SELECT id FROM sales",c))using(var reader=q.ExecuteReader())
       while(reader.Read())known.Add(Convert.ToInt64(reader.GetValue(0)));
     }
     var missing=new System.Collections.Generic.List<string[]>();
     using(var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())){
      c.Open();using(var q=new SQLiteCommand("SELECT id,created_at,customer_name,gross_total FROM sales ORDER BY id",c))
      using(var reader=q.ExecuteReader()){
       while(reader.Read()){
        long id=Convert.ToInt64(reader.GetValue(0));
        if(!known.Contains(id))missing.Add(new string[]{id.ToString(),Convert.ToString(reader.GetValue(1)),Convert.ToString(reader.GetValue(2)),Convert.ToString(reader.GetValue(3))});
       }
      }
     }
     string message="BANCO IMPORTADO"+Environment.NewLine+imported+Environment.NewLine+Environment.NewLine+
      "BANCO ORIGINAL SELECIONADO"+Environment.NewLine+original+Environment.NewLine+Environment.NewLine+
      "Vendas ausentes por número de identificação: "+missing.Count+Environment.NewLine+
      "A comparação considera todos os números, inclusive lacunas antigas."+Environment.NewLine+
      "Nenhum registro foi alterado. Números iguais podem conter dados diferentes.";
     if(missing.Count>0 && MessageBox.Show(message+Environment.NewLine+Environment.NewLine+"Deseja salvar a relação das vendas ausentes em CSV?","Localizar vendas ausentes",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes){
      using(var save=new SaveFileDialog {Title="Salvar relação de vendas ausentes",Filter="CSV (*.csv)|*.csv",FileName="vendas-ausentes-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"}){
       if(save.ShowDialog()==DialogResult.OK){
        if(File.Exists(save.FileName))throw new Exception("Escolha um nome de relatório novo para não sobrescrever arquivos.");
        using(var writer=new StreamWriter(save.FileName,false,new System.Text.UTF8Encoding(true))){
         writer.WriteLine("numero;data;cliente;valor");
         foreach(var sale in missing){
          var parts=new System.Collections.Generic.List<string>();
          foreach(var value in sale)parts.Add(((char)34)+value.Replace(((char)34).ToString(),new string((char)34,2))+((char)34));
          writer.WriteLine(string.Join(";",parts.ToArray()));
         }
        }
        MessageBox.Show("Relatório salvo: "+save.FileName);
       }
      }
     }else if(missing.Count==0)MessageBox.Show(message,"Localizar vendas ausentes");
    }catch(Exception ex){MessageBox.Show(ex.Message,"Comparação não concluída",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void ImportDatabase(object sender,EventArgs e) {
   // Nunca substituir um banco existente durante importacao.
   // O fluxo de recuperacao cria uma COPIA de teste e preserva os registros ativos.
   if(File.Exists(dbPath)){
    MessageBox.Show("Ja existe um banco de dados no programa. A importacao direta foi bloqueada para impedir que vendas, fiado, produtos e pagamentos desaparecam.\n\nSelecione o banco de origem na proxima janela para PREPARAR uma recuperacao em copia de teste. O banco atual nao sera substituido.","Protecao dos dados",MessageBoxButtons.OK,MessageBoxIcon.Information);
    PrepareRecoveredSales(sender,e);
    return;
   }
   using(var dlg=new OpenFileDialog {Title="Selecionar banco antigo (somente leitura)",Filter="Bancos SQLite|*.db;*.sqlite;*.sqlite3|Todos os arquivos|*.*"}) {
    if(dlg.ShowDialog()!=DialogResult.OK)return;
    try {
     if(Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(dbPath),StringComparison.OrdinalIgnoreCase))throw new Exception("Selecione um banco de origem diferente.");
     using(var source=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dlg.FileName,ReadOnly=true,FailIfMissing=true}.ToString())) {
      source.Open();
      foreach(var table in new[]{"sales","sale_items","credit_payments","products"}) {
       using(var cmd=new SQLiteCommand("SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@name",source)) {cmd.Parameters.AddWithValue("@name",table);if(Convert.ToInt32(cmd.ExecuteScalar())==0)throw new Exception("Tabela ausente: "+table);}
      }
      var temp=Path.Combine(dataDir,"importacao-"+Guid.NewGuid().ToString("N")+".db");
      try {
       using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=temp,FailIfMissing=false}.ToString())) {dest.Open();source.BackupDatabase(dest,"main","main",-1,null,0);}
       using(var verify=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=temp,ReadOnly=true}.ToString())) {verify.Open();using(var check=new SQLiteCommand("PRAGMA integrity_check",verify))if(Convert.ToString(check.ExecuteScalar())!="ok")throw new Exception("Falha na verificacao SQLite");}
       if(File.Exists(dbPath))File.Copy(dbPath,dbPath+".backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),false);
       File.Copy(temp,dbPath,true);
      } finally {if(File.Exists(temp))File.Delete(temp);}
     }
     status.Text="Banco importado. Original preservado.";ShowTable("sales","SELECT id,created_at,customer_name,payment_method,gross_total,status FROM sales ORDER BY created_at DESC LIMIT 2000");
    } catch(Exception ex) {MessageBox.Show(ex.Message,"Importacao nao concluida",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  void OpenConsult(bool credit) {if(!File.Exists(dbPath)){MessageBox.Show("Importe primeiro o banco.");return;}using(var form=new ConsultaForm(dbPath,credit))form.ShowDialog(this);}
  void ShowTable(string table,string sql) {
   if(!File.Exists(dbPath)) {MessageBox.Show("Importe primeiro o banco de dados.");return;}
   try {using(var conn=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=dbPath,ReadOnly=true}.ToString())) {conn.Open();var dt=new DataTable();using(var cmd=new SQLiteCommand(sql,conn))using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(dt);grid.DataSource=dt;var home=Controls["homePanel"];if(home!=null){Controls.Remove(home);home.Dispose();}grid.Visible=true;grid.BringToFront();header.BringToFront();sideMenu.BringToFront();status.BringToFront();status.Text=table+": "+dt.Rows.Count+" registros exibidos (somente leitura).";}}
   catch(Exception ex) {MessageBox.Show(ex.Message,"Erro na consulta",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}