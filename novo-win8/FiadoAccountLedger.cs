using System;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace ArmazemGratidao {
 // Recebimentos sem venda vinculada: nao sao credito antecipado e nao podem
 // ser tratados como novos pagamentos quando o credito da carteira e utilizado.
 internal static class FiadoAccountLedger {
  internal const string CreditFilter="(UPPER(COALESCE(s.payment_method,'')) LIKE '%FIADO%' OR UPPER(COALESCE(s.status,'')) LIKE '%FIADO%' OR EXISTS (SELECT 1 FROM sale_payments fp WHERE fp.sale_id=s.id AND UPPER(TRIM(COALESCE(fp.payment_method,''))) LIKE '%FIADO%'))";
  internal const string SaleBalance="COALESCE(s.gross_total,0)-COALESCE((SELECT SUM(p.amount) FROM credit_payments p WHERE p.sale_id=s.id),0)";
  internal static bool Exists(SQLiteConnection cn,SQLiteTransaction tx=null) {
   using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='fiado_account_receipts'",cn,tx))
    return Convert.ToInt64(cmd.ExecuteScalar())>0;
  }
  internal static void CreateTables(SQLiteConnection cn,SQLiteTransaction tx){
   using(var cmd=new SQLiteCommand(@"CREATE TABLE IF NOT EXISTS fiado_account_receipts(
    id INTEGER PRIMARY KEY AUTOINCREMENT,receipt_key TEXT NOT NULL UNIQUE,
    customer_name TEXT NOT NULL COLLATE NOCASE,amount NUMERIC NOT NULL CHECK(amount>0),
    received_at TEXT NOT NULL,method TEXT NOT NULL,description TEXT NOT NULL,
    balance_before NUMERIC NOT NULL,balance_after NUMERIC NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now','localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
   )",cn,tx))cmd.ExecuteNonQuery();
   using(var cmd=new SQLiteCommand(@"CREATE INDEX IF NOT EXISTS ix_fiado_account_receipts_customer
    ON fiado_account_receipts(customer_name,received_at,id)",cn,tx))cmd.ExecuteNonQuery();
   using(var cmd=new SQLiteCommand(@"CREATE TABLE IF NOT EXISTS fiado_account_receipt_audit(
    id INTEGER PRIMARY KEY AUTOINCREMENT,receipt_id INTEGER NOT NULL,
    customer_name TEXT NOT NULL,old_amount NUMERIC NOT NULL,new_amount NUMERIC NOT NULL,
    old_date TEXT NOT NULL,new_date TEXT NOT NULL,old_method TEXT NOT NULL,
    new_method TEXT NOT NULL,old_description TEXT NOT NULL,new_description TEXT NOT NULL,
    reason TEXT NOT NULL,changed_at TEXT NOT NULL DEFAULT (datetime('now','localtime'))
   )",cn,tx))cmd.ExecuteNonQuery();
  }
  internal static decimal SalesDue(SQLiteConnection cn,SQLiteTransaction tx,string client) {
   using(var cmd=new SQLiteCommand("SELECT COALESCE(SUM(CASE WHEN "+SaleBalance+
    ">0 THEN "+SaleBalance+" ELSE 0 END),0) FROM sales s WHERE "+CreditFilter+
    " AND TRIM(COALESCE(s.customer_name,''))=@name COLLATE NOCASE",cn,tx)){
    cmd.Parameters.AddWithValue("@name",(client??"").Trim());
    return decimal.Round(Convert.ToDecimal(cmd.ExecuteScalar()),2);
   }
  }
  internal static decimal Received(SQLiteConnection cn,SQLiteTransaction tx,string client){
   if(!Exists(cn,tx))return 0m;
   using(var cmd=new SQLiteCommand(
    "SELECT COALESCE(SUM(amount),0) FROM fiado_account_receipts WHERE customer_name=@name COLLATE NOCASE",cn,tx)){
    cmd.Parameters.AddWithValue("@name",(client??"").Trim());
    return decimal.Round(Convert.ToDecimal(cmd.ExecuteScalar()),2);
   }
  }
  internal static decimal Due(SQLiteConnection cn,SQLiteTransaction tx,string client){
   return Math.Max(0m,SalesDue(cn,tx,client)-Received(cn,tx,client));
  }
  internal static string BackupVerified(SQLiteConnection cn,string database,string prefix){
   string directory=Path.GetDirectoryName(database);
   string config=Path.Combine(directory,"backup-location.txt");
   string folder=File.Exists(config)?File.ReadAllText(config).Trim():Path.Combine(directory,"Backups");
   if(string.IsNullOrWhiteSpace(folder)||!Path.IsPathRooted(folder))throw new InvalidOperationException("Pasta de backup de Fiado invalida.");
   Directory.CreateDirectory(folder);
   string backup=Path.Combine(folder,prefix+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".db");
   using(var copy=new SQLiteConnection(new SQLiteConnectionStringBuilder{DataSource=backup}.ToString())){
    copy.Open();
    cn.BackupDatabase(copy,"main","main",-1,null,0);
    using(var integrity=new SQLiteCommand("PRAGMA integrity_check",copy))
     if(!string.Equals(Convert.ToString(integrity.ExecuteScalar()),"ok",StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("Backup do Fiado invalido; lancamento cancelado.");
   }
   if(!File.Exists(backup)||new FileInfo(backup).Length<=0)
    throw new InvalidOperationException("Backup do Fiado vazio; lancamento cancelado.");
   return backup;
  }
 }
}
