using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace ArmazemGratidao {
 public static class PaymentFees {
  public static readonly string[] Methods={"DINHEIRO","PIX","PIX_QR","PIX_SEM_QR","DEBITO","CREDITO","ALIMENTACAO","ALELO","PLUXXE","TICKET","VR"};
  static SQLiteConnection Open(string db){var c=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=db,FailIfMissing=true}.ToString());c.Open();return c;}
  public static void Prepare(SQLiteConnection c){
   using(var q=new SQLiteCommand("CREATE TABLE IF NOT EXISTS payment_fee_rates (payment_method TEXT PRIMARY KEY,percent NUMERIC NOT NULL DEFAULT 0 CHECK(percent>=0 AND percent<=100),updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP)",c))q.ExecuteNonQuery();
  }
  public static Dictionary<string,decimal> Load(SQLiteConnection c){
   var d=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
   foreach(string method in Methods)d[method]=0m;
   using(var check=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='payment_fee_rates'",c)){
    if(Convert.ToInt32(check.ExecuteScalar())==0)return d;
   }
   using(var q=new SQLiteCommand("SELECT payment_method,percent FROM payment_fee_rates",c))
   using(var reader=q.ExecuteReader())while(reader.Read()){
    string name=Convert.ToString(reader[0]);if(d.ContainsKey(name))d[name]=Convert.ToDecimal(reader[1],System.Globalization.CultureInfo.InvariantCulture);
   }
   return d;
  }
  public static decimal Fee(Dictionary<string,decimal> rates,string method,decimal amount){
   decimal rate;if(!rates.TryGetValue(method,out rate))rate=0;
   return Math.Round(amount*rate/100m,2,MidpointRounding.AwayFromZero);
  }
  public static void ShowEditor(string db,IWin32Window owner){
   if(!File.Exists(db)){MessageBox.Show("Banco de dados não encontrado.");return;}
   try{
    using(var c=Open(db)){Prepare(c);}
    using(var c=Open(db)){
     var rates=Load(c);
     using(var dlg=new Form{Text="Configurar Taxas de Cartões e Pagamentos",Width=560,Height=660,MinimumSize=new Size(510,570),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(229,242,255)}){
      var intro=new Label{Dock=DockStyle.Top,Height=77,Padding=new Padding(15,10,15,0),Text="Configure o percentual cobrado pela operadora para cada forma.\n0,00% significa sem taxa. A taxa é descontada do valor líquido esperado, não do valor pago pelo cliente.",Font=new Font("Segoe UI",10,FontStyle.Bold)};
      var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=Methods.Length,AutoScroll=true,Padding=new Padding(15),BackColor=Color.White};
      panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
      var fields=new Dictionary<string,NumericUpDown>();
      foreach(string name in Methods){
       var label=new Label{Text=name,AutoSize=true,Anchor=AnchorStyles.Left,Font=new Font("Segoe UI",10,FontStyle.Bold)};
       var number=new NumericUpDown{DecimalPlaces=3,Increment=.1m,Minimum=0,Maximum=100,Width=155,Value=Math.Min(100,Math.Max(0,rates[name]))};
       fields[name]=number;
       panel.Controls.Add(label);panel.Controls.Add(number);
      }
      var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=64,Padding=new Padding(12),FlowDirection=FlowDirection.RightToLeft};
      var save=new Button{Text="Salvar taxas",Width=180,Height=39,BackColor=Color.FromArgb(24,86,169),ForeColor=Color.White};
      var cancel=new Button{Text="Cancelar",Width=115,Height=39};cancel.Click+=(s,e)=>dlg.Close();
      save.Click+=(s,e)=>{
       try{
        using(var target=Open(db)){
         string backup=db+".antes-taxas-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".db";
         using(var dest=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
          dest.Open();target.BackupDatabase(dest,"main","main",-1,null,0);
         }
         using(var tx=target.BeginTransaction()){
          foreach(string name in Methods)
           using(var command=new SQLiteCommand("INSERT OR REPLACE INTO payment_fee_rates(payment_method,percent,updated_at) VALUES(@name,@percent,datetime('now','localtime'))",target,tx)){
            command.Parameters.AddWithValue("@name",name);command.Parameters.AddWithValue("@percent",fields[name].Value);command.ExecuteNonQuery();
           }
          tx.Commit();
         }
        }
        MessageBox.Show("Taxas salvas com backup. Serão aplicadas somente às próximas vendas.","Taxas");dlg.Close();
       }catch(Exception ex){MessageBox.Show("Taxas não salvas: "+ex.Message,"Taxas",MessageBoxButtons.OK,MessageBoxIcon.Error);}
      };
      footer.Controls.Add(save);footer.Controls.Add(cancel);
      dlg.Controls.Add(panel);dlg.Controls.Add(footer);dlg.Controls.Add(intro);dlg.ShowDialog(owner);
     }
    }
   }catch(Exception ex){MessageBox.Show("Não foi possível abrir as taxas: "+ex.Message,"Taxas",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}