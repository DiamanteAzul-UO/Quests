/*****************************************************
 * QuestItemMatcher.cs
 * Resolver unico de "tipo de item" do sistema de quests
 *
 * Por que existe:
 *   O sistema aceita dois kinds de item nas definicoes JSON:
 *     1. Tipos reais do ServUO  (ex.: "Bandage", "IronOre", "TratadoApoioElfico")
 *     2. Tipos "virtuais" de quest (ex.: "CartaLacradaOlin", "AmuletoOssoRunas")
 *        que NAO possuem classe C# e sao representados por um QuestItem
 *        com a tag QuestItemType.
 *
 *   Antes desta classe, cada ponto do sistema resolvia itens de um jeito
 *   diferente (OnItemCollected comparava GetType().Name, SyncCollectObjectives
 *   desistia se FindTypeByName retornasse null, ConsumeItemsByName usava
 *   IndexOf...). Resultado: objetivos Collect/Deliver/Craft de itens virtuais
 *   NUNCA progrediam e a quest ficava impossivel de concluir.
 *
 *   Aqui existe UMA regra de match usada por todo o sistema.
 *
 * Versao: 1.0.0
 * Compativel com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using Server;
using Server.Mobiles;

namespace Server.Custom.QuestSystem.Quests
{
    /// <summary>
    /// Regras centralizadas de identificacao, contagem, consumo e criacao
    /// de itens referenciados por string nas definicoes de quest.
    /// </summary>
    public static class QuestItemMatcher
    {
        #region Type resolution

        private static readonly Dictionary<string, Type> _typeCache =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Resolve o nome de um tipo (classe C#) para o Type correspondente.
        /// Retorna null quando o tipo nao existe no shard (item "virtual" de quest).
        /// O resultado fica em cache porque FindTypeByName varre todos os assemblies.
        /// </summary>
        public static Type ResolveType(string typeSpec)
        {
            if (string.IsNullOrWhiteSpace(typeSpec))
                return null;

            typeSpec = typeSpec.Trim();

            lock (_typeCache)
            {
                Type cached;
                if (_typeCache.TryGetValue(typeSpec, out cached))
                    return cached;
            }

            Type resolved = null;

            try
            {
                // FindTypeByName casa pelo nome CURTO da classe (ignora maiusculas).
                resolved = ScriptCompiler.FindTypeByName(typeSpec);

                if (resolved == null)
                    resolved = ScriptCompiler.FindTypeByFullName(typeSpec);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[QuestItemMatcher] Erro ao resolver tipo '" + typeSpec + "': " + ex.Message);
            }

            if (resolved != null && !typeof(Item).IsAssignableFrom(resolved))
                resolved = null;

            lock (_typeCache)
            {
                _typeCache[typeSpec] = resolved;
            }

            return resolved;
        }

        /// <summary>
        /// True quando o typeSpec nao tem classe C# no shard e precisa ser
        /// representado por um QuestItem com tag.
        /// </summary>
        public static bool IsVirtualType(string typeSpec)
        {
            return !string.IsNullOrWhiteSpace(typeSpec) && ResolveType(typeSpec) == null;
        }

        private static readonly Dictionary<string, Type> _anyTypeCache =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Resolve um nome de tipo SEM restringir a Item.
        ///
        /// Por que existe separadamente de ResolveType:
        ///   ResolveType descarta de proposito tudo que nao for Item (para que um
        ///   "item virtual" de quest nao acabe instanciando uma classe qualquer).
        ///   Mas o matching de CRIATURAS (KillObjective.TargetType, DefendObjective
        ///   .EnemyType, CollectObjective.FromMobTypes) precisa resolver Mobile.
        ///   Usar ResolveType la devolvia sempre null, entao heranca nunca casava:
        ///   "targetType": "BaseCreature" ou "Animal" falhava silenciosamente e so
        ///   o nome exato da classe ("Rabbit") funcionava.
        /// </summary>
        /// <returns>O Type resolvido, ou null quando a classe nao existe no shard.</returns>
        public static Type ResolveAnyType(string typeSpec)
        {
            if (string.IsNullOrWhiteSpace(typeSpec))
                return null;

            typeSpec = typeSpec.Trim();

            lock (_anyTypeCache)
            {
                Type cached;
                if (_anyTypeCache.TryGetValue(typeSpec, out cached))
                    return cached;
            }

            Type resolved = null;

            try
            {
                resolved = ScriptCompiler.FindTypeByName(typeSpec);

                if (resolved == null)
                    resolved = ScriptCompiler.FindTypeByFullName(typeSpec);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[QuestItemMatcher] Erro ao resolver tipo '" + typeSpec + "': " + ex.Message);
            }

            lock (_anyTypeCache)
            {
                _anyTypeCache[typeSpec] = resolved;
            }

            return resolved;
        }

        /// <summary>Limpa o cache de tipos (usado apos [questreload / recompile).</summary>
        public static void ClearTypeCache()
        {
            lock (_typeCache)
            {
                _typeCache.Clear();
            }

            lock (_anyTypeCache)
            {
                _anyTypeCache.Clear();
            }
        }

        #endregion

        #region Matching

        /// <summary>
        /// Verifica se um item satisfaz a especificacao da definicao de quest.
        /// </summary>
        /// <param name="item">Item avaliado.</param>
        /// <param name="typeSpec">Nome da classe C# ou tag virtual (ex.: "CartaLacradaOlin").</param>
        /// <param name="displayName">Nome amigavel opcional (ex.: "Carta Lacrada de Olin").</param>
        /// <param name="hue">Hue exigido; 0 significa "qualquer hue".</param>
        /// <param name="allowDeleted">
        /// Permite casar um item já removido do mundo. Necessário apenas no
        /// caminho de Destroy: EventSink.ItemDeleted dispara DEPOIS de
        /// Item.Delete() marcar o item como Deleted, e sem isso a tag de item
        /// virtual de quest nunca seria reconhecida na destruição.
        /// </param>
        public static bool Matches(Item item, string typeSpec, string displayName = null, int hue = 0,
                                   bool allowDeleted = false)
        {
            if (item == null)
                return false;

            if (item.Deleted && !allowDeleted)
                return false;

            if (hue > 0 && item.Hue != hue)
                return false;

            // 1) Tag de quest item (caminho dos itens virtuais) — checado primeiro
            //    porque um QuestItem criado para "CartaLacradaOlin" tem GetType().Name == "QuestItem".
            string tag = GetQuestTag(item);
            if (!string.IsNullOrEmpty(tag))
            {
                if (MatchesToken(tag, typeSpec) || MatchesToken(tag, displayName))
                    return true;
            }

            if (string.IsNullOrWhiteSpace(typeSpec) && string.IsNullOrWhiteSpace(displayName))
                return false;

            // 2) Classe C# real (aceita subclasses: "BaseWeapon" casa "Longsword")
            if (!string.IsNullOrWhiteSpace(typeSpec))
            {
                Type real = ResolveType(typeSpec);
                if (real != null && real.IsInstanceOfType(item))
                    return true;

                if (MatchesToken(item.GetType().Name, typeSpec))
                    return true;
            }

            // 3) Nome exibido no jogo (item renomeado pelo GM / placed no mundo)
            string itemName = item.Name;

            if (!string.IsNullOrEmpty(itemName))
            {
                if (MatchesToken(itemName, typeSpec) || MatchesToken(itemName, displayName))
                    return true;
            }

            return false;
        }

        /// <summary>Sobrecarga sem nome amigavel.</summary>
        public static bool Matches(Item item, string typeSpec, int hue)
        {
            return Matches(item, typeSpec, null, hue);
        }

        /// <summary>
        /// Recupera a tag de item de quest, se houver.
        /// </summary>
        public static string GetQuestTag(Item item)
        {
            IQuestTaggedItem tagged = item as IQuestTaggedItem;
            if (tagged != null && !string.IsNullOrEmpty(tagged.QuestItemType))
                return tagged.QuestItemType;

            return null;
        }

        /// <summary>
        /// Compara dois identificadores ignorando caixa, acentos e espacos.
        /// Assim "CartaLacradaOlin" casa com "carta lacrada olin" e
        /// "Mestre de Docas Olin" casa com "mestre de docas olin".
        /// </summary>
        private static bool MatchesToken(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;

            return Normalize(a) == Normalize(b);
        }

        /// <summary>
        /// Normaliza texto: minusculas, sem acentos e sem espacos/pontuacao.
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            StringBuilder sb = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                switch (c)
                {
                    case 'á': case 'à': case 'ã': case 'â': case 'ä': c = 'a'; break;
                    case 'é': case 'è': case 'ê': case 'ë': c = 'e'; break;
                    case 'í': case 'ì': case 'î': case 'ï': c = 'i'; break;
                    case 'ó': case 'ò': case 'õ': case 'ô': case 'ö': c = 'o'; break;
                    case 'ú': case 'ù': case 'û': case 'ü': c = 'u'; break;
                    case 'ç': c = 'c'; break;
                    case 'ñ': c = 'n'; break;
                    case 'Á': case 'À': case 'Ã': case 'Â': case 'Ä': c = 'A'; break;
                    case 'É': case 'È': case 'Ê': case 'Ë': c = 'E'; break;
                    case 'Í': case 'Ì': case 'Î': case 'Ï': c = 'I'; break;
                    case 'Ó': case 'Ò': case 'Õ': case 'Ô': case 'Ö': c = 'O'; break;
                    case 'Ú': case 'Ù': case 'Û': case 'Ü': c = 'U'; break;
                    case 'Ç': c = 'C'; break;
                    case 'Ñ': c = 'N'; break;
                }

                c = char.ToLowerInvariant(c);

                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        #endregion

        #region Counting

        /// <summary>
        /// Conta itens que satisfazem a especificacao em TODO o inventario do
        /// jogador (mochila, sub-containers e itens equipados).
        /// </summary>
        public static int CountInPack(PlayerMobile player, string typeSpec, string displayName = null, int hue = 0)
        {
            if (player == null || player.Deleted)
                return 0;

            int total = 0;

            if (player.Backpack != null)
                total += CountInContainer(player.Backpack, typeSpec, displayName, hue, 0);

            // Itens equipados tambem contam (ex.: "vista o Amuleto").
            foreach (Item equipped in player.Items)
            {
                if (equipped == null || equipped.Deleted)
                    continue;

                if (equipped == player.Backpack)
                    continue;

                if (Matches(equipped, typeSpec, displayName, hue))
                    total += Math.Max(1, equipped.Amount);
            }

            return total;
        }

        /// <summary>Sobrecarga sem nome amigavel.</summary>
        public static int CountInPack(PlayerMobile player, string typeSpec, int hue)
        {
            return CountInPack(player, typeSpec, null, hue);
        }

        private static int CountInContainer(Item container, string typeSpec, string displayName, int hue, int depth)
        {
            if (container == null || container.Deleted || depth > 8)
                return 0;

            int total = 0;

            // O proprio container pode ser o item procurado (ex.: entregar uma mochila).
            if (depth > 0 && Matches(container, typeSpec, displayName, hue))
                total += Math.Max(1, container.Amount);

            List<Item> items = container.Items;
            if (items == null)
                return total;

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];

                if (item == null || item.Deleted)
                    continue;

                if (Matches(item, typeSpec, displayName, hue))
                    total += Math.Max(1, item.Amount);

                // Recursao: itens dentro de bolsas/pacas dentro da mochila.
                if (item.Items != null && item.Items.Count > 0)
                    total += CountInContainer(item, typeSpec, displayName, hue, depth + 1);
            }

            return total;
        }

        #endregion

        #region Consuming

        /// <summary>
        /// Remove <paramref name="amount"/> itens que satisfacam a especificacao.
        /// Retorna quantos foram efetivamente removidos.
        /// </summary>
        public static int Consume(PlayerMobile player, string typeSpec, string displayName, int hue, int amount)
        {
            if (player == null || player.Deleted || amount <= 0)
                return 0;

            if (string.IsNullOrWhiteSpace(typeSpec) && string.IsNullOrWhiteSpace(displayName))
                return 0;

            List<Item> candidates = new List<Item>();
            CollectMatches(player.Backpack, typeSpec, displayName, hue, candidates, 0);

            int removed = 0;

            foreach (Item item in candidates)
            {
                if (removed >= amount)
                    break;

                if (item == null || item.Deleted)
                    continue;

                int have = Math.Max(1, item.Amount);
                int take = Math.Min(have, amount - removed);

                if (take <= 0)
                    continue;

                if (!item.Stackable || take >= have)
                {
                    item.Delete();
                    removed += take;
                }
                else
                {
                    item.Amount -= take;
                    removed += take;
                }
            }

            return removed;
        }

        /// <summary>Sobrecarga sem nome amigavel.</summary>
        public static int Consume(PlayerMobile player, string typeSpec, int hue, int amount)
        {
            return Consume(player, typeSpec, null, hue, amount);
        }

        private static void CollectMatches(Item container, string typeSpec, string displayName, int hue,
                                           List<Item> results, int depth)
        {
            if (container == null || container.Deleted || depth > 8)
                return;

            List<Item> items = container.Items;
            if (items == null)
                return;

            // Copia: Consume() deleta itens enquanto iteramos.
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];

                if (item == null || item.Deleted)
                    continue;

                if (Matches(item, typeSpec, displayName, hue))
                    results.Add(item);
                else if (item.Items != null && item.Items.Count > 0)
                    CollectMatches(item, typeSpec, displayName, hue, results, depth + 1);
            }
        }

        #endregion

        #region Creation

        /// <summary>
        /// Cria o item descrito por (typeSpec, displayName).
        ///
        /// Se a classe existir no shard, instancia a classe real.
        /// Se NAO existir, cria um QuestItem com a tag typeSpec — assim itens
        /// "virtuais" (CartaLacradaOlin, AmuletoOssoRunas, SeloFortaleza...)
        /// passam a existir de verdade e podem ser coletados/entregues.
        ///
        /// Nunca retorna null: o fallback garante que recompensas e grantItems
        /// nao sejam silenciosamente descartados.
        /// </summary>
        public static Item Create(string typeSpec, string displayName, int hue = 0, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(typeSpec))
                typeSpec = string.IsNullOrWhiteSpace(displayName) ? "QuestItemGenerico" : displayName;

            Type real = ResolveType(typeSpec);

            if (real != null)
            {
                try
                {
                    Item item = Activator.CreateInstance(real) as Item;

                    if (item != null)
                    {
                        if (!string.IsNullOrWhiteSpace(displayName))
                            item.Name = displayName;

                        if (hue > 0)
                            item.Hue = hue;

                        item.LootType = LootType.Blessed;

                        if (amount > 1)
                        {
                            if (item.Stackable)
                                item.Amount = amount;
                            else
                                Console.WriteLine("[QuestItemMatcher] AVISO: '" + typeSpec +
                                                  "' nao e empilhavel; amount=" + amount + " foi ignorado.");
                        }

                        return item;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[QuestItemMatcher] Nao foi possivel instanciar '" + typeSpec +
                                      "' (" + ex.GetType().Name + "). Usando QuestItem com tag.");
                }
            }

            return QuestItem.Create(typeSpec, displayName, hue, amount);
        }

        /// <summary>
        /// Entrega um item ao jogador, caindo no chao se a mochila estiver cheia.
        /// </summary>
        public static bool GiveTo(PlayerMobile player, Item item, out bool droppedOnGround)
        {
            droppedOnGround = false;

            if (player == null || player.Deleted || item == null)
                return false;

            if (player.Backpack != null && player.Backpack.TryDropItem(player, item, false))
                return true;

            if (player.AddToBackpack(item))
                return true;

            item.MoveToWorld(player.Location, player.Map);
            droppedOnGround = true;
            return true;
        }

        #endregion
    }
}
