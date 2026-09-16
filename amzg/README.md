# AMZ.G — canal de atualização

Este diretório é exclusivo do AMZ.G, baseado no Armazém Gratidão PDV 12.1.5.

O aplicativo consulta `amzg/manifest.json` por HTTPS. Uma atualização somente é aceita quando:

- a versão publicada é superior à instalada;
- o endereço do pacote usa HTTPS;
- o pacote é ZIP;
- o SHA-256 do download é exatamente o publicado no manifesto;
- o pacote passa pela análise e preflight do atualizador do AMZ.G;
- o backup de segurança é criado antes da aplicação.

O banco de dados operacional não deve ser distribuído dentro dos pacotes de atualização.

## Publicação

As versões devem usar tags no formato `amzg-vX.Y.Z` e pacote `AMZG-vX.Y.Z.zip`. O `manifest.json` só deve ser atualizado depois que o pacote definitivo estiver publicado e seu SHA-256 tiver sido calculado.
