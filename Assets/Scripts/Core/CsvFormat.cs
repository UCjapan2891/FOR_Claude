using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VRClass.Core
{
    /// <summary>
    /// CSV の1行を組み立てる。カルチャに依存しない書式（小数点は常に「.」）。
    /// </summary>
    public static class CsvFormat
    {
        /// <summary>カンマ・引用符・改行を含む値を "..." で囲み、中の " は "" にする。null は空欄。</summary>
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            bool needsQuote = value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0
                              || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0;
            if (!needsQuote)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>値をエスケープしてカンマで結ぶ。改行は付けない。</summary>
        public static string Join(IEnumerable<string> values)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (string v in values)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(Escape(v));
                first = false;
            }

            return sb.ToString();
        }

        /// <summary>秒を小数3桁で（SPEC 6章：time 列）。</summary>
        public static string Seconds(double seconds)
        {
            return seconds.ToString("F3", CultureInfo.InvariantCulture);
        }

        /// <summary>小数を指定の桁数で。</summary>
        public static string Number(double value, int decimals)
        {
            return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        /// <summary>null なら空欄、値があれば指定の桁数で（head.csv の θ など）。</summary>
        public static string Number(double? value, int decimals)
        {
            return value.HasValue ? Number(value.Value, decimals) : string.Empty;
        }

        public static string Integer(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
