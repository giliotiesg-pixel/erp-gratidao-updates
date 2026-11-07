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

  public static void Apply(PdvForm form,FlowLayoutPanel header,FlowLayoutPanel footer,
   Panel listArea,FlowLayoutPanel rapidInventoryBar,
   TextBox code,NumericUpDown qty,ComboBox saleUnit,Button add,
   Button finish,Button edit,Button chooseCustomer,Button remove,Button clear,
   TextBox customer,NumericUpDown received,Label changeLabel,
   Label discountLabel,ComboBox discountMode,NumericUpDown discountInput,Label total,
   DataGridView grid,DataGridView stockView,bool manualEntry){
   form.SuspendLayout();
   form.BackColor=Surface;
   form.MinimumSize=new Size(980,600);
   form.Font=new Font("Segoe UI",9.5f,FontStyle.Regular);
   var layout=new TableLayoutPanel{Name="amzgPdvLayout",Dock=DockStyle.Fill,
    BackColor=Surface,ColumnCount=1,RowCount=manualEntry?6:5,
    Padding=new Padding(12,0,12,11),Margin=Padding.Empty};
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,53)); // marca azul
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); // voltar
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,87)); // produto
   if(manualEntry)layout.RowStyles.Add(new RowStyle(SizeType.Absolute,49)); // estoque no PDV
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); // lista e acoes
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,108)); // resumo
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
     int x=Math.Max(18,(branding.Width-385)/2), y=15;
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
    Font=new Font("Segoe UI",16f,FontStyle.Bold),ForeColor=Color.White};
   branding.Controls.Add(brandText);
   layout.Controls.Add(branding,0,row++);

   // O MainForm coloca aqui o botao Voltar ao menu, que deve ocupar a primeira linha.
   var backSlot=new Panel{Name="pdvBackSlot",Dock=DockStyle.Fill,BackColor=Surface,
    Margin=new Padding(0,1,0,1)};
   layout.Controls.Add(backSlot,0,row++);

   var searchFrame=new Panel{Name="amzgPdvSearchBar",Dock=DockStyle.Fill,
    BackColor=Pale,Padding=new Padding(10,5,10,5),
    Margin=new Padding(0,0,0,4)};
   Outline(searchFrame);
   var search=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,
    BackColor=Pale,Margin=Padding.Empty,Padding=Padding.Empty};
   search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));
   search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,13));
   search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,12));
   search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,23));
   search.RowStyles.Add(new RowStyle(SizeType.Absolute,22));
   search.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   string[] names={"Código / Nome do Produto","Qtd.","Unidade",""};
   for(int i=0;i<4;i++)search.Controls.Add(TextLabel(names[i]),i,0);
   code.Name="amzgPdvCodeInput";
   code.Font=new Font("Segoe UI",12f,FontStyle.Regular);
   code.Dock=DockStyle.Fill;
   code.Margin=new Padding(5,4,7,5);
   code.BorderStyle=BorderStyle.FixedSingle;
   code.BackColor=Color.White;
   Rounded(code,9);
   qty.Name="amzgPdvQtyInput";
   qty.Dock=DockStyle.Fill;qty.Margin=new Padding(5,5,6,6);
   qty.Font=new Font("Segoe UI",11,FontStyle.Regular);
   qty.TextAlign=HorizontalAlignment.Center;
   saleUnit.Name="amzgPdvUnitInput";
   saleUnit.Dock=DockStyle.Fill;saleUnit.Margin=new Padding(5,5,7,6);
   saleUnit.Font=new Font("Segoe UI",11,FontStyle.Bold);
   saleUnit.FlatStyle=FlatStyle.Flat;
   ButtonTheme(add,"⊕  Adicionar Produto",Red,Color.White,43);
   add.Name="amzgPdvAddProduct";
   add.Dock=DockStyle.Fill;add.Margin=new Padding(7,3,5,5);
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
    ColumnCount=2,RowCount=1,Margin=new Padding(0,4,0,6),
    BackColor=Surface};
   main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,264));
   main.RowStyles.Add(new RowStyle(SizeType.Percent,100));

   listArea.Dock=DockStyle.Fill;
   listArea.Padding=new Padding(2);
   listArea.Margin=new Padding(0,0,10,0);
   listArea.BackColor=Color.White;
   listArea.Name="amzgPdvItemBox";
   Outline(listArea);
   grid.Dock=DockStyle.Fill;grid.BackgroundColor=Color.White;
   grid.BorderStyle=BorderStyle.None;
   grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
   grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(224,235,250);
   grid.ColumnHeadersDefaultCellStyle.ForeColor=Color.FromArgb(29,59,104);
   grid.ColumnHeadersDefaultCellStyle.Font=new Font("Segoe UI",9,FontStyle.Bold);
   grid.DefaultCellStyle.ForeColor=Color.FromArgb(28,57,96);
   grid.RowTemplate.Height=34;
   grid.DataBindingComplete+=(sender,args)=>{
    foreach(DataGridViewColumn col in grid.Columns){
     switch(col.Name){
      case "Codigo": col.HeaderText="#";col.MinimumWidth=67;col.FillWeight=80;break;
      case "Produto": col.HeaderText="Produto";col.MinimumWidth=220;col.FillWeight=245;break;
      case "Quantidade": col.HeaderText="Qtd.";col.FillWeight=75;break;
      case "Unidade": col.HeaderText="UN";col.FillWeight=50;break;
      case "Preco": col.HeaderText="Preço (R$)";col.FillWeight=90;break;
      case "Total": col.HeaderText="Total (R$)";col.FillWeight=95;break;
      case "ExcluirItem": col.Visible=false;break; // Remover Item permanece na lateral.
     }
    }
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
    BackColor=Surface,Margin=new Padding(0),AutoScroll=true};
   var buttons=new FlowLayoutPanel{Name="amzgPdvActionStack",
    FlowDirection=FlowDirection.TopDown,WrapContents=false,
    AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
    BackColor=Surface,Margin=Padding.Empty,Padding=new Padding(2,1,1,1)};
   side.Controls.Add(buttons);
   ButtonTheme(finish,"▣  Finalizar Venda",Navy,Color.White,51);
   finish.Name="amzgPdvFinish";
   ButtonTheme(edit,"✎  Editar Qtd./Preço",Color.FromArgb(223,234,250),DeepNavy,44);
   edit.Name="amzgPdvEdit";
   ButtonTheme(chooseCustomer,"♙  Cliente / Cadastrar",Color.FromArgb(223,234,250),DeepNavy,44);
   chooseCustomer.Name="amzgPdvChooseCustomer";
   ButtonTheme(remove,"▤  Remover Item",Color.FromArgb(253,234,238),Red,44);
   remove.Name="amzgPdvRemove";
   ButtonTheme(clear,"▤  Excluir Todos",Color.FromArgb(224,235,249),DeepNavy,38);
   clear.Name="amzgPdvClear";
   foreach(Button btn in new[]{finish,edit,chooseCustomer,remove,clear}){
    btn.Dock=DockStyle.None;
    btn.Margin=new Padding(3,3,3,3);
    buttons.Controls.Add(btn);
   }
   // Botao de exclusao permanece menos proeminente, abaixo da separacao.
   buttons.Controls.SetChildIndex(clear,4);
   var separator=new Label{Name="amzgPdvActionDivider",Height=13,
    BorderStyle=BorderStyle.None,
    BackColor=Surface,Margin=new Padding(6,6,6,3)};
   buttons.Controls.Add(separator);
   buttons.Controls.SetChildIndex(separator,4);
   var others=new Button{Name="amzgPdvMoreOptions",Text="⚙  Outras Opções   ⌄",
    Height=37,FlatStyle=FlatStyle.Flat,ForeColor=DeepNavy,
    BackColor=Color.FromArgb(220,232,250),
    Font=new Font("Segoe UI",9.2f,FontStyle.Bold),Cursor=Cursors.Hand,
    Margin=new Padding(3,3,3,3)};
   others.FlatAppearance.BorderSize=0;
   Rounded(others,9);
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
   options.Opening+=(sender,args)=>others.Text="⚙  Outras Opções   ⌃";
   options.Closed+=(sender,args)=>others.Text="⚙  Outras Opções   ⌄";
   form.FormClosed+=(sender,args)=>options.Dispose();
   buttons.Controls.Add(others);
   Action fitSide=()=>{
    int width=Math.Max(190,side.ClientSize.Width-(side.VerticalScroll.Visible?18:0)-7);
    if(buttons.Width!=width)buttons.Width=width;
    foreach(Control b in buttons.Controls)if(b.Width!=width-7)b.Width=width-7;
   };
   side.Resize+=(sender,args)=>fitSide();
   buttons.ControlAdded+=(sender,args)=>fitSide();
   fitSide();
   main.Controls.Add(side,1,0);
   layout.Controls.Add(main,0,row++);

   var summary=new Panel{Name="amzgPdvSummary",Dock=DockStyle.Fill,
    BackColor=Color.FromArgb(231,240,252),
    Margin=new Padding(0,2,0,0),Padding=new Padding(10,9,10,8)};
   Outline(summary);
   var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5,
    RowCount=2,Margin=Padding.Empty,BackColor=summary.BackColor};
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,23));
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,13));
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,17));
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,22));
   bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
   bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,25));
   bottom.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   bottom.Controls.Add(TextLabel("Cliente:"),0,0);
   bottom.Controls.Add(TextLabel("Recebido:"),1,0);
   bottom.Controls.Add(TextLabel("Desconto:"),2,0);
   customer.Name="amzgPdvCustomer";
   customer.Dock=DockStyle.Fill;customer.Margin=new Padding(5,3,9,8);
   customer.BackColor=Color.White;
   customer.Cursor=Cursors.Hand;
   customer.Click+=(sender,args)=>chooseCustomer.PerformClick();
   Rounded(customer,8);
   received.Name="amzgPdvReceived";
   received.Dock=DockStyle.Fill;received.Margin=new Padding(5,3,8,8);
   received.Font=new Font("Segoe UI",10f);
   var discountGroup=new TableLayoutPanel{ColumnCount=2,RowCount=1,
    Dock=DockStyle.Fill,Margin=new Padding(3,3,6,8)};
   discountGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
   discountGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   discountMode.Dock=DockStyle.Fill;discountMode.Margin=new Padding(0,0,3,0);
   discountInput.Dock=DockStyle.Fill;discountInput.Margin=new Padding(0);
   discountMode.Font=new Font("Segoe UI",9f);
   discountInput.Font=new Font("Segoe UI",10f);
   discountGroup.Controls.Add(discountMode,0,0);
   discountGroup.Controls.Add(discountInput,1,0);
   bottom.Controls.Add(customer,0,1);
   bottom.Controls.Add(received,1,1);
   bottom.Controls.Add(discountGroup,2,1);
   changeLabel.Name="amzgPdvChange";
   changeLabel.Dock=DockStyle.Fill;
   changeLabel.AutoSize=false;
   changeLabel.TextAlign=ContentAlignment.MiddleCenter;
   changeLabel.Font=new Font("Segoe UI",14f,FontStyle.Bold);
   changeLabel.Margin=new Padding(4,0,4,0);
   bottom.Controls.Add(changeLabel,3,0);
   bottom.SetRowSpan(changeLabel,2);
   total.Name="amzgPdvTotal";
   total.AutoSize=false;
   total.Dock=DockStyle.Fill;total.Margin=new Padding(4,5,0,4);
   total.BackColor=Red;total.ForeColor=Color.White;
   total.Font=new Font("Segoe UI",17f,FontStyle.Bold);
   total.TextAlign=ContentAlignment.MiddleCenter;
   total.AutoEllipsis=true;
   Rounded(total,11);
   bottom.Controls.Add(total,4,0);bottom.SetRowSpan(total,2);
   summary.Controls.Add(bottom);
   layout.Controls.Add(summary,0,row++);

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
