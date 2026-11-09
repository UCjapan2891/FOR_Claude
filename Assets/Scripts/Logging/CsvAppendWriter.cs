using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRClass.Core;

namespace VRClass.Logging
{
    /// <summary>
    /// CSV ファイルに1行ずつ追記し、書くたびにフラッシュする。
    /// アプリが落ちても、それまでに書いた行は残る（CLAUDE.md 3章）。
    ///
    /// - ファイルが空なら、UTF-8 の BOM と見出し行を先に書く（Excel で日本語が化けないように）
    /// - 既にあるファイルには追記する（2本目の授業も同じファイルに続ける）
    /// - 書き込み中も他のプロセスから読める（FileShare.Read）
    /// </summary>
    public sealed class CsvAppendWriter : IDisposable
    {
        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        private readonly FileStream _stream;
        private readonly StreamWriter _writer;
        private readonly bool _flushToDisk;
        private readonly int _columnCount;

        /// <param name="path">書き込むファイル</param>
        /// <param name="header">見出し行の列名</param>
        /// <param name="flushToDisk">true なら OS のキャッシュも毎回ディスクへ書き出す（電源断にも備える。少し重い）</param>
        public CsvAppendWriter(string path, IReadOnlyList<string> header, bool flushToDisk)
        {
            if (header == null || header.Count == 0)
            {
                throw new ArgumentException("見出し行が空です。", nameof(header));
            }

            string dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Path = path;
            _columnCount = header.Count;
            _flushToDisk = flushToDisk;
            _stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            bool isNew = _stream.Length == 0;
            if (isNew)
            {
                _stream.Write(Utf8Bom, 0, Utf8Bom.Length);
            }

            _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { NewLine = "\n" };
            if (isNew)
            {
                WriteRow(header);
            }
        }

        public string Path { get; }

        public bool IsDisposed { get; private set; }

        /// <summary>1行書いてフラッシュする。列の数が見出しと違えば例外。</summary>
        public void WriteRow(IReadOnlyList<string> values)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(CsvAppendWriter), "ログファイルは既に閉じています：" + Path);
            }

            if (values.Count != _columnCount)
            {
                throw new ArgumentException($"列の数が見出しと違います（{values.Count} ≠ {_columnCount}）。", nameof(values));
            }

            _writer.WriteLine(CsvFormat.Join(values));
            _writer.Flush();
            _stream.Flush(_flushToDisk);
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            _writer.Dispose(); // _stream も閉じる
        }
    }
}
