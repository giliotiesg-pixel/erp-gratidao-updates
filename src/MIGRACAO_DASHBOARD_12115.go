package main

import "fmt"

type Dashboard12115 struct { TodaySales,MonthSales,TodayProfit,MonthProfit float64; TodayCount,MonthCount int; LowStock int }

func dashboard12115() Dashboard12115 {
 var d Dashboard12115
 d.TodaySales=parseF(scalar("SELECT COALESCE(SUM(total),0) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime')"))
 d.MonthSales=parseF(scalar("SELECT COALESCE(SUM(total),0) FROM sales WHERE status='CONCLUIDA' AND strftime('%Y-%m',created_at,'localtime')=strftime('%Y-%m','now','localtime')"))
 d.TodayProfit=parseF(scalar("SELECT COALESCE(SUM(profit),0) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime')"))
 d.MonthProfit=parseF(scalar("SELECT COALESCE(SUM(profit),0) FROM sales WHERE status='CONCLUIDA' AND strftime('%Y-%m',created_at,'localtime')=strftime('%Y-%m','now','localtime')"))
 fmt.Sscan(scalar("SELECT COUNT(*) FROM sales WHERE status='CONCLUIDA' AND date(created_at,'localtime')=date('now','localtime')"),&d.TodayCount)
 fmt.Sscan(scalar("SELECT COUNT(*) FROM sales WHERE status='CONCLUIDA' AND strftime('%Y-%m',created_at,'localtime')=strftime('%Y-%m','now','localtime')"),&d.MonthCount)
 fmt.Sscan(scalar("SELECT COUNT(*) FROM products WHERE active=1 AND stock<=min_stock"),&d.LowStock)
 return d
}

func recentSales12115(limit int) ([][]string,error) {if limit<=0||limit>100{limit=20};return queryRows(fmt.Sprintf("SELECT id,sale_number,customer_name,payment_method,printf('%%.2f',total),created_at FROM sales WHERE status='CONCLUIDA' ORDER BY id DESC LIMIT %d",limit),6)}

func monthlyHistory12115(months int) ([][]string,error) {if months<=0||months>36{months=12};return queryRows(fmt.Sprintf("SELECT strftime('%%Y-%%m',created_at,'localtime') mes,COUNT(*),printf('%%.2f',SUM(total)),printf('%%.2f',SUM(profit)) FROM sales WHERE status='CONCLUIDA' GROUP BY mes ORDER BY mes DESC LIMIT %d",months),4)}
