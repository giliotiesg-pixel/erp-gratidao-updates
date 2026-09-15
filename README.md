# ERP Gratidão — Atualizações

Repositório público usado pelo atualizador automático do ERP Gratidão.

O programa consulta `manifest.json` por HTTPS para verificar a versão disponível.

## Segurança

Antes de instalar uma atualização, o ERP deve validar o SHA-256 do pacote e realizar backup local dos dados e do executável atual.

Não publicar uma nova versão no manifesto antes de o pacote correspondente estar disponível e o SHA-256 ter sido conferido.
