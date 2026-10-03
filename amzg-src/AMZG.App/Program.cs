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
  BuildMenu();Controls.Add(body);Controls.Add(menu);Controls.Add(header);header.BringToFront();OpenInicio();Shown+=async (_,_)=>await CheckUpdateAsync(false);
 }
 void BuildMenu(){
  var f=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(10,14,10,14)};
  foreach(var name in new[]{"Início","PDV","Vendas","Caixa","Produtos","Estoque","Validade","Compras","Fiscal","Clientes / Fornecedores","Empresa","Fiado","Contas","Lucro","Relatórios","Ofertas","Cofre Digital","Configurações"}){
   var b=new RoundedButton{Radius=12,Text=name,Width=210,Height=34,Margin=new Padding(0,2,0,2),FlatStyle=FlatStyle.Flat,BackColor=name=="Início"?Color.FromArgb(15,61,102):Color.White,ForeColor=name=="Início"?Color.White:Color.FromArgb(32,33,36),Font=new Font("Segoe UI",9,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(10,0,0,0)};
   b.FlatAppearance.BorderColor=Color.FromArgb(220,227,238);if(name=="Início")b.Click+=(_,_)=>OpenInicio();if(name=="Configurações")b.Click+=(_,_)=>OpenSettings();f.Controls.Add(b);
  } menu.Controls.Add(f);
 }
 void OpenSettings(){
  body.Controls.Clear();var p=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(24),BackColor=Color.FromArgb(244,246,248)};
  p.Controls.Add(new Label{Text="Configurações",AutoSize=true,Font=new Font("Segoe UI",19,FontStyle.Bold),Margin=new Padding(0,0,0,14)});
  p.Controls.Add(new Label{Text="Atualizações do sistema",AutoSize=true,Font=new Font("Segoe UI",12,FontStyle.Bold),Margin=new Padding(0,0,0,8)});
  p.Controls.Add(new Label{Text="Versão instalada: "+AutomaticUpdater.CurrentVersion,AutoSize=true,ForeColor=Color.FromArgb(66,91,114),Margin=new Padding(0,0,0,10)});
  var b=new RoundedButton{Radius=12,Text="Verificar atualização",AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(15,61,102)};
  b.Click+=async (_,_)=>await CheckUpdateAsync(true);p.Controls.Add(b);body.Controls.Add(p);
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
  var h=new Panel{Height=74};h.Controls.Add(new Label{Text="Faturamento",Dock=DockStyle.Top,Height=38,Font=new Font("Segoe UI",19,FontStyle.Bold)});h.Controls.Add(new Label{Text="Resumo das vendas e dos valores faturados.",Dock=DockStyle.Bottom,Height=28,ForeColor=Color.FromArgb(66,91,114),Font=new Font("Segoe UI",10)});page.Controls.Add(h);
  var cards=new TableLayoutPanel{Height=92,ColumnCount=4,Dock=DockStyle.Top,Margin=new Padding(0,0,0,18)};for(int i=0;i<4;i++)cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
  string[] labs={"Faturamento hoje","Vendas hoje","Lucro hoje","Faturamento do mês"}, vals={"R$ 0,00","0","R$ 0,00","R$ 0,00"};
  for(int i=0;i<4;i++){var c=Card();c.Margin=new Padding(i==0?0:7,0,i==3?0:7,0);c.Controls.Add(new Label{Text=vals[i],Dock=DockStyle.Fill,Padding=new Padding(14,14,0,0),Font=new Font("Segoe UI",16,FontStyle.Bold),ForeColor=Color.FromArgb(15,45,89)});c.Controls.Add(new Label{Text=labs[i],Dock=DockStyle.Top,Height=28,Padding=new Padding(14,8,0,0),ForeColor=Color.FromArgb(100,116,139)});cards.Controls.Add(c,i,0);}page.Controls.Add(cards);
  var two=new TableLayoutPanel{Height=285,ColumnCount=2,Dock=DockStyle.Top,Margin=new Padding(0,0,0,16)};two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,66.7f));two.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.3f));
  two.Controls.Add(TableCard("Produtos mais vendidos hoje",new[]{"Produto","Quantidade","Total"},new[]{"Atualizar produtos mais vendidos"}),0,0);
  two.Controls.Add(TableCard("Produtos mais vendidos do mês",new[]{"Produto","Qtd. vendida","Vendas","Total"},new[]{"Atualizar","Histórico dos meses anteriores"}),1,0);page.Controls.Add(two);
  var recent=TableCard("Vendas recentes",new[]{"Data","Cliente","Pagamento","Total"},Array.Empty<string>());recent.Height=250;page.Controls.Add(recent);
  scroll.Controls.Add(page);body.Controls.Add(scroll);
 }
 RoundedPanel TableCard(string title,string[] cols,string[] actions){
  var c=Card();c.Margin=new Padding(0,0,8,0);var head=new Panel{Dock=DockStyle.Top,Height=48,Padding=new Padding(10,7,10,5)};head.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",11,FontStyle.Bold)});
  if(actions.Length>0){var f=new FlowLayoutPanel{Dock=DockStyle.Right,AutoSize=true,WrapContents=false};foreach(var a in actions)f.Controls.Add(new RoundedButton{Radius=11,Text=a,AutoSize=true,Height=28,Margin=new Padding(4,0,0,0),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Color.FromArgb(15,61,102)});head.Controls.Add(f);f.BringToFront();}
  var g=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None};foreach(var x in cols)g.Columns.Add("c"+g.Columns.Count,x);c.Controls.Add(g);c.Controls.Add(head);return c;
 }
}
public sealed class RoundedPanel:Panel{public int Radius{get;set;}=16;protected override void OnResize(EventArgs e){base.OnResize(e);SetShape();}protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);SetShape();}void SetShape(){if(Width<2||Height<2)return;using var p=RoundRect(new Rectangle(0,0,Width,Height),Radius);Region=new Region(p);}protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var p=RoundRect(new Rectangle(0,0,Width-1,Height-1),Radius);using var pen=new Pen(Color.FromArgb(224,229,236));e.Graphics.DrawPath(pen,p);}internal static GraphicsPath RoundRect(Rectangle r,int radius){int d=Math.Max(2,radius*2);var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}}
public sealed class RoundedButton:Button{public int Radius{get;set;}=12;protected override void OnResize(EventArgs e){base.OnResize(e);if(Width>1&&Height>1){using var p=RoundedPanel.RoundRect(new Rectangle(0,0,Width,Height),Radius);Region=new Region(p);}}}
