using System;
using VRClass.Core;

namespace VRClass.Logging
{
    /// <summary>
    /// head.csv を書く（SPEC 6章）。1人1ファイルで、2本の授業を lesson 列で区別する。
    /// 列：time, lesson, pos_x, pos_y, pos_z, rot_x, rot_y, rot_z, rot_w, theta
    /// theta（対象部品との角度、度）は判定対象がないとき空欄。
    /// Unity の型に依存しないよう、値は float で受け取る（テストしやすくするため）。
    /// </summary>
    public sealed class HeadLogWriter : IDisposable
    {
        public static readonly string[] Header =
        {
            "time", "lesson", "pos_x", "pos_y", "pos_z", "rot_x", "rot_y", "rot_z", "rot_w", "theta",
        };

        private const int PositionDecimals = 4; // 0.1 mm
        private const int RotationDecimals = 5;
        private const int AngleDecimals = 2;

        private readonly CsvAppendWriter _writer;
        private readonly string[] _row = new string[Header.Length];

        /// <param name="path">head.csv のパス（<see cref="LogPaths.HeadFile"/>）</param>
        public HeadLogWriter(string path)
        {
            // 10 Hz で毎回ディスクまで書き出すと重いので、OS へのフラッシュにとどめる。
            // アプリが落ちた場合は OS が書き出すので残る。
            _writer = new CsvAppendWriter(path, Header, flushToDisk: false);
        }

        public string Path => _writer.Path;

        public void Write(double time, string lesson,
            float posX, float posY, float posZ,
            float rotX, float rotY, float rotZ, float rotW,
            float? thetaDeg)
        {
            _row[0] = CsvFormat.Seconds(time);
            _row[1] = lesson ?? string.Empty;
            _row[2] = CsvFormat.Number(posX, PositionDecimals);
            _row[3] = CsvFormat.Number(posY, PositionDecimals);
            _row[4] = CsvFormat.Number(posZ, PositionDecimals);
            _row[5] = CsvFormat.Number(rotX, RotationDecimals);
            _row[6] = CsvFormat.Number(rotY, RotationDecimals);
            _row[7] = CsvFormat.Number(rotZ, RotationDecimals);
            _row[8] = CsvFormat.Number(rotW, RotationDecimals);
            _row[9] = CsvFormat.Number((double?)thetaDeg, AngleDecimals);
            _writer.WriteRow(_row);
        }

        public void Dispose() => _writer.Dispose();
    }
}
