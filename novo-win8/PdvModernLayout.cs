using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArmazemGratidao {
 // Apenas reorganiza controles existentes do PDV: os handlers originais ficam intactos.
 // Layout reproduz o painel aprovado: voltar em cima, produto logo abaixo,
 // carrinho a esquerda, acoes a direita e resumo na faixa inferior.
 internal static class PdvModernLayout {
  static readonly Color Navy=Color.FromArgb(21,67,143);
  static readonly Color DeepNavy=Color.FromArgb(14,52,122);
  static readonly Color Red=Color.FromArgb(190,29,53);
  static readonly Color Pale=Color.FromArgb(234,242,253);
  static readonly Color Surface=Color.FromArgb(248,250,254);
  static readonly Color Edge=Color.FromArgb(186,205,234);

  static void Round(Control control,int radius){
   if(control.IsDisposed||control.Width<radius*2+4||control.Height<radius*2+4)return;
   using(var path=new GraphicsPath()){
    var w=control.Width-1;var h=control.Height-1;var d=radius*2;
    path.AddArc(0,0,d,d,180,90);
    path.AddArc(w-d,0,d,d,270,90);
    path.AddArc(w-d,h-d,d,d,0,90);
    path.AddArc(0,h-d,d,d,90,90);
    path.CloseFigure();
    var old=control.Region;
    control.Region=new Region(path);
    if(old!=null)old.Dispose();
   }
  }
  static void Rounded(Control control,int radius){
   control.Resize+=(sender,args)=>Round(control,radius);
   Round(control,radius);
  }
  static void Outline(Panel panel){
   panel.Paint+=(sender,args)=>{
    if(panel.Width<20||panel.Height<20)return;
    args.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
    RoundedVisuals.DrawOutline(args.Graphics,
     new Rectangle(1,1,panel.Width-3,panel.Height-3),13,Edge,1.2f);
   };
  }
  static Label TextLabel(string value){
   return new Label{Text=value,Dock=DockStyle.Fill,
    TextAlign=ContentAlignment.MiddleLeft,
    Font=new Font("Segoe UI",9.3f,FontStyle.Bold),
    ForeColor=Color.FromArgb(36,55,89),
    Margin=new Padding(6,0,4,0)};
  }
  static Panel Cell(string title,Control input){
   var p=new Panel{Dock=DockStyle.Fill,Margin=new Padding(3,0,3,0)};
   var label=TextLabel(title);label.Dock=DockStyle.Top;label.Height=23;
   input.Dock=DockStyle.Fill;
   input.Margin=new Padding(0);
   p.Controls.Add(input);p.Controls.Add(label);
   return p;
  }
  static void ButtonTheme(Button button,string text,Color back,Color fore,int height){
   button.Text=text;
   button.Name="amzgPdv"+(button.Name.Length==0?"Action":button.Name);
   button.Height=height;
   button.FlatStyle=FlatStyle.Flat;
   button.FlatAppearance.BorderSize=0;
   button.FlatAppearance.MouseOverBackColor=ControlPaint.Light(back,0.18f);
   button.FlatAppearance.MouseDownBackColor=ControlPaint.Dark(back,0.08f);
   button.BackColor=back;button.ForeColor=fore;
   button.Font=new Font("Segoe UI",10f,FontStyle.Bold);
   button.Cursor=Cursors.Hand;
   button.UseMnemonic=false;
   button.AutoEllipsis=true;
   Rounded(button,10);
  }

  static void ApplyWide(PdvForm form,FlowLayoutPanel header,FlowLayoutPanel footer,Panel listArea,TextBox code,NumericUpDown qty,ComboBox unit,Button add,Button finish,Button edit,Button chooseCustomer,Button fiado,Button remove,Button clear,Button otherPayments,Button newSale,Button whatsApp,Label stats,TextBox customer,TextBox received,Label change,Label discountLabel,ComboBox discountMode,NumericUpDown discountInput,Label total,DataGridView grid){
   form.SuspendLayout();form.BackColor=Surface;form.MinimumSize=new Size(860,570);
   var root=new TableLayoutPanel{Name="amzgPdvLayout",Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(6),Margin=Padding.Empty};
   root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));root.RowStyles.Add(new RowStyle(SizeType.Absolute,53));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var brand=new Panel{Name="amzgPdvBrand",Dock=DockStyle.Fill,BackColor=DeepNavy,Margin=Padding.Empty};
   var title=new Label{Text="Armazém Gratidão - PDV",Dock=DockStyle.Fill,ForeColor=Color.White,Font=new Font("Segoe UI",15,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0)};
   var more=new Button{Name="amzgPdvMoreOptions",Dock=DockStyle.Right,Width=112};
   ButtonTheme(more,"☰ Opções",Navy,Color.White,32);more.Name="amzgPdvMoreOptions";
   var menu=new ContextMenuStrip{Name="amzgPdvMenu",Font=new Font("Segoe UI",10)};
   foreach(string name in new[]{"Clientes","Calculadora","Editar Qtd. / Preço","Remover Item","Remover todos","Fiado","Outras Formas","Nova Venda","Enviar WhatsApp"}){
    string action=name;menu.Items.Add(name,null,(sender,args)=>form.RunWideMenu(action));
   }
   more.Click+=(sender,args)=>menu.Show(more,new Point(Math.Max(0,more.Width-menu.Width),more.Height));form.FormClosed+=(sender,args)=>menu.Dispose();
   brand.Controls.Add(title);brand.Controls.Add(more);root.Controls.Add(brand,0,0);
   var top=new TableLayoutPanel{Name="amzgPdvSearchBar",Dock=DockStyle.Fill,ColumnCount=5,RowCount=1,Margin=new Padding(0,3,0,3),BackColor=Pale};
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,63));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,88));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,37));top.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var backSlot=new Panel{Name="pdvBackSlot",Dock=DockStyle.Fill,Margin=Padding.Empty};top.Controls.Add(backSlot,0,0);
   code.Name="amzgPdvCodeInput";qty.Name="amzgPdvQtyInput";unit.Name="amzgPdvUnitInput";customer.Name="amzgPdvCustomer";
   code.Font=new Font("Segoe UI",10.5f);qty.Font=new Font("Segoe UI",10.5f);unit.Font=new Font("Segoe UI",10);
   customer.Font=new Font("Segoe UI",10,FontStyle.Bold);customer.Cursor=Cursors.Hand;customer.Click+=(sender,args)=>form.RunWideMenu("Clientes");
   top.Controls.Add(Cell("Código / Nome do produto",code),1,0);top.Controls.Add(Cell("Qtd.",qty),2,0);top.Controls.Add(Cell("UN",unit),3,0);top.Controls.Add(Cell("Código / Cliente",customer),4,0);root.Controls.Add(top,0,1);
   var main=new TableLayoutPanel{Name="amzgPdvMain",Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
   main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   listArea.Name="amzgPdvItemBox";listArea.Dock=DockStyle.Fill;listArea.Margin=new Padding(0,0,5,0);listArea.Padding=new Padding(1);listArea.BackColor=Color.White;
   // Colunas explicitamente ligadas ao DataTable; não usar DisplayedCells,
   // que encolhe a descrição e deixa o resto da largura vazio com apenas um item.
   grid.Name="amzgPdvWideGrid";grid.Dock=DockStyle.Fill;grid.Margin=Padding.Empty;
   grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.EnableHeadersVisualStyles=false;
   grid.AutoGenerateColumns=false;grid.Columns.Clear();
   grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
   grid.ColumnHeadersVisible=true;grid.RowHeadersVisible=false;
   grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
   grid.ColumnHeadersHeight=36;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
   grid.CellBorderStyle=DataGridViewCellBorderStyle.None;
   grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(204,224,249);
   grid.ColumnHeadersDefaultCellStyle.ForeColor=DeepNavy;
   grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Color.FromArgb(204,224,249);
   grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=DeepNavy;
   grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9.5f,FontStyle.Bold);
   grid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.True;
   grid.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;
   grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(3,0,3,0);
   grid.Font=new Font("Segoe UI",10f,FontStyle.Bold);
   grid.DefaultCellStyle.ForeColor=Color.FromArgb(16,34,65);
   grid.DefaultCellStyle.BackColor=Color.White;
   grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(166,207,248);
   grid.DefaultCellStyle.SelectionForeColor=Color.FromArgb(16,34,65);
   grid.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
   grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(230,238,249);
   grid.RowTemplate.Height=24;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
   grid.MultiSelect=false;grid.ScrollBars=ScrollBars.Both;grid.AllowUserToResizeRows=false;
   string[] fields={"Codigo","Produto","Unidade","Quantidade","Preco","Total"};
   string[] captions={"Código de barras","Descrição do produto","Un.","Qtd.","Valor unit. (R$)","Total (R$)"};
   int[] min={105,115,34,43,70,67};float[] weight={21,34,7,9,15,14};
   for(int i=0;i<fields.Length;i++){
    var col=new DataGridViewTextBoxColumn{Name=fields[i],DataPropertyName=fields[i],HeaderText=captions[i],
     AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,MinimumWidth=min[i],FillWeight=weight[i],SortMode=DataGridViewColumnSortMode.NotSortable};
    col.DefaultCellStyle.Padding=new Padding(3,0,3,0);
    if(i==2||i==3)col.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
    if(i==3)col.DefaultCellStyle.Format="0.###";
    if(i==4||i==5){col.DefaultCellStyle.Format="N2";col.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;}
    grid.Columns.Add(col);
   }
   grid.DataBindingComplete+=(sender,args)=>{
    grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
    foreach(DataGridViewRow r in grid.Rows)r.Height=24;
   };
   var delete=new DataGridViewButtonColumn{Name="ExcluirItem",HeaderText="Excluir",Text="×",UseColumnTextForButtonValue=true,AutoSizeMode=DataGridViewAutoSizeColumnMode.None,Width=52,MinimumWidth=45,FlatStyle=FlatStyle.Flat,SortMode=DataGridViewColumnSortMode.NotSortable};
   delete.DefaultCellStyle.BackColor=Color.FromArgb(255,237,240);
   delete.DefaultCellStyle.ForeColor=Red;
   delete.DefaultCellStyle.Font=new Font("Segoe UI",12,FontStyle.Bold);
   grid.Columns.Add(delete);
   // Duas linhas independentes garantem que o título nunca cubra o cabeçalho.
   var listLayout=new TableLayoutPanel{Name="amzgPdvGridLayout",Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,Padding=Padding.Empty,BackColor=Color.White};
   listLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
   listLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var listTitle=new Label{Name="amzgPdvListTitle",Text="Produtos da venda",Dock=DockStyle.Fill,Margin=Padding.Empty,BackColor=DeepNavy,ForeColor=Color.White,Font=new Font("Segoe UI",13,FontStyle.Bold),Padding=new Padding(9,0,0,0),TextAlign=ContentAlignment.MiddleLeft};
   listLayout.Controls.Add(listTitle,0,0);listLayout.Controls.Add(grid,0,1);
   listArea.Controls.Add(listLayout);listLayout.BringToFront();Outline(listArea);main.Controls.Add(listArea,0,0);
   var side=new TableLayoutPanel{Name="amzgPdvActions",Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=new Padding(3,0,0,0)};
   side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.Absolute,75));side.RowStyles.Add(new RowStyle(SizeType.Absolute,66));side.RowStyles.Add(new RowStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.Absolute,105));side.RowStyles.Add(new RowStyle(SizeType.Absolute,52));
   received.Name="amzgPdvReceived";received.Font=new Font("Segoe UI",19,FontStyle.Bold);
   var summary=Cell("Recebido (R$)",received);summary.Name="amzgPdvSummary";summary.BackColor=Pale;side.Controls.Add(summary,0,0);
   change.Name="amzgPdvChange";change.AutoSize=false;change.Dock=DockStyle.Fill;change.Font=new Font("Segoe UI",15,FontStyle.Bold);change.TextAlign=ContentAlignment.MiddleCenter;change.BackColor=Pale;side.Controls.Add(change,0,1);
   var totalFrame=new Panel{Name="amzgPdvTotalFrame",Dock=DockStyle.Fill,BackColor=DeepNavy,Margin=new Padding(3)};
   var totalTitle=new Label{Text="TOTAL DA VENDA (R$)",Dock=DockStyle.Top,Height=29,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold)};
   total.Name="amzgPdvTotal";total.AutoSize=false;total.Dock=DockStyle.Fill;total.BackColor=DeepNavy;total.ForeColor=Color.White;total.Font=new Font("Segoe UI",26,FontStyle.Bold);total.TextAlign=ContentAlignment.MiddleCenter;total.AutoEllipsis=true;
   totalFrame.Controls.Add(total);totalFrame.Controls.Add(totalTitle);side.Controls.Add(totalFrame,0,3);Rounded(totalFrame,9);
   ButtonTheme(finish,"Finalizar Venda",Red,Color.White,44);finish.Name="amzgPdvFinish";finish.Dock=DockStyle.Fill;finish.Margin=new Padding(3);side.Controls.Add(finish,0,4);
   main.Controls.Add(side,1,0);root.Controls.Add(main,0,2);
   var hidden=new Panel{Name="amzgPdvHiddenState",Visible=false,Size=new Size(1,1)};
   foreach(Control c in new Control[]{add,edit,chooseCustomer,fiado,remove,clear,otherPayments,newSale,whatsApp,stats,discountLabel,discountMode,discountInput})hidden.Controls.Add(c);
   add.Name="amzgPdvAddProduct";edit.Name="amzgPdvEdit";fiado.Name="amzgPdvFiado";otherPayments.Name="amzgPdvOtherPayments";newSale.Name="amzgPdvNewSale";whatsApp.Name="amzgPdvWhatsApp";stats.Name="amzgPdvSaleStats";
   form.Controls.Remove(header);form.Controls.Remove(footer);header.Dispose();footer.Dispose();form.Controls.Add(hidden);form.Controls.Add(root);root.BringToFront();
   var suggestion=form.Controls.Find("pdvSuggestions",true);if(suggestion.Length>0)suggestion[0].BringToFront();form.ResumeLayout(true);
  }

  public static void Apply(PdvForm form,FlowLayoutPanel header,FlowLayoutPanel footer,
   Panel listArea,FlowLayoutPanel rapidInventoryBar,
   TextBox code,NumericUpDown qty,ComboBox saleUnit,Button add,
   Button finish,Button edit,Button chooseCustomer,Button openFiado,Button remove,Button clear,
   Button otherPayments,Button newSale,Button whatsApp,Label saleStats,
   TextBox customer,TextBox received,Label changeLabel,
   Label discountLabel,ComboBox discountMode,NumericUpDown discountInput,Label total,
   DataGridView grid,DataGridView stockView,bool manualEntry){
   if(!manualEntry){ApplyWide(form,header,footer,listArea,code,qty,saleUnit,add,finish,edit,chooseCustomer,openFiado,remove,clear,otherPayments,newSale,whatsApp,saleStats,customer,received,changeLabel,discountLabel,discountMode,discountInput,total,grid);return;}
   form.SuspendLayout();
   form.BackColor=Surface;
   form.MinimumSize=new Size(860,570);
   form.Font=new Font("Segoe UI",9.5f,FontStyle.Regular);
   var layout=new TableLayoutPanel{Name="amzgPdvLayout",Dock=DockStyle.Fill,
    BackColor=Surface,ColumnCount=1,RowCount=manualEntry?7:6,
    Padding=new Padding(7,0,7,4),Margin=Padding.Empty};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,35)); // marca
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); // voltar
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); // cliente, recebido, desconto
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); // produto compacto
   if(manualEntry)layout.RowStyles.Add(new RowStyle(SizeType.Absolute,49));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); // lista ampliada e acoes
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,20)); // contagem de itens
   int row=0;

   var branding=new Panel{Name="amzgPdvBrand",Dock=DockStyle.Fill,
    BackColor=DeepNavy,Margin=new Padding(0)};
   branding.Paint+=(sender,args)=>{
    using(var brush=new LinearGradientBrush(branding.ClientRectangle,
      Color.FromArgb(14,68,157),Color.FromArgb(9,43,102),0f))
     args.Graphics.FillRectangle(brush,branding.ClientRectangle);
    args.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
    using(var red=new Pen(Color.FromArgb(217,39,64),5f))
     args.Graphics.DrawLine(red,branding.Width-195,branding.Height-5,branding.Width,branding.Height-5);
    using(var pen=new Pen(Color.White,2.1f)){
     int x=Math.Max(18,(branding.Width-385)/2), y=5;
     args.Graphics.DrawLine(pen,x,y,x+7,y);
     args.Graphics.DrawLine(pen,x+7,y,x+12,y+20);
     args.Graphics.DrawLine(pen,x+12,y+20,x+35,y+20);
     args.Graphics.DrawLine(pen,x+10,y+4,x+38,y+4);
     args.Graphics.DrawLine(pen,x+38,y+4,x+33,y+16);
     args.Graphics.FillEllipse(Brushes.White,x+14,y+23,5,5);
     args.Graphics.FillEllipse(Brushes.White,x+30,y+23,5,5);
    }
   };
   var brandText=new Label{Text=manualEntry?"Armazém Gratidão - Vendas Anotadas":"Armazém Gratidão - PDV",
    AutoSize=false,Dock=DockStyle.Fill,
    TextAlign=ContentAlignment.MiddleCenter,BackColor=Color.Transparent,
    Font=new Font("Segoe UI",14f,FontStyle.Bold),ForeColor=Color.White};
   branding.Controls.Add(brandText);
   layout.Controls.Add(branding,0,row++);

   // O MainForm coloca aqui o botao Voltar ao menu, que deve ocupar a primeira linha.
   var backSlot=new Panel{Name="pdvBackSlot",Dock=DockStyle.Fill,BackColor=Surface,
    Margin=new Padding(0,1,0,1)};
   layout.Controls.Add(backSlot,0,row++);
   int summaryRow=row++;

   var searchFrame=new Panel{Name="amzgPdvSearchBar",Dock=DockStyle.Fill,
    BackColor=Pale,Padding=new Padding(6,2,6,2),
    Margin=new Padding(0,0,0,4)};
   Outline(searchFrame);
   var search=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,
    BackColor=Pale,Margin=Padding.Empty,Padding=Padding.Empty};
   search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
   search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,13));
   search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,12));
   search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,23));
   search.RowStyles.Add(new RowStyle(SizeType.Absolute,19));
   search.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   string[] names={"Código / Nome do Produto","Qtd.","Unidade",""};
   for(int i=0;i<4;i++)search.Controls.Add(TextLabel(names[i]),i,0);
   code.Name="amzgPdvCodeInput";
   code.Font=new Font("Segoe UI",10.5f,FontStyle.Regular);
   code.Dock=DockStyle.Fill;
   code.Margin=new Padding(4,1,5,1);
   code.BorderStyle=BorderStyle.FixedSingle;
   code.BackColor=Color.White;
   Rounded(code,9);
   qty.Name="amzgPdvQtyInput";
   qty.Dock=DockStyle.Fill;qty.Margin=new Padding(4,1,5,1);
   qty.Font=new Font("Segoe UI",11,FontStyle.Regular);
   qty.TextAlign=HorizontalAlignment.Center;
   saleUnit.Name="amzgPdvUnitInput";
   saleUnit.Dock=DockStyle.Fill;saleUnit.Margin=new Padding(4,1,5,1);
   saleUnit.Font=new Font("Segoe UI",11,FontStyle.Bold);
   saleUnit.FlatStyle=FlatStyle.Flat;
   ButtonTheme(add,"+  Inserir (Enter)",Navy,Color.White,28);
   add.Name="amzgPdvAddProduct";
   add.Dock=DockStyle.Fill;add.Margin=new Padding(4,0,4,1);
   search.Controls.Add(code,0,1);
   search.Controls.Add(qty,1,1);
   search.Controls.Add(saleUnit,2,1);
   search.Controls.Add(add,3,1);
   searchFrame.Controls.Add(search);
   layout.Controls.Add(searchFrame,0,row++);

   if(manualEntry&&rapidInventoryBar!=null){
    rapidInventoryBar.Dock=DockStyle.Fill;
    rapidInventoryBar.Margin=new Padding(0,0,0,5);
    rapidInventoryBar.Padding=new Padding(5,4,5,3);
    rapidInventoryBar.BackColor=Pale;
    foreach(Control child in rapidInventoryBar.Controls){
     var button=child as Button;
     if(button!=null)Rounded(button,8);
    }
    layout.Controls.Add(rapidInventoryBar,0,row++);
   }

   var main=new TableLayoutPanel{Name="amzgPdvMain",Dock=DockStyle.Fill,
    ColumnCount=2,RowCount=1,Margin=new Padding(0,2,0,2),
    BackColor=Surface};
   main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));
   main.RowStyles.Add(new RowStyle(SizeType.Percent,100));

   listArea.Dock=DockStyle.Fill;
   listArea.Padding=new Padding(2);
   listArea.Margin=new Padding(0,0,6,0);
   listArea.BackColor=Color.White;
   listArea.Name="amzgPdvItemBox";
   Outline(listArea);
   grid.Dock=DockStyle.Fill;grid.BackgroundColor=Color.White;
   grid.BorderStyle=BorderStyle.None;
   grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
   grid.ColumnHeadersDefaultCellStyle.BackColor=Navy;
   grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.White;
   grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9,FontStyle.Bold);
   grid.DefaultCellStyle.ForeColor=Color.FromArgb(28,57,96);
   grid.Font=new Font("Segoe UI",9.5f,FontStyle.Regular);
   grid.ColumnHeadersHeight=29;
   grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
   grid.RowTemplate.Height=24;
   grid.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
   grid.DataBindingComplete+=(sender,args)=>{
    foreach(DataGridViewColumn col in grid.Columns){
     switch(col.Name){
      case "Codigo": col.HeaderText="Código";col.MinimumWidth=108;col.FillWeight=110;break;
      case "Produto": col.HeaderText="Produto";col.MinimumWidth=185;col.FillWeight=260;break;
      case "Quantidade": col.HeaderText="Qtd.";col.FillWeight=75;break;
      case "Unidade": col.HeaderText="UN";col.FillWeight=50;break;
      case "Preco": col.HeaderText="Preço (R$)";col.FillWeight=90;break;
      case "Total": col.HeaderText="Total (R$)";col.FillWeight=95;break;
      case "ExcluirItem": col.Visible=false;break; // Remover Item permanece na lateral.
     }
     if(col.Name=="Quantidade"||col.Name=="Unidade")col.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleCenter;
     if(col.Name=="Preco"||col.Name=="Total"){col.DefaultCellStyle.Format="N2";col.DefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleRight;}
    }
    grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None;
    foreach(DataGridViewRow item in grid.Rows)item.Height=24;
   };
   var empty=new Label{Name="amzgPdvEmptyState",AutoSize=false,
    Text="🛒\r\n\r\nNenhum produto adicionado.\r\nUtilize o campo acima para incluir itens na venda.",
    Font=new Font("Segoe UI",11,FontStyle.Regular),
    ForeColor=Color.FromArgb(112,125,151),
    TextAlign=ContentAlignment.MiddleCenter,
    BackColor=Color.White};
   listArea.Controls.Add(empty);
   Action positionEmpty=()=>{
    if(empty.IsDisposed)return;
    empty.Bounds=new Rectangle(Math.Max(7,(listArea.ClientSize.Width-390)/2),
      Math.Max(54,(listArea.ClientSize.Height-155)/2),Math.Min(390,Math.Max(40,listArea.ClientSize.Width-20)),126);
    empty.Visible=grid.Rows.Count==0&&!stockView.Visible;
    if(empty.Visible)empty.BringToFront();
   };
   listArea.Resize+=(sender,args)=>positionEmpty();
   grid.DataBindingComplete+=(sender,args)=>positionEmpty();
   stockView.VisibleChanged+=(sender,args)=>positionEmpty();
   positionEmpty();
   main.Controls.Add(listArea,0,0);

   var side=new Panel{Name="amzgPdvActions",Dock=DockStyle.Fill,
    BackColor=Surface,Margin=Padding.Empty,Padding=new Padding(3)};
   var totalFrame=new Panel{Name="amzgPdvTotalFrame",Dock=DockStyle.Bottom,Height=70,BackColor=Red,Margin=Padding.Empty};
   var totalTitle=new Label{Text="Total da Venda (R$)",Dock=DockStyle.Top,Height=23,
    BackColor=Navy,ForeColor=Color.White,TextAlign=ContentAlignment.MiddleCenter,
    Font=new Font("Segoe UI",10,FontStyle.Bold)};
   total.Name="amzgPdvTotal";total.AutoSize=false;total.Dock=DockStyle.Fill;
   total.BackColor=Red;total.ForeColor=Color.White;total.Font=new Font("Segoe UI",24f,FontStyle.Bold);
   total.TextAlign=ContentAlignment.MiddleCenter;total.AutoEllipsis=true;
   totalFrame.Controls.Add(total);totalFrame.Controls.Add(totalTitle);Rounded(totalFrame,9);
   var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
   var buttons=new FlowLayoutPanel{Name="amzgPdvActionStack",Dock=DockStyle.Top,
    FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
    BackColor=Surface,Margin=Padding.Empty,Padding=new Padding(1,0,1,3)};
   scroll.Controls.Add(buttons);
   side.Controls.Add(scroll);side.Controls.Add(totalFrame);
   const int actionHeight=30;
   ButtonTheme(edit,"✎  Editar Qtd. / Preço",Navy,Color.White,actionHeight);edit.Name="amzgPdvEdit";
   ButtonTheme(remove,"⊖  Remover Item",Navy,Color.White,actionHeight);remove.Name="amzgPdvRemove";
   ButtonTheme(clear,"▤  Excluir Todos",Navy,Color.White,actionHeight);clear.Name="amzgPdvClear";
   ButtonTheme(openFiado,"Fiado",Navy,Color.White,actionHeight);openFiado.Name="amzgPdvFiado";
   ButtonTheme(otherPayments,"Outras Formas",Navy,Color.White,actionHeight);otherPayments.Name="amzgPdvOtherPayments";
   ButtonTheme(newSale,"Nova Venda",Navy,Color.White,actionHeight);newSale.Name="amzgPdvNewSale";
   ButtonTheme(whatsApp,"Enviar WhatsApp",Navy,Color.White,actionHeight);whatsApp.Name="amzgPdvWhatsApp";
   ButtonTheme(finish,"▣  Finalizar Venda",Red,Color.White,actionHeight);finish.Name="amzgPdvFinish";
   var others=new Button{Name="amzgPdvMoreOptions"};
   ButtonTheme(others,"⚙  Outras Opções",Navy,Color.White,actionHeight);others.Name="amzgPdvMoreOptions";
   var options=new ContextMenuStrip();
   options.Items.Add("Selecionar cliente",null,(sender,args)=>chooseCustomer.PerformClick());
   options.Items.Add("Ajustar desconto",null,(sender,args)=>discountInput.Focus());
   options.Items.Add("Excluir todos os itens",null,(sender,args)=>clear.PerformClick());
   if(manualEntry&&rapidInventoryBar!=null)
    options.Items.Add("Todos os Produtos no PDV",null,(sender,args)=>{
     foreach(Control child in rapidInventoryBar.Controls)
      if(child is Button&&child.Text.IndexOf("TODOS OS PRODUTOS",StringComparison.OrdinalIgnoreCase)>=0){
       ((Button)child).PerformClick();break;
      }
    });
   others.Click+=(sender,args)=>options.Show(others,new Point(0,others.Height));
   form.FormClosed+=(sender,args)=>options.Dispose();
   foreach(Button btn in new[]{edit,remove,clear,openFiado,otherPayments,newSale,whatsApp,others,finish}){
    btn.Dock=DockStyle.None;btn.Margin=new Padding(2,1,2,1);
    btn.Font=new Font("Segoe UI",9f,FontStyle.Bold);
    buttons.Controls.Add(btn);
   }
   Action fitSide=()=>{
    int count=buttons.Controls.Count;
    int height=Math.Max(24,Math.Min(30,(scroll.ClientSize.Height-8)/Math.Max(1,count)-2));
    int width=Math.Max(115,scroll.ClientSize.Width-(scroll.VerticalScroll.Visible?SystemInformation.VerticalScrollBarWidth:0)-7);
    foreach(Control item in buttons.Controls){
     if(item.Width!=width)item.Width=width;
     if(item.Height!=height)item.Height=height;
    }
   };
   scroll.Resize+=(sender,args)=>fitSide();
   form.Shown+=(sender,args)=>fitSide();
   fitSide();
   main.Controls.Add(side,1,0);
   layout.Controls.Add(main,0,row++);

   var summary=new Panel{Name="amzgPdvSummary",Dock=DockStyle.Fill,
    BackColor=Pale,Margin=new Padding(0,0,0,3),Padding=new Padding(5,3,5,3)};
   Outline(summary);
   var top=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=8,RowCount=1,Margin=Padding.Empty,BackColor=Pale};
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,58));
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,37));
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,72));
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,72));
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,19));
   top.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   top.Controls.Add(TextLabel("Cliente:"),0,0);
   customer.Name="amzgPdvCustomer";customer.Dock=DockStyle.Fill;customer.Margin=new Padding(1,2,4,1);
   customer.BackColor=Color.White;customer.Cursor=Cursors.Hand;customer.Font=new Font("Segoe UI",10f,FontStyle.Bold);
   customer.Click+=(sender,args)=>chooseCustomer.PerformClick();
   top.Controls.Add(customer,1,0);
   ButtonTheme(chooseCustomer,"Clientes",Navy,Color.White,30);
   chooseCustomer.Name="amzgPdvChooseCustomer";chooseCustomer.Dock=DockStyle.Fill;
   chooseCustomer.Margin=new Padding(1,2,4,2);
   top.Controls.Add(chooseCustomer,2,0);
   top.Controls.Add(TextLabel("Recebido:"),3,0);
   received.Name="amzgPdvReceived";received.Dock=DockStyle.Fill;received.Margin=new Padding(1,2,4,1);
   received.Font=new Font("Segoe UI",10f);
   top.Controls.Add(received,4,0);
   top.Controls.Add(TextLabel("Desconto:"),5,0);
   var discountGroup=new TableLayoutPanel{ColumnCount=2,RowCount=1,Dock=DockStyle.Fill,Margin=new Padding(1,2,3,1)};
   discountGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,43));
   discountGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   discountMode.Dock=DockStyle.Fill;discountMode.Margin=new Padding(0,0,2,0);
   discountInput.Dock=DockStyle.Fill;discountInput.Margin=Padding.Empty;
   discountMode.Font=new Font("Segoe UI",9f);discountInput.Font=new Font("Segoe UI",10f);
   discountGroup.Controls.Add(discountMode,0,0);discountGroup.Controls.Add(discountInput,1,0);
   top.Controls.Add(discountGroup,6,0);
   changeLabel.Name="amzgPdvChange";changeLabel.Dock=DockStyle.Fill;changeLabel.AutoSize=false;
   changeLabel.TextAlign=ContentAlignment.MiddleCenter;changeLabel.Font=new Font("Segoe UI",9f,FontStyle.Bold);
   changeLabel.Padding=Padding.Empty;changeLabel.Margin=new Padding(2,0,2,0);
   top.Controls.Add(changeLabel,7,0);
   summary.Controls.Add(top);
   layout.Controls.Add(summary,0,summaryRow);
   saleStats.Name="amzgPdvSaleStats";saleStats.Dock=DockStyle.Fill; saleStats.AutoSize=false;
   saleStats.TextAlign=ContentAlignment.MiddleLeft;saleStats.Font=new Font("Segoe UI",9,FontStyle.Bold);
   saleStats.ForeColor=DeepNavy;saleStats.Padding=new Padding(5,0,0,0);
   layout.Controls.Add(saleStats,0,row++);

   // Os labels antigos do layout em FlowLayoutPanel sao descartados;
   // nenhum botao/evento/controle de dados e substituido por outro.
   form.Controls.Remove(header);form.Controls.Remove(footer);
   if(rapidInventoryBar!=null)form.Controls.Remove(rapidInventoryBar);
   header.Dispose();footer.Dispose();
   form.Controls.Add(layout);
   layout.BringToFront();
   // Sugestoes de codigo continuam sobre os demais controles.
   var suggestion=form.Controls.Find("pdvSuggestions",true);
   if(suggestion.Length>0)suggestion[0].BringToFront();
   form.ResumeLayout(true);
  }
 }
}
