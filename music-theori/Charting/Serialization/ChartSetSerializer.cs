using System;
using System.Globalization;
using System.IO;
using System.Numerics;

using theori.GameModes;

namespace theori.Charting.Serialization
{
    public sealed class ChartSetSerializer
    {
        public string ParentDirectory { get; }

        public ChartSetSerializer(string chartsDir)
        {
            ParentDirectory = chartsDir;
        }

        public ChartSetInfo LoadFromFile(string directory, string fileName)
        {
            string filePath = Path.Combine(ParentDirectory, directory, fileName);
            var fsi = new FileInfo(filePath);
            fsi.Refresh(); // TODO(local): is this needed?

            long lastWriteTime = fsi.LastWriteTimeUtc.Ticks;
            var setInfo = new ChartSetInfo()
            {
                LastWriteTime = lastWriteTime,
                FilePath = directory,
                FileName = fileName,
            };

            using (var reader = new StreamReader(File.OpenRead(filePath)))
                DeserializeChartSetInfo(reader, setInfo);

            return setInfo;
        }

        private void DeserializeChartSetInfo(StreamReader reader, ChartSetInfo setInfo)
        {
            ChartInfo? chartInfo = null;
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                if (line == "[chart-info]")
                {
                    chartInfo = new ChartInfo() { Set = setInfo };
                    setInfo.Charts.Add(chartInfo);
                }
                else if (line.TrySplit('=', out string key, out string value))
                {
                    if (chartInfo == null) continue;
                    switch (key)
                    {
                        case "game-mode": chartInfo.GameMode = GameMode.GetInstance(value); break;
                        case "file-type": chartInfo.ChartFileType = value; break;

                        case "chart-file": chartInfo.FileName = value; break;
                        case "song-title": chartInfo.SongTitle = value; break;
                        case "song-artist": chartInfo.SongArtist = value; break;
                        case "song-file": chartInfo.SongFileName = value; break;
                        case "song-volume": chartInfo.SongVolume = int.Parse(value); break;
                        case "chart-offset": chartInfo.ChartOffset = double.Parse(value); break;
                        case "charter": chartInfo.Charter = value; break;
                        case "chart-duration": chartInfo.ChartDuration = double.Parse(value); break;

                        case "jacket-file": chartInfo.JacketFileName = value; break;
                        case "jacket-artist": chartInfo.JacketArtist = value; break;
                        case "background-file": chartInfo.BackgroundFileName = value; break;
                        case "background-artist": chartInfo.BackgroundArtist = value; break;
                        case "difficulty-level": chartInfo.DifficultyLevel = double.Parse(value); break;
                        case "difficulty-index": chartInfo.DifficultyIndex = int.Parse(value); break;
                        case "difficulty-name": chartInfo.DifficultyName = value; break;
                        case "difficulty-name-short": chartInfo.DifficultyNameShort = value; break;

                        float itof(int o) => int.Parse(value.Substring(o * 2, 2), NumberStyles.HexNumber) / 255.0f;
                        case "difficulty-color": chartInfo.DifficultyColor = new Vector3(itof(0), itof(1), itof(2)); break;
                    }
                }
            }
        }

        public void SaveToFile(ChartSetInfo setInfo)
        {
            string filePath = Path.Combine(ParentDirectory, setInfo.FilePath, setInfo.FileName);
            Directory.CreateDirectory(Path.Combine(ParentDirectory, setInfo.FilePath));

            using (var writer = new StreamWriter(File.Open(filePath, FileMode.Create)))
                SerializeSetInfo(writer, setInfo);

            var fsi = new FileInfo(filePath);
            fsi.Refresh(); // TODO(local): is this needed?

            setInfo.LastWriteTime = fsi.LastWriteTimeUtc.Ticks;
        }

        private void SerializeSetInfo(StreamWriter writer, ChartSetInfo setInfo)
        {
            foreach (var chartInfo in setInfo.Charts)
            {
                writer.WriteLine($"[chart-info]");
                writer.WriteLine($"chart-file={ chartInfo.FileName }");
                writer.WriteLine($"chart-file={ chartInfo.FileName }");
                writer.WriteLine($"song-title={ chartInfo.SongTitle }");
                writer.WriteLine($"song-artist={ chartInfo.SongArtist }");
                writer.WriteLine($"song-file={ chartInfo.SongFileName }");
                writer.WriteLine($"song-volume={ chartInfo.SongVolume }");
                writer.WriteLine($"chart-offset={ chartInfo.ChartOffset.Seconds }");
                writer.WriteLine($"charter={ chartInfo.Charter }");
                writer.WriteLine($"chart-duration={ chartInfo.ChartDuration.Seconds }");

                WriteOptS("game-mode", chartInfo.GameMode?.Name);
                WriteOptS("file-type", chartInfo.ChartFileType);
                WriteOptS("jacket-file", chartInfo.JacketFileName);
                WriteOptS("jacket-artist", chartInfo.JacketArtist);
                WriteOptS("background-file", chartInfo.BackgroundFileName);
                WriteOptS("background-artist", chartInfo.BackgroundArtist);
                writer.WriteLine($"difficulty-level={ chartInfo.DifficultyLevel }");
                WriteOptI("difficulty-index", chartInfo.DifficultyIndex);
                WriteOptS("difficulty-name", chartInfo.DifficultyName);
                WriteOptS("difficulty-name-short", chartInfo.DifficultyNameShort);

                if (chartInfo.DifficultyColor != null)
                {
                    var c = chartInfo.DifficultyColor.Value;
                    int ival(float f) => MathL.RoundToInt(f * 255);
                    writer.WriteLine($"difficulty-color={ival(c.X):X2}{ival(c.Y):X2}{ival(c.Z):X2}");
                }
            }

            void WriteOptI(string key, int? value)
            {
                if (value == null) return;
                writer.WriteLine($"{ key }={ value.Value }");
            }

            void WriteOptS(string key, string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                writer.WriteLine($"{ key }={ value }");
            }
        }
    }
}
