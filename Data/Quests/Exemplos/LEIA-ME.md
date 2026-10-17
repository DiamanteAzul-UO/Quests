# Exemplos de quest — um arquivo por quest, um objetivo por quest

Esta pasta existe para responder a duas coisas:

1. **Ter um arquivo de referência funcional para cada um dos 14 tipos de objetivo.**
   Cada exemplo tem exatamente **um** objetivo, então dá para isolar o tipo que
   você quer estudar sem ruído.
2. **Demonstrar pré-requisitos de skill** — o caso pedido: uma quest de ladino
   que **só aparece** para quem tem `Stealing` e `Snooping`.

> ⚠️ Estes arquivos são **carregados de verdade**: o `QuestLoader` varre
> `Data/Quests/` recursivamente (`SearchOption.AllDirectories`). Para desativar
> os exemplos, mova esta pasta para fora de `Data/Quests/`.

## Como colocar para funcionar

```
[questreload          ← carrega os JSON (faça isto PRIMEIRO)
[questdiag            ← diz o que está impossível e por quê
[questdiag npc        ← só o binding de NPCs
```

Depois vincule os NPCs no mundo. Os 10 IDs usados aqui já estão listados em
`../Fortaleza_Abalada_NPCS.txt`, com o comando pronto para cada um:

```
[questnpcset npcid npc_ferreiro_dorik
```

**A ordem importa**: `[questnpcset npcid` chama `AutoBindQuestsById`, que só
encontra as quests se elas já estiverem carregadas. Rodar antes do `[questreload`
vincula o NPC a zero quests.

## Os 15 arquivos

| Arquivo | Tipo de objetivo | Quem dá | Alvo |
|---|---|---|---|
| `exemplo_01_kill.json` | `Kill` | Capitã Maelis | `Rat` ×5 |
| `exemplo_02_collect.json` | `Collect` | Enfermeira Sera | `Bandage` ×10 |
| `exemplo_03_deliver.json` | `Deliver` | Cozinheira Brina | `BreadLoaf` ×3 → Sera |
| `exemplo_04_talkto.json` | `TalkTo` | Erudita Naira | Marcus, o Guia |
| `exemplo_05_escort.json` | `Escort` | Ferreiro Dorik | até *the New Haven Farm* |
| `exemplo_06_defend.json` | `Defend` | Capitã Maelis | 2 ondas de `Skeleton` |
| `exemplo_07_destroy.json` | `Destroy` | Erudita Naira | `StoneGargoyle` ×3 |
| `exemplo_08_explore.json` | `Explore` | Marcus, o Guia | Haven Moongate (3763, 2769) |
| `exemplo_09_craft.json` | `Craft` | Lenhadora Tessa | `Shirt` (Tailoring) |
| `exemplo_10_useskill.json` | `UseSkill` | Lenhadora Tessa | `Tracking` ×3 |
| `exemplo_11_skillgain.json` | `SkillGain` | Minerador Garth | `Mining` 30.0 |
| `exemplo_12_interact.json` | `Interact` | Ferreiro Dorik | `Anvil` (duplo clique) |
| `exemplo_13_useitem.json` | `UseItem` | Helena, a Curandeira | `Bandage` ×3 |
| `exemplo_14_survive.json` | `Survive` | Capitã Maelis | 120 s em *Haven Mountains* |
| `exemplo_15_ladino_roubo.json` | `Collect` **+ pré-requisitos** | Mestre de Docas Olin | `GoldRing` ×1 |

Todo tipo de criatura, item e skill usado acima foi **conferido contra classes
reais do ServUO**. Nomes que não existem (`Deer`, `Goblin`, `WoodenDoor`,
`Garlick`, `SpiderSilk`) foram evitados de propósito — eles fariam o objetivo
casar apenas por nome exibido, ou nunca casar.

Coordenadas e regiões são reais: New Haven fica no **Trammel**, e os nomes de
região (`the New Haven Farm`, `Haven Mountains`, `Haven Moongate`) vêm do
`Data/Regions.xml` do ServUO.

## A quest do ladino (item 15)

É o caso pedido: **roubar um item, visível só com `Stealing` e `Snooping`**.

```json
"prerequisites": {
  "requiredSkills": [
    { "skill": "Stealing", "value": 30.0 },
    { "skill": "Snooping", "value": 30.0 }
  ],
  "failMessage": "Você não tem mãos leves o bastante para este trabalho."
}
```

O gating funciona porque `GetQuestStatus` agora devolve `QuestStatus.Locked`
quando `Prerequisites.Check` falha. A lista do NPC filtra por `Available`/`Active`,
então uma quest `Locked` **some da lista** — o jogador sem as skills nem fica
sabendo que ela existe.

Antes dessa correção, `Locked` existia no enum mas nunca era usado: a quest
aparecia para todo mundo e só barrava no momento do aceite.

### Por que o objetivo é `Collect` e não `UseSkill`

Isto é contraintuitivo, então vale registrar. `Stealing` no ServUO usa
`CheckTargetSkill`, que passa por `Mobile_SkillCheckTarget`:

```csharp
if (value < minSkill)  return false;  // "too difficult" — SEM disparar evento
if (value >= maxSkill) return true;   // "no challenge"   — SEM disparar evento
...
return CheckSkill(from, skill, target, chance);  // só aqui dispara SkillCheck
```

Ou seja, `EventSink.SkillCheck` **só dispara quando o teste é desafiador**. Um
ladino habilidoso roubando um item leve recebe `true` direto, sem evento — e um
objetivo `UseSkill` ficaria parado em 0 justamente para o jogador que a quest
deveria premiar.

`Collect` é creditado por `OnItemObtained`, que dispara quando o item roubado
cai na mochila (`Mobile.Obtained` cobre qualquer item exceto a própria mochila e
o bank box). É o caminho garantido.

**Limitação honesta:** `Collect` não *prova* que o item foi roubado — comprar ou
saquear o mesmo anel também conta. Se o seu shard precisa dessa garantia, o
caminho é um gancho próprio dentro de `Scripts/Skills/Stealing.cs` chamando
`QuestManager.Instance`. Não dá para obter isso só com eventos genéricos.

## Armadilhas documentadas nos `_meta`

Cada arquivo traz um bloco `_meta` explicando o tipo. Três armadilhas merecem
destaque porque **tornavam a quest impossível sem nenhum aviso**:

### 1. `craftSkill` é o nome da FERRAMENTA, não do ofício

Em runtime o valor comparado vem de `e.Tool.GetType().Name` — ou seja
`SewingKit`, `SmithyHammer`, `DovetailSaw`. Escrever `"craftSkill": "Tailoring"`
nunca casava. `CraftSystemMatches` agora aceita **os dois**: o nome da
ferramenta (comportamento antigo) ou o nome do ofício, traduzido por uma tabela.

Ferramentas aceitas por ofício: `Blacksmith` → `SmithyHammer`/`SmithHammer`/
`RunicHammer`/`AncientSmithyHammer`/`WornHammer` · `Tailoring` → `SewingKit`/
`RunicSewingKit` · `Carpentry` → `DovetailSaw`/`RunicDovetailSaw`/`DrawKnife` ·
`Fletching`/`Bowcraft` → `FletcherTools` · `Alchemy` → `MortarPestle` ·
`Inscription` → `ScribesPen` · `Tinkering` → `TinkersTools` ·
`Cartography` → `MapmakersPen`.

### 2. Nunca use `locationHint` em objetivo `Explore`

O parser lê `locationHint` como **alias de `locationName`** em `Explore`. E
`locationName` não é texto decorativo: vira exigência de **região**, combinada
com **E** (não OU) com as coordenadas. Como o texto de uma dica não é nome de
região, `IsInsideExploreArea` retorna `false` sempre e o objetivo nunca completa.

Use apenas `description` para o texto visível.

> Em `Defend` o parser tem o mesmo alias, mas lá `locationName` é **decorativo**:
> o driver (`SpawnWave`) usa apenas `locationX`/`locationY` e cai para a posição
> do jogador quando são zero. Ou seja, em `Defend` uma dica no lugar errado não
> trava a quest — em `Explore`, trava.

### 3. `SkillGain` em modo `ReachValue` travava quem já tinha a skill

O objetivo só creditava em `OnSkillGain`. Um jogador que já estivesse acima do
alvo — ou no cap — nunca mais gerava ganho naquela skill e ficava em 0 para
sempre. `SyncSkillGainObjectives` agora credita no aceite, seguindo o mesmo
padrão que `SyncCollectObjectives` já usava para itens.

## Checklist antes de considerar uma quest pronta

```
[questreload
[questdiag <id-da-sua-quest>
[questdiag npc
```

`[questdiag` aponta, com o motivo exato: objetivo sem alvo, tipo que não existe
no shard, **item virtual sem fonte de obtenção**, escolta sem destino, NPC sem
binding, pré-requisito apontando para quest inexistente, `required <= 0`, região
inexistente e `Survive` com duração maior que o `timeLimit` da quest.
