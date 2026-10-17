# Tutorial do GM: criando quests com `[criarquest`

Guia campo a campo do criador de quests do MysteryShardRP. Ao final, a quest vira um arquivo `.json` em `Data/Quests/`.

**Requisito:** nível de acesso **GameMaster** ou superior.

---

## 1. Fluxo geral

1. Digite `[criarquest` no jogo. Abre o gump de 6 passos.
2. Preencha cada passo e clique em **Avançar ▶** (ou **◀ Voltar**).
3. No passo 6, revise e clique em **✦ SALVAR QUEST**.
4. Digite `[questreload` para carregar a quest no servidor.
5. Teste com uma conta de jogador.

### Regras do gump que você precisa saber

- **Os campos são salvos ao clicar em Avançar/Voltar**, e só os do passo atual. Fechar o gump no X **perde tudo**: nada é gravado até o passo 6.
- **Não dá para apagar um campo já preenchido.** Se você deixa um campo vazio e avança, o gump mantém o valor antigo. Para corrigir, escreva outro texto no lugar.
- **Não use acentos nem caracteres especiais** nos textos digitados (use "Descricao", não "Descrição"). O gump foi feito sem acentos e o cliente pode exibir caracteres quebrados.
- Salvar com um **ID que já existe sobrescreve o arquivo** sem avisar.
- Se o ID ou o Nome estiverem vazios, o salvamento é recusado e você volta ao passo 1.

---

## 2. Passo 1: Identidade

| Campo | O que colocar | Exemplo |
| --- | --- | --- |
| **ID da Quest** | Identificador único, **minúsculo, sem espaços, sem acento**. Vira o nome do arquivo (`ID.json`). Padrão sugerido: `categoria_tema_nome`. Nunca reutilize um ID. | `quest_side_portao_sitiado` |
| **Nome** | Título que o jogador vê no diário. Pode ter espaços. | `Portao Sitiado` |
| **Descricao** | Resumo da história e do que o jogador deve fazer, em 2 a 4 frases, na voz do diário. | `Os mortos-vivos atacam o portao norte. O Capitao Aldric precisa de ajuda.` |
| **Categoria** | Uma das opções abaixo. Digite exatamente assim, com a primeira letra maiúscula. | `Side` |
| **Dificuldade** | Uma das opções abaixo. | `Normal` |
| **Chain ID** | Nome da **cadeia** (sequência de quests ligadas). Deixe **vazio** se a quest for avulsa. Todas as quests da mesma cadeia usam o **mesmo** Chain ID. | `main_myrthos_ato1` |
| **Chain Order** | Posição na cadeia: `1`, `2`, `3`... Use `0` para quest avulsa. | `2` |

**Categorias**

| Categoria | Uso recomendado |
| --- | --- |
| `Tutorial` | Primeiros passos de personagens novos |
| `Main` | História principal, normalmente em cadeia |
| `Side` | Missões secundárias, avulsas |
| `Daily` | Repetíveis diárias |
| `Weekly` | Repetíveis semanais |
| `Event` | Eventos temporários |
| `Hidden` | Quests escondidas, descobertas por exploração |

**Dificuldades:** `Easy` (iniciante), `Normal`, `Hard`, `Elite`, `Legendary`. Combine com as recompensas do passo 4: quanto maior a dificuldade, maior a recompensa.

---

## 3. Passo 2: NPCs

### NPC que dá a quest

| Campo | O que colocar | Exemplo |
| --- | --- | --- |
| **Giver NPC ID** | ID interno do NPC que entrega a quest. Deve ser **idêntico** ao ID usado no NPC dentro do jogo. Minúsculo, sem espaços. | `npc_capitao_guarda` |
| **Giver NPC Nome** | Nome de exibição do NPC (só texto, para o diário). | `Capitao da Guarda Aldric` |

### NPC de entrega

| Campo | O que colocar |
| --- | --- |
| **TurnIn NPC ID / Nome** | NPC onde o jogador entrega a quest. **Se for o mesmo que deu a quest, repita os mesmos valores do Giver.** Se for outro NPC, use o ID dele. |

> Se o ID do NPC não bater com o ID configurado no NPC do jogo, a quest nunca aparece (ou nunca pode ser entregue). É o erro mais comum.

### Configurações

| Opção | Marque quando... |
| --- | --- |
| **Repetivel** | O jogador puder refazer a quest depois de concluída (Daily, Weekly, farm). |
| **Compartilhavel** | Quem aceitar a quest puder dividir com os membros da party. |
| **Auto-iniciar** | A quest começar sozinha, sem falar com o NPC (tutoriais, eventos de área). Se marcar, o diálogo "ao aceitar" ainda deve ser preenchido. |
| **Cooldown (horas)** | Só importa se for Repetivel. Tempo de espera antes de refazer. `24` = diária, `168` = semanal, `0` = sem espera. Só números inteiros. |

---

## 4. Passo 3: Objetivos

Clique em **Adicionar objetivo** e preencha a janela. Repita para cada objetivo. O lixo (botão vermelho à direita) remove um objetivo.

> A lista mostra cerca de 6 objetivos por vez. Se precisar de mais, divida em duas quests de uma cadeia.

### Campos da janela "Novo Objetivo"

| Campo | O que colocar |
| --- | --- |
| **Tipo** | O que o jogador faz. Ver tabela abaixo. Padrão: `kill`. |
| **Descricao** | Texto do objetivo no diário. Escreva como ação. Ex.: `Derrote os esqueletos`. |
| **Alvo (tipo)** | Nome **técnico** do alvo. Para mobs e itens, é o **nome da classe C#** (ex.: `Esqueleto`, `ZumbiCustom`, `CopperCoin`). Para NPCs ou locais, o ID/nome interno usado pelo sistema. |
| **Nome do alvo** | Nome **amigavel**, exibido ao jogador. Ex.: `Esqueletos`. |
| **Quantidade** | Quantas vezes. Mínimo 1 (valores inválidos viram 1). |
| **Dica local** | Onde encontrar o alvo. Ex.: `Estrada ao norte do portao`. Ajuda o jogador a não ficar perdido. |

### Tipos de objetivo

| Tipo | O jogador deve... | Alvo (tipo) normalmente é... |
| --- | --- | --- |
| `kill` | Matar criaturas | classe do mob |
| `collect` | Reunir itens | classe do item |
| `explore` | Chegar a um local | ID/nome da região |
| `deliver` | Entregar algo a um NPC | ID do NPC destinatário |
| `talkto` | Conversar com um NPC | ID do NPC |
| `escort` | Escoltar um NPC/animal | ID/classe do escoltado |
| `craft` | Fabricar itens | classe do item |
| `useskill` | Usar uma habilidade | nome da skill |
| `interact` | Interagir com objeto/NPC | ID/classe do alvo |
| `defend` | Defender um ponto ou NPC | ID do ponto/NPC |

> Estas são as convenções esperadas. **O que o "Alvo (tipo)" realmente aceita depende do código do seu QuestManager/loader** (o gump só grava o texto). Se um tipo novo não conta progresso, confira no código como cada tipo compara o alvo.

**Dicas de design**

- Prefira 1 a 3 objetivos por quest para iniciantes.
- Combine tipos: `talkto` → `kill` → `deliver` conta uma história melhor do que só `kill`.
- Preencha sempre a **Dica local**. Jogadores novos não sabem onde ir.

---

## 5. Passo 4: Recompensas

| Campo | O que colocar | Referência |
| --- | --- | --- |
| **Ouro** | Quantidade de ouro, só números. | Easy: 100-500 · Normal: 500-2000 · Hard+: conforme a economia do shard |
| **Fama** | Pontos de fama ganhos. | 0 a algumas centenas |
| **Karma** | Pontos de karma. **Aceita valor negativo?** Depende do seu loader; teste antes. | 0 a algumas centenas |

Campos vazios ou com texto inválido viram `0`.

### Itens de recompensa

Clique em **Adicionar item**:

| Campo | O que colocar |
| --- | --- |
| **Tipo (class)** | Nome **exato da classe C#** do item. Maiúsculas importam. Ex.: `CopperCoin`, `CustomBandage`. |
| **Nome** | Nome de exibição (só informativo no diário/revisão). |
| **Quantidade** | Número de itens (mínimo 1). |

> **Cuidado com duplicar ouro:** o padrão do campo Tipo é `Gold`. Se você já colocou valor no campo **Ouro**, não adicione também um item `Gold`, ou o jogador recebe duas vezes. Se sua economia usa moedas customizadas (como `CopperCoin`), deixe **Ouro = 0** e entregue a recompensa como item.

---

## 6. Passo 5: Diálogo

Três caixas, uma para cada momento da conversa com o NPC:

| Campo | Quando aparece | O que escrever |
| --- | --- | --- |
| **Ao aceitar** | Jogador aceita a quest | Apresenta o problema e pede ajuda |
| **Em progresso** | Jogador volta falar sem ter terminado | Lembrete curto do que falta |
| **Ao completar** | Jogador entrega a quest | Agradecimento e entrega da recompensa |

**Várias falas:** separe com `|` (pipe). Cada trecho vira uma fala separada do NPC.

```
Os mortos cercam o portao!|Preciso de alguem corajoso.|Derrote 10 esqueletos e volte aqui.
```

**Regras:** não use aspas duplas no meio do texto, escreva tudo sem acentos, e mantenha cada fala curta (uma ou duas frases) para caber na tela do NPC.

---

## 7. Passo 6: Revisão e salvar

Confere ID, Nome, Categoria, Dificuldade, NPC, Repetível, número de Objetivos, Ouro e número de Itens. Se algo estiver errado, volte com **◀ Voltar**. Estando tudo certo, clique em **SALVAR QUEST**.

Mensagem de sucesso: `Quest salva com sucesso!` com o nome do arquivo. O arquivo vai para `Data/Quests/<ID>.json` (espaços do ID viram `_`). Depois digite **`[questreload`**.

---

## 8. Exemplo completo: "Portao Sitiado"

**Passo 1**

- ID: `quest_side_portao_sitiado`
- Nome: `Portao Sitiado`
- Descricao: `Esqueletos e zumbis cercam o portao norte. O Capitao Aldric precisa de reforcos.`
- Categoria: `Side` · Dificuldade: `Normal`
- Chain ID: *(vazio)* · Chain Order: `0`

**Passo 2**

- Giver: `npc_capitao_guarda` / `Capitao da Guarda Aldric`
- TurnIn: `npc_capitao_guarda` / `Capitao da Guarda Aldric`
- Marcar: nenhum · Cooldown: `0`

**Passo 3** (dois objetivos)

1. Tipo `kill` · Descricao `Derrote os esqueletos` · Alvo `Esqueleto` · Nome `Esqueletos` · Qtd `10` · Dica `Estrada ao norte do portao`
2. Tipo `kill` · Descricao `Derrote os zumbis` · Alvo `ZumbiCustom` · Nome `Zumbis` · Qtd `5` · Dica `Perto do portao`

**Passo 4**

- Ouro `0` · Fama `50` · Karma `25`
- Itens: `CopperCoin` x `200`; `CustomBandage` x `10`

**Passo 5**

- Aceitar: `Os mortos cercam o portao!|Preciso de ajuda, aventureiro.`
- Progresso: `Ainda ha mortos-vivos la fora. Continue!`
- Completar: `O portao esta salvo! Aqui esta sua recompensa.`

---

## 9. Checklist antes de salvar

- [ ] ID único, minúsculo, sem espaços e sem acento
- [ ] Nome e Descrição preenchidos
- [ ] Categoria e Dificuldade escritas exatamente como na lista
- [ ] ID do NPC igual ao ID configurado no NPC do jogo
- [ ] TurnIn preenchido (repita o Giver se for o mesmo NPC)
- [ ] Pelo menos 1 objetivo, com **classe C# correta** no Alvo
- [ ] Dica local em cada objetivo
- [ ] Ouro não duplicado com item `Gold`
- [ ] Nomes de classe dos itens conferidos (maiúsculas incluídas)
- [ ] Diálogos preenchidos, sem aspas duplas, falas separadas por `|`

## 10. Problemas comuns

| Sintoma | Causa provável |
| --- | --- |
| Quest não aparece no NPC | Giver NPC ID diferente do ID do NPC no jogo, ou faltou `[questreload` |
| Objetivo nunca progride | Alvo (tipo) com nome de classe errado ou tipo não suportado pelo loader |
| Recompensa não chega | Nome da classe do item errado |
| Ouro recebido em dobro | Ouro preenchido **e** item `Gold` |
| Texto estranho/quadrado no jogo | Acentos ou símbolos especiais nos campos |
| Valor antigo reaparece | O gump não apaga campos vazios; escreva um novo valor |
| Perdi tudo ao fechar | Nada é salvo antes do passo 6; só feche depois de salvar |
| Quest sumiu/mudou | Outro GM salvou com o mesmo ID e sobrescreveu o arquivo |