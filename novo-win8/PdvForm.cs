using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Security.Cryptography;

namespace ArmazemGratidao {
 public sealed class PdvForm : StyledDialogForm {
  sealed class Item {public long Id;public string Barcode,InternalCode,Name;public decimal Qty,Price,Cost;public string Unit="UN";public decimal Total {get{return Math.Round(Qty*Price,2);}}}
  readonly string db;
  bool finishingSale=false;
  decimal discountValue=0m;
  bool discountPercent=false;
  readonly bool manualEntry;
  public bool IsManualEntry {get{return manualEntry;}}

  readonly DateTimePicker manualDate=new DateTimePicker {Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy HH:mm:ss",ShowUpDown=true,Width=220};

  readonly List<Item> cart=new List<Item>();
  readonly TextBox code=new TextBox {Width=260,Font=new Font("Segoe UI",14,FontStyle.Bold)};
  readonly NumericUpDown qty=new NumericUpDown {Minimum=0.001M,Maximum=999999,DecimalPlaces=3,Increment=1M,Value=1,Width=95};
  readonly ComboBox saleUnit=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=80};
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
  // Catálogo do estoque exibido dentro do PDV, separado do carrinho de vendas.
  readonly DataGridView stockView=new DataGridView{Dock=DockStyle.Fill,Visible=false,ReadOnly=true,
   AllowUserToAddRows=false,AllowUserToDeleteRows=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
   MultiSelect=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,
   ScrollBars=ScrollBars.Both,RowHeadersVisible=false,BackgroundColor=Color.White};
  readonly TextBox stockSearch=new TextBox{Width=185,Font=new Font("Segoe UI",10)};
  readonly Label stockHint=new Label{AutoSize=true,Padding=new Padding(8,8,0,0),ForeColor=Color.FromArgb(22,75,160)};
  bool stockViewOpen=false;
  readonly Label total=new Label {AutoSize=true,Font=new Font("Segoe UI",23,FontStyle.Bold),ForeColor=Color.FromArgb(175,25,45)};
  readonly Label discountLabel=new Label {AutoSize=true,Padding=new Padding(0,9,0,0)};
  readonly ComboBox discountMode=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=58};
  readonly NumericUpDown discountInput=new NumericUpDown {DecimalPlaces=2,Maximum=99999999,Width=92};
  readonly Label changeLabel=new Label {AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(22,75,160),Padding=new Padding(0,9,0,0)};
  readonly ComboBox payment=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
  readonly TextBox customer=new TextBox {Width=205,ReadOnly=true,TabStop=false,Font=new Font("Segoe UI",11,FontStyle.Bold),BackColor=Color.White};
  readonly Timer lookupDelay=new Timer {Interval=250};
  readonly ListBox suggestions=new ListBox {Visible=false,Width=510,Height=190,Font=new Font("Segoe UI",10)};
  readonly Dictionary<int,string> suggestionCodes=new Dictionary<int,string>();
  bool suppressLookup=false;
  bool draftReady=false;
  bool draftSaveWarned=false;
  readonly NumericUpDown received=new NumericUpDown {DecimalPlaces=2,Maximum=99999999,Width=130};
  public PdvForm(string database):this(database,false){}
  public PdvForm(string database,bool manualMode) {
   manualEntry=manualMode;
   db=database;Text="Armazem Gratidao - PDV";Width=1280;Height=800;MinimumSize=new Size(720,490);BackColor=Color.FromArgb(246,249,253);
   grid.ScrollBars=ScrollBars.Both;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;
   var working=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(Width,working.Width),Math.Min(Height,working.Height));
   var header=new FlowLayoutPanel {Dock=DockStyle.Top,Height=62,Padding=new Padding(6,6,6,3),BackColor=Color.White,AutoScroll=true,WrapContents=false};
   FlowLayoutPanel rapidInventoryBar=manualEntry?new FlowLayoutPanel{Dock=DockStyle.Top,Height=48,Padding=new Padding(5,4,5,3),WrapContents=false,AutoScroll=true,BackColor=Color.FromArgb(229,242,255)}:null;
   if(manualEntry){
    Text="Armazem Gratidao - Lançamento rápido de vendas anotadas";
    // A data será conferida somente depois que todos os produtos forem lançados.
    manualDate.Value=DateTime.Now;
   }
   header.Controls.Add(new Label {Text="Código / Nome:",AutoSize=true,ForeColor=Color.Black,Padding=new Padding(0,9,0,0)});
   header.Controls.Add(code);header.Controls.Add(new Label {Text="Qtd:",AutoSize=true,ForeColor=Color.Black,Padding=new Padding(0,9,0,0)});header.Controls.Add(qty);
   saleUnit.Items.AddRange(new object[]{"UN","KG"});saleUnit.SelectedIndex=0;
   saleUnit.SelectedIndexChanged+=(s,e)=>{bool byKg=Convert.ToString(saleUnit.SelectedItem)=="KG";qty.Increment=byKg?0.001M:1M;qty.DecimalPlaces=byKg?3:0;if(!byKg&&qty.Value!=decimal.Truncate(qty.Value))qty.Value=decimal.Ceiling(qty.Value);};
   header.Controls.Add(new Label {Text="Unidade:",AutoSize=true,ForeColor=Color.Black,Padding=new Padding(0,9,0,0)});header.Controls.Add(saleUnit);
   var add=new Button {Text="ADICIONAR PRODUTO",Width=190,Height=40,Font=new Font("Segoe UI",10,FontStyle.Bold),BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};add.FlatAppearance.BorderSize=0;add.Click+=(s,e)=>AddProduct();header.Controls.Add(add);
   if(manualEntry){
    // Acesso independente ao catalogo completo, dentro da mesma tela do PDV.
    // O botao Editar Qtd./Preco mantem seu comportamento original do carrinho.
    var inventory=new Button{Text="TODOS OS PRODUTOS NO PDV",Width=235,Height=36,
     Font=new Font("Segoe UI",9,FontStyle.Bold),BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
    inventory.Click+=(s,e)=>ShowStockInPdv();
    var importNotes=new Button{Text="IMPORTAR VENDAS ANOTADAS",Width=235,Height=36,
     Font=new Font("Segoe UI",9,FontStyle.Bold),BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
    importNotes.Click+=(s,e)=>ImportNotebookSales();
    rapidInventoryBar.Controls.Add(importNotes);
    var refreshStock=new Button{Text="ATUALIZAR TELA",Width=145,Height=36,BackColor=Color.FromArgb(31,104,172),ForeColor=Color.White};
    refreshStock.Click+=(s,e)=>{if(stockViewOpen)LoadStockInPdv();else Render();};
    rapidInventoryBar.Controls.Add(refreshStock);
    rapidInventoryBar.Controls.Add(inventory);
    var back=new Button{Text="VOLTAR AOS ITENS DA VENDA",Width=228,Height=36,BackColor=Color.FromArgb(175,35,48),ForeColor=Color.White};
    back.Click+=(s,e)=>ShowSaleCart();
    rapidInventoryBar.Controls.Add(back);
    rapidInventoryBar.Controls.Add(new Label{Text="Buscar:",AutoSize=true,Padding=new Padding(7,10,0,0)});
    // Pesquisa de estoque grande: não executar SELECT a cada tecla pressionada.
    var stockSearchDelay=new System.Windows.Forms.Timer{Interval=280};
    stockSearchDelay.Tick+=(s,e)=>{stockSearchDelay.Stop();if(stockViewOpen&&!IsDisposed)LoadStockInPdv();};
    stockSearch.TextChanged+=(s,e)=>{stockSearchDelay.Stop();if(stockViewOpen)stockSearchDelay.Start();};
    FormClosed+=(s,e)=>{stockSearchDelay.Stop();stockSearchDelay.Dispose();};
    rapidInventoryBar.Controls.Add(stockSearch);
    rapidInventoryBar.Controls.Add(stockHint);
   }
   code.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Down&&suggestions.Visible&&suggestions.Items.Count>0){suggestions.Focus();suggestions.SelectedIndex=0;e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;lookupDelay.Stop();if(manualEntry&&suggestions.Visible&&suggestions.Items.Count>0){suggestions.SelectedIndex=0;ChooseSuggestion();}else{suggestions.Visible=false;AddProduct();}}};
   code.TextChanged+=(s,e)=>{lookupDelay.Stop();if(!suppressLookup&&code.Text.Trim().Length>=(manualEntry?1:2))lookupDelay.Start();else suggestions.Visible=false;};
   lookupDelay.Tick+=(s,e)=>{lookupDelay.Stop();FindSuggestions();};
   suggestions.MouseClick+=(s,e)=>{int index=suggestions.IndexFromPoint(e.Location);if(e.Button==MouseButtons.Left&&index>=0){suggestions.SelectedIndex=index;ChooseSuggestion();}};
   suggestions.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;ChooseSuggestion();}else if(e.KeyCode==Keys.Escape){suggestions.Visible=false;code.Focus();}};
   var footer=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=132,Padding=new Padding(6,4,6,4),BackColor=Color.White,AutoScroll=true,WrapContents=true};
   // As formas permanecem disponíveis na janela de pagamento aberta ao finalizar.
   // O seletor fica apenas como estado interno, sem caixa antecipada no PDV.
   payment.Items.AddRange(PaymentFees.Methods.Cast<object>().Concat(new object[]{"FIADO","DIVIDIDO"}).ToArray());payment.SelectedIndex=0;
   // O nome do cliente fica no estado interno; so aparece na janela aberta pelo botao.
   customer.Text="Consumidor";
   footer.Controls.Add(new Label {Text="Cliente:",AutoSize=true,Padding=new Padding(0,9,0,0)});
   footer.Controls.Add(customer);
   var chooseCustomer=new Button {Text="CLIENTE / CADASTRAR",Width=170,Height=38,BackColor=Color.FromArgb(31,104,172),ForeColor=Color.White,Font=new Font("Segoe UI",9,FontStyle.Bold)};
   chooseCustomer.Click+=(s,e)=>ChooseCustomerInWindow();footer.Controls.Add(chooseCustomer);
   footer.Controls.Add(new Label {Text="Recebido:",AutoSize=true,Padding=new Padding(0,9,0,0)});footer.Controls.Add(received);footer.Controls.Add(changeLabel);
   footer.SetFlowBreak(changeLabel,true);
   payment.SelectedIndexChanged+=(s,e)=>{UpdateChange();SaveDraft();};received.ValueChanged+=(s,e)=>{UpdateChange();SaveDraft();};
   customer.TextChanged+=(s,e)=>SaveDraft();manualDate.ValueChanged+=(s,e)=>SaveDraft();
   received.Enter+=(s,e)=>received.Select(0,received.Text.Length);
   var finish=new Button {Text="FINALIZAR VENDA",Height=38,Width=165,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",9,FontStyle.Bold)};finish.FlatAppearance.BorderSize=0;finish.Click+=(s,e)=>Finish();footer.Controls.Add(finish);
   var edit=new Button {Text="EDITAR QTD. / PREÇO",Width=170,Height=38,Font=new Font("Segoe UI",9,FontStyle.Bold)};edit.Click+=(s,e)=>ChooseEditAction();footer.Controls.Add(edit);
   var remove=new Button {Text="REMOVER ITEM",Width=125,Height=38,Font=new Font("Segoe UI",9,FontStyle.Bold)};remove.Click+=(s,e)=>RemoveSelected();footer.Controls.Add(remove);
   var clear=new Button {Text="EXCLUIR TODOS",Width=135,Height=38,Font=new Font("Segoe UI",9,FontStyle.Bold)};clear.Click+=(s,e)=>ClearCart();footer.Controls.Add(clear);
   footer.SetFlowBreak(clear,true);
   foreach(Control button in new Control[]{chooseCustomer,finish,edit,remove,clear})button.Margin=new Padding(4,3,4,3);
   discountLabel.Text="Desconto:";discountMode.Items.AddRange(new object[]{"R$","%"});discountMode.SelectedIndex=0;
   discountMode.SelectedIndexChanged+=(s,e)=>{discountPercent=discountMode.SelectedIndex==1;discountInput.Maximum=discountPercent?100:99999999;discountValue=discountInput.Value;Render();};
   discountInput.ValueChanged+=(s,e)=>{discountValue=discountInput.Value;Render();};
   footer.Controls.Add(discountLabel);footer.Controls.Add(discountMode);footer.Controls.Add(discountInput);footer.Controls.Add(total);
   var deleteColumn=new DataGridViewButtonColumn {Name="ExcluirItem",HeaderText="Cestinho",Text="🗑",UseColumnTextForButtonValue=true,Width=78,AutoSizeMode=DataGridViewAutoSizeColumnMode.None};
   grid.Columns.Add(deleteColumn);
   grid.CellContentClick+=(s,e)=>{
    if(e.RowIndex<0||e.ColumnIndex<0||grid.Columns[e.ColumnIndex].Name!="ExcluirItem")return;
    if(e.RowIndex>=cart.Count)return;
    string itemName=cart[e.RowIndex].Name;
    if(MessageBox.Show("Excluir do carrinho somente "+itemName+"?","Excluir produto",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes){
     cart.RemoveAt(e.RowIndex);Render();
    }
   };
   grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex].Name!="ExcluirItem")EditSelected();};
   grid.Font=new Font("Segoe UI",10,FontStyle.Regular);grid.RowTemplate.Height=36;
   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(22,75,160);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   grid.ColumnHeadersHeight=38;grid.RowHeadersVisible=false;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(238,244,252);
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(40,100,170);grid.DefaultCellStyle.SelectionForeColor=Color.White;
   grid.CellFormatting+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&e.Value!=null){string name=grid.Columns[e.ColumnIndex].Name.ToLowerInvariant();if(name.Contains("descri")||name.Contains("nome")||name.Contains("name")){e.CellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;e.CellStyle.Padding=new Padding(7,4,7,4);}}};
   grid.DataBindingComplete+=(s,e)=>{foreach(DataGridViewColumn column in grid.Columns){if(column.Name.ToLowerInvariant().Contains("descri")||column.Name.ToLowerInvariant().Contains("nome")||column.Name.ToLowerInvariant().Contains("name")){column.MinimumWidth=260;column.FillWeight=240;column.DefaultCellStyle.WrapMode=DataGridViewTriState.True;}}grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;};
   var listArea=new Panel{Dock=DockStyle.Fill};
   listArea.Controls.Add(grid);
   listArea.Controls.Add(stockView);
   stockView.Font=new Font("Segoe UI",10);
   stockView.RowTemplate.Height=35;
   stockView.EnableHeadersVisualStyles=false;
   stockView.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(22,75,160);
   stockView.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   stockView.ColumnHeadersHeight=38;
   stockView.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(238,244,252);
   stockView.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)EditSelectedStockInPdv();};
   stockView.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;EditSelectedStockInPdv();}};
   Controls.Add(listArea);Controls.Add(footer);Controls.Add(header);
   if(rapidInventoryBar!=null)Controls.Add(rapidInventoryBar);
   suggestions.Name="pdvSuggestions";
   suggestions.Parent=this;
   if(manualEntry){lookupDelay.Interval=160;suggestions.Width=600;suggestions.Height=245;}
   // Mover os MESMOS controles e seus eventos para o layout aprovado na previa.
   PdvModernLayout.Apply(this,header,footer,listArea,rapidInventoryBar,
    code,qty,saleUnit,add,finish,edit,chooseCustomer,remove,clear,
    customer,received,changeLabel,discountLabel,discountMode,discountInput,total,
    grid,stockView,manualEntry);
   PositionSuggestions();
   LoadDraft();
   FormClosing+=(sender,args)=>SaveDraft();
   Render();code.Focus();
  }
  SQLiteConnection Open() {var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,FailIfMissing=true}.ToString());c.Open();return c;}
  static decimal Dec(object o) {return Convert.ToDecimal(o,CultureInfo.InvariantCulture);}
  // A lista de clientes nao e carregada em caixa visivel do PDV.
  List<string> GetCustomerNames(){
   var names=new List<string>();
   var existing=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"Consumidor"};
   using(var connection=Open()){
    using(var cmd=new SQLiteCommand("SELECT DISTINCT TRIM(customer_name) FROM sales WHERE customer_name IS NOT NULL AND TRIM(customer_name)<>'' ORDER BY TRIM(customer_name) LIMIT 500",connection))
    using(var reader=cmd.ExecuteReader())while(reader.Read()){
     string name=Convert.ToString(reader[0]);
     if(!string.IsNullOrWhiteSpace(name) && existing.Add(name))names.Add(name);
    }
    using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='customers'",connection)){
     if(Convert.ToInt32(check.ExecuteScalar())>0){
      using(var cmd=new SQLiteCommand("SELECT DISTINCT TRIM(name) FROM customers WHERE name IS NOT NULL AND TRIM(name)<>'' ORDER BY TRIM(name) LIMIT 1000",connection))
      using(var reader=cmd.ExecuteReader())while(reader.Read()){
       string name=Convert.ToString(reader[0]);
       if(!string.IsNullOrWhiteSpace(name) && existing.Add(name))names.Add(name);
      }
     }
    }
   }
   names.Sort(StringComparer.CurrentCultureIgnoreCase);
   names.Insert(0,"Consumidor");
   return names;
  }
  void ChooseCustomerInWindow(){ChooseCustomerInWindow(false);}
  bool ChooseCustomerInWindow(bool fiadoRequired){
   try{
    var names=GetCustomerNames();
    if(fiadoRequired)names.RemoveAll(name=>
     string.Equals(name,"Consumidor",StringComparison.OrdinalIgnoreCase));
    using(var dialog=new StyledDialogForm{Text=fiadoRequired?"PDV | Fiado - Selecionar cliente":"PDV | Selecionar cliente",Width=535,Height=535,MinimumSize=new Size(430,380),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
     var search=new TextBox{Dock=DockStyle.Top,Font=new Font("Segoe UI",12),Height=36};
     var hint=new Label{Text="Pesquisar cliente pelo nome:",Dock=DockStyle.Top,Height=30,Font=new Font("Segoe UI",10,FontStyle.Bold)};
     var list=new ListBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",12),IntegralHeight=false};
     var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=50,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(8,4,8,4)};
     var select=new Button{Text="Selecionar cliente",Width=160,Height=38,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
     var register=new Button{Text="Cadastrar novo",Width=145,Height=38,BackColor=Color.FromArgb(31,104,172),ForeColor=Color.White};
     var cancel=new Button{Text="Cancelar",Dock=DockStyle.Bottom,Height=37};
     Action fill=()=>{
      string filter=search.Text.Trim();
      list.BeginUpdate();
      try{
       list.Items.Clear();
       foreach(string name in names)
        if(filter.Length==0||name.IndexOf(filter,StringComparison.CurrentCultureIgnoreCase)>=0)
         list.Items.Add(name);
      }finally{list.EndUpdate();}
      if(!fiadoRequired&&list.Items.Count>0)list.SelectedIndex=0;
     };
     search.TextChanged+=(sender,args)=>fill();
     select.Click+=(sender,args)=>{if(list.SelectedItem!=null){dialog.Tag=Convert.ToString(list.SelectedItem);dialog.DialogResult=DialogResult.OK;}};
     register.Click+=(sender,args)=>{if(QuickRegisterCustomer()){dialog.Tag=customer.Text;dialog.DialogResult=DialogResult.OK;}};
     list.DoubleClick+=(sender,args)=>{if(list.SelectedItem!=null){dialog.Tag=Convert.ToString(list.SelectedItem);dialog.DialogResult=DialogResult.OK;}};
     cancel.Click+=(sender,args)=>dialog.DialogResult=DialogResult.Cancel;
     actions.Controls.Add(select);actions.Controls.Add(register);
     dialog.Controls.Add(list);dialog.Controls.Add(search);dialog.Controls.Add(hint);dialog.Controls.Add(actions);dialog.Controls.Add(cancel);
     fill();
     if(dialog.ShowDialog(this)!=DialogResult.OK||!(dialog.Tag is string))return false;
     string selected=((string)dialog.Tag).Trim();
     if(fiadoRequired && (selected.Length<2||
      string.Equals(selected,"Consumidor",StringComparison.OrdinalIgnoreCase)||
      !CustomerRegistered(selected))){
      MessageBox.Show("Selecione ou cadastre um cliente identificado para o Fiado.","Cliente obrigatorio");
      return false;
     }
     customer.Text=selected;return true;
    }
   }catch(Exception ex){
    MessageBox.Show("Não foi possível listar os clientes: "+ex.Message,"Clientes",MessageBoxButtons.OK,MessageBoxIcon.Error);
    return false;
   }
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
   using(var form=new StyledDialogForm{Text="PDV | Cadastro rápido de cliente",Width=550,Height=335,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
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
        using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM customers WHERE LOWER(TRIM(name))=LOWER(TRIM(@name))",connection)){
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
    if(ok){customer.Text=name.Text.Trim();}
    return ok;
   }
  }
  static string SearchFoldSql(string column){
   string expression="LOWER(COALESCE("+column+",''))";
   string accented="áàâãäéèêëíìîïóòôõöúùûüçÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ";
   string plain="aaaaaeeeeiiiiooooouuuucaaaaaeeeeiiiiooooouuuuc";
   for(int i=0;i<accented.Length;i++)expression="REPLACE("+expression+",'"+accented[i]+"','"+plain[i]+"')";
   return expression;
  }
  static string SearchFold(string term){
   string normalized=(term??"").ToLowerInvariant().Normalize(NormalizationForm.FormD);
   var result=new StringBuilder();
   foreach(char c in normalized)if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)result.Append(c);
   return result.ToString().Normalize(NormalizationForm.FormC);
  }
  void PositionSuggestions(){
   if(code.Parent==null||IsDisposed||!IsHandleCreated)return;
   Point location=PointToClient(code.Parent.PointToScreen(new Point(code.Left,code.Bottom)));
   suggestions.Left=Math.Max(0,Math.Min(location.X,ClientSize.Width-suggestions.Width));
   suggestions.Top=Math.Max(0,location.Y+3);
   suggestions.BringToFront();
  }
  void FindSuggestions(){
   string term=code.Text.Trim();
   if(term.Length<(manualEntry?1:2)){suggestions.Visible=false;return;}
   try{
    suggestions.Items.Clear();suggestionCodes.Clear();
    string folded=SearchFold(term);
    var matches=new List<Tuple<string,string,decimal,decimal,int>>();
    using(var connection=Open())
    using(var cmd=new SQLiteCommand("SELECT barcode,internal_code,name,price,stock FROM products",connection))
    using(var reader=cmd.ExecuteReader())while(reader.Read()){
     string name=Convert.ToString(reader["name"]);
     string barcode=Convert.ToString(reader["barcode"]);
     string internalCode=Convert.ToString(reader["internal_code"]);
     string key=!string.IsNullOrWhiteSpace(barcode)?barcode:internalCode;
     if(string.IsNullOrWhiteSpace(key))continue;
     string normalized=SearchFold(name);
     bool begins=normalized.StartsWith(folded,StringComparison.Ordinal);
     if(!begins&&!normalized.Contains(folded)&&!barcode.StartsWith(term,StringComparison.OrdinalIgnoreCase)&&!internalCode.StartsWith(term,StringComparison.OrdinalIgnoreCase))continue;
     matches.Add(Tuple.Create(name,key,Dec(reader["price"]),Dec(reader["stock"]),begins?0:1));
    }
    foreach(var item in matches.OrderBy(x=>x.Item5).ThenBy(x=>x.Item1,StringComparer.CurrentCultureIgnoreCase).Take(30)){
     int index=suggestions.Items.Add(item.Item1+"  |  R$ "+item.Item3.ToString("N2")+"  |  QTD "+item.Item4.ToString("N3")+"  |  "+item.Item2);
     suggestionCodes[index]=item.Item2;
    }
    suggestions.Visible=suggestions.Items.Count>0;
    if(suggestions.Visible){PositionSuggestions();suggestions.BringToFront();if(manualEntry&&suggestions.SelectedIndex<0)suggestions.SelectedIndex=0;}
   }catch(Exception ex){suggestions.Visible=false;if(manualEntry)statusLookupError(ex.Message);}
  }
  void statusLookupError(string detail){if(!IsDisposed)Text="Lançamento rápido | Erro na busca: "+detail;}
  void ChooseSuggestion(){
   string selected;
   if(!suggestionCodes.TryGetValue(suggestions.SelectedIndex,out selected))return;
   suppressLookup=true;lookupDelay.Stop();suggestions.Visible=false;code.Text=selected;suppressLookup=false;
   AddProduct(true);
  }
  // Conferência explícita do estoque real antes da venda. Não permite estoque negativo.
  bool AdjustEmptyStock(Item item,decimal previous,decimal required){
   using(var dlg=new StyledDialogForm{Text="PDV | Ajustar estoque para continuar a venda",Width=610,Height=345,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
    var info=new Label{Left=15,Top=12,Width=565,Height=75,Font=new Font("Segoe UI",11,FontStyle.Bold),ForeColor=Color.FromArgb(18,65,143),
     Text=item.Name+"\nEstoque registrado: "+previous.ToString("N3")+"  |  Necessário para a venda: "+required.ToString("N3")};
    var quantity=new NumericUpDown{Left=220,Top=100,Width=225,DecimalPlaces=3,Minimum=0,Maximum=10000000,Value=Math.Min(10000000,Math.Max(0,required))};
    var reason=new TextBox{Left=220,Top=151,Width=340,MaxLength=250,Text="Correção de quantidade após conferência física"};
    var save=new Button{Left=220,Top=210,Width=210,Height=40,Text="Salvar quantidade real",BackColor=Color.FromArgb(25,96,174),ForeColor=Color.White};
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
  // Ao escolher um produto da lista, informar a quantidade antes de inserir no carrinho.
  // Leitura direta por codigo de barras continua usando a quantidade do PDV, sem abrir popup.
  decimal? AskProductQuantity(Item product,decimal defaultQuantity){
   using(var dialog=new StyledDialogForm{Text="PDV | Quantidade do produto",Width=540,Height=290,MinimumSize=new Size(480,265),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.FromArgb(229,242,255),KeyPreview=true}){
    var name=new Label{Text=product.Name,Left=20,Top=19,Width=475,Height=54,Font=new Font("Segoe UI",12,FontStyle.Bold),ForeColor=Color.FromArgb(20,69,147)};
    bool byWeight=Convert.ToString(saleUnit.SelectedItem)=="KG";
    var caption=new Label{Text=byWeight?"Quantidade (kg):":"Quantidade (unidades):",Left=20,Top=92,Width=210,Height=28,Font=new Font("Segoe UI",10,FontStyle.Bold)};
    var amount=new NumericUpDown{Left=236,Top=86,Width=240,Height=37,DecimalPlaces=byWeight?3:0,Increment=byWeight?0.001m:1m,Minimum=byWeight?0.001m:1m,Maximum=999999m,Value=Math.Max(byWeight?0.001m:1m,Math.Min(999999m,byWeight?defaultQuantity:Math.Ceiling(defaultQuantity))),Font=new Font("Segoe UI",14,FontStyle.Bold)};
    var add=new Button{Text="Adicionar ao carrinho",Left=20,Top=159,Width=245,Height=44,DialogResult=DialogResult.OK,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
    var cancel=new Button{Text="Cancelar",Left=283,Top=159,Width=193,Height=44,DialogResult=DialogResult.Cancel};
    dialog.Controls.AddRange(new Control[]{name,caption,amount,add,cancel});
    dialog.AcceptButton=add;dialog.CancelButton=cancel;
    dialog.Shown+=(sender,args)=>{amount.Focus();amount.Select(0,amount.Text.Length);};
    return dialog.ShowDialog(this)==DialogResult.OK?(decimal?)amount.Value:null;
   }
  }
  // Uma lista completa, sem paginação nem janela adicional. Consultar não movimenta o banco.
  void ShowStockInPdv(){
   if(!manualEntry)return;
   lookupDelay.Stop();suggestions.Visible=false;stockViewOpen=true;
   grid.Visible=false;stockView.Visible=true;LoadStockInPdv();if(stockSearch!=null&&stockSearch.CanFocus)stockSearch.Focus();
  }
  void ShowSaleCart(){
   if(!manualEntry)return;
   stockViewOpen=false;stockView.Visible=false;grid.Visible=true;code.Focus();
  }
  long StockSelectionId(){
   if(stockView==null||stockView.IsDisposed||stockView.CurrentRow==null||!stockView.Columns.Contains("ID"))return 0L;
   var cell=stockView.CurrentRow.Cells["ID"];
   if(cell==null)return 0L;
   var value=cell.Value;
   return value==null||value==DBNull.Value?0L:Convert.ToInt64(value);
  }
  void LoadStockInPdv(){
   if(!manualEntry||!stockViewOpen)return;
   try{
    long beforeSelection=0;
    try{beforeSelection=StockSelectionId();}catch(NullReferenceException){beforeSelection=0;}
    var table=new DataTable();
    table.Columns.Add("ID",typeof(long));table.Columns.Add("Código");
    table.Columns.Add("Código de barras");table.Columns.Add("Descrição");
    table.Columns.Add("Em estoque",typeof(decimal));table.Columns.Add("Preço",typeof(decimal));
    table.Columns.Add("Custo",typeof(decimal));table.Columns.Add("Qtd. lançada",typeof(decimal));
    string term=stockSearch.Text.Trim();
    using(var cn=Open())
    using(var cmd=new SQLiteCommand("SELECT id,barcode,internal_code,name,stock,price,cost FROM products WHERE (@term='' OR name LIKE @search OR barcode LIKE @search OR internal_code LIKE @search) ORDER BY name COLLATE NOCASE,id",cn)){
     cmd.Parameters.AddWithValue("@term",term);
     cmd.Parameters.AddWithValue("@search","%"+term+"%");
     using(var rd=cmd.ExecuteReader())while(rd.Read()){
      long id=Convert.ToInt64(rd["id"]);
      table.Rows.Add(id,Convert.ToString(rd["internal_code"]),Convert.ToString(rd["barcode"]),
       Convert.ToString(rd["name"]),Dec(rd["stock"]),Dec(rd["price"]),Dec(rd["cost"]),
       cart.Where(x=>x.Id==id).Sum(x=>x.Qty));
     }
    }
    stockView.DataSource=table;
    if(stockView==null||stockView.IsDisposed)return;
    if(stockView.Columns.Contains("ID"))stockView.Columns["ID"].Visible=false;
    if(stockView.Columns.Contains("Descrição"))stockView.Columns["Descrição"].MinimumWidth=295;
    foreach(string column in new[]{"Em estoque","Preço","Custo","Qtd. lançada"})
     if(stockView.Columns.Contains(column))
      stockView.Columns[column].DefaultCellStyle.Format=(column=="Em estoque"||column=="Qtd. lançada")?"N3":"N2";
    stockHint.Text=table.Rows.Count+" produtos | Selecione e clique em Editar Qtd. / Preço (ou dê duplo clique)";
    if(beforeSelection>0){
     foreach(DataGridViewRow row in stockView.Rows)if(!row.IsNewRow && row.Cells["ID"]!=null && row.Cells["ID"].Value!=null && row.Cells["ID"].Value!=DBNull.Value && Convert.ToInt64(row.Cells["ID"].Value)==beforeSelection){
      stockView.CurrentCell=row.Cells["Descrição"];break;
     }
    }
   }catch(Exception ex){
    stockHint.Text="Erro ao carregar";
    MessageBox.Show("Erro ao listar produtos no PDV: "+ex.Message,"Lançamento rápido",MessageBoxButtons.OK,MessageBoxIcon.Error);
   }
  }
  // Inclui ou altera somente o produto escolhido. Nenhuma venda é gravada ao listar o catálogo.
  void EditSelectedStockInPdv(){
   if(!manualEntry||!stockViewOpen)return;
   long id=StockSelectionId();
   if(id<=0){MessageBox.Show("Selecione um produto no estoque.");return;}
   try{
    Item selected=null;decimal available=0;
    using(var cn=Open())
    using(var cmd=new SQLiteCommand("SELECT id,barcode,internal_code,name,stock,price,cost FROM products WHERE id=@id",cn)){
     cmd.Parameters.AddWithValue("@id",id);
     using(var rd=cmd.ExecuteReader())if(rd.Read()){
      selected=new Item{Id=id,Barcode=Convert.ToString(rd["barcode"]),InternalCode=Convert.ToString(rd["internal_code"]),
       Name=Convert.ToString(rd["name"]),Price=Dec(rd["price"]),Cost=Dec(rd["cost"])};
      available=Dec(rd["stock"]);
     }
    }
    if(selected==null){MessageBox.Show("Produto não encontrado.");return;}
    string unit=Convert.ToString(saleUnit.SelectedItem)=="KG"?"KG":"UN";
    Item existing=cart.FirstOrDefault(x=>x.Id==id&&x.Unit==unit);
    using(var dialog=new StyledDialogForm{Text="Lançamento rápido | Quantidade e preço",
     Width=510,Height=300,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,
     MaximizeBox=false,MinimizeBox=false,BackColor=Color.White}){
     var name=new Label{Text=selected.Name,Left=16,Top=16,Width=465,Height=45,Font=new Font("Segoe UI",11,FontStyle.Bold)};
     var quantity=new NumericUpDown{Left=175,Top=75,Width=200,Minimum=unit=="KG"?0.001m:1m,
      Maximum=999999,DecimalPlaces=unit=="KG"?3:0,Increment=unit=="KG"?0.001m:1m,Value=existing==null?1m:existing.Qty};
     var price=new NumericUpDown{Left=175,Top=118,Width=200,Minimum=0,Maximum=99999999,
      DecimalPlaces=2,Value=existing==null?selected.Price:existing.Price};
     var preview=new Label{Left=17,Top=166,Width=460,Height=28,Font=new Font("Segoe UI",11,FontStyle.Bold),
      ForeColor=Color.FromArgb(22,75,160)};
     Action refresh=()=>preview.Text="Subtotal: R$ "+Math.Round(quantity.Value*price.Value,2).ToString("N2");
     quantity.ValueChanged+=(s,e)=>refresh();price.ValueChanged+=(s,e)=>refresh();refresh();
     var ok=new Button{Text=existing==null?"INCLUIR NA VENDA":"SALVAR ALTERAÇÕES",Left=175,Top=209,Width=200,Height=36,
      BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
     var cancel=new Button{Text="Cancelar",Left=385,Top=209,Width=100,Height=36};
     ok.Click+=(s,e)=>{if(price.Value<=0){MessageBox.Show("Preço deve ser maior que zero.");return;}dialog.DialogResult=DialogResult.OK;};
     cancel.Click+=(s,e)=>dialog.DialogResult=DialogResult.Cancel;
     dialog.Controls.AddRange(new Control[]{name,
      new Label{Text="Quantidade da venda:",Left=16,Top=80,Width=155},
      new Label{Text="Preço desta venda:",Left=16,Top=123,Width=155},
      quantity,price,preview,ok,cancel});
     dialog.AcceptButton=ok;dialog.CancelButton=cancel;
     if(dialog.ShowDialog(this)!=DialogResult.OK)return;
     decimal required=quantity.Value+cart.Where(x=>x.Id==id&&!object.ReferenceEquals(x,existing)).Sum(x=>x.Qty);
     if(available<required){
      if(!AdjustEmptyStock(selected,available,required))return;
      using(var cn=Open())
      using(var cmd=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",cn)){
       cmd.Parameters.AddWithValue("@id",id);available=Dec(cmd.ExecuteScalar());
      }
      if(available<required){MessageBox.Show("Estoque insuficiente. Item não incluído.");return;}
     }
     if(existing==null){selected.Qty=quantity.Value;selected.Price=price.Value;selected.Unit=unit;cart.Add(selected);}
     else{existing.Qty=quantity.Value;existing.Price=price.Value;}
     Render();LoadStockInPdv();stockView.Focus();
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível editar o item: "+ex.Message,"Lançamento rápido",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  // Abrir o mesmo cadastro de produtos do Armazem, com estoque inteiro e edicao auditada.
  // Nao recria cadastros nem altera historico de vendas anotadas.
  void OpenFullInventoryForManualSales(){
   if(!manualEntry)return;
   try{
    lookupDelay.Stop();suggestions.Visible=false;
    long selectedStockProductId=0;
    using(var products=new ProdutosForm(db,true)){
     if(products.ShowDialog(this)==DialogResult.OK)
      selectedStockProductId=products.SelectedQuickSaleProductId;
    }
    // Atualizar apenas os dados dos itens ja presentes no carrinho ainda nao finalizado.
    // Os itens e quantidades permanecem; alteracoes feitas no cadastro refletem no lancamento.
    int changed=0;
    using(var conn=Open()){
     foreach(var item in cart){
      using(var cmd=new SQLiteCommand("SELECT name,barcode,internal_code,price,cost FROM products WHERE id=@id",conn)){
       cmd.Parameters.AddWithValue("@id",item.Id);
       using(var rd=cmd.ExecuteReader()){
        if(!rd.Read())continue;
        string name=Convert.ToString(rd["name"]),barcode=Convert.ToString(rd["barcode"]),internalCode=Convert.ToString(rd["internal_code"]);
        decimal price=Dec(rd["price"]),cost=Dec(rd["cost"]);
        if(item.Name!=name||item.Barcode!=barcode||item.InternalCode!=internalCode||item.Price!=price||item.Cost!=cost){
         item.Name=name;item.Barcode=barcode;item.InternalCode=internalCode;item.Price=price;item.Cost=cost;changed++;
        }
       }
      }
     }
    }
    Render();
    if(changed>0)MessageBox.Show(changed+" item(ns) do carrinho foram atualizados com nome, custo e preco cadastrados. Confira os totais antes de finalizar.","Lançamento rápido",MessageBoxButtons.OK,MessageBoxIcon.Information);
    if(selectedStockProductId>0)AddQuickInventoryProduct(selectedStockProductId);
    code.Focus();
   }catch(Exception ex){MessageBox.Show("Nao foi possivel abrir ou atualizar o estoque: "+ex.Message,"Lançamento rápido",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  // Usa o ID exato selecionado no Estoque Completo; assim o produto vira item
  // da venda anotada, passando pelas mesmas validacoes de quantidade e estoque do PDV.
  void AddQuickInventoryProduct(long productId){
   if(!manualEntry||productId<=0)return;
   try{
    Item selected=null;decimal available=0m;
    using(var cn=Open())
    using(var cmd=new SQLiteCommand("SELECT id,barcode,internal_code,name,price,cost,stock FROM products WHERE id=@id",cn)){
     cmd.Parameters.AddWithValue("@id",productId);
     using(var rd=cmd.ExecuteReader()){
      if(rd.Read()){
       selected=new Item{Id=Convert.ToInt64(rd["id"]),Barcode=Convert.ToString(rd["barcode"]),InternalCode=Convert.ToString(rd["internal_code"]),Name=Convert.ToString(rd["name"]),Price=Dec(rd["price"]),Cost=Dec(rd["cost"])};
       available=Dec(rd["stock"]);
      }
     }
    }
    if(selected==null){MessageBox.Show("Produto nao encontrado no estoque. Consulte o cadastro.");return;}
    decimal? entered=AskProductQuantity(selected,qty.Value);
    if(!entered.HasValue)return;
    decimal quantity=entered.Value;
    string unit=Convert.ToString(saleUnit.SelectedItem)=="KG"?"KG":"UN";
    decimal required=quantity+cart.Where(x=>x.Id==selected.Id).Sum(x=>x.Qty);
    if(available<required){
     if(!AdjustEmptyStock(selected,available,required))return;
     using(var cn=Open())
     using(var cmd=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",cn)){
      cmd.Parameters.AddWithValue("@id",selected.Id);
      available=Dec(cmd.ExecuteScalar());
     }
     if(available<required){MessageBox.Show("Estoque insuficiente para a quantidade informada.");return;}
    }
    var existing=cart.FirstOrDefault(x=>x.Id==selected.Id&&x.Unit==unit);
    if(existing!=null)existing.Qty+=quantity;
    else{selected.Qty=quantity;selected.Unit=unit;cart.Add(selected);}
    qty.Value=1;Render();
    MessageBox.Show(selected.Name+" adicionado a venda anotada. Para alterar a quantidade ou o preco apenas desta venda, use Editar Qtd./Preco.","Lancamento rapido",MessageBoxButtons.OK,MessageBoxIcon.Information);
   }catch(Exception ex){MessageBox.Show("Nao foi possivel adicionar o produto: "+ex.Message,"Lancamento rapido",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }

  // Importacao assistida: uma venda por vez, sem gravacao automatica.
  // CSV UTF-8: venda;produto;quantidade;pagamento (data e hora opcionais).
  sealed class NotebookLine {public string Sale,Product,Payment,Date;public decimal Quantity;}
  static string[] SplitNotebookCsv(string line,char separator){
   var fields=new List<string>();var value=new StringBuilder();bool quoted=false;
   for(int i=0;i<line.Length;i++){
    char ch=line[i];
    if(ch=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){value.Append('"');i++;}else quoted=!quoted;}
    else if(ch==separator&&!quoted){fields.Add(value.ToString().Trim());value.Length=0;}
    else value.Append(ch);
   }
   if(quoted)throw new FormatException("Aspas nao fechadas no CSV.");
   fields.Add(value.ToString().Trim());return fields.ToArray();
  }
  static string NotebookField(string[] cells,Dictionary<string,int> columns,params string[] keys){
   foreach(string key in keys){int index;if(columns.TryGetValue(key,out index)&&index<cells.Length)return cells[index].Trim();}
   return "";
  }
  void ImportNotebookSales(){
   if(cart.Count>0){MessageBox.Show("Finalize ou limpe a venda atual antes de importar. O carrinho existente foi preservado.","Importar vendas");return;}
   using(var picker=new OpenFileDialog{Title="Selecionar vendas anotadas (CSV)",Filter="Planilha CSV (*.csv)|*.csv",CheckFileExists=true}){
    if(picker.ShowDialog(this)!=DialogResult.OK)return;
    try{
     var lines=File.ReadAllLines(picker.FileName,Encoding.UTF8);
     if(lines.Length<2)throw new Exception("Arquivo sem linhas de vendas.");
     char separator=lines[0].Count(x=>x==';')>=lines[0].Count(x=>x==',')?';':',';
     string[] headers=SplitNotebookCsv(lines[0].TrimStart('\uFEFF'),separator);
     var columns=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
     for(int i=0;i<headers.Length;i++)columns[SearchFold(headers[i]).Replace(" ","").Replace("_","")]=i;
     if(!columns.ContainsKey("produto")&&!columns.ContainsKey("descricao"))throw new Exception("Falta coluna Produto ou Descricao.");
     if(!columns.ContainsKey("venda")&&!columns.ContainsKey("numerovenda"))throw new Exception("Falta coluna Venda.");
     var entries=new List<NotebookLine>();
     for(int i=1;i<lines.Length;i++){
      if(string.IsNullOrWhiteSpace(lines[i]))continue;
      var cells=SplitNotebookCsv(lines[i],separator);
      string number=NotebookField(cells,columns,"venda","numerovenda");
      string product=NotebookField(cells,columns,"produto","descricao");
      string quantity=NotebookField(cells,columns,"quantidade","qtd");
      decimal amount;
      if(string.IsNullOrWhiteSpace(number)||string.IsNullOrWhiteSpace(product)||!decimal.TryParse(quantity,NumberStyles.Number,CultureInfo.GetCultureInfo("pt-BR"),out amount)||amount<=0)
       throw new Exception("Linha "+(i+1)+": venda, produto ou quantidade invalida.");
      entries.Add(new NotebookLine{Sale=number,Product=product,Quantity=amount,
       Payment=NotebookField(cells,columns,"pagamento","formapagamento"),
       Date=NotebookField(cells,columns,"data","datavenda")});
     }
     if(entries.Count==0)throw new Exception("Nenhum produto para importar.");
     var groups=entries.GroupBy(x=>x.Sale).ToList();
     using(var dialog=new StyledDialogForm{Text="Selecionar venda anotada",Width=720,Height=480,StartPosition=FormStartPosition.CenterParent}){
      var list=new ListBox{Dock=DockStyle.Fill,Font=new Font("Segoe UI",11)};
      foreach(var group in groups)list.Items.Add("Venda "+group.Key+" | "+group.Sum(x=>x.Quantity).ToString("N3")+" unidades | "+group.First().Payment+" | "+group.Count()+" produtos");
      var info=new Label{Text="Selecione UMA venda. Confira itens, valores, data e pagamento antes de finalizar. O arquivo nao e gravado automaticamente.",Dock=DockStyle.Top,Height=58};
      var choose=new Button{Text="CARREGAR VENDA SELECIONADA",Dock=DockStyle.Bottom,Height=42,DialogResult=DialogResult.OK};
      dialog.Controls.Add(list);dialog.Controls.Add(info);dialog.Controls.Add(choose);
      if(list.Items.Count>0)list.SelectedIndex=0;
      dialog.AcceptButton=choose;
      if(dialog.ShowDialog(this)!=DialogResult.OK||list.SelectedIndex<0)return;
      var selected=groups[list.SelectedIndex].ToList();
      // Exigir correspondencia unica no cadastro; nunca escolher produto parecido silenciosamente.
      var imported=new List<Item>();
      using(var cn=Open()){
       foreach(var entry in selected){
        var matches=new List<Item>();
        using(var cmd=new SQLiteCommand("SELECT id,barcode,internal_code,name,price,cost FROM products WHERE lower(name)=lower(@name) OR barcode=@name OR internal_code=@name",cn)){
         cmd.Parameters.AddWithValue("@name",entry.Product);
         using(var rd=cmd.ExecuteReader())while(rd.Read())matches.Add(new Item{
          Id=Convert.ToInt64(rd["id"]),Barcode=Convert.ToString(rd["barcode"]),
          InternalCode=Convert.ToString(rd["internal_code"]),Name=Convert.ToString(rd["name"]),
          Price=Dec(rd["price"]),Cost=Dec(rd["cost"]),Qty=entry.Quantity});
        }
        // Se o nome do CSV nao for identico ao cadastro, procurar equivalencia normalizada.
        // Nunca selecionar automaticamente quando houver mais de um candidato.
        if(matches.Count==0){
         string wanted=SearchFold(entry.Product).Trim();
         using(var fallback=new SQLiteCommand("SELECT id,barcode,internal_code,name,price,cost FROM products ORDER BY name,id",cn))
         using(var rd=fallback.ExecuteReader())while(rd.Read()){
          string registered=SearchFold(Convert.ToString(rd["name"])).Trim();
          if(registered!=wanted)continue;
          matches.Add(new Item{Id=Convert.ToInt64(rd["id"]),Barcode=Convert.ToString(rd["barcode"]),
           InternalCode=Convert.ToString(rd["internal_code"]),Name=Convert.ToString(rd["name"]),
           Price=Dec(rd["price"]),Cost=Dec(rd["cost"]),Qty=entry.Quantity});
         }
        }
        // Alias confirmado pelo codigo de barras exibido no cadastro.
        if(matches.Count==0 && SearchFold(entry.Product).Trim()=="suco mid de abacaxi"){
         using(var alias=new SQLiteCommand("SELECT id,barcode,internal_code,name,price,cost FROM products WHERE barcode=@barcode",cn)){
          alias.Parameters.AddWithValue("@barcode","7891132082407");
          using(var rd=alias.ExecuteReader())while(rd.Read()){
           string registered=SearchFold(Convert.ToString(rd["name"]));
           if(registered.Contains("mid") && registered.Contains("abacaxi"))
            matches.Add(new Item{Id=Convert.ToInt64(rd["id"]),Barcode=Convert.ToString(rd["barcode"]),
             InternalCode=Convert.ToString(rd["internal_code"]),Name=Convert.ToString(rd["name"]),
             Price=Dec(rd["price"]),Cost=Dec(rd["cost"]),Qty=entry.Quantity});
          }
         }
        }
        if(matches.Count!=1)throw new Exception("Produto '"+entry.Product+"' encontrado "+matches.Count+" vez(es). Confira a descricao no cadastro ou informe o codigo de barras/interno no CSV. Nenhum item foi carregado.");
        imported.Add(matches[0]);
       }
      }
      string method=SearchFold(selected[0].Payment.Trim()).ToUpperInvariant().Replace(" ","_");
      if(method=="DEBITO")method="DEBITO";
      if(method=="CREDITO")method="CREDITO";
      if(selected.Any(x=>!string.Equals(x.Payment.Trim(),selected[0].Payment.Trim(),StringComparison.OrdinalIgnoreCase)))
       throw new Exception("Formas de pagamento diferentes na mesma venda. Confira o arquivo.");
      DateTime parsed;
      if(!string.IsNullOrWhiteSpace(selected[0].Date)){
       if(!DateTime.TryParse(selected[0].Date,CultureInfo.GetCultureInfo("pt-BR"),DateTimeStyles.None,out parsed)||parsed>DateTime.Now)
        throw new Exception("Data invalida ou futura na venda "+selected[0].Sale+".");
       if(selected.Any(x=>x.Date!=selected[0].Date))throw new Exception("Datas diferentes na mesma venda.");
       manualDate.Value=parsed;
      }
      if(!payment.Items.Cast<object>().Any(x=>string.Equals(Convert.ToString(x),method,StringComparison.OrdinalIgnoreCase)))
       throw new Exception("Forma de pagamento nao reconhecida: "+method);
      foreach(var item in imported){
       var existing=cart.FirstOrDefault(x=>x.Id==item.Id);
       if(existing!=null)existing.Qty+=item.Qty;else cart.Add(item);
      }
      payment.SelectedItem=method;
      Render();SaveDraft();
      MessageBox.Show("Venda "+selected[0].Sale+" carregada no carrinho. Confira data, pagamento, quantidade e preco antes de finalizar. Para as demais vendas, repita a importacao. Nao importe uma venda ja registrada.","Conferencia obrigatoria");
     }
    }catch(Exception ex){MessageBox.Show("Importacao cancelada: "+ex.Message,"Importar vendas",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   }
  }
  void AddProduct(bool askQuantity=false) {
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
      using(var cmd=new SQLiteCommand("SELECT id,barcode,internal_code,name,price,cost,stock FROM products ORDER BY name,id",connection)){
       using(var reader=cmd.ExecuteReader())while(reader.Read()){
        if(!SearchFold(Convert.ToString(reader["name"])).Contains(SearchFold(search)))continue;
        if(matches.Count>=101)break;
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
     using(var dialog=new StyledDialogForm {Text="Selecione o produto",Width=780,Height=500,StartPosition=FormStartPosition.CenterParent,MinimumSize=new Size(580,350)}){
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
    if(askQuantity){
     decimal? entered=AskProductQuantity(selected,quantity);
     if(!entered.HasValue)return;
     quantity=entered.Value;
    }
    string unit=Convert.ToString(saleUnit.SelectedItem)=="KG"?"KG":"UN";
    var existing=cart.FirstOrDefault(x=>x.Id==selected.Id&&x.Unit==unit);
    decimal required=quantity+cart.Where(x=>x.Id==selected.Id).Sum(x=>x.Qty);
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
  decimal DiscountAmount(){
   decimal gross=cart.Sum(i=>i.Total);
   return Math.Min(gross,discountPercent?Math.Round(gross*discountValue/100m,2,MidpointRounding.AwayFromZero):discountValue);
  }
  decimal FinalAmount(){return cart.Sum(i=>i.Total)-DiscountAmount();}
  void ConfigureDiscount(){
   using(var dlg=new StyledDialogForm{Text="Desconto na venda",Width=440,Height=250,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
    var mode=new ComboBox{Left=20,Top=28,Width=175,DropDownStyle=ComboBoxStyle.DropDownList};
    mode.Items.AddRange(new object[]{"Valor em R$","Porcentagem %"});mode.SelectedIndex=discountPercent?1:0;
    var value=new NumericUpDown{Left=210,Top=28,Width=175,Maximum=10000000,DecimalPlaces=2,Value=discountValue};
    var preview=new Label{Left=20,Top=85,Width=390,Height=35,Font=new Font("Segoe UI",10,FontStyle.Bold)};
    Action update=()=>{decimal gross=cart.Sum(i=>i.Total);decimal amount=mode.SelectedIndex==1?Math.Round(gross*value.Value/100m,2,MidpointRounding.AwayFromZero):value.Value;preview.Text="Desconto: R$ "+amount.ToString("N2")+" | Total: R$ "+(gross-amount).ToString("N2");preview.ForeColor=amount>=gross?Color.DarkRed:Color.DarkBlue;};
    mode.SelectedIndexChanged+=(sender,args)=>{value.Maximum=mode.SelectedIndex==1?100:10000000;if(value.Value>value.Maximum)value.Value=value.Maximum;update();};
    value.ValueChanged+=(sender,args)=>update();if(mode.SelectedIndex==1)value.Maximum=100;update();
    var apply=new Button{Text="Aplicar desconto",Left=20,Top=145,Width=175,Height=38};
    var cancel=new Button{Text="Cancelar",Left=210,Top=145,Width=175,Height=38};
    apply.Click+=(sender,args)=>{decimal gross=cart.Sum(i=>i.Total);decimal amount=mode.SelectedIndex==1?Math.Round(gross*value.Value/100m,2,MidpointRounding.AwayFromZero):value.Value;if(amount>=gross&&gross>0){MessageBox.Show("O desconto deve ser menor que o total da venda.");return;}discountValue=value.Value;discountPercent=mode.SelectedIndex==1;Render();dlg.DialogResult=DialogResult.OK;};
    cancel.Click+=(sender,args)=>dlg.Close();dlg.Controls.AddRange(new Control[]{mode,value,preview,apply,cancel});dlg.ShowDialog(this);
   }
  }
  sealed class CartDraftLine {
   public long Id {get;set;}
   public string Barcode {get;set;}
   public string InternalCode {get;set;}
   public string Name {get;set;}
   public decimal Qty {get;set;}
   public decimal Price {get;set;}
   public decimal Cost {get;set;}
   public string Unit {get;set;}
  }
  sealed class CartDraft {
   public List<CartDraftLine> Items {get;set;}
   public string Customer {get;set;}
   public string Payment {get;set;}
   public decimal Received {get;set;}
   public decimal DiscountValue {get;set;}
   public bool DiscountPercent {get;set;}
   public string SaleDate {get;set;}
  }
  string DraftPath(){
   string key=Path.GetFullPath(db).ToUpperInvariant()+"|"+(manualEntry?"ANOTADAS":"PDV");
   byte[] bytes=SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
   string hash=BitConverter.ToString(bytes).Replace("-","");
   return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "ArmazemGratidaoNovo","Carrinhos",hash+".json");
  }
  void LoadDraft(){
   try{
    string path=DraftPath();
    if(File.Exists(path)){
     var saved=new JavaScriptSerializer().Deserialize<CartDraft>(File.ReadAllText(path,Encoding.UTF8));
     if(saved==null||saved.Items==null)throw new Exception("Arquivo de carrinho incompleto.");
     foreach(var row in saved.Items){
      if(row.Id<=0||row.Qty<=0||row.Price<0||row.Cost<0||string.IsNullOrWhiteSpace(row.Name))
       throw new Exception("Item inválido no carrinho salvo.");
     }
     cart.Clear();
     foreach(var row in saved.Items)cart.Add(new Item{Id=row.Id,Barcode=row.Barcode,InternalCode=row.InternalCode,
       Name=row.Name,Qty=row.Qty,Price=row.Price,Cost=row.Cost,Unit=row.Unit??"UN"});
     discountValue=saved.DiscountValue;
     discountPercent=saved.DiscountPercent;
     discountMode.SelectedIndex=discountPercent?1:0;
     discountInput.Value=Math.Max(discountInput.Minimum,Math.Min(discountInput.Maximum,discountValue));
     customer.Text=string.IsNullOrWhiteSpace(saved.Customer)?"Consumidor":saved.Customer;
     int paymentIndex=payment.Items.IndexOf(saved.Payment??"");
     if(paymentIndex>=0)payment.SelectedIndex=paymentIndex;
     if(saved.Received>=received.Minimum&&saved.Received<=received.Maximum)received.Value=saved.Received;
     DateTime restoredDate;
     if(manualEntry&&DateTime.TryParseExact(saved.SaleDate,"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out restoredDate)
      &&restoredDate>=manualDate.MinDate&&restoredDate<=manualDate.MaxDate)manualDate.Value=restoredDate;
     if(cart.Count>0)MessageBox.Show("Carrinho não finalizado recuperado: "+cart.Count+" linha(s). Confira os itens antes de finalizar.",
       "Recuperação do carrinho",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }
    draftReady=true;
   }catch(Exception ex){
    draftReady=false; // Nunca sobrescrever um arquivo anterior que não pôde ser recuperado.
    MessageBox.Show("Não foi possível recuperar o carrinho salvo. O arquivo foi preservado e não será sobrescrito nesta janela.\n"+ex.Message,
     "Proteção do carrinho",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   }
  }
  void SaveDraft(){
   if(!draftReady)return;
   try{
    var snapshot=new CartDraft{
     Items=cart.Select(i=>new CartDraftLine{Id=i.Id,Barcode=i.Barcode,InternalCode=i.InternalCode,
       Name=i.Name,Qty=i.Qty,Price=i.Price,Cost=i.Cost,Unit=i.Unit}).ToList(),
     Customer=customer.Text,Payment=Convert.ToString(payment.SelectedItem),Received=received.Value,
     DiscountValue=discountValue,DiscountPercent=discountPercent,
     SaleDate=manualEntry?manualDate.Value.ToString("yyyy-MM-dd HH:mm:ss"):""
    };
    string path=DraftPath(),folder=Path.GetDirectoryName(path);
    Directory.CreateDirectory(folder);
    string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{
     File.WriteAllText(temp,new JavaScriptSerializer().Serialize(snapshot),Encoding.UTF8);
     if(File.Exists(path))File.Replace(temp,path,null);
     else File.Move(temp,path);
    }finally{if(File.Exists(temp))File.Delete(temp);}
    draftSaveWarned=false;
   }catch(Exception ex){
    if(!draftSaveWarned){draftSaveWarned=true;MessageBox.Show("Não foi possível salvar automaticamente o carrinho. Não feche o PDV sem resolver o problema.\n"+ex.Message,
      "Falha no salvamento do carrinho",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   }
  }
  void Render() {
   var dt=new DataTable();dt.Columns.Add("Codigo");dt.Columns.Add("Produto");dt.Columns.Add("Quantidade",typeof(decimal));dt.Columns.Add("Unidade");dt.Columns.Add("Preco",typeof(decimal));dt.Columns.Add("Total",typeof(decimal));
   foreach(var i in cart)dt.Rows.Add(i.Barcode,i.Name,i.Qty,i.Unit,i.Price,i.Total);
   grid.DataSource=dt;
   if(grid.Columns.Contains("ExcluirItem"))grid.Columns["ExcluirItem"].DisplayIndex=grid.Columns.Count-1;
   SaveDraft();total.Text="TOTAL: R$ "+FinalAmount().ToString("N2")+(DiscountAmount()>0?"  (desconto R$ "+DiscountAmount().ToString("N2")+")":"");UpdateChange();
  }
  void UpdateChange(){
   bool cash=Convert.ToString(payment.SelectedItem)=="DINHEIRO";
   changeLabel.Visible=cash;
   received.Enabled=cash;
   if(!cash)return;
   decimal difference=received.Value-FinalAmount();
   changeLabel.Text=difference>=0?"TROCO: R$ "+difference.ToString("N2"):"FALTA: R$ "+(-difference).ToString("N2");
   changeLabel.ForeColor=difference>=0?Color.FromArgb(22,75,160):Color.FromArgb(175,25,45);
  }

  void UpdateRegisteredPrice(){
   if(finishingSale){MessageBox.Show("Aguarde finalizar a venda.");return;}
   if(cart.Count==0){MessageBox.Show("Adicione produtos ao carrinho.");return;}
   try{
    using(var conn=Open())
    using(var dialog=new StyledDialogForm{Text="Atualizar preços dos produtos do carrinho",Width=920,Height=590,MinimumSize=new Size(580,350),StartPosition=FormStartPosition.CenterParent}){
     var table=new DataTable();
     table.Columns.Add("ID",typeof(long));table.Columns.Add("Código");table.Columns.Add("Produto");
     table.Columns.Add("Quantidade",typeof(decimal));table.Columns.Add("Preço atual",typeof(decimal));table.Columns.Add("Novo preço",typeof(decimal));
     foreach(var item in cart.GroupBy(x=>x.Id).Select(g=>g.First())){
      using(var query=new SQLiteCommand("SELECT price FROM products WHERE id=@id",conn)){
       query.Parameters.AddWithValue("@id",item.Id);
       var value=query.ExecuteScalar();if(value==null||value==DBNull.Value)throw new Exception("Produto não encontrado: "+item.Name);
       table.Rows.Add(item.Id,item.Barcode,item.Name,cart.Where(x=>x.Id==item.Id).Sum(x=>x.Qty),Dec(value),Dec(value));
      }
     }
     // Criar as colunas antes de vincular o DataTable: em WinForms antigo
     // AutoGenerateColumns pode ocorrer só quando o controle ganha BindingContext.
     var list=new DataGridView{Dock=DockStyle.Fill,AutoGenerateColumns=false,
      AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,
      AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,ScrollBars=ScrollBars.Both};
     foreach(DataColumn dataColumn in table.Columns){
      var col=new DataGridViewTextBoxColumn{
       Name=dataColumn.ColumnName,HeaderText=dataColumn.ColumnName,
       DataPropertyName=dataColumn.ColumnName,ReadOnly=dataColumn.ColumnName!="Novo preço"
      };
      if(dataColumn.ColumnName=="ID")col.Visible=false;
      if(dataColumn.ColumnName=="Preço atual"||dataColumn.ColumnName=="Novo preço")col.DefaultCellStyle.Format="N2";
      if(dataColumn.ColumnName=="Novo preço")col.DefaultCellStyle.BackColor=Color.FromArgb(255,250,220);
      list.Columns.Add(col);
     }
     list.DataSource=table;
     var note=new Label{Text="Altere somente a coluna NOVO PREÇO. Os preços serão atualizados no cadastro e na venda atual, sem modificar vendas anteriores.",Dock=DockStyle.Top,Height=52,Padding=new Padding(12),Font=new Font("Segoe UI",10,FontStyle.Bold)};
     var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=60,Padding=new Padding(10)};
     var save=new Button{Text="Salvar preços alterados",Width=220,Height=37,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
     var cancel=new Button{Text="Cancelar",Width=120,Height=37};
     bar.Controls.Add(save);bar.Controls.Add(cancel);cancel.Click+=(sender,args)=>dialog.Close();
     save.Click+=(sender,args)=>{
      try{
       list.EndEdit();
       var changes=new List<Tuple<long,decimal,decimal>>();
       foreach(DataRow row in table.Rows){
        decimal previous=Convert.ToDecimal(row["Preço atual"]),next;
        if(!decimal.TryParse(Convert.ToString(row["Novo preço"]),NumberStyles.Number,CultureInfo.CurrentCulture,out next)&&!decimal.TryParse(Convert.ToString(row["Novo preço"]),NumberStyles.Number,CultureInfo.InvariantCulture,out next))throw new Exception("Preço inválido: "+row["Produto"]);
        if(next<=0||next>99999999)throw new Exception("Preço fora do limite: "+row["Produto"]);
        if(next!=previous)changes.Add(Tuple.Create(Convert.ToInt64(row["ID"]),previous,next));
       }
       if(changes.Count==0){MessageBox.Show("Nenhum preço foi alterado.");return;}
       if(MessageBox.Show("Confirmar alteração de "+changes.Count+" produto(s) no cadastro e no carrinho?","Confirmar preços",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
       string backup=db+".antes-alterar-precos-pdv-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
       using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){dest.Open();conn.BackupDatabase(dest,"main","main",-1,null,0);}
       using(var tx=conn.BeginTransaction()){
        using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS product_price_audit(id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,previous_price NUMERIC NOT NULL,new_price NUMERIC NOT NULL,changed_at TEXT NOT NULL,origin TEXT NOT NULL)",conn,tx))create.ExecuteNonQuery();
        foreach(var change in changes){
         using(var update=new SQLiteCommand("UPDATE products SET price=@new WHERE id=@id AND price=@old",conn,tx)){
          update.Parameters.AddWithValue("@new",change.Item3);update.Parameters.AddWithValue("@id",change.Item1);update.Parameters.AddWithValue("@old",change.Item2);
          if(update.ExecuteNonQuery()!=1)throw new Exception("Preço mudou em outra tela. Nenhum preço desta operação foi gravado.");
         }
         using(var audit=new SQLiteCommand("INSERT INTO product_price_audit(product_id,previous_price,new_price,changed_at,origin) VALUES(@id,@old,@new,@date,'PDV')",conn,tx)){
          audit.Parameters.AddWithValue("@id",change.Item1);audit.Parameters.AddWithValue("@old",change.Item2);audit.Parameters.AddWithValue("@new",change.Item3);
          audit.Parameters.AddWithValue("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));audit.ExecuteNonQuery();
         }
        }
        tx.Commit();
       }
       foreach(var item in cart){
        var change=changes.FirstOrDefault(x=>x.Item1==item.Id);
        if(change!=null)item.Price=change.Item3;
       }
       Render();dialog.DialogResult=DialogResult.OK;dialog.Close();
       MessageBox.Show(changes.Count+" preço(s) atualizados no cadastro e no carrinho. Novo total: R$ "+FinalAmount().ToString("N2"));
      }catch(Exception ex){MessageBox.Show("Preços não atualizados: "+ex.Message,"Atualização de preços",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
     };
     dialog.Controls.Add(list);dialog.Controls.Add(note);dialog.Controls.Add(bar);dialog.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível abrir os preços do carrinho: "+ex.Message);}
  }

  // Editor em grade: mostra todo o cadastro e altera somente as linhas modificadas.
  // O estoque nunca e alterado aqui: entradas e correcoes devem usar o modulo Estoque.
  void EditAllStockInPdv(){
   if(!manualEntry)return;
   try{
    var data=new DataTable();
    data.Columns.Add("ID",typeof(long));data.Columns.Add("Codigo");data.Columns.Add("Descricao");
    data.Columns.Add("Estoque",typeof(decimal));data.Columns.Add("Custo",typeof(decimal));
    data.Columns.Add("Preco",typeof(decimal));data.Columns.Add("Qtd. venda",typeof(decimal));
    using(var cn=Open())
    using(var cmd=new SQLiteCommand("SELECT id,internal_code,name,stock,cost,price FROM products ORDER BY name COLLATE NOCASE,id",cn))
    using(var rd=cmd.ExecuteReader())while(rd.Read()){
     long id=Convert.ToInt64(rd["id"]);
     data.Rows.Add(id,Convert.ToString(rd["internal_code"]),Convert.ToString(rd["name"]),
      Dec(rd["stock"]),Dec(rd["cost"]),Dec(rd["price"]),cart.Where(x=>x.Id==id).Sum(x=>x.Qty));
    }
    data.AcceptChanges();
    using(var dialog=new StyledDialogForm{Text="Lançamento rapido | Editar todos os produtos",Width=1100,Height=700,StartPosition=FormStartPosition.CenterParent}){
     var table=new DataGridView{Dock=DockStyle.Fill,DataSource=data,AllowUserToAddRows=false,
      AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,
      SelectionMode=DataGridViewSelectionMode.CellSelect,BackgroundColor=Color.White};
     foreach(string name in new[]{"ID","Codigo","Descricao","Estoque"})table.Columns[name].ReadOnly=true;
     table.Columns["ID"].Visible=false;
     table.Columns["Descricao"].MinimumWidth=290;
     var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,WrapContents=false};
     var save=new Button{Text="SALVAR ALTERACOES",Width=190,Height=38,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
     var cancel=new Button{Text="CANCELAR",Width=125,Height=38};
     var hint=new Label{Text="Edite custo, preco e quantidade da venda. Estoque: somente leitura. Nada e vendido ate finalizar.",Dock=DockStyle.Top,Height=36};
     save.Click+=(sender,args)=>{
      try{
       table.EndEdit();
       var changed=data.Rows.Cast<DataRow>().Where(x=>x.RowState==DataRowState.Modified).ToList();
       var updates=new List<Tuple<long,decimal,decimal,decimal>>();
       foreach(DataRow row in changed){
        long id=Convert.ToInt64(row["ID"]);
        decimal cost=Convert.ToDecimal(row["Custo"]),price=Convert.ToDecimal(row["Preco"]),amount=Convert.ToDecimal(row["Qtd. venda"]);
        if(cost<0||price<=0||amount<0||amount>999999)throw new Exception("Valor invalido: "+row["Descricao"]);
        updates.Add(Tuple.Create(id,cost,price,amount));
       }
       foreach(var u in updates){
        var row=data.Rows.Cast<DataRow>().First(x=>Convert.ToInt64(x["ID"])==u.Item1);
        if(u.Item4>Convert.ToDecimal(row["Estoque"]))
         throw new Exception("Estoque insuficiente para "+row["Descricao"]+". Atualize pelo modulo Estoque.");
       }
       using(var cn=Open()){
        using(var tx=cn.BeginTransaction()){
         foreach(var u in updates){
          using(var cmd=new SQLiteCommand("UPDATE products SET cost=@cost,price=@price WHERE id=@id",cn,tx)){
           cmd.Parameters.AddWithValue("@cost",u.Item2);cmd.Parameters.AddWithValue("@price",u.Item3);
           cmd.Parameters.AddWithValue("@id",u.Item1);
           if(cmd.ExecuteNonQuery()!=1)throw new Exception("Produto nao encontrado: "+u.Item1);
          }
         }
         tx.Commit();
        }
       }
       foreach(var u in updates){
        var row=data.Rows.Cast<DataRow>().First(x=>Convert.ToInt64(x["ID"])==u.Item1);
        var current=cart.FirstOrDefault(x=>x.Id==u.Item1);
        if(u.Item4==0){cart.RemoveAll(x=>x.Id==u.Item1);continue;}

        if(current!=null){current.Qty=u.Item4;current.Price=u.Item3;current.Cost=u.Item2;}
        else cart.Add(new Item{Id=u.Item1,InternalCode=Convert.ToString(row["Codigo"]),
         Name=Convert.ToString(row["Descricao"]),Qty=u.Item4,Price=u.Item3,Cost=u.Item2});
       }
       Render();LoadStockInPdv();dialog.DialogResult=DialogResult.OK;
      }catch(Exception ex){MessageBox.Show("Alteracoes nao concluidas: "+ex.Message,"Conferencia",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
     };
     cancel.Click+=(sender,args)=>dialog.DialogResult=DialogResult.Cancel;
     bar.Controls.Add(save);bar.Controls.Add(cancel);
     dialog.Controls.Add(table);dialog.Controls.Add(bar);dialog.Controls.Add(hint);
     dialog.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show("Erro ao abrir produtos: "+ex.Message,"Lançamento rapido",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void ChooseEditAction(){
   if(manualEntry&&stockViewOpen){EditAllStockInPdv();return;}
   using(var dialog=new StyledDialogForm{Text="PDV | Editar produto",Width=430,Height=185,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.White}){
    var hint=new Label{Text="O que deseja alterar?",Left=18,Top=17,Width=380,Height=28,Font=new Font("Segoe UI",11,FontStyle.Bold)};
    var item=new Button{Text="Quantidade / preço desta venda",Left=18,Top=62,Width=187,Height=48,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
    var registered=new Button{Text="Preço cadastrado do produto",Left=214,Top=62,Width=187,Height=48,BackColor=Color.FromArgb(31,104,172),ForeColor=Color.White};
    item.Click+=(s,e)=>{dialog.Close();EditSelected();};
    registered.Click+=(s,e)=>{dialog.Close();UpdateRegisteredPrice();};
    dialog.Controls.AddRange(new Control[]{hint,item,registered});dialog.ShowDialog(this);
   }
  }
  void EditSelected(){
   if(grid.CurrentRow==null||grid.CurrentRow.Index<0||grid.CurrentRow.Index>=cart.Count){MessageBox.Show("Selecione um produto do carrinho.");return;}
   var item=cart[grid.CurrentRow.Index];
   try{
    using(var dialog=new StyledDialogForm {Text="Editar item da venda",Width=450,Height=265,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
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
     decimal required=quantityInput.Value+cart.Where(x=>x.Id==item.Id && !object.ReferenceEquals(x,item)).Sum(x=>x.Qty);
     if(required>available){
      if(!AdjustEmptyStock(item,available,required))return;
      using(var refreshed=Open())
      using(var check=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",refreshed)){
       check.Parameters.AddWithValue("@id",item.Id);
       var refreshedValue=check.ExecuteScalar();
       if(refreshedValue==null||refreshedValue==DBNull.Value)throw new Exception("Produto não encontrado após conferência.");
       available=Dec(refreshedValue);
      }
      if(required>available){MessageBox.Show("Estoque ainda insuficiente. Disponível: "+available.ToString("N3"));return;}
     }
     item.Qty=quantityInput.Value;item.Price=priceInput.Value;
     Render();
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Não foi possível editar o item");}
  }
  void ClearCart(){
   if(cart.Count==0){MessageBox.Show("O carrinho já está vazio.");return;}
   if(MessageBox.Show("Remover todos os itens desta venda em andamento? Nenhuma venda registrada será excluída.","Confirmar limpeza",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
   cart.Clear();discountMode.SelectedIndex=0;discountInput.Value=0;discountValue=0;discountPercent=false;code.Clear();qty.Value=1;received.Value=0;Render();code.Focus();
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
   // O saldo parcial depende desta tabela; não iniciar a gravação se ela não existir.
   if(method=="FIADO")RequireColumns(conn,"credit_payments","sale_id","amount","payment_method","paid_at","note");
  }
  List<KeyValuePair<string,decimal>> SelectSplitPayments(decimal amount){
   var methods=PaymentFees.Methods.Concat(new[]{"FIADO"}).ToArray();
   using(var dialog=new StyledDialogForm {Text="Pagamento dividido (até 3 formas)",Width=485,Height=340,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
    var combos=new ComboBox[3];var values=new NumericUpDown[3];
    var info=new Label {Left=18,Top=15,Width=440,Height=28,Text="Total da venda: R$ "+amount.ToString("N2"),Font=new Font("Segoe UI",11,FontStyle.Bold)};
    dialog.Controls.Add(info);
    for(int i=0;i<3;i++){
     var label=new Label {Left=18,Top=58+i*53,Width=55,Text=(i+1)+"ª forma"};
     combos[i]=new ComboBox {Left=85,Top=53+i*53,Width=165,DropDownStyle=ComboBoxStyle.DropDownList};
     combos[i].Items.AddRange(methods);combos[i].SelectedIndex=i==0?0:-1;
     values[i]=new NumericUpDown {Left=265,Top=53+i*53,Width=175,DecimalPlaces=2,Maximum=99999999};
     // Começa em zero para inserir cada pagamento sem precisar apagar o total.
     dialog.Controls.AddRange(new Control[]{label,combos[i],values[i]});
    }
    var status=new Label {Left=18,Top=218,Width=430,Height=25};
    Action update=()=>{decimal paid=values.Sum(v=>v.Value);status.Text="Somado: R$ "+paid.ToString("N2")+"  |  Restante: R$ "+(amount-paid).ToString("N2");status.ForeColor=paid>amount?Color.DarkRed:Color.FromArgb(22,75,160);};
    foreach(var v in values)v.ValueChanged+=(s,e)=>update();foreach(var combo in combos)combo.SelectedIndexChanged+=(s,e)=>update();update();
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

  // Gera PDF local simples, sem serviços externos e sem identificar o recibo como NFC-e.
  static string ReceiptAscii(string value){
   string normalized=(value??"").Normalize(NormalizationForm.FormD);
   var result=new StringBuilder();
   foreach(char ch in normalized){
    if(CharUnicodeInfo.GetUnicodeCategory(ch)==UnicodeCategory.NonSpacingMark)continue;
    result.Append(ch>=32&&ch<=126?ch:' ');
   }
   return result.ToString();
  }
  static string PdfHex(string value){
   byte[] data=Encoding.ASCII.GetBytes(ReceiptAscii(value));
   return "<"+BitConverter.ToString(data).Replace("-","")+">";
  }
  string CreateSaleReceiptPdf(long saleId){
   var lines=new List<string>();
   lines.Add("ARMAZEM GRATIDAO");
   lines.Add("COMPROVANTE DE VENDA - NAO FISCAL");
   lines.Add("Este documento nao substitui nota fiscal ou NFC-e.");
   lines.Add(new string('-',70));
   using(var conn=Open()){
    using(var cmd=new SQLiteCommand("SELECT id,created_at,customer_name,payment_method,gross_total FROM sales WHERE id=@id",conn)){
     cmd.Parameters.AddWithValue("@id",saleId);
     using(var rd=cmd.ExecuteReader()){
      if(!rd.Read())throw new Exception("Venda nao encontrada para gerar comprovante.");
      lines.Add("Venda: "+Convert.ToString(rd["id"])+"   Data: "+Convert.ToString(rd["created_at"]));
      lines.Add("Cliente: "+Convert.ToString(rd["customer_name"]));
      lines.Add("Pagamento: "+Convert.ToString(rd["payment_method"]));
      lines.Add("TOTAL: R$ "+Dec(rd["gross_total"]).ToString("N2"));
     }
    }
    lines.Add(new string('-',70));
    lines.Add("PRODUTO                                 QTD      UNIT.       TOTAL");
    using(var cmd=new SQLiteCommand("SELECT name,quantity,unit_price,total FROM sale_items WHERE sale_id=@id ORDER BY id",conn)){
     cmd.Parameters.AddWithValue("@id",saleId);
     using(var rd=cmd.ExecuteReader())while(rd.Read()){
      string name=ReceiptAscii(Convert.ToString(rd["name"]));
      if(name.Length>39)name=name.Substring(0,39);
      lines.Add(name.PadRight(40)+Dec(rd["quantity"]).ToString("0.###").PadLeft(7)+" "+Dec(rd["unit_price"]).ToString("N2").PadLeft(10)+" "+Dec(rd["total"]).ToString("N2").PadLeft(10));
     }
    }
   }
   lines.Add(new string('-',70));
   lines.Add("Obrigado pela preferencia!");
   lines.Add("Documento de controle interno - nao fiscal.");
   return WriteComprovantePdf(lines,"Venda-"+saleId+".pdf");
  }
  string WriteComprovantePdf(List<string> lines,string filename){
   // PDF escrito em objetos padrao; cada pagina suporta 55 linhas.
   var objects=new List<string>();
   objects.Add(""); // catalogo
   objects.Add(""); // paginas
   objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
   var pageIds=new List<int>();
   for(int offset=0;offset<lines.Count;offset+=55){
    var pageLines=lines.Skip(offset).Take(55).ToList();
    var content=new StringBuilder();
    // Ícone de cestinho desenhado no PDF (somente ilustrativo; exclusões são feitas no PDV).
    if(offset==0&&filename.StartsWith("Venda-",StringComparison.OrdinalIgnoreCase)){
     content.Append("q 0.12 0.30 0.62 RG 1.6 w ");
     content.Append("510 791 m 548 791 l 544 773 l 514 773 l h S ");
     content.Append("518 797 m 525 806 l 533 806 l 540 797 l S ");
     content.Append("521 785 m 521 777 l 531 785 m 531 777 l 541 785 m 541 777 l S Q\n");
    }
    int y=810;
    foreach(string line in pageLines){
     content.Append("BT /F1 9 Tf 36 ").Append(y).Append(" Td ").Append(PdfHex(line)).Append(" Tj ET\n");
     y-=14;
    }
    int streamLen=Encoding.ASCII.GetByteCount(content.ToString());
    int streamId=objects.Count+1;
    objects.Add("<< /Length "+streamLen+" >>\nstream\n"+content+"endstream");
    int pageId=objects.Count+1;
    objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents "+streamId+" 0 R >>");
    pageIds.Add(pageId);
   }
   objects[0]="<< /Type /Catalog /Pages 2 0 R >>";
   objects[1]="<< /Type /Pages /Count "+pageIds.Count+" /Kids [ "+string.Join(" ",pageIds.Select(id=>id+" 0 R"))+" ] >>";
   var pdf=new StringBuilder("%PDF-1.4\n");
   var positions=new List<int>{0};
   foreach(string obj in objects){
    positions.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
    int id=positions.Count-1;
    pdf.Append(id).Append(" 0 obj\n").Append(obj).Append("\nendobj\n");
   }
   int xref=Encoding.ASCII.GetByteCount(pdf.ToString());
   pdf.Append("xref\n0 ").Append(objects.Count+1).Append("\n0000000000 65535 f \n");
   for(int i=1;i<positions.Count;i++)pdf.Append(positions[i].ToString("D10")).Append(" 00000 n \n");
   pdf.Append("trailer\n<< /Size ").Append(objects.Count+1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF");
   string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Armazem Gratidao","Comprovantes");
   Directory.CreateDirectory(folder);
   string destination=Path.Combine(folder,filename);
   File.WriteAllBytes(destination,Encoding.ASCII.GetBytes(pdf.ToString()));
   return destination;
  }
  string CreateOpenBalancePdf(string clientName){
   var lines=new List<string>{"ARMAZEM GRATIDAO","EXTRATO DE VALORES EM ABERTO - NAO FISCAL","Cliente: "+clientName,"Emitido em: "+DateTime.Now.ToString("dd/MM/yyyy HH:mm"),new string('-',65)};
   decimal sum=0;int count=0;
   using(var conn=Open())using(var cmd=new SQLiteCommand("SELECT s.id,s.created_at,s.gross_total,COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0) AS paid FROM sales s WHERE LOWER(TRIM(COALESCE(s.customer_name,'')))=LOWER(TRIM(@client)) AND (UPPER(COALESCE(s.payment_method,'')) LIKE '%FIADO%' OR UPPER(COALESCE(s.status,'')) LIKE '%FIADO%') ORDER BY s.created_at,s.id",conn)){
    cmd.Parameters.AddWithValue("@client",clientName);
    using(var rd=cmd.ExecuteReader())while(rd.Read()){
     decimal due=Dec(rd["gross_total"])-Dec(rd["paid"]);
     if(due<=0)continue;
     count++;sum+=due;
     lines.Add("Venda #"+rd["id"]+"   "+rd["created_at"]+"   Em aberto: R$ "+due.ToString("N2"));
    }
   }
   if(count==0)lines.Add("Nenhuma venda fiado em aberto encontrada.");
   lines.Add(new string('-',65));lines.Add("TOTAL EM ABERTO: R$ "+sum.ToString("N2"));
   lines.Add("Extrato informativo. Nao substitui nota fiscal.");
   return WriteComprovantePdf(lines,"Fiado-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".pdf");
  }
  string CreateReceiptText(long saleId){
   var lines=new List<string>{"ARMAZEM GRATIDAO","CUPOM DE VENDA - NAO FISCAL",new string('-',38)};
   using(var conn=Open()){
    using(var cmd=new SQLiteCommand("SELECT created_at,customer_name,payment_method,gross_total FROM sales WHERE id=@id",conn)){
     cmd.Parameters.AddWithValue("@id",saleId);
     using(var rd=cmd.ExecuteReader()){
      if(!rd.Read())throw new Exception("Venda não encontrada.");
      lines.Add("Venda #"+saleId+"   "+rd["created_at"]);
      lines.Add("Cliente: "+rd["customer_name"]);lines.Add("Pagamento: "+rd["payment_method"]);
      lines.Add("TOTAL R$ "+Dec(rd["gross_total"]).ToString("N2"));
     }
    }
    using(var cmd=new SQLiteCommand("SELECT name,quantity,unit_price,total FROM sale_items WHERE sale_id=@id ORDER BY id",conn)){
     cmd.Parameters.AddWithValue("@id",saleId);
     using(var rd=cmd.ExecuteReader())while(rd.Read()){
      lines.Add(Convert.ToString(rd["name"]));
      lines.Add(Dec(rd["quantity"]).ToString("0.###")+" x R$ "+Dec(rd["unit_price"]).ToString("N2")+" = R$ "+Dec(rd["total"]).ToString("N2"));
     }
    }
   }
   lines.Add(new string('-',38));lines.Add("Comprovante nao fiscal.");
   string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Armazem Gratidao","Comprovantes");
   Directory.CreateDirectory(folder);
   string file=Path.Combine(folder,"Cupom-Venda-"+saleId+".txt");
   File.WriteAllLines(file,lines.ToArray(),Encoding.UTF8);return file;
  }
  void OfferSaleReceipt(long saleId,string clientName,decimal saleTotal){
   try{
    using(var dialog=new StyledDialogForm{Text="Comprovantes e WhatsApp - Venda #"+saleId,Width=660,Height=330,StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
     var title=new Label{Text="Escolha o documento para o cliente:",Left=16,Top=18,Width=610,Height=30,Font=new Font("Segoe UI",12,FontStyle.Bold)};
     var option=new ComboBox{Left=16,Top=58,Width=600,DropDownStyle=ComboBoxStyle.DropDownList};
     option.Items.AddRange(new object[]{"Cupom de venda (TXT - não fiscal)","Comprovante da venda (PDF - não fiscal)","Total em aberto do cliente (PDF - não fiscal)"});
     option.SelectedIndex=0;
     var send=new Button{Text="Preparar envio WhatsApp",Left=16,Top=120,Width=230,Height=43};
     var save=new Button{Text="Gerar e abrir arquivo",Left=258,Top=120,Width=210,Height=43};
     var close=new Button{Text="Fechar",Left=480,Top=120,Width=135,Height=43};
     var note=new Label{Text="O arquivo fica salvo em Documentos / Armazem Gratidao / Comprovantes.\nNo WhatsApp comum, o anexo deve ser selecionado manualmente.\nCupom não fiscal não substitui NFC-e autorizada.",Left=16,Top=182,Width=610,Height=80};
     Func<string> generate=()=>{
      if(option.SelectedIndex==0)return CreateReceiptText(saleId);
      if(option.SelectedIndex==1)return CreateSaleReceiptPdf(saleId);
      if(string.IsNullOrWhiteSpace(clientName)||string.Equals(clientName,"Consumidor",StringComparison.OrdinalIgnoreCase))throw new Exception("Selecione um cliente identificado para consultar o saldo em aberto.");
      return CreateOpenBalancePdf(clientName);
     };
     save.Click+=(sender,args)=>{try{string file=generate();Process.Start(new ProcessStartInfo(file){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,"Comprovante");}};
     send.Click+=(sender,args)=>{
      try{
       string file=generate();
       string label=option.SelectedIndex==2?"extrato de valores em aberto":"comprovante da venda #"+saleId;
       string url="https://wa.me/?text="+Uri.EscapeDataString("Armazém Gratidão: segue "+label+".");
       Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
       MessageBox.Show("Documento salvo:\n"+file+"\n\nSelecione o contato e anexe este arquivo na conversa do WhatsApp. O envio não é automático.","WhatsApp");
      }catch(Exception ex){MessageBox.Show(ex.Message,"WhatsApp");}
     };
     close.Click+=(sender,args)=>dialog.Close();
     dialog.Controls.AddRange(new Control[]{title,option,send,save,close,note});dialog.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show("Venda salva, mas erro no comprovante: "+ex.Message,"Comprovante");}
  }
  // Alerta de possível repetição; não bloqueia vendas legítimas do mesmo dia.
  bool ConfirmManualSaleDate(){
   if(!manualEntry)return true;
   using(var dialog=new StyledDialogForm{Text="Conferir data da venda anotada",Width=500,Height=250,MinimumSize=new Size(460,220),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Color.White}){
    var title=new Label{Text="Produtos lançados. Confira a data e hora ORIGINAL desta venda:",Left=18,Top=17,Width=450,Height=42,Font=new Font("Segoe UI",10,FontStyle.Bold)};
    var picker=new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="dd/MM/yyyy HH:mm:ss",ShowUpDown=true,Left=18,Top=70,Width=255,Value=manualDate.Value};
    var ok=new Button{Text="Confirmar data e continuar",Left=18,Top=128,Width=240,Height=38,BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White,DialogResult=DialogResult.OK};
    var cancel=new Button{Text="Voltar aos produtos",Left=272,Top=128,Width=175,Height=38,DialogResult=DialogResult.Cancel};
    dialog.Controls.AddRange(new Control[]{title,picker,ok,cancel});dialog.AcceptButton=ok;dialog.CancelButton=cancel;
    if(dialog.ShowDialog(this)!=DialogResult.OK)return false;
    if(picker.Value>DateTime.Now.AddMinutes(2)){MessageBox.Show("A data da venda anotada não pode estar no futuro.");return false;}
    manualDate.Value=picker.Value;return true;
   }
  }
  bool ShowPaymentChoice(){
   using(var dialog=new StyledDialogForm{Text="Escolha a forma de pagamento",Width=690,Height=530,
    MinimumSize=new Size(570,450),StartPosition=FormStartPosition.CenterParent,
    BackColor=Color.FromArgb(238,245,252)}){
    var title=new Label{Text="TOTAL DA VENDA: R$ "+FinalAmount().ToString("N2"),Dock=DockStyle.Top,
     Height=72,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",19,FontStyle.Bold),ForeColor=Color.FromArgb(22,75,160)};
    var info=new Label{Text="Selecione a forma de pagamento. Para dividir em até 3 formas, escolha DIVIDIDO.",Dock=DockStyle.Top,Height=50,
     TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",10,FontStyle.Bold)};
    var choices=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=true,Padding=new Padding(18)};
    foreach(string name in payment.Items.Cast<object>().Select(x=>Convert.ToString(x))){
     string selected=name;
     var btn=new Button{Text=selected,Width=185,Height=58,Margin=new Padding(7),
      Font=new Font("Segoe UI",11,FontStyle.Bold),BackColor=selected=="FIADO"?Color.FromArgb(175,35,48):Color.FromArgb(22,75,160),
      ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
     btn.FlatAppearance.BorderSize=0;
     btn.Click+=(sender,args)=>{payment.SelectedItem=selected;dialog.DialogResult=DialogResult.OK;dialog.Close();};
     choices.Controls.Add(btn);
    }
    var cancel=new Button{Text="Voltar à venda",Dock=DockStyle.Bottom,Height=44,DialogResult=DialogResult.Cancel};
    dialog.Controls.Add(choices);dialog.Controls.Add(info);dialog.Controls.Add(title);dialog.Controls.Add(cancel);
    return dialog.ShowDialog(this)==DialogResult.OK;
   }
  }
  // Controle de casco APENAS para Coca retornavel de 1 litro vidro ou de 2 litros.
  internal static bool IsCocaReturnableBottle(string description){
   if(string.IsNullOrWhiteSpace(description))return false;
   string normalized=description.Normalize(NormalizationForm.FormD);
   var clean=new StringBuilder(normalized.Length);
   foreach(char character in normalized)
    if(CharUnicodeInfo.GetUnicodeCategory(character)!=UnicodeCategory.NonSpacingMark)
     clean.Append(char.ToLowerInvariant(character));
   string name=System.Text.RegularExpressions.Regex.Replace(clean.ToString(),@"[^a-z0-9]+"," ").Trim();
   if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"(?:^| )coca(?: |$)"))return false;
   bool liter1=System.Text.RegularExpressions.Regex.IsMatch(name,@"(?:^| )(?:1\s*(?:l|litro|litros)|1000\s*ml)(?: |$)");
   bool liter2=System.Text.RegularExpressions.Regex.IsMatch(name,@"(?:^| )(?:2\s*(?:l|litro|litros)|2000\s*ml)(?: |$)");
   if(!liter1&&!liter2)return false;
   if(System.Text.RegularExpressions.Regex.IsMatch(name,@"\b(?:nao|sem)\s+retorn\w*\b"))return false;
   bool returnable=System.Text.RegularExpressions.Regex.IsMatch(name,@"\b(?:retorn\w*|ret|casco)\b");
   bool glass1=liter1&&System.Text.RegularExpressions.Regex.IsMatch(name,@"\bvidro\b");
   return returnable||glass1;
  }
  bool SelectBottleLoan(out int count,out bool paid){
   count=0;paid=false;
   var eligible=cart.Where(i=>IsCocaReturnableBottle(i.Name)).ToList();
   if(eligible.Count==0)return true;
   using(var dialog=new StyledDialogForm{Text="Cascos retornáveis — controle separado",Width=520,Height=285,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,BackColor=Color.FromArgb(229,242,255)}){
    var title=new Label{Text="O cliente levou casco retornável junto com a bebida?",Left=18,Top=16,Width=475,Height=35,Font=new Font("Segoe UI",11,FontStyle.Bold)};
    var qtyLabel=new Label{Text="Quantidade de cascos:",Left=18,Top=64,Width=195,Height=28};
    decimal suggested=Math.Max(1m,Math.Min(999m,Math.Ceiling(eligible.Sum(i=>i.Qty))));
    var qty=new NumericUpDown{Minimum=1,Maximum=999,Value=suggested,Left=215,Top=61,Width=90};
    var deposit=new CheckBox{Text="Recebeu a caução de R$ 4 por casco",Left=18,Top=108,Width=445,Height=30,Checked=true};
    var note=new Label{Text="A caução não entra no valor da venda. O nome e a retirada entram no controle de cascos.",Left=18,Top=145,Width=475,Height=36};
    var yes=new Button{Text="Registrar casco",Left=18,Top=195,Width=150,Height=35,DialogResult=DialogResult.OK};
    var no=new Button{Text="Sem casco",Left=178,Top=195,Width=130,Height=35,DialogResult=DialogResult.Ignore};
    var back=new Button{Text="Voltar",Left=318,Top=195,Width=100,Height=35,DialogResult=DialogResult.Cancel};
    dialog.Controls.AddRange(new Control[]{title,qtyLabel,qty,deposit,note,yes,no,back});
    var result=dialog.ShowDialog(this);
    if(result==DialogResult.Cancel)return false;
    if(result==DialogResult.OK){count=(int)qty.Value;paid=deposit.Checked;}
    return true;
   }
  }
  bool ConfirmCashReceived(decimal totalDue){
   using(var dialog=new StyledDialogForm{
    Text=manualEntry?"Lançamento rápido | Dinheiro recebido":"PDV | Pagamento em dinheiro",Width=530,Height=355,
    MinimumSize=new Size(490,325),StartPosition=FormStartPosition.CenterParent,
    FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,
    BackColor=Color.FromArgb(229,242,255),KeyPreview=true
   }){
    var totalLabel=new Label{Text=(manualEntry?"VENDA ANOTADA | TOTAL: R$ ":"TOTAL DA VENDA: R$ ")+totalDue.ToString("N2"),Left=20,Top=16,Width=475,Height=52,
     Font=new Font("Segoe UI",17,FontStyle.Bold),ForeColor=Color.FromArgb(22,75,160)};
    var inputLabel=new Label{Text="Valor recebido em dinheiro (R$):",Left=20,Top=86,Width=460,Height=26,
     Font=new Font("Segoe UI",10,FontStyle.Bold)};
    var cash=new NumericUpDown{Left=20,Top=116,Width=462,Height=42,DecimalPlaces=2,Increment=1m,
     Minimum=0,Maximum=99999999m,Font=new Font("Segoe UI",18,FontStyle.Bold),
     Value=Math.Min(99999999m,Math.Max(totalDue,received.Value))};
    var change=new Label{Left=20,Top=167,Width=465,Height=44,Font=new Font("Segoe UI",14,FontStyle.Bold)};
    Action calculate=()=>{
     decimal remaining=cash.Value-totalDue;
     change.Text=remaining>=0?"TROCO: R$ "+remaining.ToString("N2"):"FALTA: R$ "+(-remaining).ToString("N2");
     change.ForeColor=remaining>=0?Color.FromArgb(22,75,160):Color.FromArgb(175,25,45);
    };
    cash.ValueChanged+=(sender,args)=>calculate();calculate();
    var confirm=new Button{Text="Confirmar dinheiro",Left=20,Top=239,Width=230,Height=42,
     BackColor=Color.FromArgb(22,75,160),ForeColor=Color.White};
    var cancel=new Button{Text="Voltar à venda",Left=265,Top=239,Width=216,Height=42,DialogResult=DialogResult.Cancel};
    confirm.Click+=(sender,args)=>{
     if(cash.Value<totalDue){MessageBox.Show("Valor recebido menor que o total. Confira o dinheiro antes de finalizar.","Dinheiro insuficiente",MessageBoxButtons.OK,MessageBoxIcon.Warning);cash.Focus();return;}
     dialog.DialogResult=DialogResult.OK;dialog.Close();
    };
    dialog.Controls.AddRange(new Control[]{totalLabel,inputLabel,cash,change,confirm,cancel});
    dialog.AcceptButton=confirm;dialog.CancelButton=cancel;
    dialog.Shown+=(sender,args)=>{cash.Focus();cash.Select(0,cash.Text.Length);};
    if(dialog.ShowDialog(this)!=DialogResult.OK)return false;
    // Somente salvar o valor informado após confirmação da janela.
    received.Value=cash.Value;
    UpdateChange();
    return true;
   }
  }
  void Finish(){
   if(finishingSale)return;
   if(cart.Count==0){MessageBox.Show("Carrinho vazio.");return;}
   if(DiscountAmount()>=cart.Sum(i=>i.Total)){MessageBox.Show("O desconto deve ser menor que o total da venda.");return;}
   if(!ShowPaymentChoice())return;
   if(!ConfirmManualSaleDate())return;
   foreach(var item in cart){
    if(item.Id<=0||item.Qty<=0||item.Price<0||item.Cost<0||item.Total<=0){
     MessageBox.Show("Item inválido no carrinho: "+item.Name+". Confira quantidade, preço e cadastro antes de finalizar.","Conferência dos itens",MessageBoxButtons.OK,MessageBoxIcon.Warning);
     return;
    }
   }
   // Nova conferência: a quantidade real também pode ser corrigida antes do pagamento.
   // Somar todas as linhas do mesmo produto e nunca permitir estoque negativo.
   try{
    var requirements=cart.GroupBy(x=>x.Id).Select(g=>new {Product=g.First(),Required=g.Sum(x=>x.Qty)}).ToList();
    foreach(var requirement in requirements){
     decimal available;
     using(var connection=Open())
     using(var check=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",connection)){
      check.Parameters.AddWithValue("@id",requirement.Product.Id);
      var value=check.ExecuteScalar();
      if(value==null || value==DBNull.Value)throw new Exception("Produto não encontrado: "+requirement.Product.Name);
      available=Dec(value);
     }
     if(available<requirement.Required){
      if(!AdjustEmptyStock(requirement.Product,available,requirement.Required))return;
      using(var connection=Open())
      using(var check=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",connection)){
       check.Parameters.AddWithValue("@id",requirement.Product.Id);
       available=Dec(check.ExecuteScalar());
      }
      if(available<requirement.Required){MessageBox.Show("Estoque ainda insuficiente para "+requirement.Product.Name);return;}
     }
    }
   }catch(Exception ex){MessageBox.Show("Conferência de estoque não concluída: "+ex.Message,"PDV",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
   var method=Convert.ToString(payment.SelectedItem);
   string customerName=string.IsNullOrWhiteSpace(customer.Text)?"Consumidor":customer.Text.Trim();
   int bottleCount;bool bottlePaid;
   if(!SelectBottleLoan(out bottleCount,out bottlePaid))return;
   if(bottleCount>0 && string.Equals(customerName,"Consumidor",StringComparison.OrdinalIgnoreCase)){
    ChooseCustomerInWindow();customerName=customer.Text.Trim();
    if(customerName.Length<2 || string.Equals(customerName,"Consumidor",StringComparison.OrdinalIgnoreCase)){MessageBox.Show("Selecione um cliente para controlar os cascos.");return;}
   }
   var gross=cart.Sum(i=>i.Total);
   var discount=DiscountAmount();
   var sum=gross-discount;
   if(sum<=0){MessageBox.Show("A venda precisa ter total maior que zero. Confira os preços dos itens.","Valor inválido",MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
   List<KeyValuePair<string,decimal>> split=null;
   if(method=="DIVIDIDO"){split=SelectSplitPayments(sum);if(split==null)return;}
   if(method=="DINHEIRO"&&!ConfirmCashReceived(sum))return;
   if(method=="DINHEIRO"&&received.Value<sum){MessageBox.Show("Valor recebido menor que o total.");return;}
   // A selecao do cliente e a ultima etapa do pagamento, inclusive no dividido com Fiado.
   bool requiresFiadoCustomer=method=="FIADO"||(method=="DIVIDIDO"&&split.Any(p=>p.Key=="FIADO"));
   if(requiresFiadoCustomer){
    if(!ChooseCustomerInWindow(true))return;
    customerName=customer.Text.Trim();
    if(!CustomerRegistered(customerName)){
     MessageBox.Show("Selecione ou cadastre um cliente valido para o Fiado.","Cliente obrigatorio");return;
    }
   }
   string details=method=="DIVIDIDO"?string.Join(Environment.NewLine,split.Select(p=>p.Key+": R$ "+p.Value.ToString("N2"))):"Pagamento: "+method;
   if(method=="DINHEIRO")details+=Environment.NewLine+"Recebido: R$ "+received.Value.ToString("N2")+Environment.NewLine+"Troco: R$ "+(received.Value-sum).ToString("N2");
   // A taxa é custo do estabelecimento e nunca aumenta o valor pago pelo cliente.
   Dictionary<string,decimal> feeRates;
   try{using(var read=Open())feeRates=PaymentFees.Load(read);}
   catch(Exception ex){MessageBox.Show("Não foi possível consultar as taxas: "+ex.Message,"Venda não iniciada",MessageBoxButtons.OK,MessageBoxIcon.Error);return;}
   bool splitFiado=method=="DIVIDIDO"&&split.Any(p=>p.Key=="FIADO");
   string recordedMethod=splitFiado?"FIADO":method;
   var paymentParts=method=="FIADO"?new List<KeyValuePair<string,decimal>>():method=="DIVIDIDO"?split.Where(p=>p.Key!="FIADO").ToList():new List<KeyValuePair<string,decimal>>{new KeyValuePair<string,decimal>(method,sum)};
   decimal estimatedFee=paymentParts.Sum(p=>PaymentFees.Fee(feeRates,p.Key,p.Value));
   details+=Environment.NewLine+"Cliente: "+customerName;
   if(bottleCount>0)details+=Environment.NewLine+"Cascos: "+bottleCount+" | Caução "+(bottlePaid?"recebida":"pendente")+" (fora da venda)";
   if(estimatedFee>0)details+=Environment.NewLine+"Taxas da operadora: R$ "+estimatedFee.ToString("N2")+" (custo da loja)";

   if(discount>0)details+=Environment.NewLine+"Subtotal: R$ "+gross.ToString("N2")+" | Desconto: R$ "+discount.ToString("N2");
   if(manualEntry && manualDate.Value>DateTime.Now.AddMinutes(2)){MessageBox.Show("A data da venda anotada não pode estar no futuro.");return;}
   if(MessageBox.Show("Total: R$ "+sum.ToString("N2")+Environment.NewLine+details+Environment.NewLine+Environment.NewLine+"Confirmar finalização?","Conferir pagamento",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   finishingSale=true;
   try {
    if(bottleCount>0)CascosForm.EnsureSaleLink(db);
    // Prepara a carteira antes da transacao; bloqueia a venda se o backup inicial falhar.
    if(method=="FIADO" || splitFiado){FiadoClientesForm.EnsureCustomerCredit(db);FiadoClientesForm.EnsureCreditLimits(db);}
    using(var c=Open()){
     ValidateSaleSchema(c,recordedMethod);
     if(splitFiado)RequireColumns(c,"sale_payments","sale_id","payment_method","amount");
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
     if(method=="FIADO" || splitFiado){
      decimal plannedFiado=method=="FIADO"?sum:split.Where(part=>part.Key=="FIADO").Sum(part=>part.Value);
      // Lancamentos historicos nao consomem o limite do periodo atual.
      if(!manualEntry)FiadoClientesForm.EnforceCreditLimit(c,tx,customerName,plannedFiado);
     }
     long saleId;
     using(var cmd=new SQLiteCommand("INSERT INTO sales(customer_name,payment_method,gross_total,total_cost,net_profit,profit_reserve_10,created_at) VALUES(@customer,@method,@total,@cost,@profit,@reserve,@saleDate); SELECT last_insert_rowid();",c,tx)){
      var cost=cart.Sum(i=>i.Cost*i.Qty);var profit=sum-cost;
      cmd.Parameters.AddWithValue("@saleDate",manualEntry?manualDate.Value.ToString("yyyy-MM-dd HH:mm:ss"):DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));cmd.Parameters.AddWithValue("@customer",customerName);cmd.Parameters.AddWithValue("@method",recordedMethod);cmd.Parameters.AddWithValue("@total",sum);cmd.Parameters.AddWithValue("@cost",cost);cmd.Parameters.AddWithValue("@profit",profit);cmd.Parameters.AddWithValue("@reserve",Math.Round(profit*0.10M,2));cmd.Parameters.AddWithValue("@received",method=="DINHEIRO"?received.Value:sum);cmd.Parameters.AddWithValue("@change",method=="DINHEIRO"?received.Value-sum:0);cmd.Parameters.AddWithValue("@status",method=="FIADO"?"FIADO":"CONCLUIDA");saleId=Convert.ToInt64(cmd.ExecuteScalar());
     }
     if(bottleCount>0)CascosForm.RecordFromSale(c,tx,saleId,customerName,bottleCount,bottlePaid,manualEntry?manualDate.Value.ToString("yyyy-MM-dd HH:mm:ss"):DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
     if(method=="FIADO" || splitFiado){
      decimal fiadoValue=method=="FIADO"?sum:split.Where(part=>part.Key=="FIADO").Sum(part=>part.Value);
      FiadoClientesForm.ApplyCustomerCredit(c,tx,saleId,customerName,fiadoValue);
     }
     if(discount>0){
      using(var create=new SQLiteCommand("CREATE TABLE IF NOT EXISTS sale_discounts(sale_id INTEGER PRIMARY KEY,gross_before NUMERIC NOT NULL,discount_amount NUMERIC NOT NULL,discount_type TEXT NOT NULL,discount_value NUMERIC NOT NULL)",c,tx))create.ExecuteNonQuery();
      using(var ins=new SQLiteCommand("INSERT INTO sale_discounts(sale_id,gross_before,discount_amount,discount_type,discount_value) VALUES(@sale,@gross,@amount,@type,@value)",c,tx)){
       ins.Parameters.AddWithValue("@sale",saleId);ins.Parameters.AddWithValue("@gross",gross);ins.Parameters.AddWithValue("@amount",discount);ins.Parameters.AddWithValue("@type",discountPercent?"PERCENT":"AMOUNT");ins.Parameters.AddWithValue("@value",discountValue);ins.ExecuteNonQuery();
      }
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
     if(paymentParts.Count>0){
      var payments=paymentParts;
      foreach(var part in payments)using(var q=new SQLiteCommand("INSERT INTO sale_payments(sale_id,payment_method,amount) VALUES(@sale,@method,@total)",c,tx)){
       q.Parameters.AddWithValue("@sale",saleId);q.Parameters.AddWithValue("@method",part.Key);q.Parameters.AddWithValue("@total",part.Value);q.ExecuteNonQuery();
      }
     }
     // Pagamentos recebidos na venda parcialmente fiada reduzem somente o saldo pendente.
     if(splitFiado){
      foreach(var part in paymentParts)using(var q=new SQLiteCommand("INSERT INTO credit_payments(sale_id,amount,payment_method,paid_at,note) VALUES(@sale,@amount,@method,datetime('now','localtime'),@note)",c,tx)){
       q.Parameters.AddWithValue("@sale",saleId);q.Parameters.AddWithValue("@amount",part.Value);
       q.Parameters.AddWithValue("@method",part.Key);q.Parameters.AddWithValue("@note","Pagamento inicial de venda dividida com Fiado");
       if(q.ExecuteNonQuery()!=1)throw new Exception("Falha ao registrar pagamento inicial do Fiado.");
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
     // Notifica todas as janelas de Vendas abertas, inclusive lançamentos retroativos.
     foreach(Form window in Application.OpenForms.Cast<Form>().ToArray()){
      var salesWindow=window as ConsultaForm;
      if(salesWindow!=null)try{salesWindow.RefreshAfterRecordedSale();}catch(Exception ex){System.Diagnostics.Debug.WriteLine("Atualização de Vendas: "+ex.Message);}
     }
     cart.Clear();discountMode.SelectedIndex=0;discountInput.Value=0;discountValue=0;discountPercent=false;received.Value=0;customer.Text="Consumidor";Render();code.Focus();
     OfferSaleReceipt(saleId,customerName,sum);
    }
    }
   }catch(Exception ex){MessageBox.Show(ex.Message,"Venda nao finalizada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   finally{finishingSale=false;}
  }
 }
}
