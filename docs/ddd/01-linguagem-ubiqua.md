# Linguagem Ubíqua — FIAP Cloud Games

> Fonte da verdade: o código. Cada termo abaixo aparece como tipo, método ou rota no repositório.
> Se um termo mudar aqui, muda lá — e vice-versa.

## Termos do domínio

| Termo | O que é | Onde vive no código |
|---|---|---|
| **Usuário** (`User`) | Quem acessa a plataforma. Nasce sempre ativo e com papel `User`. | `Domain/Identity/User.cs` |
| **Papel** (`UserRole`) | `User` ou `Administrator`. Não há terceiro. | `Domain/Identity/UserRole.cs` |
| **E-mail** (`Email`) | Identificador de login. Normalizado (trim + minúsculas) e único. | `Domain/Identity/Email.cs` |
| **Política de senha** (`PasswordPolicy`) | Mínimo 8 caracteres, com letra, número e caractere especial. | `Domain/Identity/PasswordPolicy.cs` |
| **Jogo** (`Game`) | Item do catálogo. Tem preço base e pode ser desativado. | `Domain/Catalog/Game.cs` |
| **Promoção** (`Promotion`) | Desconto percentual com vigência `[início, fim)`. | `Domain/Catalog/Promotion.cs` |
| **Preço vigente** (`CurrentPrice`) | Preço base menos o maior desconto ativo naquele instante. | `Domain/Catalog/PricingService.cs` |
| **Aquisição** (`Acquire`) | O ato de um usuário adicionar um jogo à própria biblioteca. | `Application/Library/AcquireGameHandler.cs` |
| **Entrada de biblioteca** (`LibraryEntry`) | O vínculo usuário–jogo, com data e preço do momento. | `Domain/Library/LibraryEntry.cs` |
| **Preço de aquisição** (`AcquisitionPrice`) | Fotografia do preço vigente no instante da aquisição. Nunca é recalculado. | `LibraryEntry.AcquisitionPrice` |

## Termos proibidos

Estes termos não devem nomear o fluxo nem sugerir que existe uma transação financeira. Eles só
podem aparecer em frases negativas que deixem explícito o que está fora do escopo:

| Proibido | Use | Por quê |
|---|---|---|
| compra, comprar | **aquisição**, **adquirir** | O enunciado só diz "jogos adquiridos" e nunca descreve cobrança. Falar em compra inventa um requisito que não existe. |
| pagamento, cobrança, checkout, carrinho, cartão | *(nada — não há esse conceito)* | Não existe fluxo financeiro no escopo. |
| purchase, payment, buy | **acquire**, **acquisition** | O mesmo, em inglês: os identificadores do código seguem a regra. |
| cliente | **usuário** | "Cliente" carrega semântica comercial que o domínio não tem. |

> **A aquisição é simulada.** Ela vincula um jogo ao usuário e registra o preço vigente, sem
> qualquer transação financeira. Isto é uma `[HIPÓTESE]` do grupo, não uma exigência do enunciado —
> o PDF nunca explica como o jogo entra na biblioteca.

## Classificação dos termos

Todo conceito não-óbvio carrega uma etiqueta, para separar o que o enunciado exige do que o grupo
decidiu:

- **`[REQUISITO]`** — rastreia uma linha literal do enunciado. Ex.: senha forte, papéis, JWT.
- **`[DECISÃO]`** — escolha técnica do grupo. Ex.: PostgreSQL, `xmin` como token de concorrência,
  Controllers em vez de Minimal API.
- **`[HIPÓTESE]`** — preenche lacuna que o enunciado deixou. Ex.: aquisição simulada, sobreposição
  de promoções permitida, jogo inativo permanecer na biblioteca.

## Eventos

Nomes estáveis, no passado, emitidos como log estruturado:

`UserRegistered` · `LoginFailed` · `UserBlocked` · `UserUnblocked` ·
`UserSelfDeactivationRejected` · `GameCreated` · `PromotionCreated` · `GameAddedToLibrary`

## Comandos

Nomes no imperativo, um por caso de uso:

`RegisterUser` · `LoginUser` · `ChangeUserStatus` · `CreateGame` · `CreatePromotion` ·
`AcquireGame` · `ListGames` · `GetGame` · `ListUsers` · `GetCurrentUser` · `GetMyLibrary`
