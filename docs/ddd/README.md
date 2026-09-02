# Documentação DDD — FIAP Cloud Games

Conjunto definido pelo `ADR-004-diagramas-ddd.md`, a partir do material das aulas 03 a 06.

| # | Artefato | Fonte | Export |
|---|---|---|---|
| 1 | Linguagem Ubíqua | [`01-linguagem-ubiqua.md`](./01-linguagem-ubiqua.md) | — |
| 2 | Event Storming — cadastro de usuário | [`02-event-storming-usuario.md`](./02-event-storming-usuario.md) | [`PNG`](./exports/02-event-storming-usuario.png) |
| 3 | Event Storming — cadastro de jogo | [`03-event-storming-jogo.md`](./03-event-storming-jogo.md) | [`PNG`](./exports/03-event-storming-jogo.png) |
| 4 | Mapa de Contexto | [`04-mapa-de-contexto.md`](./04-mapa-de-contexto.md) | [`PNG`](./exports/04-mapa-de-contexto.png) |
| 5 | Mapa de Agregados | [`05-mapa-de-agregados.md`](./05-mapa-de-agregados.md) | [`PNG`](./exports/05-mapa-de-agregados.png) |
| 6 | Arquitetura em camadas | [`06-arquitetura-em-camadas.md`](./06-arquitetura-em-camadas.md) | [`PNG`](./exports/06-arquitetura-em-camadas.png) |

Todos os diagramas são Mermaid e renderizam direto no GitHub. Os PNGs foram gerados das fontes
acima com Mermaid CLI 11.12.0, versão Fase 1, em 01/09/2026.

**Cada elemento aponta o arquivo que o implementa.** Não é modelagem paralela ao código: é o
código descrito na notação da disciplina. Se um nome mudar em um lado, tem de mudar no outro.
