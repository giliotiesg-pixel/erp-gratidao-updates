using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace ArmazemGratidao {
 public sealed class PdvForm : Form {
  sealed class Item {public long Id;public string Barcode,InternalCode,Name;public decimal Qty,Price,Cost;public string Unit="UN";public decimal Total {get{return Math.Round(Qty*Price,2);}}}
  readonly string db;
  bool finishingSale=false;
  readonly List<Item> cart=new List<Item>();
  readonly TextBox code=new TextBox {Width=260,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  readonly NumericUpDown qty=new NumericUpDown {Minimum=0.001M,Maximum=999999,DecimalPlaces=3,Increment=1M,Value=1,Width=95};
  readonly ComboBox saleUnit=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=80};
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
  readonly Label total=new Label {AutoSize=true,Font=new Font("Segoe UI",20,FontStyle.Bold),ForeColor=Color.FromArgb(175,25,45)};
  readonly Label changeLabel=new Label {AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(22,75,160),Padding=new Padding(0,9,0,0)};
  readonly ComboBox payment=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
  readonly ComboBox customer=new ComboBox {Width=205,DropDownStyle=ComboBoxStyle.DropDown};
  readonly Timer lookupDelay=new Timer {Interval=250};
  readonly ListBox suggestions=new ListBox {Visible=false,Width=510,Height=190,Font=new Font("Segoe UI",10)};
  readonly Dictionary<int,string> suggestionCodes=new Dictionary<int,string>();
  bool suppressLookup=false;
  readonly NumericUpDown received=new NumericUpDown {DecimalPlaces=2,Maximum=99999999,Width=130};
  public PdvForm(string database) {
   db=database;Text="Armazem Gratidao - PDV";Width=1100;Height=730;BackColor=Color.FromArgb(229,242,255);
   var header=new FlowLayoutPanel {Dock=DockStyle.Top,Height=64,Padding=new Padding(8),BackColor=Color.FromArgb(220,95,105)};
   header.Controls.Add(new Label {Text="Código / Nome:",AutoSize=true,ForeColor=Color.Black,Padding=new Padding(0,9,0,0)});
   header.Controls.Add(code);header.Controls.Add(new Label {Text="Qtd:",AutoSize=true,ForeColor=Color.Black,Padding=new Padding(0,9,0,0)});header.Controls.Add(qty);
   saleUnit.Items.AddRange(new object[]{"UN","KG"});saleUnit.SelectedIndex=0;
   saleUnit.SelectedIndexChanged+=(s,e)=>{bool byKg=Convert.ToString(saleUnit.SelectedItem)=="KG";qty.Increment=byKg?0.001M:1M;qty.DecimalPlaces=byKg?3:0;if(!byKg&&qty.Value!=decimal.Truncate(qty.Value))qty.Value=decimal.Ceiling(qty.Value);};
   header.Controls.Add(new Label {Text="Unidade:",AutoSize=true,ForeColor=Color.Black,Padding=new Padding(0,9,0,0)});header.Controls.Add(saleUnit);
   var add=new Button {Text="ADICIONAR PRODUTO",Width=190,Height=36,Font=new Font("Segoe UI",10,FontStyle.Bold)};add.Click+=(s,e)=>AddProduct();header.Controls.Add(add);
   code.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Down&&suggestions.Visible&&suggestions.Items.Count>0){suggestions.Focus();suggestions.SelectedIndex=0;e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;lookupDelay.Stop();suggestions.Visible=false;AddProduct();}};
   code.TextChanged+=(s,e)=>{lookupDelay.Stop();if(!suppressLookup&&code.Text.Trim().Length>=2)lookupDelay.Start();else suggestions.Visible=false;};
   lookupDelay.Tick+=(s,e)=>{lookupDelay.Stop();FindSuggestions();};
   suggestions.DoubleClick+=(s,e)=>ChooseSuggestion();
   suggestions.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;ChooseSuggestion();}else if(e.KeyCode==Keys.Escape){suggestions.Visible=false;code.Focus();}};
   var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=126,Padding=new Padding(10),AutoScroll=true};
   footer.Controls.Add(new Label {Text="Pagamento:",AutoSize=true,Padding=new Padding(0,9,0,0)});
   payment.Items.AddRange(new object[]{"DINHEIRO","PIX","PIX_QR","PIX_SEM_QR","DEBITO","CREDITO","ALIMENTACAO","ALELO","PLUXXE","TICKET","VR","FIADO","DIVIDIDO"});payment.SelectedIndex=0;footer.Controls.Add(payment);
   footer.Controls.Add(new Label {Text="Cliente:",AutoSize=true,Padding=new Padding(0,9,0,0)});footer.Controls.Add(customer);
   customer.Text="Consumidor";
   var chooseCustomer=new Button {Text="CLIENTE",Width=110,Height=36,BackColor=Color.FromArgb(215,218,222),ForeColor=Color.Black,Font=new Font("Segoe UI",10,FontStyle.Bold)};
   chooseCustomer.Click+=(s,e)=>{LoadCustomerOptions();customer.Focus();customer.DroppedDown=true;};footer.Controls.Add(chooseCustomer);
   var registerCustomer=new Button {Text="CADASTRAR CLIENTE",Width=165,Height=36,BackColor=Color.FromArgb(31,104,172),ForeColor=Color.White};registerCustomer.Click+=(s,e)=>QuickRegisterCustomer();footer.Controls.Add(registerCustomer);
   customer.Enter+=(s,e)=>LoadCustomerOptions();
   payment.SelectedIndexChanged+=(s,e)=>{if(Convert.ToString(payment.SelectedItem)=="FIADO")LoadCustomerOptions();};
   footer.Controls.Add(new Label {Text="Recebido:",AutoSize=true,Padding=new Padding(0,9,0,0)});footer.Controls.Add(received);footer.Controls.Add(changeLabel);
   payment.SelectedIndexChanged+=(s,e)=>UpdateChange();received.ValueChanged+=(s,e)=>UpdateChange();
   received.Enter+=(s,e)=>received.Select(0,received.Text.Length);
   var finish=new Button {Text="FINALIZAR VENDA",Height=43,Width=180,BackColor=Color.FromArgb(30,100,175),ForeColor=Color.White};finish.Click+=(s,e)=>Finish();footer.Controls.Add(finish);
   var edit=new Button {Text="EDITAR QUANTIDADE / PREÇO",Width=235,Height=40,Font=new Font("Segoe UI",9,FontStyle.Bold)};edit.Click+=(s,e)=>EditSelected();footer.Controls.Add(edit);
   var remove=new Button {Text="REMOVER ITEM",Width=145,Height=40,Font=new Font("Segoe UI",9,FontStyle.Bold)};remove.Click+=(s,e)=>RemoveSelected();footer.Controls.Add(remove);
   var clear=new Button {Text="LIMPAR CARRINHO",Width=155,Height=40,Font=new Font("Segoe UI",9,FontStyle.Bold)};clear.Click+=(s,e)=>ClearCart();footer.Controls.Add(clear);footer.Controls.Add(total);
   grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditSelected();};
   grid.Font=new Font("Segoe UI",11,FontStyle.Regular);grid.RowTemplate.Height=38;
   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(22,75,160);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   grid.ColumnHeadersHeight=38;grid.RowHeadersVisible=false;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(238,244,252);
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(40,100,170);grid.DefaultCellStyle.SelectionForeColor=Color.White;
   grid.CellFormatting+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&e.Value!=null){string name=grid.Columns[e.ColumnIndex].Name.ToLowerInvariant();if(name.Contains("descri")||name.Contains("nome")||name.Contains("name")){e.CellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;e.CellStyle.Padding=new Padding(7,4,7,4);}}};
   grid.DataBindingComplete+=(s,e)=>{foreach(DataGridViewColumn column in grid.Columns){if(column.Name.ToLowerInvariant().Contains("descri")||column.Name.ToLowerInvariant().Contains("nome")||column.Name.ToLowerInvariant().Contains("name")){column.MinimumWidth=260;column.FillWeight=240;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}}grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;};
   Controls.Add(grid);Controls.Add(footer);Controls.Add(header);
   suggestions.Parent=this;suggestions.Left=header.Left+130;suggestions.Top=header.Bottom;suggestions.BringToFront();
   Render();code.Focus();
  }
  SQLiteConnection Open() {var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,FailIfMissing=true}.ToString());c.Open();return c;}
  static decimal Dec(object o) {return Convert.ToDecimal(o,CultureInfo.InvariantCulture);}
  void LoadCustomerOptions(){
   try{
    string typed=customer.Text;
    var names=new List<string>();
    var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using(var connection=Open()){
     using(var cmd=new SQLiteCommand("SELECT DISTINCT TRIM(customer_name) FROM sales WHERE customer_name IS NOT NULL AND TRIM(customer_name)<>'' ORDER BY TRIM(customer_name) LIMIT 500",connection))
     using(var reader=cmd.ExecuteReader())while(reader.Read()){
      string name=reader.GetString(0);if(existing.Add(name))names.Add(name);
     }
     using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customers'",connection)){
      if(Convert.ToInt32(check.ExecuteScalar())>0){
       using(var cmd=new SQLiteCommand("SELECT DISTINCT TRIM(name) FROM customers WHERE name IS NOT NULL AND TRIM(name)<>'' ORDER BY TRIM(name) LIMIT 1000",connection))
       using(var reader=cmd.ExecuteReader())while(reader.Read()){
        string name=reader.GetString(0);if(existing.Add(name))names.Add(name);
       }
      }
     }
    }
    names.Sort(StringComparer.CurrentCultureIgnoreCase);
    customer.BeginUpdate();customer.Items.Clear();customer.Items.AddRange(names.Cast<object>().ToArray());customer.EndUpdate();
    customer.Text=typed;customer.SelectionStart=customer.Text.Length;
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao buscar clientes");}
  }
  bool CustomerRegistered(string name){
   if(string.IsNullOrWhiteSpace(name))return false;
   using(var connection=Open()){
    using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customers'",connection)){
     if(Convert.ToInt32(check.ExecuteScalar())>0){
      using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM customers WHERE LOWER(TRIM(name))=LOWER(TRIM(@name))",connection)){
       cmd.Parameters.AddWithValue("@name",name);
       if(Convert.ToInt32(cmd.ExecuteScalar())>0)return true;
      }
     }
    }
    // Clientes históricos existentes no Fiado continuam válidos, sem cópias.
    using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM sales WHERE LOWER(TRIM(COALESCE(customer_name,'')))=LOWER(TRIM(@name))",connection)){
     cmd.Parameters.AddWithValue("@name",name);
     return Convert.ToInt32(cmd.ExecuteScalar())>0;
    }
   }
  }
  static string CustomerDigits(string input){
   return new string((input??"").Where(char.IsDigit).ToArray());
  }
  bool QuickRegisterCustomer(){
   string initial=customer.Text.Trim();
   if(string.Equals(initial,"Consumidor",StringComparison.OrdinalIgnoreCase))initial="";
   using(var form=new Form{Text="PDV | Cadastro rápido de cliente",Width=550,Height=335,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
    var name=new TextBox{Left=165,Top=20,Width=335,Text=initial,MaxLength=180,Font=new Font("Segoe UI",11)};
    var phone=new TextBox{Left=165,Top=70,Width=335,MaxLength=40,Font=new Font("Segoe UI",11)};
    var doc=new TextBox{Left=165,Top=120,Width=335,MaxLength=20,Font=new Font("Segoe UI",11)};
    var hint=new Label{Left=20,Top=165,Width=480,Height=42,Text="CPF/CNPJ e telefone opcionais. Informe o nome correto para vincular a venda ao Fiado."};
    var save=new Button{Left=165,Top=220,Width=195,Height=38,Text="Salvar e selecionar",BackColor=Color.FromArgb(20,90,167),ForeColor=Color.White};
    var cancel=new Button{Left=380,Top=220,Width=120,Height=38,Text="Cancelar"};
    cancel.Click+=(s,e)=>form.Close();
    form.Controls.AddRange(new Control[]{new Label{Text="Nome / Razão social",Left=20,Top=24,Width=135},
     new Label{Text="Telefone",Left=20,Top=74,Width=135},
     new Label{Text="CPF ou CNPJ",Left=20,Top=124,Width=135},name,phone,doc,hint,save,cancel});
    save.Click+=(s,e)=>{
     string newName=name.Text.Trim(),document=CustomerDigits(doc.Text);
     if(newName.Length<2||string.Equals(newName,"Consumidor",StringComparison.OrdinalIgnoreCase)){
      MessageBox.Show("Informe o nome do cliente. Consumidor não pode ser utilizado no Fiado.");return;
     }
     if(document.Length>0&&document.Length!=11&&document.Length!=14){
      MessageBox.Show("CPF/CNPJ deve conter 11 ou 14 dígitos. Confira ou deixe em branco.");return;
     }
     try{
      using(var connection=Open()){
       // O registro é feito uma vez. Se o nome já existir, apenas selecionar.
       bool found=false;
       using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customers'",connection))
        found=Convert.ToInt32(check.ExecuteScalar())>0;
       if(found){
        using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM customers WHERE LOWER(TRIM(name))=LOW(TRIM(@name))",connection)){
         cmd.Parameters.AddWithValue("@name",newName);
         if(Convert.ToInt32(cmd.ExecuteScalar())>0){
          customer.Text=newName;form.DialogResult=DialogResult.OK;return;
         }
        }
       }
       string backup=db+".antes-cadastro-rapido-cliente-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
       using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
        copy.Open();connection.BackupDatabase(copy,"main","main",-1,null,0);
       }
       using(var tx=connection.BeginTransaction()){
        using(var schema=new SQLiteCommand("CREATE TABLE IF NOT EXISTS customers (id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL,phone TEXT,document TEXT)",connection,tx))schema.ExecuteNonQuery();
        var columns=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var cmd=new SQLiteCommand("PRAGMA table_info(customers)",connection,tx))
        using(var reader=cmd.ExecuteReader())while(reader.Read())columns.Add(Convert.ToString(reader["name"]));
        if(!columns.Contains("name")||!columns.Contains("id"))throw new Exception("Cadastro de clientes incompatível.");
        if(!columns.Contains("phone"))using(var cmd=new SQLiteCommand("ALTER TABLE customers ADD COLUMN phone TEXT",connection,tx))cmd.ExecuteNonQuery();
        if(!columns.Contains("document"))using(var cmd=new SQLiteCommand("ALTER TABLE customers ADD COLUMN document TEXT",connection,tx))cmd.ExecuteNonQuery();
        using(var check=new SQLiteCommand("SELECT COUNT(*) FROM customers WHERE LOWER(TRIM(name))=LOWER(TRIM(@name)) OR (@doc<>'' AND REPLACE(REPLACE(REPLACE(COALESCE(document,''),'.',''),'-',''),'/','')=@doc)",connection,tx)){
         check.Parameters.AddWithValue("@name",newName);check.Parameters.AddWithValue("@doc",document);
         if(Convert.ToInt32(check.ExecuteScalar())>0)throw new Exception("Cliente já cadastrado com este nome ou CPF/CNPJ. Escolha o cadastro existente.");
        }
        long id;
        using(var insert=new SQLiteCommand("INSERT INTO customers(name,phone,document) VALUES(@name,@phone,@doc)",connection,tx)){
         insert.Parameters.AddWithValue("@name",newName);
         insert.Parameters.AddWithValue("@phone",phone.Text.Trim());
         insert.Parameters.AddWithValue("@doc",document);
         if(insert.ExecuteNonQuery()!=1)throw new Exception("Cliente não cadastrado.");
        }
        using(var cmd=new SQLiteCommand("SELECT last_insert_rowid()",connection,tx))id=Convert.ToInt64(cmd.ExecuteScalar());
        using(var audit=new SQLiteCommand("CREATE TABLE IF NOT EXISTS customer_registration_audit(id INTEGER PRIMARY KEY AUTOINCREMENT,customer_id INTEGER,action TEXT NOT NULL,old_name TEXT,old_phone TEXT,old_document TEXT,new_name TEXT,new_phone TEXT,new_document TEXT,changed_at TEXT NOT NULL)",connection,tx))audit.ExecuteNonQuery();
        using(var audit=new SQLiteCommand("INSERT INTO customer_registration_audit(customer_id,action,old_name,old_phone,old_document,new_name,new_phone,new_document,changed_at) VALUES(@id,'CADASTRO_RAPIDO_PDV','','','',@name,@phone,@doc,datetime('now','localtime'))",connection,tx)){
         audit.Parameters.AddWithValue("@id",id);audit.Parameters.AddWithValue("@name",newName);
         audit.Parameters.AddWithValue("@phone",phone.Text.Trim());audit.Parameters.AddWithValue("@doc",document);
         audit.ExecuteNonQuery();
        }
        tx.Commit();
       }
      }
      customer.Text=newName;form.DialogResult=DialogResult.OK;
     }catch(Exception ex){MessageBox.Show("Cadastro não salvo: "+ex.Message,"Cadastro rápido",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    bool ok=form.ShowDialog(this)==DialogResult.OK;
    if(ok){LoadCustomerOptions();customer.Text=name.Text.Trim();}
    return ok;
   }
  }
  void FindSuggestions(){
   string term=code.Text.Trim();
   if(term.Length<2)return;
   try{
    suggestions.Items.Clear();suggestionCodes.Clear();
    using(var connection=Open())
    using(var cmd=new SQLiteCommand("SELECT barcode,internal_code,name,price,stock FROM products WHERE name LIKE @start OR name LIKE @term OR TRIM(COALESCE(barcode,'')) LIKE @start OR TRIM(COALESCE(internal_code,'')) LIKE @start ORDER BY CASE WHEN name LIKE @start THEN 0 ELSE 1 END,name LIMIT 30",connection)){
     cmd.Parameters.AddWithValue("@start",term+"%");cmd.Parameters.AddWithValue("@term","%"+term+"%");
     using(var reader=cmd.ExecuteReader())while(reader.Read()){
      string barcode=Convert.ToString(reader["barcode"]);string internalCode=Convert.ToString(reader["internal_code"]);
      string key=!string.IsNullOrWhiteSpace(barcode)?barcode:internalCode;
      if(string.IsNullOrWhiteSpace(key))continue;
      int index=suggestions.Items.Add(Convert.ToString(reader["name"])+"  |  R$ "+Dec(reader["price"]).ToString("N2")+"  |  QTD "+Dec(reader["stock"]).ToString("N3")+"  |  "+key);
      suggestionCodes[index]=key;
     }
    }
    suggestions.Visible=suggestions.Items.Count>0;
    if(suggestions.Visible)suggestions.BringToFront();
   }catch{suggestions.Visible=false;}
  }
  void ChooseSuggestion(){
   string selected;
   if(!suggestionCodes.TryGetValue(suggestions.SelectedIndex,out selected))return;
   suppressLookup=true;lookupDelay.Stop();suggestions.Visible=false;code.Text=selected;suppressLookup=false;
   AddProduct();
  }
  // Conferência explícita do estoque real antes da venda. Não permite estoque negativo.
  bool AdjustEmptyStock(Item item,decimal previous,decimal required){
   using(var dlg=new Form{Text="PDV | Produto sem estoque - conferir quantidade",Width=610,Height=345,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
    var info=new Label{Left=15,Top=12,Width=565,Height=75,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(18,65,143),
     Text=item.Name+"\nEstoque registrado: "+previous.ToString("N3")+"  |  Necessário para a venda: "+required.ToString("N3")};
    var quantity=new NumericUpDown{Left=220,Top=100,Width=225,DecimalPlaces=3,Minimum=0,Maximum=10000000,Value=Math.Min(10000000,Math.Max(0,required))};
    var reason=new TextBox{Left=220,Top=151,Width=340,MaxLength=250,Text="Conferência física de estoque no PDV"};
    var save=new Button{Left=220,Top=210,Width=210,Height=40,Text="Confirmar estoque",BackColor=Color.FromArgb(25,96,174),ForeColor=Color.White};
    var cancel=new Button{Left=445,Top=210,Width=115,Height=40,Text="Cancelar"};
    cancel.Click+=(s,e)=>dlg.Close();
    dlg.Controls.AddRange(new Control[]{info,quantity,reason,save,cancel,
     new Label{Left=15,Top=103,AutoSize=true,Text="Quantidade REAL disponível:"},
     new Label{Left=15,Top=154,AutoSize=true,Text="Motivo da conferência:"}});
    save.Click+=(s,e)=>{
     if(quantity.Value<required){MessageBox.Show("A quantidade real deve cobrir o item e as unidades já colocadas no carrinho.");return;}
     if(string.IsNullOrWhiteSpace(reason.Text)){MessageBox.Show("Informe o motivo da conferência.");return;}
     if(MessageBox.Show("Confirmar que existem "+quantity.Value.ToString("N3")+" unidades de "+item.Name+" fisicamente no estabelecimento?\nEsta operação ajustará o estoque cadastrado e ficará na auditoria.","Conferir estoque real",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
     try{
      string backup;
      using(var connection=Open()){
       using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('runtime_operation_context','stock_audit_log')",connection))
        if(Convert.ToInt32(check.ExecuteScalar())!=2)throw new Exception("Banco sem proteção ou auditoria. Nenhuma quantidade alterada.");
       backup=db+".antes-conferencia-pdv-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
       using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
        dest.Open();connection.BackupDatabase(dest,"main","main",-1,null,0);
       }
       using(var tx=connection.BeginTransaction()){
        decimal before;
        using(var q=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",connection,tx)){
         q.Parameters.AddWithValue("@id",item.Id);
         object value=q.ExecuteScalar();
         if(value==null||value==DBNull.Value)throw new Exception("Produto não encontrado.");
         before=Dec(value);
        }
        if(before!=previous)throw new Exception("O estoque foi alterado por outra operação. Pesquise o produto novamente.");
        int oldAuthorized;string oldOperation;
        using(var q=new SQLiteCommand("SELECT authorized,operation FROM runtime_operation_context WHERE id=1",connection,tx))
        using(var rd=q.ExecuteReader()){
         if(!rd.Read())throw new Exception("Proteção do banco indisponível.");
         oldAuthorized=Convert.ToInt32(rd["authorized"]);oldOperation=Convert.ToString(rd["operation"]);
        }
        using(var q=new SQLiteCommand("UPDATE runtime_operation_context SET authorized=1,operation='AJUSTE_MANUAL_PDV_AUDITADO' WHERE id=1",connection,tx))
         if(q.ExecuteNonQuery()!=1)throw new Exception("Falha na autorização de ajuste.");
        using(var q=new SQLiteCommand("UPDATE products SET stock=@next WHERE id=@id AND stock=@previous",connection,tx)){
         q.Parameters.AddWithValue("@next",quantity.Value);q.Parameters.AddWithValue("@id",item.Id);q.Parameters.AddWithValue("@previous",before);
         if(q.ExecuteNonQuery()!=1)throw new Exception("Estoque alterado simultaneamente. Nada gravado.");
        }
        using(var q=new SQLiteCommand("INSERT INTO stock_audit_log(product_id,product_name,barcode,event_type,quantity_before,quantity_after,quantity_delta,reference_type,reason) VALUES(@id,@name,@barcode,'AJUSTE_MANUAL_PDV',@before,@after,@difference,'ESTOQUE',@reason)",connection,tx)){
         q.Parameters.AddWithValue("@id",item.Id);q.Parameters.AddWithValue("@name",item.Name);q.Parameters.AddWithValue("@barcode",item.Barcode);
         q.Parameters.AddWithValue("@before",before);q.Parameters.AddWithValue("@after",quantity.Value);q.Parameters.AddWithValue("@difference",quantity.Value-before);
         q.Parameters.AddWithValue("@reason",reason.Text.Trim());if(q.ExecuteNonQuery()!=1)throw new Exception("Falha ao auditar ajuste.");
        }
        using(var q=new SQLiteCommand("UPDATE runtime_operation_context SET authorized=@auth,operation=@operation WHERE id=1",connection,tx)){
         q.Parameters.AddWithValue("@auth",oldAuthorized);q.Parameters.AddWithValue("@operation",oldOperation);
         if(q.ExecuteNonQuery()!=1)throw new Exception("Falha ao restaurar proteção.");
        }
        tx.Commit();
       }
      }
      dlg.DialogResult=DialogResult.OK;
      MessageBox.Show("Quantidade real conferida e registrada na auditoria. Backup criado: "+backup,"Estoque do PDV");
     }catch(Exception ex){MessageBox.Show("Estoque não alterado: "+ex.Message,"PDV",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    return dlg.ShowDialog(this)==DialogResult.OK;
   }
  }
  void AddProduct() {
   string search=code.Text.Trim();if(search.Length==0)return;
   try {
    var matches=new List<Item>();
    var stocks=new Dictionary<long,decimal>();
    using(var connection=Open()){
     // Priorizar a busca exata por código; pesquisar descrição apenas se não houver código correspondente.
     using(var cmd=new SQLiteCommand("SELECT id,barcode,internal_code,name,price,cost,stock FROM products WHERE barcode=@code OR internal_code=@code ORDER BY name,id LIMIT 101",connection)){
      cmd.Parameters.AddWithValue("@code",search);
      using(var reader=cmd.ExecuteReader())while(reader.Read()){
       var item=new Item {Id=Convert.ToInt64(reader["id"]),Barcode=Convert.ToString(reader["barcode"]),InternalCode=Convert.ToString(reader["internal_code"]),Name=Convert.ToString(reader["name"]),Price=Dec(reader["price"]),Cost=Dec(reader["cost"])};
       matches.Add(item);stocks[item.Id]=Dec(reader["stock"]);
      }
     }
     if(matches.Count==0){
      using(var cmd=new SQLiteCommand("SELECT id,barcode,internal_code,name,price,cost,stock FROM products WHERE name LIKE @name ORDER BY name,id LIMIT 101",connection)){
       cmd.Parameters.AddWithValue("@name","%"+search+"%");
       using(var reader=cmd.ExecuteReader())while(reader.Read()){
        var item=new Item {Id=Convert.ToInt64(reader["id"]),Barcode=Convert.ToString(reader["barcode"]),InternalCode=Convert.ToString(reader["internal_code"]),Name=Convert.ToString(reader["name"]),Price=Dec(reader["price"]),Cost=Dec(reader["cost"])};
        matches.Add(item);stocks[item.Id]=Dec(reader["stock"]);
       }
      }
     }
    }
    if(matches.Count==0){
     if(MessageBox.Show("Produto não cadastrado: "+search+"\\n\\nAbrir o cadastro de produto agora?","Produto não encontrado",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){
      using(var products=new ProdutosForm(db))products.OpenNewProduct(search);
      // Após o cadastro, mantenha a pesquisa e tente localizar o novo produto novamente.
      code.Text=search;code.Focus();code.SelectAll();
      MessageBox.Show("Se o produto foi salvo, pressione Enter para incluí-lo na venda.","Voltar ao PDV");
     }
     return;
    }
    if(matches.Count>1 && matches.Count(x=>string.Equals((x.Barcode??"").Trim(),search,StringComparison.OrdinalIgnoreCase))>1){MessageBox.Show("Atenção: existem produtos diferentes cadastrados com o mesmo código de barras. Confira a descrição e o preço antes de selecionar.","Código de barras duplicado",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    if(matches.Count>1 && matches.Count(x=>string.Equals((x.InternalCode??"").Trim(),search,StringComparison.OrdinalIgnoreCase))>1){MessageBox.Show("Atenção: existem produtos diferentes cadastrados com o mesmo código interno. Confira a descrição e o preço antes de selecionar.","Código interno duplicado",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
    if(matches.Count>100){matches.RemoveAt(100);MessageBox.Show("A pesquisa encontrou mais de 100 produtos. Exibindo os primeiros 100; refine a descrição ou informe o código para localizar o produto desejado.","Muitos resultados");}
    Item selected=matches[0];
    if(matches.Count>1){
     using(var dialog=new Form {Text="Selecione o produto",Width=780,Height=500,StartPosition=FormStartPosition.CenterParent,MinimumSize=new Size(580,350)}){
      var list=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
      var dt=new DataTable();dt.Columns.Add("Código de barras");dt.Columns.Add("Código interno");dt.Columns.Add("Descrição");dt.Columns.Add("Preço");dt.Columns.Add("Estoque");
      foreach(var item in matches)dt.Rows.Add(item.Barcode,item.InternalCode,item.Name,item.Price.ToString("N2"),stocks[item.Id].ToString("N3"));
      list.DataSource=dt;
      list.Columns[2].FillWeight=260;
      var choose=new Button {Text="Adicionar selecionado",Dock=DockStyle.Bottom,Height=45,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
      choose.Click+=(s,e)=>{if(list.CurrentRow!=null){dialog.Tag=list.CurrentRow.Index;dialog.DialogResult=DialogResult.OK;}};
      list.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0){dialog.Tag=e.RowIndex;dialog.DialogResult=DialogResult.OK;}};
      dialog.KeyPreview=true;
      dialog.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){dialog.DialogResult=DialogResult.Cancel;e.Handled=true;}else if(e.KeyCode==Keys.Enter && list.CurrentRow!=null){dialog.Tag=list.CurrentRow.Index;dialog.DialogResult=DialogResult.OK;e.Handled=true;e.SuppressKeyPress=true;}};
      var cancel=new Button {Text="Cancelar seleção",Dock=DockStyle.Bottom,Height=38};
      cancel.Click+=(s,e)=>{dialog.DialogResult=DialogResult.Cancel;};
      dialog.Controls.Add(list);dialog.Controls.Add(cancel);dialog.Controls.Add(choose);
      if(dialog.ShowDialog(this)!=DialogResult.OK||!(dialog.Tag is int))return;
      int selectedIndex=(int)dialog.Tag;
      if(selectedIndex<0||selectedIndex>=matches.Count)return;
      selected=matches[selectedIndex];
     }
    }
    decimal quantity=qty.Value;
    string unit=Convert.ToString(saleUnit.SelectedItem)=="KG"?"KG":"UN";
    var existing=cart.FirstOrDefault(x=>x.Id==selected.Id&&x.Unit==unit);
    decimal required=quantity+(existing==null?0:existing.Qty);
    if(stocks[selected.Id]<required){
     // Para estoque zerado ou insuficiente, permitir conferência física autorizada.
     if(!AdjustEmptyStock(selected,stocks[selected.Id],required))return;
     using(var refreshed=Open())
     using(var check=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",refreshed)){
      check.Parameters.AddWithValue("@id",selected.Id);
      stocks[selected.Id]=Dec(check.ExecuteScalar());
     }
     if(stocks[selected.Id]<required){MessageBox.Show("Quantidade ainda insuficiente para a venda.");return;}
    }
    if(existing!=null)existing.Qty+=quantity;
    else {selected.Qty=quantity;selected.Unit=unit;cart.Add(selected);}
    suppressLookup=true;code.Clear();suppressLookup=false;suggestions.Visible=false;qty.Value=1;Render();code.Focus();
   }catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao adicionar");}
  }
  void Render() {
   var dt=new DataTable();dt.Columns.Add("Codigo");dt.Columns.Add("Produto");dt.Columns.Add("Quantidade",typeof(decimal));dt.Columns.Add("Unidade");dt.Columns.Add("Preco",typeof(decimal));dt.Columns.Add("Total",typeof(decimal));
   foreach(var i in cart)dt.Rows.Add(i.Barcode,i.Name,i.Qty,i.Unit,i.Price,i.Total);
   grid.DataSource=dt;total.Text="TOTAL: R$ "+cart.Sum(i=>i.Total).ToString("N2");UpdateChange();
  }
  void UpdateChange(){
   bool cash=Convert.ToString(payment.SelectedItem)=="DINHEIRO";
   changeLabel.Visible=cash;
   received.Enabled=cash;
   if(!cash)return;
   decimal difference=received.Value-cart.Sum(i=>i.Total);
   changeLabel.Text=difference>=0?"TROCO: R$ "+difference.ToString("N2"):"FALTA: R$ "+(-difference).ToString("N2");
   changeLabel.ForeColor=difference>=0?Color.FromArgb(22,75,160):Color.FromArgb(175,25,45);
  }
  void EditSelected(){
   if(grid.CurrentRow==null||grid.CurrentRow.Index<0||grid.CurrentRow.Index>=cart.Count){MessageBox.Show("Selecione um produto do carrinho.");return;}
   var item=cart[grid.CurrentRow.Index];
   try{
    using(var dialog=new Form {Text="Editar item da venda",Width=450,Height=265,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
     var nameLabel=new Label {Text=item.Name,Left=15,Top=15,Width=400,Height=35,Font=new Font("Segoe UI",11,FontStyle.Bold)};
     var quantityLabel=new Label {Text="Quantidade:",Left=15,Top=65,Width=125};
     var quantityInput=new NumericUpDown {Left=150,Top=60,Width=210,DecimalPlaces=3,Minimum=0.001M,Maximum=999999,Value=item.Qty};
     var priceLabel=new Label {Text="Preço unitário (R$):",Left=15,Top=105,Width=130};
     var priceInput=new NumericUpDown {Left=150,Top=100,Width=210,DecimalPlaces=2,Minimum=0,Maximum=99999999,Value=item.Price};
     var preview=new Label {Left=15,Top=145,Width=400,Height=26,Font=new Font("Segoe UI",11,FontStyle.Bold)};
     Action update=()=>preview.Text="Subtotal: R$ "+Math.Round(quantityInput.Value*priceInput.Value,2).ToString("N2");
     quantityInput.ValueChanged+=(s,e)=>update();priceInput.ValueChanged+=(s,e)=>update();update();
     var save=new Button {Text="Aplicar alterações",Left=205,Top=180,Width=155,Height=34,DialogResult=DialogResult.OK};
     dialog.Controls.AddRange(new Control[]{nameLabel,quantityLabel,quantityInput,priceLabel,priceInput,preview,save});
     dialog.AcceptButton=save;
     if(dialog.ShowDialog(this)!=DialogResult.OK)return;
     decimal available;
     using(var connection=Open())using(var cmd=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",connection)){
      cmd.Parameters.AddWithValue("@id",item.Id);
      var value=cmd.ExecuteScalar();if(value==null||value==DBNull.Value)throw new Exception("Produto não encontrado no estoque.");
      available=Dec(value);
     }
     if(quantityInput.Value>available){MessageBox.Show("Estoque insuficiente. Disponível: "+available.ToString("N3"));return;}
     item.Qty=quantityInput.Value;item.Price=priceInput.Value;
     Render();
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Não foi possível editar o item");}
  }
  void ClearCart(){
   if(cart.Count==0){MessageBox.Show("O carrinho já está vazio.");return;}
   if(MessageBox.Show("Remover todos os itens desta venda em andamento? Nenhuma venda registrada será excluída.","Confirmar limpeza",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
   cart.Clear();code.Clear();qty.Value=1;received.Value=0;Render();code.Focus();
  }
  void RemoveSelected(){if(grid.CurrentRow==null)return;int n=grid.CurrentRow.Index;if(n>=0&&n<cart.Count){cart.RemoveAt(n);Render();}}
  static void RequireColumns(SQLiteConnection conn,string table,params string[] required) {
   var existing=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var cmd=new SQLiteCommand("PRAGMA table_info("+table+")",conn))
   using(var reader=cmd.ExecuteReader())while(reader.Read())existing.Add(Convert.ToString(reader["name"]));
   foreach(var column in required)if(!existing.Contains(column))
    throw new Exception("Banco incompatível para gravar venda: coluna "+table+"."+column+" ausente. Nenhum registro foi alterado.");
  }
  void ValidateSaleSchema(SQLiteConnection conn,string method) {
   RequireColumns(conn,"sales","id","customer_name","payment_method","gross_total","total_cost","net_profit","profit_reserve_10","created_at");
   RequireColumns(conn,"sale_items","sale_id","product_id","barcode","name","quantity","unit_price","unit_cost","total");
   RequireColumns(conn,"products","id","stock");
   RequireColumns(conn,"stock_audit_log","product_id","product_name","barcode","event_type","quantity_before","quantity_after","quantity_delta","reference_type","reference_id","reason");
   if(method!="FIADO")RequireColumns(conn,"sale_payments","sale_id","payment_method","amount");
  }
  List<KeyValuePair<string,decimal>> SelectSplitPayments(decimal amount){
   var methods=PaymentFees.Methods;
   using(var dialog=new Form {Text="Pagamento dividido (até 3 formas)",Width=485,Height=340,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
    var combos=new ComboBox[3];var values=new NumericUpDown[3];
    var info=new Label {Left=18,Top=15,Width=440,Height=28,Text="Total da venda: R$ "+amount.ToString("N2"),Font=new Font("Segoe UI",11,FontStyle.Bold)};
    dialog.Controls.Add(info);
    for(int i=0;i<3;i++){
     var label=new Label {Left=18,Top=58+i*53,Width=55,Text=(i+1)+"ª forma"};
     combos[i]=new ComboBox {Left=85,Top=53+i*53,Width=165,DropDownStyle=ComboBoxStyle.DropDownList};
     combos[i].Items.AddRange(methods);combos[i].SelectedIndex=i==0?0:-1;
     values[i]=new NumericUpDown {Left=265,Top=53+i*53,Width=175,DecimalPlaces=2,Maximum=99999999};
     if(i==0)values[i].Value=Math.Min(amount,values[i].Maximum);
     dialog.Controls.AddRange(new Control[]{label,combos[i],values[i]});
    }
    var status=new Label {Left=18,Top=218,Width=430,Height=25};
    Action update=()=>status.Text="Informado: R$ "+values.Sum(v=>v.Value).ToString("N2")+" / Total: R$ "+amount.ToString("N2");
    foreach(var v in values)v.ValueChanged+=(s,e)=>update();update();
    var confirm=new Button {Text="Confirmar pagamentos",Left=240,Top=250,Width=200,Height=35};
    List<KeyValuePair<string,decimal>> selected=null;
    confirm.Click+=(s,e)=>{
     var parts=new List<KeyValuePair<string,decimal>>();
     for(int i=0;i<3;i++){
      if(values[i].Value==0)continue;
      if(combos[i].SelectedIndex<0){MessageBox.Show("Escolha a forma para cada valor informado.");return;}
      string method=Convert.ToString(combos[i].SelectedItem);
      if(parts.Any(p=>p.Key==method)){MessageBox.Show("Utilize cada forma de pagamento apenas uma vez.");return;}
      parts.Add(new KeyValuePair<string,decimal>(method,values[i].Value));
     }
     if(parts.Count<2){MessageBox.Show("Para pagamento dividido, informe pelo menos duas formas.");return;}
     if(parts.Sum(p=>p.Value)!=amount){MessageBox.Show("A soma dos pagamentos deve ser exatamente R$ "+amount.ToString("N2"));return;}
     selected=parts;dialog.DialogResult=DialogResult.OK;
    };
    dialog.Controls.Add(status);dialog.Controls.Add(confirm);
    return dialog.ShowDialog(this)==DialogResult.OK?selected:null;
   }
  }
  void Finish(){
   if(finishingSale)return;
   if(cart.Count==0){MessageBox.Show("Carrinho vazio.");return;}
   foreach(var item in cart){
    if(item.Id<=0||item.Qty<=0||item.Price<0||item.Cost<0||item.Total<=0){
     MessageBox.Show("Item inválido no carrinho: "+item.Name+". Confira quantidade, preço e cadastro antes de finalizar.","Conferência dos itens",MessageBoxButtons.OK,MessageBoxIcon.Warning);
     return;
    }
   }
   var method=Convert.ToString(payment.SelectedItem);
   string customerName=string.IsNullOrWhiteSpace(customer.Text)?"Consumidor":customer.Text.Trim();
   if(method=="FIADO"){
    if(string.Equals(customerName,"Consumidor",StringComparison.OrdinalIgnoreCase)){
     if(MessageBox.Show("Cliente não informado. Cadastrar cliente agora para finalizar a venda Fiado?","Cadastro rápido",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
     if(!QuickRegisterCustomer())return;
     customerName=customer.Text.Trim();
    }
    try{
     if(!CustomerRegistered(customerName)){
      if(MessageBox.Show("O cliente "+customerName+" não está cadastrado. Cadastrar agora?","Fiado",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      if(!QuickRegisterCustomer())return;
      customerName=customer.Text.Trim();
     }
    }catch(Exception ex){MessageBox.Show("Não foi possível verificar o cadastro do cliente: "+ex.Message,"Fiado");return;}
   }
   var sum=cart.Sum(i=>i.Total);
   if(sum<=0){MessageBox.Show("A venda precisa ter total maior que zero. Confira os preços dos itens.","Valor inválido",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
   List<KeyValuePair<string,decimal>> split=null;
   if(method=="DIVIDIDO"){split=SelectSplitPayments(sum);if(split==null)return;}
   if(method=="DINHEIRO"&&received.Value<sum){MessageBox.Show("Valor recebido menor que o total.");return;}
   string details=method=="DIVIDIDO"?string.Join(Environment.NewLine,split.Select(p=>p.Key+": R$ "+p.Value.ToString("N2"))):"Pagamento: "+method;
   if(method=="DINHEIRO")details+=Environment.NewLine+"Recebido: R$ "+received.Value.ToString("N2")+Environment.NewLine+"Troco: R$ "+(received.Value-sum).ToString("N2");
   // A taxa é custo do estabelecimento e nunca aumenta o valor pago pelo cliente.
   Dictionary<string,decimal> feeRates;
   try{using(var read=Open())feeRates=PaymentFees.Load(read);}
   catch(Exception ex){MessageBox.Show("Não foi possível consultar as taxas: "+ex.Message,"Venda não iniciada",MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
   var paymentParts=method=="FIADO"?new List<KeyValuePair<string,decimal>>():method=="DIVIDIDO"?split:new List<KeyValuePair<string,decimal>>{new KeyValuePair<string,decimal>(method,sum)};
   decimal estimatedFee=paymentParts.Sum(p=>PaymentFees.Fee(feeRates,p.Key,p.Value));
   details+=Environment.NewLine+"Cliente: "+customerName;
   if(estimatedFee>0)details+=Environment.NewLine+"Taxas da operadora: R$ "+estimatedFee.ToString("N2")+" (custo da loja)";

   if(MessageBox.Show("Total: R$ "+sum.ToString("N2")+Environment.NewLine+details+Environment.NewLine+Environment.NewLine+"Confirmar finalização?","Conferir pagamento",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   finishingSale=true;
   try {
    using(var c=Open()){
     ValidateSaleSchema(c,method);
     using(var tx=c.BeginTransaction()) {
     // Autorizar somente a baixa da venda, dentro da mesma transação dos itens e da auditoria.
     // A proteção original do estoque é restaurada antes do COMMIT.
     bool hasStockContext=false;int previousAuthorization=0;string previousOperation="";
     using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='runtime_operation_context'",c,tx))
      hasStockContext=Convert.ToInt32(check.ExecuteScalar())>0;
     if(hasStockContext){
      using(var get=new SQLiteCommand("SELECT authorized,operation FROM runtime_operation_context WHERE id=1",c,tx))
      using(var reader=get.ExecuteReader()){
       if(!reader.Read())throw new Exception("Proteção de estoque inconsistente: contexto de autorização ausente. Venda não gravada.");
       previousAuthorization=Convert.ToInt32(reader["authorized"]);
       previousOperation=Convert.ToString(reader["operation"]);
      }
      using(var authorize=new SQLiteCommand("UPDATE runtime_operation_context SET authorized=1,operation='VENDA_PDV_AUDITADA' WHERE id=1",c,tx))
       if(authorize.ExecuteNonQuery()!=1)throw new Exception("Não foi possível autorizar a baixa de estoque da venda.");
     }
     long saleId;
     using(var cmd=new SQLiteCommand("INSERT INTO sales(customer_name,payment_method,gross_total,total_cost,net_profit,profit_reserve_10,created_at) VALUES(@customer,@method,@total,@cost,@profit,@reserve,datetime('now','localtime')); SELECT last_insert_rowid();",c,tx)){
      var cost=cart.Sum(i=>i.Cost*i.Qty);var profit=sum-cost;
      cmd.Parameters.AddWithValue("@customer",customerName);cmd.Parameters.AddWithValue("@method",method);cmd.Parameters.AddWithValue("@total",sum);cmd.Parameters.AddWithValue("@cost",cost);cmd.Parameters.AddWithValue("@profit",profit);cmd.Parameters.AddWithValue("@reserve",Math.Round(profit*0.10M,2));cmd.Parameters.AddWithValue("@received",method=="DINHEIRO"?received.Value:sum);cmd.Parameters.AddWithValue("@change",method=="DINHEIRO"?received.Value-sum:0);cmd.Parameters.AddWithValue("@status",method=="FIADO"?"FIADO":"CONCLUIDA");saleId=Convert.ToInt64(cmd.ExecuteScalar());
     }
     foreach(var i in cart) {
      decimal before;
      using(var q=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",c,tx)){q.Parameters.AddWithValue("@id",i.Id);before=Dec(q.ExecuteScalar());}
      if(before<i.Qty)throw new Exception("Estoque insuficiente: "+i.Name+". Venda cancelada sem alteracoes.");
      using(var q=new SQLiteCommand("UPDATE products SET stock=stock-@qty WHERE id=@id AND stock>=@qty",c,tx)){q.Parameters.AddWithValue("@qty",i.Qty);q.Parameters.AddWithValue("@id",i.Id);if(q.ExecuteNonQuery()!=1)throw new Exception("Falha na baixa de estoque: "+i.Name);}
      using(var q=new SQLiteCommand("INSERT INTO sale_items(sale_id,product_id,barcode,name,quantity,unit_price,unit_cost,total) VALUES(@sale,@id,@barcode,@name,@qty,@price,@cost,@total)",c,tx)){
       q.Parameters.AddWithValue("@sale",saleId);q.Parameters.AddWithValue("@id",i.Id);q.Parameters.AddWithValue("@barcode",i.Barcode);q.Parameters.AddWithValue("@name",i.Name);q.Parameters.AddWithValue("@qty",i.Qty);q.Parameters.AddWithValue("@price",i.Price);q.Parameters.AddWithValue("@cost",i.Cost);q.Parameters.AddWithValue("@total",i.Total);q.ExecuteNonQuery();
      }
      using(var q=new SQLiteCommand("INSERT INTO stock_audit_log(product_id,product_name,barcode,event_type,quantity_before,quantity_after,quantity_delta,reference_type,reference_id,reason) VALUES(@id,@name,@barcode,'VENDA',@before,@after,@delta,'SALE',@sale,'Baixa de estoque no PDV')",c,tx)){
       q.Parameters.AddWithValue("@id",i.Id);q.Parameters.AddWithValue("@name",i.Name);q.Parameters.AddWithValue("@barcode",i.Barcode);q.Parameters.AddWithValue("@before",before);q.Parameters.AddWithValue("@after",before-i.Qty);q.Parameters.AddWithValue("@delta",-i.Qty);q.Parameters.AddWithValue("@sale",saleId);q.ExecuteNonQuery();
      }
     }
     if(method!="FIADO"){
      var payments=paymentParts;
      foreach(var part in payments)using(var q=new SQLiteCommand("INSERT INTO sale_payments(sale_id,payment_method,amount) VALUES(@sale,@method,@total)",c,tx)){
       q.Parameters.AddWithValue("@sale",saleId);q.Parameters.AddWithValue("@method",part.Key);q.Parameters.AddWithValue("@total",part.Value);q.ExecuteNonQuery();
      }
     }
     // Auditoria financeira separada: preserva sales.net_profit histórico e registra
     // a taxa real calculada no instante da venda e por forma de pagamento.
     using(var setup=new SQLiteCommand("CREATE TABLE IF NOT EXISTS sale_payment_fees (id INTEGER PRIMARY KEY AUTOINCREMENT,sale_id INTEGER NOT NULL,payment_method TEXT NOT NULL,base_amount NUMERIC NOT NULL,fee_percent NUMERIC NOT NULL,fee_amount NUMERIC NOT NULL,created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)",c,tx))setup.ExecuteNonQuery();
     foreach(var part in paymentParts){
      decimal rate=feeRates.ContainsKey(part.Key)?feeRates[part.Key]:0m;
      decimal fee=PaymentFees.Fee(feeRates,part.Key,part.Value);
      using(var insertFee=new SQLiteCommand("INSERT INTO sale_payment_fees(sale_id,payment_method,base_amount,fee_percent,fee_amount) VALUES(@id,@method,@base,@percent,@fee)",c,tx)){
       insertFee.Parameters.AddWithValue("@id",saleId);
       insertFee.Parameters.AddWithValue("@method",part.Key);
       insertFee.Parameters.AddWithValue("@base",part.Value);
       insertFee.Parameters.AddWithValue("@percent",rate);
       insertFee.Parameters.AddWithValue("@fee",fee);
       insertFee.ExecuteNonQuery();
      }
     }
     if(hasStockContext){
      using(var restore=new SQLiteCommand("UPDATE runtime_operation_context SET authorized=@auth,operation=@operation WHERE id=1",c,tx)){
       restore.Parameters.AddWithValue("@auth",previousAuthorization);
       restore.Parameters.AddWithValue("@operation",previousOperation);
       if(restore.ExecuteNonQuery()!=1)throw new Exception("Falha ao restaurar a proteção do estoque. Venda revertida.");
      }
     }
     tx.Commit();
     MessageBox.Show("Venda #"+saleId+" registrada. Estoque atualizado.");cart.Clear();received.Value=0;customer.Text="Consumidor";Render();code.Focus();
    }
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Venda nao finalizada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   finally{finishingSale=false;}
  }
 }
}