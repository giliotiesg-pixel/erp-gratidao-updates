using System.Drawing;
using System.Drawing.Drawing2D;
using AMZG.Core;
namespace AMZG.App;
internal static class Program { [STAThread] static void Main(){ApplicationConfiguration.Initialize();Application.Run(new MainForm());} }
public sealed class MainForm:Form{
 readonly Panel menu=new(){Width=250,Dock=DockStyle.Left,BackColor=Color.White,AutoScroll=true};
 readonly Panel body=new(){Dock=DockStyle.Fill,BackColor=Color.FromArgb(244,246,248)};
 readonly Panel header=new(){Height=54,Dock=DockStyle.Top,BackColor=Color.FromArgb(15,61,102)};
 readonly Button toggle=new(){Text="☰",Width=52,Dock=DockStyle.Left,FlatStyle=FlatStyle.Flat,ForeColor=Color.White,BackColor=Color.FromArgb(15,61,102),Font=new Font("Segoe UI",14,FontStyle.Bold)};
 bool menuOpen=true;
 public MainForm(){
  Text="AMZ.G — Migração v2.1.9";WindowState=FormWindowState.Maximized;MinimumSize=new Size(1100,700);
  header.Controls.Add(new Label{Text="ARMAZÉM GRATIDÃO",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,Font=new Font("Segoe UI",15,FontStyle.Bold)});
  header.Controls.Add(toggle);toggle.BringToFront();toggle.Click+=(_,_)=>{menuOpen=!menuOpen;menu.Visible=menuOpen;};
  BuildMenu();Controls.Add(body);Controls.Add(menu);Controls.Add(header);header.BringToFront();OpenInicio();ApplyBoldStyle(this);Shown+=async (_,_)=>await CheckUpdateAsync(false);
 }
 void BuildMenu(){
  var f=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(10,14,10,14)};
  foreach(var name in new[]{"Início","PDV","Vendas","Caixa","Produtos","Estoque","Validade","Compras","Fiscal","Clientes / Fornecedores","Empresa","Fiado","Contas","Lucro","Relatórios","Ofertas","Cofre Digital","Configurações"}){
   var b=new RoundedButton{Radius=12,Text=name,Width=210,Height=34,Margin=new Padding(0,2,0,2),FlatStyle=FlatStyle.Flat,BackColor=name=="Início"?Color.FromArgb(15,61,102):Color.White,ForeColor=name=="Início"?Color.White:Color.FromArgb(32,33,36),Font=new Font("Segoe UI",9,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0)};
   b.FlatAppearance.BorderColor=Color.FromArgb(220,227,238);if(name=="Início")b.Click+=(_,_)=>OpenInicio();if(name=="PDV")b.Click+=(_,_)=>OpenPdv();if(name=="Configurações")b.Click+=(_,_)=>OpenSettings();f.Controls.Add(b);
  } menu.Controls.Add(f);
 }
 void OpenPdv(){
  body.Controls.Clear();
  var page=new Panel{Dock=DockStyle.Fill,Padding=new Padding(18),AutoScroll=true,BackColor=Color.FromArgb(244,246,248)};
  var title=new Label{Text="PDV — Ponto de Venda",Dock=DockStyle.Top,Height=42,Font=new Font("Segoe UI",20,FontStyle.Bold),ForeColor=Color.FromArgb(15,45,89)};
  var top=new TableLayoutPanel{Dock=DockStyle.Top,Height=92,ColumnCount=5,Padding=new Padding(0,8,0,8)};
  top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,105));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,155));top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));
  var search=new TextBox{Name="saleSearch",Dock=DockStyle.Fill,Font=new Font("Segoe UI",14,FontStyle.Bold),PlaceholderText="Digite a descrição ou bipe o código de barras"};
  var suggestions=new ListBox{Name="pdvProductSuggestions",Visible=false,Height=150,Font=new Font("Segoe UI",11,FontStyle.Bold),IntegralHeight=false};
  var productNames=new List<string>();
  void RefreshSuggestions(){
   var term=search.Text.Trim();suggestions.BeginUpdate();suggestions.Items.Clear();
   if(term.Length>0)foreach(var name in productNames.Where(x=>x.Contains(term,StringComparison.CurrentCultureIgnoreCase)).Take(12))suggestions.Items.Add(name);
   suggestions.EndUpdate();suggestions.Visible=suggestions.Items.Count>0;
  }
  search.TextChanged+=(_,_)=>RefreshSuggestions();
  suggestions.Click+=(_,_)=>{if(suggestions.SelectedItem is string name){search.Text=name;suggestions.Visible=false;search.Focus();search.SelectionStart=search.TextLength;}};
  suggestions.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter&&suggestions.SelectedItem is string name){search.Text=name;suggestions.Visible=false;search.Focus();e.SuppressKeyPress=true;}};
  search.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Down&&suggestions.Visible){suggestions.Focus();suggestions.SelectedIndex=0;e.SuppressKeyPress=true;}if(e.KeyCode==Keys.Escape)suggestions.Visible=false;};
  var qty=new NumericUpDown{Name="saleQuantity",Dock=DockStyle.Fill,Minimum=1,Maximum=9999,Value=1,DecimalPlaces=0,Font=new Font("Segoe UI",12,FontStyle.Bold)};
  var weight=new NumericUpDown{Name="saleWeight",Dock=DockStyle.Fill,Minimum=0,Maximum=9999,DecimalPlaces=3,Increment=.001M,Font=new Font("Segoe UI",12,FontStyle.Bold)};
  var consult=ActionButton("🔎 Consulta de produto",Color.White,Color.FromArgb(15,61,102));
  var add=ActionButton("+ Adicionar à venda",Color.FromArgb(15,61,102),Color.White);
  top.Controls.Add(Field("Código de barras / Produto",search),0,0);top.Controls.Add(Field("Quantidade",qty),1,0);top.Controls.Add(Field("Peso (kg)",weight),2,0);top.Controls.Add(consult,3,0);top.Controls.Add(add,4,0);

  var cartCard=Card();cartCard.Dock=DockStyle.Top;cartCard.Height=315;cartCard.Margin=new Padding(0,0,0,12);
  var cart=new DataGridView{Name="pdvCart",Dock=DockStyle.Fill,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None};
  foreach(var x in new[]{"Produto","Qtd.","Peso","Preço/kg","Preço","Total"})cart.Columns.Add("c"+cart.Columns.Count,x);
  cartCard.Controls.Add(cart);cartCard.Controls.Add(new Label{Text="Itens da venda",Dock=DockStyle.Top,Height=38,Padding=new Padding(12,9,0,0),Font=new Font("Segoe UI",12,FontStyle.Bold)});

  var bottom=new TableLayoutPanel{Dock=DockStyle.Top,Height=245,ColumnCount=2};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));
  var sale=Card();sale.Margin=new Padding(0,0,8,0);var form=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(14)};
  var customer=new ComboBox{Width=430,DropDownStyle=ComboBoxStyle.DropDown,Font=new Font("Segoe UI",11,FontStyle.Bold)};customer.Items.Add("Consumidor");customer.SelectedIndex=0;
  var discPct=new NumericUpDown{Width=150,Maximum=100,DecimalPlaces=2,Font=new Font("Segoe UI",11,FontStyle.Bold)};
  var discValue=new NumericUpDown{Width=150,Maximum=1000000,DecimalPlaces=2,Font=new Font("Segoe UI",11,FontStyle.Bold)};
  form.Controls.Add(new Label{Text="Cliente",AutoSize=true});form.Controls.Add(customer);
  var drow=new FlowLayoutPanel{Width=520,Height=65};drow.Controls.Add(Field("Desconto %",discPct));drow.Controls.Add(Field("Desconto R$",discValue));form.Controls.Add(drow);
  var pay=new ComboBox{Width=430,DropDownStyle=ComboBoxStyle.DropDownList,Font=new Font("Segoe UI",11,FontStyle.Bold)};
  pay.Items.AddRange(new object[]{"Dinheiro","Pix com QR Code","Pix sem QR Code","Cartão Débito","Cartão Crédito","Vale Pluxee","Vale VR","Vale Ticket","Vale Alelo","Fiado"});pay.SelectedIndex=0;
  form.Controls.Add(new Label{Text="Forma de pagamento",AutoSize=true});form.Controls.Add(pay);sale.Controls.Add(form);

  var totals=Card();totals.Margin=new Padding(8,0,0,0);var tf=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(16)};
  var subtotal=new Label{Text="Subtotal: R$ 0,00",AutoSize=true,Font=new Font("Segoe UI",13,FontStyle.Bold)};
  var discount=new Label{Text="Desconto: R$ 0,00",AutoSize=true,Font=new Font("Segoe UI",13,FontStyle.Bold)};
  var total=new Label{Text="TOTAL: R$ 0,00",AutoSize=true,Font=new Font("Segoe UI",24,FontStyle.Bold),ForeColor=Color.FromArgb(15,61,102)};
  var finish=ActionButton("FINALIZAR VENDA",Color.FromArgb(15,61,102),Color.White);finish.Width=280;finish.Height=45;
  tf.Controls.Add(subtotal);tf.Controls.Add(discount);tf.Controls.Add(total);tf.Controls.Add(finish);totals.Controls.Add(tf);
  bottom.Controls.Add(sale,0,0);bottom.Controls.Add(totals,1,0);

  decimal CartSubtotal(){decimal s=0;foreach(DataGridViewRow r in cart.Rows)if(r.Cells[5].Value is decimal d)s+=d;else if(decimal.TryParse(Convert.ToString(r.Cells[5].Value),out var x))s+=x;return s;}
  void Recalc(){var s=CartSubtotal();var dv=Math.Min(s,discValue.Value+s*discPct.Value/100M);subtotal.Text=$"Subtotal: {s:C2}";discount.Text=$"Desconto: {dv:C2}";total.Text=$"TOTAL: {Math.Max(0,s-dv):C2}";}
  add.Click+=(_,_)=>{if(string.IsNullOrWhiteSpace(search.Text)){MessageBox.Show("Informe ou bipe um produto.","PDV");search.Focus();return;}decimal unit=0;decimal line=0;var q=qty.Value;var w=weight.Value;line=unit*(w>0?w:q);cart.Rows.Add(search.Text,q,w,unit,unit,line);search.Clear();qty.Value=1;weight.Value=0;Recalc();search.Focus();};
  search.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){add.PerformClick();e.SuppressKeyPress=true;}};
  discPct.ValueChanged+=(_,_)=>Recalc();discValue.ValueChanged+=(_,_)=>Recalc();
  consult.Click+=(_,_)=>MessageBox.Show(string.IsNullOrWhiteSpace(search.Text)?"Bipe ou informe o código do produto.":"Consulta: "+search.Text,"Consulta de produto");
  finish.Click+=(_,_)=>{if(cart.Rows.Count==0){MessageBox.Show("Adicione produtos antes de finalizar.","PDV");return;}MessageBox.Show("Estrutura do PDV pronta. A gravação da venda no SQLite será conectada na próxima etapa.","PDV");};

  page.Controls.Add(bottom);page.Controls.Add(cartCard);page.Controls.Add(top);page.Controls.Add(title);body.Controls.Add(page);page.Controls.Add(suggestions);
  void PositionSuggestions(){var pt=page.PointToClient(search.PointToScreen(new Point(0,search.Height)));suggestions.SetBounds(pt.X,pt.Y,Math.Max(260,search.Width),150);suggestions.BringToFront();}
  page.Layout+=(_,_)=>PositionSuggestions();top.Layout+=(_,_)=>PositionSuggestions();PositionSuggestions();ApplyBoldStyle(page);search.Focus();
 }
 Control Field(string label,Control input){var p=new Panel{Width=input.Width>0?Math.Max(input.Width,100):180,Height=66,Margin=new Padding(4)};var l=new Label{Text=label,Dock=DockStyle.Top,Height=24,Font=new Font("Segoe UI",9,FontStyle.Bold)};input.Dock=DockStyle.Bottom;input.Height=34;p.Controls.Add(input);p.Controls.Add(l);return p;}
 RoundedButton ActionButton(string text,Color back,Color fore){var b=new RoundedButton{Radius=12,Text=text,Dock=DockStyle.Fill,Margin=new Padding(5,24,5,5),FlatStyle=FlatStyle.Flat,BackColor=back,ForeColor=fore,Font=new Font("Segoe UI",9,FontStyle.Bold)};b.FlatAppearance.BorderColor=Color.FromArgb(190,204,220);return b;}
 void ApplyBoldStyle(Control root){
  foreach(Control x in root.Controls){
   if(x is Button || x is Label && (x.Font.Size>=11 || x.Dock==DockStyle.Top))x.Font=new Font(x.Font,x.Font.Style|FontStyle.Bold);
   if(x.HasChildren)ApplyBoldStyle(x);
  }
 }
 void OpenSettings(){
  body.Controls.Clear();var p=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(24),BackColor=Color.FromArgb(244,246,248)};
  p.Controls.Add(new Label{Text="Configurações",AutoSize=true,Font=new Font("Segoe UI",19,FontStyle.Bold),Margin=new Padding(0,0,0,14)});
  p.Controls.Add(new Label{Text="Atualizações do sistema",AutoSize=true,Font=new Font("Segoe UI",12,FontStyle.Bold),Margin=new Padding(0,0,0,8)});
  p.Controls.Add(new Label{Text="Versão instalada: "+AutomaticUpdater.CurrentVersion,AutoSize=true,ForeColor=Color.FromArgb(66,91,114),Margin=new Padding(0,0,0,10)});
  var b=new RoundedButton{Radius=12,Text="Verificar atualização",AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(15,61,102)};
  b.Click+=async (_,_)=>await CheckUpdateAsync(true);p.Controls.Add(b);body.Controls.Add(p);ApplyBoldStyle(p);
 }
 async Task CheckUpdateAsync(bool manual){
  try{
   var m=await AutomaticUpdater.CheckAsync();
   if(m is null){if(manual)MessageBox.Show("O AMZ.G já está atualizado.","Atualizações",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
   var answer=MessageBox.Show($"Nova atualização disponível: v{m.Version}\n\n{m.Notes}\n\nDeseja baixar e instalar agora?","Atualização disponível",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
   if(answer!=DialogResult.Yes)return;
   using var dlg=new Form{Text="Atualizando AMZ.G",Width=460,Height=150,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,ControlBox=false};
   var label=new Label{Text="Baixando e validando atualização...",Dock=DockStyle.Top,Height=45,Padding=new Padding(15,15,0,0)};
   var bar=new ProgressBar{Dock=DockStyle.Top,Height=24,Margin=new Padding(15)};dlg.Controls.Add(bar);dlg.Controls.Add(label);
   var progress=new Progress<int>(x=>bar.Value=Math.Clamp(x,0,100));dlg.Shown+=async (_,_)=>{try{var file=await AutomaticUpdater.DownloadAndValidateAsync(m,progress);label.Text="Backup concluído. Instalando atualização...";await Task.Delay(500);AutomaticUpdater.ApplyAndRestart(file);}catch(Exception ex){dlg.Close();MessageBox.Show("Não foi possível instalar a atualização.\n\n"+ex.Message,"Falha na atualização",MessageBoxButtons.OK,MessageBoxIcon.Error);}};dlg.ShowDialog(this);
  }catch(Exception ex){if(manual)MessageBox.Show("Não foi possível verificar atualizações.\n\n"+ex.Message,"Atualizações",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 }
 RoundedPanel Card(){return new RoundedPanel{Radius=16,Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(1)};}
 void OpenInicio(){
  body.Controls.Clear();
  var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(18),BackColor=Color.FromArgb(244,246,248)};
  var page=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1};page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  var h=new Panel{Height=74};
  var venda=new RoundedButton{Radius=13,Text="VENDA - IR AO PDV",Dock=DockStyle.Right,Width=190,Height=38,Margin=new Padding(8),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(15,61,102),ForeColor=Color.White,Font=new Font("Segoe UI",10,FontStyle.Bold)};
  venda.Click+=(_,_)=>OpenPdv();h.Controls.Add(venda);
  h.Controls.Add(new Label{Text="Faturamento",Dock=DockStyle.Top,Height=38,Font=new Font("Segoe UI",19,FontStyle.Bold)});h.Controls.Add(new Label{Text="Resumo das vendas e dos valores faturados.",Dock=DockStyle.Bottom,Height=28,ForeColor=Color.FromArgb(66,91,114),Font=new Font("Segoe UI",10)});page.Controls.Add(h);
  var cards=new TableLayoutPanel{Height=92,ColumnCount=4,Dock=DockStyle.Top,Margin=new Padding(0,0,0,18)};for(int i=0;i<4;i++)cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
  string[] labs={"Faturamento hoje","Vendas hoje","Lucro hoje","Faturamento do mês"}, vals={"R$ 0,00","0","R$ 0,00","R$ 0,00"};
  for(int i=0;i<4;i++){var c=Card();c.Margin=new Padding(i==0?0:7,0,i==3?0:7,0);c.Controls.Add(new Label{Text=vals[i],Dock=DockStyle.Fill,Padding=new Padding(14,14,0,0),Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Color.FromArgb(15,45,89)});c.Controls.Add(new Label{Text=labs[i],Dock=DockStyle.Top,Height=28,Padding=new Padding(14,8,0,0),ForeColor=Color.FromArgb(100,116,139)});cards.Controls.Add(c,i,0);}page.Controls.Add(cards);
  var two=new TableLayoutPanel{Height=285,ColumnCount=2,Dock=DockStyle.Top,Margin=new Padding(0,0,0,16)};two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,66.7f));two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.3f));
  two.Controls.Add(TableCard("Produtos mais vendidos hoje",new[]{"Produto","Quantidade","Total"},new[]{"Atualizar produtos mais vendidos"}),0,0);
  two.Controls.Add(TableCard("Produtos mais vendidos do mês",new[]{"Produto","Qtd. vendida","Vendas","Total"},new[]{"Atualizar","Histórico dos meses anteriores"}),1,0);page.Controls.Add(two);
  var recent=TableCard("Vendas recentes",new[]{"Data","Cliente","Pagamento","Total"},Array.Empty<string>());recent.Height=250;page.Controls.Add(recent);
  scroll.Controls.Add(page);body.Controls.Add(scroll);ApplyBoldStyle(scroll);
 }
 RoundedPanel TableCard(string title,string[] cols,string[] actions){
  var c=Card();c.Margin=new Padding(0,0,8,0);var head=new Panel{Dock=DockStyle.Top,Height=48,Padding=new Padding(10,7,10,5)};head.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",11,FontStyle.Bold)});
  if(actions.Length>0){var f=new FlowLayoutPanel{Dock=DockStyle.Right,AutoSize=true,WrapContents=false};foreach(var a in actions)f.Controls.Add(new RoundedButton{Radius=11,Text=a,AutoSize=true,Height=28,Margin=new Padding(4,0,0,0),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(15,61,102)});head.Controls.Add(f);f.BringToFront();}
  var g=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None};foreach(var x in cols)g.Columns.Add("c"+g.Columns.Count,x);c.Controls.Add(g);c.Controls.Add(head);return c;
 }
}
public sealed class RoundedPanel:Panel{public int Radius{get;set;}=16;protected override void OnResize(EventArgs e){base.OnResize(e);SetShape();}protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);SetShape();}void SetShape(){if(Width<2||Height<2)return;using var p=RoundRect(new Rectangle(0,0,Width,Height),Radius);Region=new Region(p);}protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var p=RoundRect(new Rectangle(0,0,Width-1,Height-1),Radius);using var pen=new Pen(Color.FromArgb(224,229,236));e.Graphics.DrawPath(pen,p);}internal static GraphicsPath RoundRect(Rectangle r,int radius){int d=Math.Max(2,radius*2);var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}}
public sealed class RoundedButton:Button{public int Radius{get;set;}=12;protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>1&&Height>1){using var p=RoundedPanel.RoundRect(new Rectangle(0,0,Width,Height),Radius);Region=new Region(p);}}}
