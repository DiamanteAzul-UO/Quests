/*****************************************************
 * SpeechHelper.cs
 * Utilitários para processamento de fala
 * Etapa 2 - NPCBehavior
 * 
 * Versão: 1.1.0 - Simplificado para sistema de Prompt
 * Compatível com: ServUO
 *****************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Server;

namespace Server.Custom.QuestSystem.Conversation
{
    /// <summary>
    /// Helper para processamento e análise de fala
    /// </summary>
    public static class SpeechHelper
    {
        #region Word Detection

        /// <summary>
        /// Verifica se a fala contém alguma das palavras trigger
        /// </summary>
        public static bool ContainsTriggerWord(string speech, string[] triggerWords)
        {
            if (string.IsNullOrWhiteSpace(speech) || triggerWords == null || triggerWords.Length == 0)
                return false;

            speech = NormalizeSpeech(speech);

            foreach (string trigger in triggerWords)
            {
                string normalizedTrigger = NormalizeSpeech(trigger);
                
                // Verifica match exato ou como palavra isolada
                if (speech.Equals(normalizedTrigger, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Verifica se contém como palavra (não parte de outra palavra)
                if (ContainsWord(speech, normalizedTrigger))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Verifica se contém palavra isolada
        /// </summary>
        private static bool ContainsWord(string text, string word)
        {
            string pattern = $@"\b{Regex.Escape(word)}\b";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Normaliza texto removendo acentos e caracteres especiais
        /// </summary>
        public static string NormalizeSpeech(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Remove espaços extras
            text = text.Trim();
            text = Regex.Replace(text, @"\s+", " ");

            // Converte para minúsculas
            text = text.ToLowerInvariant();

            // Remove pontuação comum
            text = Regex.Replace(text, @"[.,!?;:""'`]", "");

            return text;
        }

        /// <summary>
        /// Remove acentos do texto
        /// </summary>
        public static string RemoveAccents(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (char c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != 
                    System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        #endregion

        #region Number Parsing

        /// <summary>
        /// Tenta extrair um número da fala
        /// </summary>
        public static bool TryParseNumber(string speech, out int number)
        {
            number = 0;
            
            if (string.IsNullOrWhiteSpace(speech))
                return false;

            speech = speech.Trim();

            // Tenta parse direto
            if (int.TryParse(speech, out number))
                return true;

            // Tenta extrair primeiro número encontrado
            var match = Regex.Match(speech, @"\d+");
            if (match.Success && int.TryParse(match.Value, out number))
                return true;

            // Tenta palavras numéricas
            return TryParseWordNumber(speech, out number);
        }

        /// <summary>
        /// Converte palavras numéricas (um, dois, três...)
        /// </summary>
        private static bool TryParseWordNumber(string word, out int number)
        {
            number = 0;
            word = NormalizeSpeech(word);

            var wordNumbers = new Dictionary<string, int>
            {
                { "zero", 0 },
                { "um", 1 }, { "uma", 1 }, { "primeiro", 1 }, { "primeira", 1 },
                { "dois", 2 }, { "duas", 2 }, { "segundo", 2 }, { "segunda", 2 },
                { "tres", 3 }, { "terceiro", 3 }, { "terceira", 3 },
                { "quatro", 4 }, { "quarto", 4 }, { "quarta", 4 },
                { "cinco", 5 }, { "quinto", 5 }, { "quinta", 5 },
                { "seis", 6 }, { "sexto", 6 }, { "sexta", 6 },
                { "sete", 7 }, { "setimo", 7 }, { "setima", 7 },
                { "oito", 8 }, { "oitavo", 8 }, { "oitava", 8 },
                { "nove", 9 }, { "nono", 9 }, { "nona", 9 },
                { "dez", 10 }, { "decimo", 10 }, { "decima", 10 },
                // Inglês
                { "one", 1 }, { "first", 1 },
                { "two", 2 }, { "second", 2 },
                { "three", 3 }, { "third", 3 },
                { "four", 4 }, { "fourth", 4 },
                { "five", 5 }, { "fifth", 5 },
                { "six", 6 }, { "sixth", 6 },
                { "seven", 7 }, { "seventh", 7 },
                { "eight", 8 }, { "eighth", 8 },
                { "nine", 9 }, { "ninth", 9 },
                { "ten", 10 }, { "tenth", 10 }
            };

            if (wordNumbers.TryGetValue(RemoveAccents(word), out number))
                return true;

            return false;
        }

        #endregion

        #region Response Detection

        /// <summary>
        /// Verifica se é resposta positiva (sim, aceitar, etc.)
        /// </summary>
        public static bool IsPositiveResponse(string speech)
        {
            return ContainsTriggerWord(speech, new[]
            {
                "sim", "yes", "ok", "aceitar", "aceito", "accept",
                "confirmar", "confirm", "claro", "certo", "positivo",
                "concordo", "agree", "quero", "vamos", "pode ser"
            });
        }

        /// <summary>
        /// Verifica se é resposta negativa (não, recusar, etc.)
        /// </summary>
        public static bool IsNegativeResponse(string speech)
        {
            return ContainsTriggerWord(speech, new[]
            {
                "nao", "no", "recusar", "recuso", "decline", "cancelar",
                "cancel", "desistir", "nope", "negativo", "nao quero"
            });
        }

        /// <summary>
        /// Verifica se quer voltar ao menu anterior
        /// </summary>
        public static bool IsBackCommand(string speech)
        {
            return ContainsTriggerWord(speech, QuestConstants.DefaultBackWords);
        }

        /// <summary>
        /// Verifica se é despedida
        /// </summary>
        public static bool IsFarewell(string speech)
        {
            return ContainsTriggerWord(speech, QuestConstants.DefaultFarewellWords);
        }

        /// <summary>
        /// Verifica se é saudação
        /// </summary>
        public static bool IsGreeting(string speech)
        {
            return ContainsTriggerWord(speech, QuestConstants.DefaultTriggerWords);
        }

        #endregion

        #region Text Formatting

        /// <summary>
        /// Substitui placeholders no texto
        /// </summary>
        public static string ReplacePlaceholders(string text, Dictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(text) || values == null)
                return text;

            foreach (var kvp in values)
            {
                text = text.Replace($"{{{kvp.Key}}}", kvp.Value);
                text = text.Replace($"{{{kvp.Key.ToLower()}}}", kvp.Value);
            }

            return text;
        }

        /// <summary>
        /// Substitui placeholders comuns
        /// </summary>
        public static string FormatMessage(string text, Mobile npc = null, Mobile player = null)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var replacements = new Dictionary<string, string>();

            if (player != null)
            {
                replacements["playerName"] = player.Name ?? "Aventureiro";
                replacements["player.Name"] = player.Name ?? "Aventureiro";
            }

            if (npc != null)
            {
                replacements["npcName"] = npc.Name ?? "NPC";
                replacements["npc.Name"] = npc.Name ?? "NPC";
            }

            return ReplacePlaceholders(text, replacements);
        }

        /// <summary>
        /// Quebra texto longo em múltiplas linhas
        /// </summary>
        public static List<string> WrapText(string text, int maxLineLength = 80)
        {
            var lines = new List<string>();
            
            if (string.IsNullOrEmpty(text))
                return lines;

            var words = text.Split(' ');
            var currentLine = new StringBuilder();

            foreach (string word in words)
            {
                if (currentLine.Length + word.Length + 1 > maxLineLength)
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString().Trim());
                        currentLine.Clear();
                    }
                }

                if (currentLine.Length > 0)
                    currentLine.Append(' ');
                
                currentLine.Append(word);
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine.ToString().Trim());

            return lines;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Valida range de interação
        /// </summary>
        public static bool IsInRange(Mobile from, Mobile to, int maxRange)
        {
            if (from == null || to == null)
                return false;

            if (from.Map != to.Map)
                return false;

            return from.InRange(to.Location, maxRange);
        }

        /// <summary>
        /// Valida se jogador pode interagir
        /// </summary>
        public static bool CanInteract(Mobile player)
        {
            if (player == null || player.Deleted)
                return false;

            if (!player.Alive)
                return false;

            if (player.NetState == null)
                return false;

            return true;
        }

        #endregion
    }
}