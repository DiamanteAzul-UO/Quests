# Tutorial do GM: criando e editando quests com `[criarquest`

Guia campo a campo do criador/editor de quests. Ao final, a quest vira um
arquivo `.json` em `Data/Quests/`.

**Requisito:** nível de acesso **GameMaster** ou superior.

> Este documento foi reescrito junto com o gump. A versão anterior descrevia um
> assistente de 6 passos com 6 campos fixos por objetivo, e listava categorias,
> dificuldades e tipos de objetivo que não existem nos enums do sistema. Se você
> tem um JSON antigo feito por ela, vale conferir com `[questdiag`.

---

## 1. Fluxo geral

| Comando | O que faz |
| --- | --- |
| `[criarquest` | Abre o assistente de **7 passos** com uma quest em branco |
| `[editarquest` | Abre a **lista** de quests carregadas para escolher qual editar |
| `[editarquest <id>` | Abre **direto** o editor naquela quest |
| `[questreload` | Recarrega os JSON no servidor (rodar depois de salvar) |
| `[questdiag` | Diagnóstico: o que está impossível de concluir e por quê |
| `[questdiag npc` | Só o binding de NPCs (quem está sem vínculo) |

Passos do assistente: **1 Identidade → 2 NPCs → 3 Objetivos → 4 Pré-requisitos →
5 Recompensas → 6 Diálogo → 7 Revisão**.

### Regras do gump

- **Os campos são salvos ao clicar em Avançar/Voltar**, e só os do passo atual.
  Fechar no **X perde tudo** — nada é gravado em disco até o passo 7.
- **Limpar um campo funciona**: se você apagar o texto e avançar, o valor vira
  vazio. (No gump antigo o valor antigo era mantido à força.)
- **Acentos podem ser usados** no conteúdo da quest (nome, descrição, diálogos).
  Os JSON existentes já usam. Os *rótulos* do gump é que são escritos sem acento,
  por limitação de renderização do cliente — isso não afeta o que você digita.
- **A validação bloqueia o salvamento.** No passo 7 o gump lista o que tornaria a
  quest impossível de concluir e o botão de salvar recusa até você corrigir.
- **Salvar faz backup**: se o arquivo já existir, é gravado um `.bak` ao lado
  antes de sobrescrever.
- Em modo edição o **ID é imutável** (aparece em vermelho). Mudar o ID de uma
  quest existente corromperia o histórico de quem já a completou.

---

## 2. Passo 1 — Identidade

| Campo | O que colocar | Exemplo |
| --- | --- | --- |
| **ID da Quest** | Identificador único, minúsculo, sem espaços nem acento. Vira o nome do arquivo (`ID.json`) quando a quest é nova. | `quest_side_portao_sitiado` |
| **Nome** | Título que o jogador vê no diário. | `Portao Sitiado` |
| **Descricao** | Resumo da história e do que fazer, 2 a 4 frases. | `Os mortos-vivos atacam o portao norte.` |
| **Categoria** | Uma da lista abaixo. | `Side` |
| **Dificuldade** | Uma da lista abaixo. | `Normal` |
| **Chain ID** | Nome da cadeia (sequência de quests ligadas). Vazio = quest avulsa. | `main_myrthos_ato1` |
| **Chain Order** | Posição na cadeia: `1`, `2`, `3`… Use `0` para avulsa. | `2` |
| **Proxima quest (id)** | ID da quest que esta destrava ao ser concluída. | `quest_side_portao_2` |

**Categorias válidas** (enum `QuestCategory`):
`Main`, `Side`, `Daily`, `Weekly`, `Event`, `Guild`, `Tutorial`, `Profession`.

**Dificuldades válidas** (enum `QuestDifficulty`):
`Trivial`, `Easy`, `Normal`, `Hard`, `VeryHard`, `Epic`, `Legendary`.

> Valor fora da lista não é erro fatal — o loader substitui pelo padrão (`Side` /
> `Normal`) e o `[questdiag` avisa. Mas a quest aparece com a categoria errada.

---

## 3. Passo 2 — NPCs e configurações

### NPC que oferece / que recebe

| Campo | O que colocar |
| --- | --- |
| **Giver NPC ID** | ID lógico do NPC que oferece a quest. **Deve ser idêntico** ao `QuestNpcId` vinculado no NPC do jogo. |
| **Giver NPC Nome** | Nome de exibição (texto para o diário e para o *fallback* de matching). |
| **TurnIn NPC ID / Nome** | NPC onde o jogador entrega. Se for o mesmo do giver, repita os valores. |

> **O erro mais comum de todos.** Se o ID não bater com nenhum NPC vinculado no
> mundo, a quest nunca aparece — ou nunca pode ser entregue. Vincule com:
>
> ```
> [questreload                     ← PRIMEIRO: carrega os JSON
> [questnpcset npcid npc_capita_maelis   ← DEPOIS: mira no NPC e roda
> ```
>
> A ordem importa: `[questnpcset npcid` reindexa as quests do NPC na hora, então
> rodar antes do `[questreload` vincula o NPC a zero quests.
>
> `[questdiag npc` lista exatamente quem está sem binding, com o comando pronto
> para copiar.

Se você deixar o TurnIn vazio, o gump copia o Giver automaticamente.

### Configurações

| Opção | Marque quando… |
| --- | --- |
| **Repetivel** | O jogador puder refazer depois de concluída (daily, weekly, farm). |
| **Compartilhavel** | Quem aceitar puder dividir progresso com a party. |
| **Auto-iniciar** | A quest começar sozinha, sem falar com NPC (tutorial, evento de área). |
| **Cooldown (horas)** | Só importa se Repetivel. `24` = diária, `168` = semanal, `0` = sem espera. |
| **Max. conclusoes** | `0` = ilimitado. Limite total de vezes que um jogador pode concluir. |

### Limite de tempo

| Campo | O que colocar |
| --- | --- |
| **Ativo** | Liga o cronômetro. |
| **Minutos** | Duração. Se você preencher os minutos e esquecer de marcar **Ativo**, o loader ativa sozinho. |
| **Ao expirar** | `Fail` (reprovada), `Reset` (volta ao início) ou `Nothing`. |
| **Avisos (min, \| )** | Marcos de aviso, separados por `\|`. Ex.: `30\|10\|5` avisa quando faltarem 30, 10 e 5 minutos. Cada marco dispara **uma única vez**. |

---

## 4. Passo 3 — Objetivos

Clique em **Adicionar objetivo**, escolha o **tipo** na lista e preencha o
formulário próprio daquele tipo. Cada objetivo tem botão de **editar** (lápis) e
**remover** (X).

**Sequencial**: com a caixa marcada, um objetivo só fica ativo depois que o
anterior completar. Sem marcar, todos progridem em paralelo.

> Objetivos marcados como **Opcional** não bloqueiam a conclusão. Cuidado: se
> **todos** forem opcionais, nada é obrigatório e a quest nunca completa — a
> validação do passo 7 aponta isso.

### Os 14 tipos

| Tipo | O jogador deve… | Alvo principal |
| --- | --- | --- |
| `kill` | Matar criaturas | `targetType` = classe do mob |
| `collect` | Reunir itens no inventário | `itemType` = classe do item |
| `deliver` | Levar um item a um NPC | `itemType` + `targetNpcId` |
| `talkto` | Falar com um NPC | `targetNpcId` |
| `escort` | Escoltar um NPC até um destino | `destinationRegion` **ou** coords |
| `defend` | Resiste a ondas de inimigos num local | `enemyType` + coords |
| `destroy` | Destruir objeto/estrutura/criatura | `targetType` ou `targetSerials` |
| `explore` | Ir a um local (e permanecer) | coords **ou** `locationName` |
| `craft` | Fabricar um item | `itemType` + `craftSkill` |
| `useskill` | Usar uma skill N vezes | `skill` |
| `skillgain` | Elevar uma skill até um valor | `skill` + `targetValue` |
| `interact` | Interagir com objeto (duplo clique/fala) | `targetType` ou `targetSerials` |
| `useitem` | Usar um item específico | `itemType` |
| `survive` | Sobreviver por um tempo | `durationSeconds` |

### Campos que todo objetivo tem

| Campo | O que colocar |
| --- | --- |
| **Quantidade necessaria** | Quantas vezes. Mínimo 1 — `0` ou negativo trava o objetivo. |
| **Descricao** | Texto no diário/rastreador. Escreva como ação: `Derrote os esqueletos`. |
| **Dica de local** | Onde encontrar o alvo. Jogador novo não sabe onde ir. |
| **Opcional** | Não bloqueia a conclusão. |
| **Oculto** | Não aparece no rastreador até ser descoberto. |

O formulário é paginado: se o tipo tiver muitos campos (`escort` tem 23), use
**Mais campos ▶** e **◀ Campos**.

### Tabela completa de campos por tipo

**`kill`** — `targetType` (classe do mob), `targetName` (nome exibido, casa boss
renomeado), `targetTypes` (classes extras, separadas por vírgula),
`requiredRegion` (vazio = qualquer lugar), `countPetKills`, `countPartyKills`.

**`collect`** — `itemType`, `itemName`, `itemHue` (0 = qualquer),
`consumeOnComplete` (retira os itens na entrega), `markAsQuestItem`,
`fromMobTypes` (classes que dropam o item, separadas por vírgula), `dropChance`
(%, só vale com `fromMobTypes`).

**`deliver`** — `itemType`, `itemName`, `targetNpcId`, `targetNpcName`,
`giveItemOnAccept` (o sistema cria e entrega o item no aceite), `itemHue`,
`onItemLostAction` (`Fail` ou `Nothing`).

**`talkto`** — `targetNpcId`, `targetNpcName`, `speechTrigger` (palavra exigida;
vazio = qualquer fala), `grantItems` (itens concedidos na fala, formato
`Tipo|Nome|Qtd`, vários separados por `;`).

**`escort`** — `escortNpcName`, `escortNpcId`, `destinationRegion` **ou**
`destinationX/Y/Z` + `destinationMap`, `destinationName`, `radiusToComplete`,
`spawnOnAccept`, `spawnX/Y/Z` + `spawnMap` (0 = local do jogador), `escortBody`
(`0x190` homem / `0x191` mulher), `escortHue` (0 = aleatório), `escortHits`
(0 = padrão), `defendsItself`, `teleportWhenStuck`, `maxDistanceFromOwner`,
`stuckTimeoutSeconds`, `onEscortDead` (`FailQuest` / `Respawn` / `ResetObjective`
/ `Nothing`).

**`defend`** — `enemyType`, `enemiesPerWave`, `totalWaves`,
`timeBetweenWavesSeconds`, `locationName`, `locationX/Y`, `mapName`,
`radiusTiles`.

**`destroy`** — `targetType`, `targetName`, `targetSerials` (seriais específicos,
separados por vírgula), `respawnOnFail`.

**`explore`** — `locationName`, `locationX/Y/Z`, `mapName`, `radiusTiles`,
`stayDurationSeconds` (0 = só chegar), `triggerOnEnter`.

**`craft`** — `itemType`, `itemName`, `craftSkill`, `requireExceptional`,
`consumeOnComplete`.

**`useskill`** — `skill`, `requireSuccess` (só conta se acertar).

**`skillgain`** — `skill`, `targetValue`, `startValue` (0 = valor atual), `mode`
(`ReachValue` ou `GainAmount`).

**`interact`** — `targetType`, `targetName`, `targetSerials`, `interactionType`
(`DoubleClick` ou `Speech`), `speechTrigger`.

**`useitem`** — `itemType`, `itemName`, `itemHue`, `consumeOnUse`.

**`survive`** — `durationSeconds`, `requiredRegion` (vazio = qualquer),
`failOnDeath`.

---

## 5. Passo 4 — Pré-requisitos

Define **quem pode ver e aceitar** a quest. Quem não satisfaz recebe status
`Locked`, e a quest **nem aparece** na lista do NPC.

| Campo | O que colocar |
| --- | --- |
| **Quests exigidas** | IDs separados por vírgula. O jogador precisa ter **concluído** todas. |
| **Level minimo** | `0` = sem exigência. (Só vigora se o shard expuser nível; no ServUO puro é ignorado em vez de reprovar.) |
| **Fama minima** | `0` = sem exigência. |
| **Karma minimo / maximo** | Janela de karma. Vazio = sem limite naquele lado. |
| **Faccao** | Nome da facção. Vazio = qualquer. |
| **Guilda** | Nome ou sigla. Vazio = qualquer. |
| **Racas permitidas** | Separadas por vírgula. Vazio = todas. |
| **Msg. quando recusado** | Texto que o jogador vê ao não satisfazer a condição. Vazio = mensagem padrão de cada regra. |
| **Skills minimas** | Botão **Adicionar skill** → nome + valor mínimo. |
| **Itens exigidos** | Botão **Adicionar item** → classe + nome + quantidade. A busca percorre mochila, sub-containers e equipados. |

### Exemplo: quest de ladino

Para uma quest que **só aparece** para quem tem `Stealing` e `Snooping`:

```
Skills minimas:
   Stealing >= 30.0
   Snooping >= 30.0
Msg. quando recusado: Voce nao tem maos leves o bastante para este trabalho.
```

Há um exemplo pronto em `Data/Quests/Exemplos/exemplo_15_ladino_roubo.json`.

> Cuidado com `minKarma` maior que `maxKarma`: a janela fica vazia e **ninguém**
> entra. A validação aponta.

---

## 6. Passo 5 — Recompensas

| Campo | O que colocar |
| --- | --- |
| **Ouro** | Quantidade. `0` = nada. |
| **Fama / Karma / Experiencia** | Karma aceita negativo. |
| **Depositar ouro no banco** | Marca = ouro vai direto para o banco, não para a mochila. |
| **Titulos (separar por \|)** | Títulos concedidos. |

**Adicionar item** → `Tipo`, `Nome`, `Quantidade`, `Hue` (0 = normal), `Chance`
(%, 100 = sempre). O botão X ao lado de cada entrada remove.

**Adicionar skill** → `Skill`, `Pontos ganhos`, `Aumento de cap`. O ganho é
limitado ao `SkillsCap` do jogador — não estoura o teto.

> **Tipo do item** aceita duas coisas: uma **classe real do ServUO** (`Gold`,
> `Longsword`, `Bandage`) ou um **nome de item virtual de quest**
> (`SeloFortaleza`, `CartaLacradaOlin`). Quando a classe não existe, o sistema
> cria um `QuestItem` com aquela tag automaticamente.
>
> **Não duplique ouro**: se preencheu o campo **Ouro**, não adicione também um
> item `Gold`.

---

## 7. Passo 6 — Diálogo

| Campo | Quando o NPC fala |
| --- | --- |
| **Ao oferecer** | Ao propor a quest |
| **Ao aceitar** | Quando o jogador aceita |
| **Ao recusar** | Quando o jogador recusa |
| **Em progresso** | Quando o jogador fala com a quest em andamento |
| **Ao completar** | Na entrega |
| **Mensagem final** | Mensagem de conclusão (fora da fala do NPC) |

- **Separe falas com `|`** (pipe). Ex.: `Muito bem!|Aqui esta sua recompensa!`
- **Marcadores**: `{playerName}` e `{questName}` são substituídos em jogo.

---

## 8. Passo 7 — Revisão e salvar

Mostra o resumo (ID, nome, categoria, giver, turn-in, nº de objetivos,
pré-requisitos, recompensas, limite de tempo, próxima quest) e o resultado da
**validação**.

Se houver problemas, eles aparecem em vermelho e o botão **✦ SALVAR QUEST** (ou
**✦ SALVAR ALTERACOES**, em modo edição) **recusa** a gravação, listando até 8
no chat. É de propósito: o objetivo é impedir que uma quest impossível chegue ao
disco.

Depois de salvar:

```
[questreload
[questdiag <id-da-sua-quest>
[questdiag npc
```

---

## 9. Editar uma quest existente

`[editarquest` abre a lista paginada (14 por página) com ID, nome e nº de
objetivos. Clique na seta para abrir no editor. Com `[editarquest <id>` vai
direto.

O editor **recarrega o JSON de volta para o formulário** — identidade, NPCs,
configurações, todos os objetivos com seus campos específicos, pré-requisitos,
recompensas e diálogos. Ao salvar, sobrescreve o **arquivo de origem** (com
`.bak`), não um arquivo novo.

> Se você editar uma quest que **jogadores têm em andamento**, rode
> `[questreload` depois: o progresso em andamento é realinhado automaticamente
> com a nova definição. Sem isso, remover um objetivo deixaria uma entrada órfã
> no progresso do jogador e a quest travaria para sempre.

---

## 10. Armadilhas que tornam uma quest impossível

Todas estas são detectadas pelo `[questdiag`. Estão listadas porque já
aconteceram de verdade neste sistema.

### `craftSkill` é o nome da FERRAMENTA, não do ofício

Em runtime o valor comparado é a classe da ferramenta (`SewingKit`,
`SmithyHammer`, `DovetailSaw`). O sistema **agora aceita os dois** — ofício ou
ferramenta — então `Tailoring` funciona. Mas erro de grafia nos dois lados
continua sendo objetivo impossível.

Ofícios aceitos: `Blacksmith`, `Tailoring`, `Carpentry`, `Fletching`/`Bowcraft`,
`Alchemy`, `Inscription`, `Tinkering`, `Cartography`.

### Nunca use `locationHint` em objetivo `explore`

O parser lê `locationHint` como *alias* de `locationName`, e `locationName` vira
**exigência de região** combinada com **E** (não OU) com as coordenadas. Como o
texto de uma dica não é nome de região, o objetivo nunca completa.

Use só `description` para o texto. (Em `defend` o alias existe, mas lá
`locationName` é decorativo — o driver usa só as coordenadas.)

### Item virtual precisa de uma fonte

Se o objetivo pede um item que **não existe** no shard (`SeloFortaleza`), o
sistema o cria — mas só quando alguém o entrega. Sem `grantItems` num `talkto`,
`giveItemOnAccept` num `deliver` ou `fromMobTypes` num `collect`, o jogador
nunca consegue o item. O `[questdiag` aponta como bloqueante.

### Escolta sem destino

Sem `destinationRegion` **nem** coordenadas, o NPC escoltado anda para sempre.
Prefira declarar os dois: a chegada é aceita por região **ou** por raio, então
sobrevive a região renomeada ou coordenada imprecisa.

Deixe `teleportWhenStuck` ligado: se o NPC travar em geometria, ele é
teletransportado até você em vez de parar a quest.

### `useskill` nem sempre dispara

O ServUO só dispara o evento de skill quando o teste é **desafiador**. Com skill
alta demais (`value >= maxSkill`) ou baixa demais (`value < minSkill`), ele
retorna sem evento. Para `Stealing`, prefira um objetivo `collect` do item
roubado — é o que o exemplo do ladino faz, e o motivo está documentado no
`_meta` dele.

### Alvo que não existe no shard

`targetType: "Esqueleto"` ou `"Wolf"` não são classes do ServUO — só casam pelo
**nome exibido** da criatura, o que quebra se o mob for renomeado ou spawnado com
nome padrão. Use a classe real (`Skeleton`, `GreyWolf`, `DireWolf`) e deixe o
nome em português no campo de nome, que é o *fallback* correto.

---

## 11. Checklist antes de dar por pronta

```
[questreload
[questdiag <id>          ← 0 bloqueantes?
[questdiag npc           ← todos os NPCs vinculados?
```

- [ ] `[questdiag` reporta **0 bloqueantes** para a quest
- [ ] `[questdiag npc` não lista nenhum NPC exigido por ela
- [ ] Todo tipo de item/criatura é uma **classe real** ou um item virtual **com fonte**
- [ ] Todo objetivo tem `description` e quantidade ≥ 1
- [ ] Nem todos os objetivos são opcionais
- [ ] Escolta tem destino; `explore` não usa `locationHint`
- [ ] Testou com uma conta de jogador: aceitou, progrediu e **entregou**
