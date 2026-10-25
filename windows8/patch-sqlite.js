'use strict';
const fs = require('fs');
const path = require('path');
const root = path.resolve(process.cwd(), 'app');
const pkgPath = path.join(root, 'package.json');
const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
if (pkg.version !== '12.1.6' || pkg.build.productName !== 'AMZ.G') throw new Error('Fonte inesperada');
pkg.devDependencies.electron = '22.3.27';
pkg.dependencies['better-sqlite3'] = '9.4.3';
pkg.build.asarUnpack = [...new Set([...(pkg.build.asarUnpack || []), '**/*.node'])];
fs.writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n');
for (const f of ['package-lock.json', 'npm-shrinkwrap.json']) {
 const p=path.join(root,f); if(fs.existsSync(p))fs.unlinkSync(p);
}
const adapterPath = path.join(root, 'src/database-adapter.js');
const adapter = `'use strict';
const fs = require('fs');
const path = require('path');
const NativeDatabase = require('better-sqlite3');
class StatementAdapter {
 constructor(statement) { this.statement=statement; }
 run(...args) { const r=this.statement.run(...args); return {changes:Number(r.changes||0),lastInsertRowid:Number(r.lastInsertRowid||0)}; }
 get(...args) { return this.statement.get(...args); }
 all(...args) { return this.statement.all(...args); }
 iterate(...args) { return this.statement.iterate(...args); }
 columns() { return this.statement.columns(); }
}
class DatabaseAdapter {
 constructor(filename,options={}) {
  if(options.fileMustExist && !fs.existsSync(filename)) { const e=new Error('Banco não encontrado: '+filename);e.code='SQLITE_CANTOPEN';throw e; }
  this.filename=filename;
  this._db=new NativeDatabase(filename,{readonly:Boolean(options.readonly||options.readOnly),fileMustExist:Boolean(options.fileMustExist),timeout:Number(options.timeout||5000)});
  this._db.pragma('foreign_keys = ON');
  this._transactionDepth=0;
 }
 prepare(sql) { return new StatementAdapter(this._db.prepare(sql)); }
 exec(sql) { return this._db.exec(sql); }
 close() { return this._db.close(); }
 pragma(source,options={}) { return this._db.pragma(String(source).replace(/^PRAGMA\\s+/i,''),options); }
 transaction(fn) {
  if(typeof fn!=='function')throw new TypeError('transaction requer uma função.');
  const database=this;
  return function(...args) {
   const depth=database._transactionDepth,sp='ag_sp_'+(depth+1);
   database.exec(depth===0?'BEGIN IMMEDIATE':'SAVEPOINT '+sp);
   database._transactionDepth++;
   try {
    const value=fn(...args);
    if(value && typeof value.then==='function')throw new Error('Transações assíncronas não são permitidas.');
    database._transactionDepth--;
    database.exec(depth===0?'COMMIT':'RELEASE SAVEPOINT '+sp);
    return value;
   } catch(e) {
    database._transactionDepth=Math.max(0,database._transactionDepth-1);
    try { if(depth===0)database.exec('ROLLBACK');else database.exec('ROLLBACK TO SAVEPOINT '+sp+'; RELEASE SAVEPOINT '+sp); }catch{}
    throw e;
   }
  };
 }
 async backup(destination) { fs.mkdirSync(path.dirname(destination),{recursive:true}); await this._db.backup(destination); return {destination}; }
}
module.exports=DatabaseAdapter;
`;
fs.writeFileSync(adapterPath,adapter);
for(const rel of ['scripts/recuperar-dados-automaticamente.js','scripts/consolidar-bancos-universal.js','scripts/testar-sqlite-electron.js']) {
 const p=path.join(root,rel);if(!fs.existsSync(p))throw new Error('Arquivo ausente '+rel);
 let c=fs.readFileSync(p,'utf8');
 c=c.replace(/const\\s*\\{\\s*DatabaseSync\\s*\\}\\s*=\\s*require\\(['"]node:sqlite['"]\\);?/g,"const DatabaseSync = require('../src/database-adapter');");
 if(c.includes("require('node:sqlite')"))throw new Error('Referencia node:sqlite nao migrada em '+rel);
 fs.writeFileSync(p,c);
}
const preflight=path.join(root,'scripts/preflight.js');
let pf=fs.readFileSync(preflight,'utf8');
if(!pf.includes('38.0.0'))throw new Error('Preflight inesperado');
fs.writeFileSync(preflight,pf.replaceAll('38.0.0','22.3.27'));
console.log('Adaptador SQLite alternativo aplicado ao pacote de testes');
