using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
namespace ArmazemGratidao {
 public sealed class ProdutosForm:Form {
  readonly string db;
  readonly DataGridView grid=new DataGridView {Dock=DockStyle.Fill,ReadOnly=false,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None};
  readonly TextBox search=new TextBox {Width=240};
  bool showInactive=false;
  readonly HashSet<long> checkedProducts=new HashSet<long>();
  readonly CheckBox selectAll=new CheckBox {Text="Selecionar todos",AutoSize=true,Padding=new Padding(3,9,3,0)};
  bool applyingColumnWidths=false;
  readonly Dictionary<string,int> savedColumnWidths=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
  readonly Timer searchDelay=new Timer {Interval=300};
  string WidthSettingsPath {get{return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArmazemGratidao","produtos-colunas.txt");}}
  void LoadColumnWidths(){try{if(!System.IO.File.Exists(WidthSettingsPath))return;foreach(string line in System.IO.File.ReadAllLines(WidthSettingsPath)){int sep=line.IndexOf('=');int width;if(sep>0&&int.TryParse(line.Substring(sep+1),out width)&&width>=55&&width<=2000)savedColumnWidths[line.Substring(0,sep)]=width;}}catch{}}
  void SaveColumnWidths(){try{string dir=System.IO.Path.GetDirectoryName(WidthSettingsPath);System.IO.Directory.CreateDirectory(dir);System.IO.File.WriteAllLines(WidthSettingsPath,savedColumnWidths.Select(x=>x.Key+"="+x.Value).ToArray());}catch{}}

  const int PageSize=60;
  int currentPage=0;
  bool listVisible=false;
  bool calculateMetrics=false;
  readonly Button showList=new Button {Text="Mostrar lista de produtos",Width=210,Height=33};
  readonly Button previousPage=new Button {Text="Página anterior",Width=155,Height=33};
  readonly Button nextPage=new Button {Text="Próxima página",Width=155,Height=33};
  readonly ComboBox statusFilter=new ComboBox {Width=155,DropDownStyle=ComboBoxStyle.DropDownList};
  readonly Label info=new Label {Dock=DockStyle.Bottom,Height=32};
  readonly Label metrics=new Label {Dock=DockStyle.Top,Height=45,TextAlign=ContentAlignment.MiddleLeft};
  public ProdutosForm(string database) {
   db=database;LoadColumnWidths();Text="Armazém Gratidão | Cadastro de Produtos";Width=1100;Height=720;BackColor=Color.FromArgb(229,242,255);
   var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=105,Padding=new Padding(8),BackColor=Color.FromArgb(220,95,105)};
   var searchBar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=53,Padding=new Padding(9,7,9,5),BackColor=Color.FromArgb(229,242,255),WrapContents=false};
   search.Width=360;search.Font=new Font("Segoe UI",12,FontStyle.Regular);
   searchBar.Controls.Add(new Label {Text="Pesquisar por código de barras ou descrição:",AutoSize=true,Padding=new Padding(0,8,6,0),ForeColor=Color.FromArgb(20,40,75),Font=new Font("Segoe UI",10,FontStyle.Bold)});
   searchBar.Controls.Add(search);
   statusFilter.Items.AddRange(new object[]{"Todos","Ativos","Inativos","Sem estoque","Estoque baixo","Vencidos","Vencendo em 30 dias"});statusFilter.SelectedIndex=0;statusFilter.SelectedIndexChanged+=(s,e)=>{currentPage=0;LoadProducts();};bar.Controls.Add(statusFilter);
   showList.Click+=(s,e)=>{listVisible=!listVisible;grid.Visible=listVisible;showList.Text=listVisible?"Ocultar lista de produtos":"Mostrar lista de produtos";if(listVisible)LoadProducts();else{grid.DataSource=null;info.Text="  Lista oculta. Clique em Mostrar lista de produtos para carregar.";metrics.Text="";}};bar.Controls.Add(showList);
   var find=new Button {Text="Pesquisar",Width=100,Height=33};find.Click+=(s,e)=>{currentPage=0;LoadProducts();};bar.Controls.Add(find);
   var add=new Button {Text="Inserir produto",Width=135,Height=33};add.Click+=(s,e)=>InsertProduct();bar.Controls.Add(add);
   var edit=new Button {Text="Editar selecionado",Width=155,Height=33};edit.Click+=(s,e)=>EditProduct();bar.Controls.Add(edit);
   var clear=new Button {Text="Limpar busca",Width=110,Height=33};clear.Click+=(s,e)=>{search.Clear();currentPage=0;LoadProducts();search.Focus();};bar.Controls.Add(clear);
   var metricsButton=new Button {Text="Calcular indicadores",Width=175,Height=33};metricsButton.Click+=(s,e)=>{calculateMetrics=true;LoadProducts();};bar.Controls.Add(metricsButton);
   var refresh=new Button {Text="Atualizar",Width=95,Height=33};refresh.Click+=(s,e)=>LoadProducts();bar.Controls.Add(refresh);

   var reactivate=new Button {Text="Reativar selecionado",Width=165,Height=33};reactivate.Click+=(s,e)=>ReactivateProduct();bar.Controls.Add(reactivate);
   var import=new Button {Text="Importar CSV",Width=125,Height=33};import.Click+=(s,e)=>ImportProductsCsv();bar.Controls.Add(import);
   var export=new Button {Text="Exportar CSV",Width=125,Height=33};export.Click+=(s,e)=>ExportProducts();bar.Controls.Add(export);
   var barcodeLookup=new Button {Text="Consultar código",Width=155,Height=33};barcodeLookup.Click+=async (s,e)=>await ConsultBarcode(barcodeLookup);bar.Controls.Add(barcodeLookup);
   var copyBarcode=new Button {Text="Copiar código",Width=140,Height=33};copyBarcode.Click+=(s,e)=>CopySelectedBarcode();bar.Controls.Add(copyBarcode);
   var details=new Button {Text="Detalhes do produto",Width=180,Height=33};details.Click+=(s,e)=>ShowSelectedProductDetails();bar.Controls.Add(details);
   var addQuantity=new Button {Text="Adicionar QTD",Width=150,Height=33};addQuantity.Click+=(s,e)=>AddProductQuantity();bar.Controls.Add(addQuantity);
   var history=new Button {Text="Histórico QTD",Width=150,Height=33};history.Click+=(s,e)=>ShowProductQuantityHistory();bar.Controls.Add(history);
   var exportHistory=new Button {Text="Exportar histórico QTD",Width=190,Height=33};exportHistory.Click+=(s,e)=>ExportProductQuantityHistory();bar.Controls.Add(exportHistory);
   var resetFilters=new Button {Text="Mostrar todos",Width=145,Height=33};resetFilters.Click+=(s,e)=>{search.Clear();currentPage=0;statusFilter.SelectedIndex=0;LoadProducts();};bar.Controls.Add(resetFilters);
   previousPage.Click+=(s,e)=>{if(currentPage>0){currentPage--;LoadProducts();}};bar.Controls.Add(previousPage);
   nextPage.Click+=(s,e)=>{currentPage++;LoadProducts();};bar.Controls.Add(nextPage);
   searchDelay.Tick+=(s,e)=>{searchDelay.Stop();currentPage=0;LoadProducts();};
   search.TextChanged+=(s,e)=>{searchDelay.Stop();searchDelay.Start();};
   search.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;searchDelay.Stop();currentPage=0;LoadProducts();}};
   grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex].Name!="selecionar"){grid.CurrentCell=grid.Rows[e.RowIndex].Cells[e.ColumnIndex];EditProduct();}};
   grid.CurrentCellDirtyStateChanged+=(s,e)=>{if(grid.IsCurrentCellDirty&&grid.CurrentCell!=null&&grid.Columns[grid.CurrentCell.ColumnIndex].Name=="selecionar")grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
   grid.CellValueChanged+=(s,e)=>{if(e.RowIndex<0||e.ColumnIndex<0||grid.Columns[e.ColumnIndex].Name!="selecionar")return;long id;if(long.TryParse(Convert.ToString(grid.Rows[e.RowIndex].Cells["codigo_interno"].Value),out id)){if(Convert.ToBoolean(grid.Rows[e.RowIndex].Cells["selecionar"].Value))checkedProducts.Add(id);else checkedProducts.Remove(id);}};

   var menu=new ContextMenuStrip();
   foreach(Control control in bar.Controls){
    Button action=control as Button;
    if(action!=null && action!=previousPage && action!=nextPage && action!=addQuantity && action!=history){
     var entry=new ToolStripMenuItem(action.Text);
     entry.Click+=(s,e)=>action.PerformClick();
     menu.Items.Add(entry);
    }
   }
   var filters=new ToolStripMenuItem("Filtros");
   for(int i=0;i<statusFilter.Items.Count;i++){
    int index=i;
    var option=new ToolStripMenuItem(Convert.ToString(statusFilter.Items[i]));
    option.Click+=(s,e)=>statusFilter.SelectedIndex=index;
    filters.DropDownItems.Add(option);
   }
   menu.Items.Add(filters);
   var toggleMenu=new Button {Text="☰ Menu",Width=115,Height=35,BackColor=Color.FromArgb(220,95,105),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
   toggleMenu.Click+=(s,e)=>menu.Show(toggleMenu,0,toggleMenu.Height);
   searchBar.Controls.Add(toggleMenu);
   searchBar.Controls.Add(selectAll);
   selectAll.CheckedChanged+=(s,e)=>{if(!grid.Columns.Contains("selecionar")||!grid.Columns.Contains("codigo_interno"))return;foreach(DataGridViewRow row in grid.Rows){if(row.IsNewRow)continue;long id;if(long.TryParse(Convert.ToString(row.Cells["codigo_interno"].Value),out id)){if(selectAll.Checked)checkedProducts.Add(id);else checkedProducts.Remove(id);row.Cells["selecionar"].Value=selectAll.Checked;}}};
   var deleteSelected=new Button {Text="Excluir Item",Width=115,Height=35,BackColor=Color.FromArgb(180,35,45),ForeColor=Color.White};
   deleteSelected.Click+=(s,e)=>DeleteCheckedProducts();searchBar.Controls.Add(deleteSelected);
   previousPage.Text="◀";previousPage.Width=42;previousPage.Height=35;
   nextPage.Text="▶";nextPage.Width=42;nextPage.Height=35;
   searchBar.Controls.Add(previousPage);
   searchBar.Controls.Add(nextPage);
   metrics.BackColor=Color.FromArgb(229,242,255);metrics.ForeColor=Color.FromArgb(20,40,75);metrics.Font=new Font("Segoe UI",11,FontStyle.Bold);
   StyleProductsScreen(bar);
   if(!grid.Columns.Contains("selecionar"))grid.Columns.Add(new DataGridViewCheckBoxColumn {Name="selecionar",HeaderText="☐",Width=45,MinimumWidth=40,ReadOnly=false,Frozen=true});
   Controls.Add(grid);Controls.Add(info);Controls.Add(metrics);Controls.Add(searchBar);grid.Visible=false;info.Text="  Lista de produtos oculta para abertura rápida. Clique em Mostrar lista de produtos.";previousPage.Enabled=false;nextPage.Enabled=false;
  }
  bool resizingProductsToolbar=false;
  void ResizeProductsToolbar(FlowLayoutPanel bar){
   if(resizingProductsToolbar)return;
   resizingProductsToolbar=true;
   try{
    int available=Math.Max(300,bar.ClientSize.Width-bar.Padding.Horizontal-12);
    int rowWidth=0,rows=1,rowHeight=0;
    foreach(Control control in bar.Controls){
     if(!control.Visible)continue;
     int w=control.Width+control.Margin.Horizontal;
     int h=control.Height+control.Margin.Vertical;
     if(rowWidth>0&&rowWidth+w>available){rows++;rowWidth=0;}
     rowWidth+=w;rowHeight=Math.Max(rowHeight,h);
    }
    int desired=Math.Max(58,bar.Padding.Vertical+rows*Math.Max(40,rowHeight)+8);
    if(bar.Height!=desired)bar.Height=desired;
   }finally{resizingProductsToolbar=false;}
  }
  static void StyleButton(Button b,Color background){
   b.UseVisualStyleBackColor=false;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderSize=1;
   b.FlatAppearance.BorderColor=Color.FromArgb(22,35,55);b.BackColor=background;
   b.ForeColor=Color.White;b.Font=new Font("Segoe UI",10,FontStyle.Bold);
   b.Padding=new Padding(5,2,5,2);b.Height=Math.Max(b.Height,39);
  }
  static void FitDialogToScreen(Form form){
   var bounds=Screen.FromControl(form).WorkingArea;
   int maxWidth=Math.Max(320,bounds.Width-24);
   int maxHeight=Math.Max(240,bounds.Height-24);
   form.MinimumSize=Size.Empty;
   if(form.Width>maxWidth)form.Width=maxWidth;
   if(form.Height>maxHeight)form.Height=maxHeight;
   form.AutoScroll=true;
   form.StartPosition=FormStartPosition.CenterParent;
  }
  static void StyleDialog(Form form,TableLayoutPanel panel){
   form.BackColor=Color.FromArgb(229,242,255);form.ForeColor=Color.FromArgb(18,31,50);
   form.Font=new Font("Segoe UI",10,FontStyle.Bold);
   form.Shown+=(s,e)=>FitDialogToScreen(form);
   panel.AutoScroll=true;
   panel.BackColor=form.BackColor;
   foreach(Control control in panel.Controls){
    if(control is Label){control.ForeColor=Color.FromArgb(18,31,50);control.Font=new Font("Segoe UI",10,FontStyle.Bold);}
    if(control is TextBox || control is NumericUpDown){control.BackColor=Color.White;control.ForeColor=Color.Black;control.Font=new Font("Segoe UI",11,FontStyle.Regular);}
    if(control is Button)StyleButton((Button)control,Color.FromArgb(19,92,169));
   }
  }
  void StyleProductsScreen(FlowLayoutPanel bar){
   search.BackColor=Color.White;search.ForeColor=Color.Black;search.Font=new Font("Segoe UI",11,FontStyle.Regular);
   int index=0;foreach(Control control in bar.Controls)if(control is Button){
    bool primary=index==1||index==2;
    StyleButton((Button)control,primary?Color.FromArgb(190,30,48):Color.FromArgb(20,73,158));
    index++;
   }
   grid.BackgroundColor=Color.White;grid.DefaultCellStyle.BackColor=Color.White;grid.DefaultCellStyle.ForeColor=Color.Black;
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(220,95,105);grid.DefaultCellStyle.SelectionForeColor=Color.White;
   grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(220,95,105);grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   grid.EnableHeadersVisualStyles=false;grid.Font=new Font("Segoe UI",9,FontStyle.Regular);
   grid.DefaultCellStyle.Padding=new Padding(3,2,3,2);
   grid.RowTemplate.Height=27;grid.RowTemplate.MinimumHeight=27;grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(238,244,251);
   grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
   grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;
   grid.ColumnHeadersHeight=42;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
   grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;
   grid.AllowUserToResizeColumns=true;grid.AllowUserToOrderColumns=false;grid.AllowUserToResizeRows=false;
   grid.ColumnWidthChanged+=(s,e)=>{if(!applyingColumnWidths&&e.Column!=null){savedColumnWidths[e.Column.Name]=e.Column.Width;SaveColumnWidths();}};
   grid.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
   grid.DataBindingComplete+=(s,e)=>{
    applyingColumnWidths=true;
    try{
    if(!grid.Columns.Contains("selecionar")){var checkbox=new DataGridViewCheckBoxColumn {Name="selecionar",HeaderText="✓",Width=45,MinimumWidth=40,ReadOnly=false};grid.Columns.Insert(0,checkbox);}
    if(!grid.Columns.Contains("codigo_interno"))return;
    foreach(DataGridViewRow row in grid.Rows){long id;if(long.TryParse(Convert.ToString(row.Cells["codigo_interno"].Value),out id))row.Cells["selecionar"].Value=checkedProducts.Contains(id);}
    foreach(DataGridViewColumn col in grid.Columns){col.ReadOnly=col.Name!="selecionar";col.Resizable=DataGridViewTriState.True;col.MinimumWidth=55;col.Width=115;col.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;string heading=col.HeaderText.Replace("_"," ");col.HeaderText=System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(heading.ToLowerInvariant());}
    if(grid.Columns.Contains("codigo_interno"))grid.Columns["codigo_interno"].Width=95;
    if(grid.Columns.Contains("codigo_barras"))grid.Columns["codigo_barras"].Width=160;
    if(grid.Columns.Contains("descricao")){var desc=grid.Columns["descricao"];desc.MinimumWidth=150;desc.Width=Math.Max(300,grid.ClientSize.Width-540);desc.DefaultCellStyle.WrapMode=DataGridViewTriState.True;desc.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;}
    if(grid.Columns.Contains("qtd")){grid.Columns["qtd"].HeaderText="QTD";grid.Columns["qtd"].Width=100;}
    foreach(DataGridViewColumn col in grid.Columns){int saved;if(savedColumnWidths.TryGetValue(col.Name,out saved))col.Width=Math.Max(col.MinimumWidth,saved);}
    grid.Columns["selecionar"].Width=45;grid.Columns["selecionar"].ReadOnly=false;grid.Columns["selecionar"].DisplayIndex=0;
    grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;
    }finally{applyingColumnWidths=false;}
   };
   grid.CellFormatting+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex].Name=="descricao"&&e.Value!=null){e.CellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;e.CellStyle.Padding=new Padding(5,2,5,2);}};
   info.BackColor=Color.FromArgb(229,242,255);info.ForeColor=Color.FromArgb(20,35,55);info.Font=new Font("Segoe UI",10,FontStyle.Bold);
  }
  SQLiteConnection Open(bool readOnly=true){
   var c=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=db,ReadOnly=readOnly,FailIfMissing=true}.ToString());c.Open();return c;
  }
  HashSet<string> ProductColumns(SQLiteConnection c){
   var cols=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var cmd=new SQLiteCommand("PRAGMA table_info(products)",c))
   using(var r=cmd.ExecuteReader())while(r.Read())cols.Add(Convert.ToString(r["name"]));
   return cols;
  }
  void ExportProductQuantityHistory(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto para exportar o histórico.");return;}
   long id;
   if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out id)){MessageBox.Show("Produto inválido.");return;}
   string description=Convert.ToString(grid.CurrentRow.Cells["descricao"].Value);
   try{
    using(var c=Open()){
     using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='product_quantity_movements'",c)){
      if(Convert.ToInt64(exists.ExecuteScalar())==0){MessageBox.Show("Ainda não existem entradas de QTD registradas para exportação.");return;}
     }
     using(var check=new SQLiteCommand("SELECT COUNT(*) FROM product_quantity_movements WHERE product_id=@id",c)){
      check.Parameters.AddWithValue("@id",id);
      if(Convert.ToInt64(check.ExecuteScalar())==0){MessageBox.Show("Este produto não possui entradas de QTD registradas.");return;}
     }
     using(var dlg=new SaveFileDialog {Title="Exportar histórico de QTD",Filter="CSV separado por ponto e vírgula (*.csv)|*.csv",FileName="historico-qtd-produto-"+id+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"}){
      if(dlg.ShowDialog(this)!=DialogResult.OK)return;
      string temp=dlg.FileName+".tmp-"+Guid.NewGuid().ToString("N");
      try{
       using(var cmd=new SQLiteCommand("SELECT movement_at,quantity_before,quantity_added,quantity_after,reason FROM product_quantity_movements WHERE product_id=@id ORDER BY id",c))
       using(var writer=new System.IO.StreamWriter(temp,false,new System.Text.UTF8Encoding(true))){
        cmd.Parameters.AddWithValue("@id",id);
        writer.WriteLine("ProdutoID;Descricao;DataHora;QtdAnterior;QtdAdicionada;QtdFinal;Motivo");
        using(var r=cmd.ExecuteReader())while(r.Read()){
         var cells=new string[]{id.ToString(),description,Convert.ToString(r.GetValue(0)),Convert.ToString(r.GetValue(1),System.Globalization.CultureInfo.InvariantCulture),Convert.ToString(r.GetValue(2),System.Globalization.CultureInfo.InvariantCulture),Convert.ToString(r.GetValue(3),System.Globalization.CultureInfo.InvariantCulture),Convert.ToString(r.GetValue(4))};
         for(int i=0;i<cells.Length;i++)cells[i]="\""+(cells[i]??"").Replace("\"","\"\"")+"\"";
         writer.WriteLine(string.Join(";",cells));
        }
       }
       if(System.IO.File.Exists(dlg.FileName))System.IO.File.Delete(dlg.FileName);
       System.IO.File.Move(temp,dlg.FileName);
       MessageBox.Show("Histórico de QTD exportado. O estoque e o banco não foram alterados.","Exportação concluída");
      }finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);}
     }
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível exportar o histórico.\n"+ex.Message,"Exportação de QTD",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void ShowProductQuantityHistory(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto para consultar o histórico.");return;}
   long id;
   if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out id)){MessageBox.Show("Produto inválido.");return;}
   string description=Convert.ToString(grid.CurrentRow.Cells["descricao"].Value);
   try{
    var history=new DataTable();
    using(var c=Open()){
     using(var exists=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='product_quantity_movements'",c)){
      if(Convert.ToInt64(exists.ExecuteScalar())==0){
       MessageBox.Show("Este produto ainda não possui histórico de entradas de QTD neste registro. Nenhum dado foi alterado.","Histórico de QTD");
       return;
      }
     }
     using(var cmd=new SQLiteCommand("SELECT movement_at AS data_hora, quantity_before AS qtd_anterior, quantity_added AS qtd_adicionada, quantity_after AS qtd_final, reason AS motivo FROM product_quantity_movements WHERE product_id=@id ORDER BY id DESC LIMIT 1000",c)){
      cmd.Parameters.AddWithValue("@id",id);
      using(var adapter=new SQLiteDataAdapter(cmd))adapter.Fill(history);
     }
    }
    using(var form=new Form {Text="Histórico QTD - "+description,Width=1080,Height=680,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false}){
     var header=new Label {Dock=DockStyle.Top,Height=52,Padding=new Padding(12),Text="Produto: "+description+"  |  Entradas registradas: "+history.Rows.Count+(history.Rows.Count==1000?" (últimas 1.000)":"") ,Font=new Font("Segoe UI",10,FontStyle.Bold),BackColor=Color.FromArgb(229,242,255)};
     var table=new DataGridView {Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,DataSource=history,RowHeadersVisible=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
     var close=new Button {Text="Fechar",Dock=DockStyle.Bottom,Height=42};
     close.Click+=(s,e)=>form.Close();
     form.Controls.Add(table);form.Controls.Add(header);form.Controls.Add(close);
     form.Shown+=(s,e)=>FitDialogToScreen(form);
     form.ShowDialog(this);
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível consultar o histórico. Nenhum dado foi alterado.\n"+ex.Message,"Histórico de QTD",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void AddProductQuantity(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto para adicionar quantidade.");return;}
   long id;
   if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out id)){MessageBox.Show("Produto inválido.");return;}
   string description=Convert.ToString(grid.CurrentRow.Cells["descricao"].Value);
   using(var form=new Form {Text="Adicionar QTD ao estoque",Width=650,Height=345,MinimumSize=new Size(600,320),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
    var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Padding=new Padding(15)};
    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,175));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    var productLabel=new Label {Text=description,AutoSize=true,MaximumSize=new Size(420,75),Font=new Font("Segoe UI",11,FontStyle.Bold)};
    var quantity=new NumericUpDown {DecimalPlaces=3,Minimum=0.001M,Maximum=10000000M,Increment=1M,Value=1M,Width=180};
    var reason=new TextBox {Text="Entrada manual de estoque",Width=230,MaxLength=250};
    panel.Controls.Add(new Label {Text="Produto",AutoSize=true},0,0);panel.Controls.Add(productLabel,1,0);
    panel.Controls.Add(new Label {Text="QTD a somar",AutoSize=true},0,1);panel.Controls.Add(quantity,1,1);
    panel.Controls.Add(new Label {Text="Motivo",AutoSize=true},0,2);panel.Controls.Add(reason,1,2);
    var save=new Button {Text="Confirmar entrada",Width=175,Height=38};
    save.Click+=(s,e)=>{
     if(quantity.Value<=0){MessageBox.Show("Informe uma quantidade positiva.");return;}
     if(string.IsNullOrWhiteSpace(reason.Text)){MessageBox.Show("Informe o motivo da entrada.");return;}
     try{
      using(var c=Open(false)){
       decimal previous;
       using(var read=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",c)){
        read.Parameters.AddWithValue("@id",id);
        object value=read.ExecuteScalar();
        if(value==null)throw new Exception("Produto não encontrado.");
        previous=value==DBNull.Value?0:Convert.ToDecimal(value,System.Globalization.CultureInfo.InvariantCulture);
       }
       decimal next=checked(previous+quantity.Value);
       if(MessageBox.Show("Produto: "+description+"\nEstoque atual: "+previous.ToString("N3")+"\nEntrada: +"+quantity.Value.ToString("N3")+"\nNovo estoque: "+next.ToString("N3")+"\n\nConfirmar?","Confirmar entrada de estoque",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
       string backup=db+".antes-entrada-qtd-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
       using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
        copy.Open();c.BackupDatabase(copy,"main","main",-1,null,0);
       }
       using(var tx=c.BeginTransaction()){
        using(var table=new SQLiteCommand("CREATE TABLE IF NOT EXISTS product_quantity_movements (id INTEGER PRIMARY KEY AUTOINCREMENT, product_id INTEGER NOT NULL, movement_at TEXT NOT NULL, quantity_before NUMERIC NOT NULL, quantity_added NUMERIC NOT NULL, quantity_after NUMERIC NOT NULL, reason TEXT NOT NULL)",c,tx))table.ExecuteNonQuery();
        decimal current;
        using(var read=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",c,tx)){
         read.Parameters.AddWithValue("@id",id);
         object value=read.ExecuteScalar();
         if(value==null)throw new Exception("Produto não encontrado.");
         current=value==DBNull.Value?0:Convert.ToDecimal(value,System.Globalization.CultureInfo.InvariantCulture);
        }
        if(current!=previous)throw new Exception("Estoque alterado durante a operação. Atualize a tela e tente novamente.");
        using(var update=new SQLiteCommand("UPDATE products SET stock=@stock WHERE id=@id",c,tx)){
         update.Parameters.AddWithValue("@stock",next);update.Parameters.AddWithValue("@id",id);
         if(update.ExecuteNonQuery()!=1)throw new Exception("Quantidade não atualizada.");
        }
        using(var log=new SQLiteCommand("INSERT INTO product_quantity_movements(product_id,movement_at,quantity_before,quantity_added,quantity_after,reason) VALUES(@id,@date,@before,@added,@after,@reason)",c,tx)){
         log.Parameters.AddWithValue("@id",id);log.Parameters.AddWithValue("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
         log.Parameters.AddWithValue("@before",previous);log.Parameters.AddWithValue("@added",quantity.Value);log.Parameters.AddWithValue("@after",next);log.Parameters.AddWithValue("@reason",reason.Text.Trim());
         if(log.ExecuteNonQuery()!=1)throw new Exception("Movimentação não registrada.");
        }
        tx.Commit();
       }
      }
      form.DialogResult=DialogResult.OK;form.Close();
     }catch(Exception ex){MessageBox.Show(ex.Message,"Entrada de QTD não salva",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    };
    panel.Controls.Add(save,1,3);StyleDialog(form,panel);form.Controls.Add(panel);
    if(form.ShowDialog(this)==DialogResult.OK){LoadProducts();MessageBox.Show("Quantidade adicionada e movimentação registrada.");}
   }
  }
  void AddQuantityForProduct(long id,string description,decimal suggested){
   using(var form=new Form {Text="Registrar QTD do produto",Width=460,Height=250,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog}){
    var label=new Label {Text=description,Left=15,Top=15,Width=410,Height=45};
    var quantity=new NumericUpDown {Left=15,Top=72,Width=180,DecimalPlaces=3,Maximum=10000000,Minimum=0.001M,Value=Math.Max(0.001M,suggested)};
    var save=new Button {Text="Confirmar entrada de QTD",Left=15,Top=125,Width=225,Height=36};
    form.Controls.AddRange(new Control[]{label,quantity,save});
    save.Click+=(s,e)=>{
     try{
      using(var conn=Open(false)){
       string backup=db+".antes-entrada-qtd-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
       using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){copy.Open();conn.BackupDatabase(copy,"main","main",-1,null,0);}
       using(var tx=conn.BeginTransaction()){
        decimal before;
        using(var q=new SQLiteCommand("SELECT stock FROM products WHERE id=@id",conn,tx)){q.Parameters.AddWithValue("@id",id);object val=q.ExecuteScalar();if(val==null)throw new Exception("Produto não encontrado.");before=Convert.ToDecimal(val==DBNull.Value?0:val);}
        decimal after=checked(before+quantity.Value);
        using(var q=new SQLiteCommand("CREATE TABLE IF NOT EXISTS product_quantity_movements (id INTEGER PRIMARY KEY AUTOINCREMENT,product_id INTEGER NOT NULL,movement_at TEXT NOT NULL,quantity_before NUMERIC NOT NULL,quantity_added NUMERIC NOT NULL,quantity_after NUMERIC NOT NULL,reason TEXT NOT NULL)",conn,tx))q.ExecuteNonQuery();
        using(var q=new SQLiteCommand("UPDATE products SET stock=@stock WHERE id=@id",conn,tx)){q.Parameters.AddWithValue("@stock",after);q.Parameters.AddWithValue("@id",id);if(q.ExecuteNonQuery()!=1)throw new Exception("Estoque não atualizado.");}
        using(var q=new SQLiteCommand("INSERT INTO product_quantity_movements(product_id,movement_at,quantity_before,quantity_added,quantity_after,reason) VALUES(@id,@time,@before,@added,@after,@reason)",conn,tx)){
         q.Parameters.AddWithValue("@id",id);q.Parameters.AddWithValue("@time",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));q.Parameters.AddWithValue("@before",before);q.Parameters.AddWithValue("@added",quantity.Value);q.Parameters.AddWithValue("@after",after);q.Parameters.AddWithValue("@reason","Entrada de QTD pelo cadastro de produto");q.ExecuteNonQuery();
        }
        tx.Commit();
       }
      }
      form.DialogResult=DialogResult.OK;form.Close();
     }catch(Exception ex){MessageBox.Show(ex.Message,"Entrada de QTD não realizada");}
    };
    if(form.ShowDialog(this)==DialogResult.OK)LoadProducts();
   }
  }
  void ReactivateProduct(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto inativo.");return;}
   long id;
   if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out id)){MessageBox.Show("Produto inválido.");return;}
   try{
    using(var c=Open(false)){
     var cols=ProductColumns(c);
     string column=cols.Contains("active")?"active":cols.Contains("is_active")?"is_active":cols.Contains("ativo")?"ativo":null;
     if(column==null){MessageBox.Show("Este banco não possui campo de situação compatível. Nada foi alterado.");return;}
     using(var check=new SQLiteCommand("SELECT COALESCE(\""+column+"\",1) FROM products WHERE id=@id",c)){
      check.Parameters.AddWithValue("@id",id);
      object value=check.ExecuteScalar();
      if(value==null){MessageBox.Show("Produto não encontrado.");return;}
      if(Convert.ToInt32(value)!=0){MessageBox.Show("Este produto já está ativo.");return;}
     }
     if(MessageBox.Show("Reativar o produto selecionado? O estoque não será alterado.","Confirmar reativação",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
     string backup=db+".antes-reativar-produto-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
     using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
      copy.Open();c.BackupDatabase(copy,"main","main",-1,null,0);
     }
     using(var tx=c.BeginTransaction())using(var cmd=new SQLiteCommand("UPDATE products SET \""+column+"\"=1 WHERE id=@id AND COALESCE(\""+column+"\",1)=0",c,tx)){
      cmd.Parameters.AddWithValue("@id",id);
      if(cmd.ExecuteNonQuery()!=1)throw new Exception("Nenhum produto foi reativado.");
      tx.Commit();
     }
    }
    LoadProducts();MessageBox.Show("Produto reativado. Estoque preservado.");
   }catch(Exception ex){MessageBox.Show(ex.Message,"Falha na reativação");}
  }
  void ImportProductsCsv(){
   using(var dlg=new OpenFileDialog {Title="Importar produtos (CSV)",Filter="CSV (*.csv)|*.csv"}){
    if(dlg.ShowDialog(this)!=DialogResult.OK)return;
    try{
     var rows=new List<string[]>();
     var lines=System.IO.File.ReadAllLines(dlg.FileName,System.Text.Encoding.UTF8);
     if(lines.Length<2)throw new Exception("Arquivo sem produtos.");
     var header=ParseCsvLine(lines[0]);
     int barcodeIndex=-1,nameIndex=-1,priceIndex=-1,costIndex=-1,stockIndex=-1,brandIndex=-1,categoryIndex=-1;
     for(int i=0;i<header.Count;i++){
      string h=header[i].Trim().Trim('\uFEFF').ToLowerInvariant().Replace(" ","").Replace("_","");
      if(h=="codigodebarras"||h=="barcode"||h=="ean")barcodeIndex=i;
      if(h=="descricao"||h=="name"||h=="produto")nameIndex=i;
      if(h=="preco"||h=="price"||h=="precovenda")priceIndex=i;
      if(h=="custo"||h=="cost")costIndex=i;
      if(h=="estoque"||h=="stock")stockIndex=i;
      if(h=="marca"||h=="brand")brandIndex=i;
      if(h=="categoria"||h=="category")categoryIndex=i;
     }
     if(barcodeIndex<0||nameIndex<0||priceIndex<0)throw new Exception("CSV precisa ter CodigoDeBarras, Descricao e Preco.");
     var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
     for(int n=1;n<lines.Length;n++){
      if(string.IsNullOrWhiteSpace(lines[n]))continue;
      var fields=ParseCsvLine(lines[n]);
      int max=Math.Max(barcodeIndex,Math.Max(nameIndex,priceIndex));
      if(fields.Count<=max)throw new Exception("Linha "+(n+1)+": colunas insuficientes.");
      string code=fields[barcodeIndex].Trim(),name=fields[nameIndex].Trim();
      if(code.Length==0||name.Length==0)throw new Exception("Linha "+(n+1)+": código ou descrição vazios.");
      if(!seen.Add(code))throw new Exception("Código duplicado no CSV: "+code);
      decimal price=ParseMoney(fields[priceIndex],n+1);
      decimal cost=costIndex>=0&&costIndex<fields.Count?ParseMoney(fields[costIndex],n+1):0;
      decimal stock=stockIndex>=0&&stockIndex<fields.Count?ParseMoney(fields[stockIndex],n+1):0;
      if(price<0||cost<0||stock<0)throw new Exception("Linha "+(n+1)+": valores negativos.");
      string brand=brandIndex>=0&&brandIndex<fields.Count?fields[brandIndex].Trim():"";
      string category=categoryIndex>=0&&categoryIndex<fields.Count?fields[categoryIndex].Trim():"";
      rows.Add(new[]{code,name,price.ToString(System.Globalization.CultureInfo.InvariantCulture),cost.ToString(System.Globalization.CultureInfo.InvariantCulture),stock.ToString(System.Globalization.CultureInfo.InvariantCulture),brand,category});
     }
     if(rows.Count==0)throw new Exception("Nenhum produto válido.");
     using(var c=Open(false)){
      var cols=ProductColumns(c);
      foreach(var required in new[]{"barcode","name","price","cost","stock"})if(!cols.Contains(required))throw new Exception("Banco incompatível: falta products."+required);
      if((brandIndex>=0&&!cols.Contains("brand"))||(categoryIndex>=0&&!cols.Contains("category")))
       throw new Exception("O arquivo possui Marca ou Categoria, mas o banco não contém esses campos. Importação cancelada para evitar perda de dados.");
      int existing=0;
      using(var check=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE barcode=@barcode",c)){
       var p=check.Parameters.Add("@barcode",DbType.String);
       foreach(var row in rows){p.Value=row[0];if(Convert.ToInt64(check.ExecuteScalar())>0)existing++;}
      }
      if(MessageBox.Show("Produtos no arquivo: "+rows.Count+"\nJá cadastrados (serão ignorados): "+existing+"\nNovos: "+(rows.Count-existing)+"\n\nNenhum cadastro ou estoque existente será modificado. Importar?","Conferir importação",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      if(existing==rows.Count){MessageBox.Show("Todos os produtos já estão cadastrados. Nada foi alterado.");return;}
      string backup=db+".antes-importar-produtos-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
      using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
       copy.Open();c.BackupDatabase(copy,"main","main",-1,null,0);
      }
      int inserted=0;
      using(var tx=c.BeginTransaction()){
       using(var check=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE barcode=@barcode",c,tx))
       using(var insert=new SQLiteCommand("INSERT INTO products(barcode,name,price,cost,stock"+(brandIndex>=0?",brand":"")+(categoryIndex>=0?",category":"")+") VALUES(@barcode,@name,@price,@cost,@stock"+(brandIndex>=0?",@brand":"")+(categoryIndex>=0?",@category":"")+")",c,tx)){
        var cp=check.Parameters.Add("@barcode",DbType.String);
        var bp=insert.Parameters.Add("@barcode",DbType.String);
        var np=insert.Parameters.Add("@name",DbType.String);
        var pp=insert.Parameters.Add("@price",DbType.Decimal);
        var co=insert.Parameters.Add("@cost",DbType.Decimal);
        var st=insert.Parameters.Add("@stock",DbType.Decimal);
        SQLiteParameter br=brandIndex>=0?insert.Parameters.Add("@brand",DbType.String):null;
        SQLiteParameter ca=categoryIndex>=0?insert.Parameters.Add("@category",DbType.String):null;
        foreach(var row in rows){
         cp.Value=row[0];if(Convert.ToInt64(check.ExecuteScalar())>0)continue;
         bp.Value=row[0];np.Value=row[1];
         pp.Value=decimal.Parse(row[2],System.Globalization.CultureInfo.InvariantCulture);
         co.Value=decimal.Parse(row[3],System.Globalization.CultureInfo.InvariantCulture);
         st.Value=decimal.Parse(row[4],System.Globalization.CultureInfo.InvariantCulture);
         if(br!=null)br.Value=row[5];
         if(ca!=null)ca.Value=row[6];
         inserted+=insert.ExecuteNonQuery();
        }
       }
       tx.Commit();
      }
      LoadProducts();MessageBox.Show(inserted+" produtos novos importados. Existentes preservados.\nBackup: "+backup);
     }
    }catch(Exception ex){MessageBox.Show(ex.Message,"Importação cancelada",MessageBoxButtons.OK,MessageBoxIcon.Error);}
   }
  }
  static decimal ParseMoney(string raw,int line){
   decimal result;
   if(!decimal.TryParse(raw.Trim(),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.GetCultureInfo("pt-BR"),out result)
    &&!decimal.TryParse(raw.Trim(),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out result))
    throw new Exception("Linha "+line+": número inválido: "+raw);
   return result;
  }
  static List<string> ParseCsvLine(string line){
   var values=new List<string>();var cell=new System.Text.StringBuilder();bool quoted=false;
   for(int i=0;i<line.Length;i++){
    char ch=line[i];
    if(ch=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){cell.Append('"');i++;}else quoted=!quoted;}
    else if(ch==';'&&!quoted){values.Add(cell.ToString());cell.Clear();}
    else cell.Append(ch);
   }
   if(quoted)throw new Exception("CSV com aspas não fechadas.");
   values.Add(cell.ToString());return values;
  }
  static string BuildProductsExportSql(HashSet<string> cols){
   string expiry=cols.Contains("expiry_date")?"expiry_date":cols.Contains("expiration_date")?"expiration_date":cols.Contains("validade")?"validade":null;
   string weight=cols.Contains("sold_by_weight")?"sold_by_weight":cols.Contains("is_weighted")?"is_weighted":cols.Contains("por_peso")?"por_peso":null;
   return "SELECT id,barcode,name,price,cost,stock"+(cols.Contains("brand")?",brand":"")+(cols.Contains("category")?",category":"")+(expiry!=null?",\""+expiry+"\"":"")+(weight!=null?",\""+weight+"\"":"")+" FROM products ORDER BY name";
  }
  void ExportProducts(){
   using(var dlg=new SaveFileDialog {Title="Exportar produtos para CSV",Filter="CSV separado por ponto e virgula (*.csv)|*.csv",FileName="produtos-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv"}){
    if(dlg.ShowDialog(this)!=DialogResult.OK)return;
    try{
     using(var c=Open()){
      var cols=ProductColumns(c);
      var names=new List<string> {"ID","CodigoDeBarras","Descricao","Preco","Custo","Estoque"};
      if(cols.Contains("brand"))names.Add("Marca");
      if(cols.Contains("category"))names.Add("Categoria");
      if(cols.Contains("expiry_date")||cols.Contains("expiration_date")||cols.Contains("validade"))names.Add("Validade");
      if(cols.Contains("sold_by_weight")||cols.Contains("is_weighted")||cols.Contains("por_peso"))names.Add("VendaPorPeso");
      using(var cmd=new SQLiteCommand(BuildProductsExportSql(cols),c))
      using(var r=cmd.ExecuteReader())
      using(var writer=new System.IO.StreamWriter(dlg.FileName,false,new System.Text.UTF8Encoding(true))){
       writer.WriteLine(string.Join(";",names));
      while(r.Read()){
       var cells=new string[r.FieldCount];
       for(int i=0;i<r.FieldCount;i++)cells[i]="\""+Convert.ToString(r.GetValue(i)).Replace("\"","\"\"")+"\"";
       writer.WriteLine(string.Join(";",cells));
      }
     }
     }
     MessageBox.Show("Produtos exportados. O banco não foi modificado.");
    }catch(Exception ex){MessageBox.Show(ex.Message,"Falha na exportação");}
   }
  }
  void DeleteCheckedProducts(){
   // A linha destacada também conta como seleção, sem exigir marcar a caixa.
   var ids=new HashSet<long>(checkedProducts);
   if(grid.CurrentRow!=null&&grid.Columns.Contains("codigo_interno")){
    long selectedId;
    if(long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out selectedId))ids.Add(selectedId);
   }
   if(ids.Count==0){MessageBox.Show("Selecione o produto que deseja excluir.");return;}
   if(MessageBox.Show("Remover "+ids.Count+" produto(s) da lista de produtos? O histórico de vendas será preservado.","Confirmar exclusão",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   try{
    using(var connection=Open(false)){
     var cols=ProductColumns(connection);
     string active=cols.Contains("active")?"active":cols.Contains("is_active")?"is_active":cols.Contains("ativo")?"ativo":null;
     // Um backup é obrigatório antes de qualquer alteração.
     string backup=db+".antes-excluir-produtos-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
     using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){copy.Open();connection.BackupDatabase(copy,"main","main",-1,null,0);}
     int removed=0;
     using(var tx=connection.BeginTransaction()){
      if(active==null){
       using(var alter=new SQLiteCommand("ALTER TABLE products ADD COLUMN active INTEGER NOT NULL DEFAULT 1",connection,tx))alter.ExecuteNonQuery();
       active="active";
      }
      foreach(long id in ids){
       using(var cmd=new SQLiteCommand("UPDATE products SET \""+active+"\"=0 WHERE id=@id AND COALESCE(\""+active+"\",1)<>0",connection,tx)){
        cmd.Parameters.AddWithValue("@id",id);removed+=cmd.ExecuteNonQuery();
       }
      }
      tx.Commit();
     }
     checkedProducts.Clear();
     if(selectAll.Checked)selectAll.Checked=false;
     currentPage=0;
     LoadProducts();
     MessageBox.Show(removed+" produto(s) removido(s) da lista. Histórico de vendas preservado.","Produtos");
    }
   }catch(Exception ex){MessageBox.Show("Exclusão não concluída: "+ex.Message,"Produtos",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
  void LoadProducts(){
   if(!listVisible){info.Text="  Lista oculta. Clique em Mostrar lista de produtos para carregar.";return;}
   try{
    using(var c=Open()){
    var cols=ProductColumns(c);
    string activeColumn=cols.Contains("active")?"active":cols.Contains("is_active")?"is_active":cols.Contains("ativo")?"ativo":null;
    if(showInactive && activeColumn==null){showInactive=false;MessageBox.Show("Este banco não possui campo de situação ativo/inativo. Nenhum produto foi alterado.");}
    string filter="";
    int mode=statusFilter.SelectedIndex;
    if(activeColumn!=null&&mode!=2)filter=" AND COALESCE(\""+activeColumn+"\",1)=1";
    if(mode==2&&activeColumn!=null)filter=" AND COALESCE(\""+activeColumn+"\",1)=0";
    if(mode==3)filter=" AND COALESCE(stock,0)<=0";
    if(mode==4)filter=" AND COALESCE(stock,0)>0 AND COALESCE(stock,0)<=5";
    if((mode==1||mode==2)&&activeColumn==null){info.Text="  Este banco não possui campo de status; filtro indisponível.";return;}
    string searchColumns=string.IsNullOrWhiteSpace(search.Text)?"1=1":"name LIKE @q OR TRIM(COALESCE(barcode,'')) LIKE @q";
    if(!string.IsNullOrWhiteSpace(search.Text)&&cols.Contains("internal_code"))searchColumns+=" OR TRIM(COALESCE(internal_code,'')) LIKE @q";
    if(!string.IsNullOrWhiteSpace(search.Text)&&cols.Contains("brand"))searchColumns+=" OR brand LIKE @q";
    if(!string.IsNullOrWhiteSpace(search.Text)&&cols.Contains("category"))searchColumns+=" OR category LIKE @q";
    string extraSelect=(cols.Contains("brand")?",brand AS marca":"")+(cols.Contains("category")?",category AS categoria":"");
    string expiryColumn=cols.Contains("expiry_date")?"expiry_date":cols.Contains("expiration_date")?"expiration_date":cols.Contains("validade")?"validade":null;
    if((mode==5||mode==6)&&expiryColumn==null){info.Text="  Este banco não possui campo de validade; filtro indisponível.";grid.DataSource=null;metrics.Text="";return;}
    if(mode==5)filter+=" AND date(\""+expiryColumn+"\") < date('now','localtime') AND date(\""+expiryColumn+"\") IS NOT NULL";
    if(mode==6)filter+=" AND date(\""+expiryColumn+"\") >= date('now','localtime') AND date(\""+expiryColumn+"\") <= date('now','localtime','+30 days')";
    if(expiryColumn!=null)extraSelect+=",\""+expiryColumn+"\" AS validade";
    string weightColumn=cols.Contains("sold_by_weight")?"sold_by_weight":cols.Contains("is_weighted")?"is_weighted":cols.Contains("por_peso")?"por_peso":null;
    extraSelect+=",COALESCE(stock,0) AS qtd";
    if(weightColumn!=null)extraSelect+=",\""+weightColumn+"\" AS venda_por_peso";
    long total;
    using(var count=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE ("+searchColumns+")"+filter,c)){
     count.Parameters.AddWithValue("@q","%"+search.Text.Trim()+"%");
     total=Convert.ToInt64(count.ExecuteScalar());
    }
    int pages=(int)Math.Max(1,(total+PageSize-1)/PageSize);
    if(currentPage>=pages)currentPage=pages-1;
    previousPage.Enabled=currentPage>0;
    nextPage.Enabled=currentPage<pages-1;
    using(var cmd=new SQLiteCommand("SELECT id AS codigo_interno,barcode AS codigo_barras,name AS descricao"+extraSelect+",price AS preco,cost AS custo,ROUND(COALESCE(price,0)-COALESCE(cost,0),2) AS lucro_unitario,CASE WHEN COALESCE(cost,0)>0 THEN ROUND(((COALESCE(price,0)-cost)/cost)*100,2) ELSE NULL END AS margem_percentual,stock AS estoque FROM products WHERE ("+searchColumns+")"+filter+" ORDER BY name,id LIMIT @limit OFFSET @offset",c)){
     cmd.Parameters.AddWithValue("@q","%"+search.Text.Trim()+"%");
     cmd.Parameters.AddWithValue("@limit",PageSize);
     cmd.Parameters.AddWithValue("@offset",currentPage*PageSize);
     var dt=new DataTable();using(var a=new SQLiteDataAdapter(cmd))a.Fill(dt);grid.DataSource=dt;
     info.Text="  Página "+(currentPage+1)+" de "+pages+" | "+dt.Rows.Count+" exibidos nesta página | "+total+" produtos encontrados. Cadastro preservado.";
     // Indicadores completos somente sob demanda, evitando uma varredura extra a cada pesquisa.
     if(calculateMetrics)using(var totals=new SQLiteCommand("SELECT SUM(CASE WHEN COALESCE(stock,0)<=0 THEN 1 ELSE 0 END), COALESCE(SUM(COALESCE(stock,0)),0), COALESCE(SUM(COALESCE(stock,0)*COALESCE(cost,0)),0) FROM products WHERE ("+searchColumns+")"+filter,c)){
      totals.Parameters.AddWithValue("@q","%"+search.Text.Trim()+"%");
      using(var result=totals.ExecuteReader()){
       if(result.Read()){
        long zeroStock=result.IsDBNull(0)?0:Convert.ToInt64(result.GetValue(0));
        decimal stockTotal=result.IsDBNull(1)?0:Convert.ToDecimal(result.GetValue(1),System.Globalization.CultureInfo.InvariantCulture);
        decimal stockValue=result.IsDBNull(2)?0:Convert.ToDecimal(result.GetValue(2),System.Globalization.CultureInfo.InvariantCulture);
        metrics.Text="    PRODUTOS: "+total+"     |     SEM ESTOQUE: "+zeroStock+"     |     UNIDADES: "+stockTotal.ToString("N3")+"     |     CUSTO ESTIMADO: R$ "+stockValue.ToString("N2");
       }
      }
     }
     else metrics.Text="    Indicadores detalhados disponíveis em Calcular indicadores.";
     calculateMetrics=false;
    }
   }}catch(Exception ex){MessageBox.Show(ex.Message,"Consulta de produtos");}
  }
  void EditProduct(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto.");return;}
   long id;
   if(!long.TryParse(Convert.ToString(grid.CurrentRow.Cells["codigo_interno"].Value),out id)){MessageBox.Show("Produto inválido.");return;}
   using(var c=Open()){
    var columns=ProductColumns(c);
    string expiryCol=columns.Contains("expiry_date")?"expiry_date":columns.Contains("expiration_date")?"expiration_date":columns.Contains("validade")?"validade":null;
    string weightCol=columns.Contains("sold_by_weight")?"sold_by_weight":columns.Contains("is_weighted")?"is_weighted":columns.Contains("por_peso")?"por_peso":null;
    string extra=(columns.Contains("brand")?",brand":"")+(columns.Contains("category")?",category":"")+(expiryCol!=null?",\""+expiryCol+"\" AS saved_expiry":"")+(weightCol!=null?",\""+weightCol+"\" AS saved_weight":"");
    using(var q=new SQLiteCommand("SELECT barcode,name,price,cost,stock"+extra+" FROM products WHERE id=@id",c)){
     q.Parameters.AddWithValue("@id",id);
     using(var r=q.ExecuteReader()){
      if(!r.Read()){MessageBox.Show("Produto não encontrado.");return;}
      string oldCode=Convert.ToString(r["barcode"]);
      string oldName=Convert.ToString(r["name"]);
      decimal oldPrice=Convert.ToDecimal(r["price"]);
      decimal oldCost=Convert.ToDecimal(r["cost"]);decimal oldStock=Convert.ToDecimal(r["stock"]==DBNull.Value?0:r["stock"]);
      string oldBrand=columns.Contains("brand")?Convert.ToString(r["brand"]):"";
      string oldCategory=columns.Contains("category")?Convert.ToString(r["category"]):"";
      DateTime oldExpiry=DateTime.Today;bool hasExpiry=expiryCol!=null&&DateTime.TryParse(Convert.ToString(r["saved_expiry"]),out oldExpiry);
      bool oldWeight=weightCol!=null&&r["saved_weight"]!=DBNull.Value&&Convert.ToString(r["saved_weight"])!="0";
      using(var form=new Form {Text="Editar produto #"+id,Width=930,Height=600,MinimumSize=new Size(700,550),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
       var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=11,Padding=new Padding(15)};
       panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,175));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
       var code=new TextBox {Text=oldCode};var name=new TextBox {Text=oldName,Multiline=false,Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};
       var brand=new TextBox {Text=oldBrand,Enabled=columns.Contains("brand")};
       var category=new TextBox {Text=oldCategory,Enabled=columns.Contains("category")};
       var expiry=new DateTimePicker {Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=hasExpiry,Enabled=expiryCol!=null};
       if(hasExpiry)expiry.Value=oldExpiry;
       var byWeight=new CheckBox {Text="Vendido por peso (kg)",AutoSize=true,Checked=oldWeight,Enabled=weightCol!=null};
       var price=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Value=oldPrice};
       var cost=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Value=oldCost};
       var stock=new NumericUpDown {DecimalPlaces=3,Maximum=10000000,Value=Math.Max(0,oldStock),ReadOnly=true};
       var addQty=new NumericUpDown {DecimalPlaces=3,Maximum=10000000,Minimum=0,Value=0};
       
       Control[] fields={code,name,brand,category,price,cost,stock,addQty,expiry,byWeight};string[] labels={"Código de barras","Descrição","Marca","Categoria","Preço de venda","Custo","QTD atual (consulta)","Adicionar QTD","Validade","Tipo de venda"};
       for(int i=0;i<fields.Length;i++){panel.Controls.Add(new Label {Text=labels[i],AutoSize=true},0,i);panel.Controls.Add(fields[i],1,i);}
       // Descrição: comprimento horizontal até o limite da coluna; altura normal de um campo.
       
       code.Width=160;brand.Width=160;category.Width=160;name.Multiline=false;name.AutoSize=true;name.Dock=DockStyle.Top;
       var save=new Button {Text="Salvar alterações",Width=165,Height=38,BackColor=Color.FromArgb(20,69,147),ForeColor=Color.White};
       save.Click+=(s,e)=>{
        if(string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show("Informe a descrição.");return;}
        try{
         using(var dbconn=Open(false)){
          using(var duplicate=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE barcode=@barcode AND id<>@id AND @barcode<>''",dbconn)){
           duplicate.Parameters.AddWithValue("@barcode",code.Text.Trim());duplicate.Parameters.AddWithValue("@id",id);
           if(Convert.ToInt64(duplicate.ExecuteScalar())>0)throw new Exception("Código de barras já utilizado por outro produto.");
          }
          string backup=db+".antes-editar-produto-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
          using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
           copy.Open();dbconn.BackupDatabase(copy,"main","main",-1,null,0);
          }
          string optional=(columns.Contains("brand")?",brand=@brand":"")+(columns.Contains("category")?",category=@category":"")+(expiryCol!=null?",\""+expiryCol+"\"=@expiry":"")+(weightCol!=null?",\""+weightCol+"\"=@weight":"");
          using(var tx=dbconn.BeginTransaction())using(var update=new SQLiteCommand("UPDATE products SET barcode=@barcode,name=@name,price=@price,cost=@cost"+optional+" WHERE id=@id",dbconn,tx)){
           update.Parameters.AddWithValue("@barcode",code.Text.Trim());update.Parameters.AddWithValue("@name",name.Text.Trim());update.Parameters.AddWithValue("@price",price.Value);update.Parameters.AddWithValue("@cost",cost.Value);update.Parameters.AddWithValue("@id",id);
           if(columns.Contains("brand"))update.Parameters.AddWithValue("@brand",brand.Text.Trim());
           if(columns.Contains("category"))update.Parameters.AddWithValue("@category",category.Text.Trim());
           if(expiryCol!=null)update.Parameters.AddWithValue("@expiry",expiry.Checked?(object)expiry.Value.ToString("yyyy-MM-dd"):DBNull.Value);
           if(weightCol!=null)update.Parameters.AddWithValue("@weight",byWeight.Checked?1:0);
           if(update.ExecuteNonQuery()!=1)throw new Exception("Produto não atualizado.");
           tx.Commit();
          }
         }
         form.DialogResult=DialogResult.OK;form.Close();
        }catch(Exception ex){MessageBox.Show(ex.Message,"Alteração não salva",MessageBoxButtons.OK,MessageBoxIcon.Error);}
       };
       for(int row=0;row<11;row++)panel.RowStyles.Add(new RowStyle(SizeType.Absolute,38));panel.Controls.Add(save,1,10);StyleDialog(form,panel);form.Controls.Add(panel);
       if(form.ShowDialog(this)==DialogResult.OK){LoadProducts();if(addQty.Value>0)AddQuantityForProduct(id,name.Text,addQty.Value);}
      }
     }
    }
   }
  }
  void CopySelectedBarcode(){
   if(grid.CurrentRow==null||!grid.Columns.Contains("codigo_barras")){MessageBox.Show("Selecione um produto.");return;}
   string barcode=Convert.ToString(grid.CurrentRow.Cells["codigo_barras"].Value).Trim();
   if(barcode.Length==0){MessageBox.Show("Produto sem código de barras.");return;}
   try{Clipboard.SetText(barcode);info.Text="  Código de barras copiado: "+barcode;}
   catch(Exception ex){MessageBox.Show("Não foi possível copiar: "+ex.Message);}
  }
  void ShowSelectedProductDetails(){
   if(grid.CurrentRow==null){MessageBox.Show("Selecione um produto.");return;}
   var lines=new List<string>();
   foreach(DataGridViewCell cell in grid.CurrentRow.Cells){
    if(!cell.OwningColumn.Visible)continue;
    string name=cell.OwningColumn.HeaderText.Replace("_"," ");
    string value=cell.Value==null||cell.Value==DBNull.Value?"—":Convert.ToString(cell.Value);
    lines.Add(name+": "+value);
   }
   MessageBox.Show(string.Join("\n",lines),"Detalhes do produto",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
  async System.Threading.Tasks.Task ConsultBarcode(Button trigger){
   using(var dialog=new Form {Text="Consulta de código de barras",Width=650,Height=270,MinimumSize=new Size(600,240),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
    var label=new Label {Text="Digite ou leia o código de barras:",Dock=DockStyle.Top,Height=34,Font=new Font("Segoe UI",10,FontStyle.Bold)};
    var code=new TextBox {Dock=DockStyle.Top,Font=new Font("Segoe UI",12)};
    var confirm=new Button {Text="Consultar",Dock=DockStyle.Bottom,Height=42,DialogResult=DialogResult.OK};
    dialog.Controls.Add(confirm);dialog.Controls.Add(code);dialog.Controls.Add(label);
    dialog.AcceptButton=confirm;
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;
    string barcode=code.Text.Trim();
    if(!System.Text.RegularExpressions.Regex.IsMatch(barcode,@"^\d{8,14}$")){MessageBox.Show("Código de barras inválido. Informe 8 a 14 dígitos.");return;}
    try{
     string local=LookupLocalProductName(barcode);
     if(!string.IsNullOrWhiteSpace(local)){MessageBox.Show(local+"\nFonte: banco local. Nenhum dado alterado.","Produto encontrado");return;}
     if(MessageBox.Show("Código não encontrado no catálogo local.\nDeseja pesquisar na internet?","Pesquisa opcional",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
     trigger.Enabled=false;
     string result=await System.Threading.Tasks.Task.Run(()=>LookupBarcodeName(barcode));
     MessageBox.Show(string.IsNullOrWhiteSpace(result)?"Produto não encontrado na base pública.":result+"\nFonte: consulta online. Nenhum dado alterado.","Resultado da consulta");
    }catch(Exception ex){MessageBox.Show("Não foi possível consultar a internet. O programa continua disponível offline.\n"+ex.Message,"Consulta");}
    finally{trigger.Enabled=true;}
   }
  }
  string LookupLocalProductName(string barcode){
   using(var connection=Open())
   using(var cmd=new SQLiteCommand("SELECT name FROM products WHERE TRIM(COALESCE(barcode,''))=@barcode LIMIT 1",connection)){
    cmd.Parameters.AddWithValue("@barcode",barcode.Trim());
    var result=cmd.ExecuteScalar();
    return result==null||result==DBNull.Value?null:Convert.ToString(result);
   }
  }
  static bool ValidBarcodeCheckDigit(string barcode){
   if(barcode.Length!=12&&barcode.Length!=13&&barcode.Length!=14)return true;
   int sum=0;
   for(int i=barcode.Length-2,weight=3;i>=0;i--,weight=weight==3?1:3)sum+=(barcode[i]-'0')*weight;
   return (10-(sum%10))%10==barcode[barcode.Length-1]-'0';
  }
  sealed class BarcodeProductData {public string Name,Brand,Category;}
  static string JsonProductField(string json,string key){
   string pattern="\\\""+System.Text.RegularExpressions.Regex.Escape(key)+"\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"";
   var match=System.Text.RegularExpressions.Regex.Match(json,pattern);
   return match.Success?System.Text.RegularExpressions.Regex.Unescape(match.Groups[1].Value).Trim():"";
  }
  sealed class ProductLookupWebClient:System.Net.WebClient{
   protected override System.Net.WebRequest GetWebRequest(Uri address){
    var request=base.GetWebRequest(address);
    request.Timeout=8000;
    return request;
   }
  }
  static BarcodeProductData LookupBarcodeProduct(string barcode){
   System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
   string url="https://world.openfoodfacts.org/api/v2/product/"+barcode+".json?fields=product_name_pt,product_name,brands,categories,status";
   using(var client=new ProductLookupWebClient()){
    client.Encoding=System.Text.Encoding.UTF8;
    client.Headers[System.Net.HttpRequestHeader.UserAgent]="ArmazemGratidao/1.0 (barcode product lookup)";
    string json=client.DownloadString(url);
    if(!System.Text.RegularExpressions.Regex.IsMatch(json,"\\\"status\\\"\\s*:\\s*1(?:\\D|$)"))return null;
    string name=JsonProductField(json,"product_name_pt");
    if(string.IsNullOrWhiteSpace(name))name=JsonProductField(json,"product_name");
    if(string.IsNullOrWhiteSpace(name))return null;
    string brand=JsonProductField(json,"brands");
    string category=JsonProductField(json,"categories");
    if(brand.Contains(","))brand=brand.Split(',')[0].Trim();
    if(category.Contains(","))category=category.Split(',')[0].Trim();
    name=System.Text.RegularExpressions.Regex.Replace(name,@"\s+"," ").Trim();
    brand=System.Text.RegularExpressions.Regex.Replace(brand,@"\s+"," ").Trim();
    category=System.Text.RegularExpressions.Regex.Replace(category,@"\s+"," ").Trim();
    return new BarcodeProductData {Name=name,Brand=brand,Category=category};
   }
  }
  static string LookupBarcodeName(string barcode){
   System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
   string url="https://world.openfoodfacts.org/api/v2/product/"+barcode+".json?fields=product_name_pt,product_name";
   using(var client=new System.Net.WebClient()){
    client.Encoding=System.Text.Encoding.UTF8;
    client.Headers[System.Net.HttpRequestHeader.UserAgent]="ArmazemGratidao/1.0 (product lookup)";
    string json=client.DownloadString(url);
    foreach(string key in new[]{"product_name_pt","product_name"}){
     var match=System.Text.RegularExpressions.Regex.Match(json, "\\\"" + key + "\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
     if(match.Success){
      string name=System.Text.RegularExpressions.Regex.Unescape(match.Groups[1].Value).Trim();
      if(name.Length>0)return name;
     }
    }
   }
   return null;
  }
  public void OpenNewProduct(string initialInput){
   InsertProduct(initialInput);
  }
  void InsertProduct(string initialInput=""){
   using(var form=new Form {Text="Inserir produto",Width=930,Height=630,MinimumSize=new Size(700,570),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false}){
    var panel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=12,Padding=new Padding(15)};
    panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,175));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
    var barcode=new TextBox();var name=new TextBox {Multiline=false,Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};var brand=new TextBox();var category=new TextBox();
    string initial=(initialInput??"").Trim();
    if(System.Text.RegularExpressions.Regex.IsMatch(initial,@"^\d{8,14}$"))barcode.Text=initial;
    else name.Text=initial;
    HashSet<string> availableColumns;
    using(var checkConnection=Open())availableColumns=ProductColumns(checkConnection);
    brand.Enabled=availableColumns.Contains("brand");
    category.Enabled=availableColumns.Contains("category");
    var expiry=new DateTimePicker {Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=false};
    var byWeight=new CheckBox {Text="Vendido por peso (kg)",AutoSize=true};
    expiry.Enabled=availableColumns.Contains("expiry_date")||availableColumns.Contains("expiration_date")||availableColumns.Contains("validade");
    byWeight.Enabled=availableColumns.Contains("sold_by_weight")||availableColumns.Contains("is_weighted")||availableColumns.Contains("por_peso");
    var price=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Width=170};var cost=new NumericUpDown {DecimalPlaces=2,Maximum=1000000,Width=170};var stock=new NumericUpDown {DecimalPlaces=3,Maximum=10000000,Width=170};
    
    Control[] fields={barcode,name,brand,category,price,cost,stock,expiry,byWeight};string[] labels={"Código de barras","Descrição","Marca","Categoria","Preço de venda","Custo","Estoque inicial","Validade","Tipo de venda"};
    for(int i=0;i<fields.Length;i++){panel.Controls.Add(new Label {Text=labels[i],AutoSize=true},0,i);panel.Controls.Add(fields[i],1,i);}
       // Descrição: comprimento horizontal até o limite da coluna; altura normal de um campo.
       
       barcode.Width=160;brand.Width=160;category.Width=160;name.Multiline=false;name.AutoSize=true;name.Dock=DockStyle.Top;
    var lookup=new Button {Text="Buscar dados do código",Width=225,Height=36};
    var lookupStatus=new Label {Text="Bipe o código ou digite e pressione Enter para pesquisar.",AutoSize=true,MaximumSize=new Size(450,55),ForeColor=Color.FromArgb(20,73,158)};
    bool lookingUp=false;
    string lastLookupCode="";
    int lookupGeneration=0;
    Func<System.Threading.Tasks.Task> fetchProduct=async ()=>{
     string code=barcode.Text.Trim();
     if(lookingUp||code==lastLookupCode)return;
     if(!System.Text.RegularExpressions.Regex.IsMatch(code,@"^\d{8,14}$")){lookupStatus.Text="Informe um código numérico de 8 a 14 dígitos.";return;}
     int thisLookup=++lookupGeneration;
     string initialName=name.Text,initialBrand=brand.Text,initialCategory=category.Text;
     lookingUp=true;lookup.Enabled=false;lookup.Text="Pesquisando...";lookupStatus.Text="Consultando catálogo local e base pública...";
     try{
      string localName=LookupLocalProductName(code);
      if(!string.IsNullOrWhiteSpace(localName)){
       lookupStatus.Text="Já cadastrado: "+localName+" — não é permitido duplicar.";
       MessageBox.Show("Este código já pertence ao produto: "+localName+"\nO cadastro existente não será alterado.","Produto existente");
       lastLookupCode=code;return;
      }
      var data=await System.Threading.Tasks.Task.Run(()=>LookupBarcodeProduct(code));
      if(barcode.IsDisposed||form.IsDisposed||thisLookup!=lookupGeneration||barcode.Text.Trim()!=code){if(!lookupStatus.IsDisposed)lookupStatus.Text="Código alterado durante a pesquisa. Pesquise novamente.";return;}
      if(data==null||string.IsNullOrWhiteSpace(data.Name)){
       lookupStatus.Text="Produto não encontrado. Complete o cadastro manualmente.";
       lastLookupCode=code;return;
      }
      bool editedDuringLookup=name.Text!=initialName||brand.Text!=initialBrand||category.Text!=initialCategory;
      if(editedDuringLookup){lookupStatus.Text="Campos alterados durante a pesquisa. Dados digitados preservados; pesquise novamente se desejar.";lastLookupCode=code;return;}
      bool hasManualText=!string.IsNullOrWhiteSpace(name.Text)||!string.IsNullOrWhiteSpace(brand.Text)||!string.IsNullOrWhiteSpace(category.Text);
      if(hasManualText&&MessageBox.Show("Dados encontrados na internet para "+code+".\nSubstituir os campos de descrição, marca e categoria já preenchidos?","Confirmar preenchimento",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes){
       lookupStatus.Text="Dados existentes mantidos.";lastLookupCode=code;return;
      }
      if(data.Name.Length>250){lookupStatus.Text="Descrição muito longa na base pública. Revise antes de salvar.";data.Name=data.Name.Substring(0,250).Trim();}
      name.Text=data.Name;
      if(brand.Enabled&&!string.IsNullOrWhiteSpace(data.Brand))brand.Text=data.Brand.Length>120?data.Brand.Substring(0,120).Trim():data.Brand;
      if(category.Enabled&&!string.IsNullOrWhiteSpace(data.Category))category.Text=data.Category.Length>120?data.Category.Substring(0,120).Trim():data.Category;
      lookupStatus.Text="Preenchido pela base pública. Confira os dados e informe preço, custo e estoque antes de salvar.";
      lastLookupCode=code;
     }catch(Exception ex){lookupStatus.Text="Busca indisponível. Cadastro manual liberado.";MessageBox.Show("Consulta online indisponível: "+ex.Message,"Busca de produto");}
     finally{lookingUp=false;if(!lookup.IsDisposed){lookup.Enabled=true;lookup.Text="Buscar dados do código";if(!form.IsDisposed&&barcode.Text.Trim()!=code)lookupStatus.Text="Código alterado. Pressione Enter para pesquisar o novo código.";}}
    };
    barcode.KeyDown+=async (s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await fetchProduct();}};
    barcode.Leave+=async (s,e)=>{if(System.Text.RegularExpressions.Regex.IsMatch(barcode.Text.Trim(),@"^\d{8,14}$"))await fetchProduct();};
    barcode.TextChanged+=(s,e)=>{lookupGeneration++;if(barcode.Text.Trim()!=lastLookupCode)lookupStatus.Text="Pressione Enter para consultar o código.";};
    barcode.MaxLength=14;
    barcode.ShortcutsEnabled=true;
    barcode.CharacterCasing=CharacterCasing.Normal;
    lookup.Click+=async (s,e)=>{lastLookupCode="";await fetchProduct();};
    for(int row=0;row<12;row++)panel.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
    panel.Controls.Add(lookupStatus,1,9);
    panel.Controls.Add(lookup,1,10);

    var save=new Button {Text="Salvar produto",Width=145,Height=36,BackColor=Color.FromArgb(24,84,170),ForeColor=Color.White};
    save.Click+=(s,e)=>{
     barcode.Text=barcode.Text.Trim();
     if(!string.IsNullOrWhiteSpace(barcode.Text)&&!System.Text.RegularExpressions.Regex.IsMatch(barcode.Text.Trim(),@"^\d{8,14}$")){MessageBox.Show("Código de barras inválido. Informe 8 a 14 dígitos.","Cadastro de produto");return;}
     if(!string.IsNullOrWhiteSpace(barcode.Text)&&barcode.Text.Length>=12&&!ValidBarcodeCheckDigit(barcode.Text)){if(MessageBox.Show("O dígito verificador do código de barras não confere. Deseja revisar o código antes de salvar?","Conferir código",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes){barcode.Focus();return;}}
     if(string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show("Informe a descrição.");return;}
     try{
      using(var c=Open(false)){
       var cols=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
       using(var cmd=new SQLiteCommand("PRAGMA table_info(products)",c))using(var reader=cmd.ExecuteReader())while(reader.Read())cols.Add(Convert.ToString(reader["name"]));
       foreach(var field in new[]{"barcode","name","price","cost","stock"})if(!cols.Contains(field))throw new Exception("Campo necessário ausente: products."+field);
       using(var check=new SQLiteCommand("SELECT COUNT(*) FROM products WHERE TRIM(COALESCE(barcode,''))=@code AND @code<>''",c)){
        check.Parameters.AddWithValue("@code",barcode.Text.Trim());
        if(Convert.ToInt64(check.ExecuteScalar())>0)throw new Exception("Código de barras já cadastrado.");
       }
       // Uma cópia SQLite é criada antes de qualquer alteração.
       string backup=db+".antes-produto-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
       using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder {DataSource=backup}.ToString())){
        copy.Open();c.BackupDatabase(copy,"main","main",-1,null,0);
       }
       string expiryCol=cols.Contains("expiry_date")?"expiry_date":cols.Contains("expiration_date")?"expiration_date":cols.Contains("validade")?"validade":null;
       string weightCol=cols.Contains("sold_by_weight")?"sold_by_weight":cols.Contains("is_weighted")?"is_weighted":cols.Contains("por_peso")?"por_peso":null;
       string extraCols=(cols.Contains("brand")?",brand":"")+(cols.Contains("category")?",category":"")+(expiryCol!=null?",\""+expiryCol+"\"":"")+(weightCol!=null?",\""+weightCol+"\"":"");
       string extraParams=(cols.Contains("brand")?",@brand":"")+(cols.Contains("category")?",@category":"")+(expiryCol!=null?",@expiry":"")+(weightCol!=null?",@weight":"");
       using(var tx=c.BeginTransaction())using(var cmd=new SQLiteCommand("INSERT INTO products(barcode,name,price,cost,stock"+extraCols+") VALUES(@barcode,@name,@price,@cost,@stock"+extraParams+")",c,tx)){
        cmd.Parameters.AddWithValue("@barcode",barcode.Text.Trim());cmd.Parameters.AddWithValue("@name",name.Text.Trim());cmd.Parameters.AddWithValue("@price",price.Value);cmd.Parameters.AddWithValue("@cost",cost.Value);cmd.Parameters.AddWithValue("@stock",0M);
        if(cols.Contains("brand"))cmd.Parameters.AddWithValue("@brand",brand.Text.Trim());
        if(cols.Contains("category"))cmd.Parameters.AddWithValue("@category",category.Text.Trim());
        if(expiryCol!=null)cmd.Parameters.AddWithValue("@expiry",expiry.Checked?(object)expiry.Value.ToString("yyyy-MM-dd"):DBNull.Value);
        if(weightCol!=null)cmd.Parameters.AddWithValue("@weight",byWeight.Checked?1:0);
        cmd.ExecuteNonQuery();tx.Commit();
       }
      }
      if(stock.Value>0)MessageBox.Show("Produto cadastrado com estoque zero. Para registrar as "+stock.Value.ToString("N3")+" unidades informadas, selecione o produto e use Adicionar QTD. Essa operação gera o registro de movimentação autorizado.","Estoque inicial protegido");
      form.DialogResult=DialogResult.OK;form.Close();
     }catch(Exception ex){MessageBox.Show(ex.Message,"Produto não salvo");}
    };
    panel.Controls.Add(save,1,11);StyleDialog(form,panel);form.Controls.Add(panel);
    if(form.ShowDialog(this)==DialogResult.OK){LoadProducts();if(stock.Value>0){using(var conn=Open()){using(var cmd=new SQLiteCommand("SELECT id FROM products WHERE barcode=@barcode ORDER BY id DESC LIMIT 1",conn)){cmd.Parameters.AddWithValue("@barcode",barcode.Text.Trim());object newId=cmd.ExecuteScalar();if(newId!=null)AddQuantityForProduct(Convert.ToInt64(newId),name.Text,stock.Value);}}}}
   }
  }
 }
}