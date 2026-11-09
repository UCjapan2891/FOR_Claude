using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace VRClass.Core
{
    /// <summary>
    /// participants.csv に登録された参加者番号の一覧（SPEC 2章）。
    ///
    /// ファイルの形式：
    /// - 1行に1つの id。先頭の列だけを読む（2列目以降は無視）
    /// - 1行目が数値でなければ見出し行（例：「id」）として読み飛ばす
    /// - 空行と「#」で始まる行は無視する
    /// - それ以外で読めない行・0以下・重複は <see cref="Problems"/> に記録する
    /// 氏名は扱わない（SPEC 2章）。
    /// </summary>
    public sealed class ParticipantRegistry
    {
        private readonly HashSet<int> _ids;
        private readonly List<string> _problems;

        private ParticipantRegistry(HashSet<int> ids, List<string> problems)
        {
            _ids = ids;
            _problems = problems;
        }

        /// <summary>登録されている人数。</summary>
        public int Count => _ids.Count;

        /// <summary>読み込み時に見つかった問題（行番号つき）。空なら問題なし。</summary>
        public IReadOnlyList<string> Problems => _problems;

        /// <summary>id が登録されているか。</summary>
        public bool IsRegistered(int id) => _ids.Contains(id);

        /// <summary>CSV の文字列から読み込む。</summary>
        public static ParticipantRegistry Parse(string csvText)
        {
            var ids = new HashSet<int>();
            var problems = new List<string>();
            if (csvText == null)
            {
                return new ParticipantRegistry(ids, problems);
            }

            string[] lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool firstContentLine = true;
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string line = lines[i].Trim().TrimStart('﻿');
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string first = line.Split(',')[0].Trim().Trim('"');
                bool isFirst = firstContentLine;
                firstContentLine = false;

                if (!int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                {
                    if (!isFirst)
                    {
                        problems.Add($"{lineNo}行目：数値として読めません（{first}）");
                    }

                    continue; // 1行目なら見出し行として扱う
                }

                if (id <= 0)
                {
                    problems.Add($"{lineNo}行目：参加者番号は正の整数です（{id}）");
                    continue;
                }

                if (!ids.Add(id))
                {
                    problems.Add($"{lineNo}行目：番号が重複しています（{id}）");
                }
            }

            return new ParticipantRegistry(ids, problems);
        }

        /// <summary>ファイルから読み込む。ファイルがなければ FileNotFoundException。</summary>
        public static ParticipantRegistry Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"participants.csv が見つかりません：{path}", path);
            }

            return Parse(File.ReadAllText(path));
        }

        /// <summary>
        /// 番号入力の文字列を id に変換する。正の整数でなければ false。
        /// 全角数字も受け付ける。
        /// </summary>
        public static bool TryParseInput(string input, out int id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string normalized = ToHalfWidthDigits(input.Trim());
            if (!int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
            {
                return false;
            }

            id = parsed;
            return true;
        }

        private static string ToHalfWidthDigits(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '０' && chars[i] <= '９')
                {
                    chars[i] = (char)('0' + (chars[i] - '０'));
                }
            }

            return new string(chars);
        }
    }
}
