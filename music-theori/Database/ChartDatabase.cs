﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using theori.Charting;
using theori.Graphics;

namespace theori.Database
{
    using StringSet = HashSet<string>;
    using SetDict = Dictionary<long, ChartSetInfo>;
    using StringSetDict = Dictionary<string, ChartSetInfo>;
    using ChartDict = Dictionary<long, ChartInfo>;

    public class ChartDatabase
    {
        private const int DEFAULT_VERSION = 1;

        public readonly string FilePath;

        protected virtual int Version => DEFAULT_VERSION;

        private SQLiteConnection m_connection;

        private readonly StringSet m_setFiles = new StringSet();

        private readonly SetDict m_chartSets = new SetDict();
        private readonly ChartDict m_charts = new ChartDict();

        private readonly StringSetDict m_chartSetsByFilePath = new StringSetDict();

        public IEnumerable<ChartSetInfo> ChartSets => m_chartSets.Values;
        public IEnumerable<ChartInfo> Charts => m_charts.Values;

        public ChartDatabase(string filePath)
        {
            FilePath = filePath;
            m_connection = new SQLiteConnection($"Data Source={ filePath }");
        }

        public void OpenLocal()
        {
            m_connection.Open();

            bool rebuild;
            try
            {
                int vGot = -1;
                using (var reader = ExecReader("SELECT version FROM `Database`"))
                {
                    reader.Read();
                    vGot = reader.GetInt32(0);
                }

                rebuild = vGot != Version;
            }
            catch (SQLiteException e)
            {
                Logger.Log(e.Message);
                rebuild = true;
            }

            if (rebuild)
                Initialize();
            else LoadData();
        }

        private int Exec(string commandText)
        {
            using (var command = new SQLiteCommand(commandText, m_connection))
                return command.ExecuteNonQuery();
        }

        private object ExecScalar(string commandText)
        {
            using (var command = new SQLiteCommand(commandText, m_connection))
                return command.ExecuteScalar();
        }

        private SQLiteDataReader ExecReader(string commandText)
        {
            using (var command = new SQLiteCommand(commandText, m_connection))
                return command.ExecuteReader();
        }

        private int Exec(string commandText, params object?[] values)
        {
            using (var command = new SQLiteCommand(commandText, m_connection))
            {
                for (int i = 0; i < values.Length; i++)
                {
                    var param = command.CreateParameter();
                    param.Value = values[i];

                    command.Parameters.Add(param);
                }
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Empties the database and reconstructs the tables.
        /// </summary>
        public void Initialize()
        {
            Exec($"DROP TABLE IF EXISTS Database");
            Exec($"CREATE TABLE Database ( version INTEGER )");
            Exec($"INSERT INTO Database ( rowid, version ) VALUES ( 1, { Version } )");

            InitializeTables();
        }

        /// <summary>
        /// This should drop all tables and recreate them.
        /// </summary>
        protected virtual void InitializeTables()
        {
            Exec($"DROP TABLE IF EXISTS Sets");
            Exec($"DROP TABLE IF EXISTS Charts");
            Exec($"DROP TABLE IF EXISTS Scores");

            // lwt = last write time, and should likely be epoch time (the usual 1970 one)
            //  rather than C#'s (which starts from the year 0001) if we plan for other applications
            //  reading in the database themselves.
            // If we don't care, then it's fine that it's C#'s epoch instead.
            // C# gives easy access to DateTime ticks, which are 100 nanoseconds each and count
            //  from 1/1/0001.
            Exec($@"CREATE TABLE Sets (
                id INTEGER PRIMARY KEY,
                lwt INTEGER NOT NULL,
                uploadID INTEGER,
                filePath TEXT NOT NULL,
                fileName TEXT NOT NULL
            )");

            Exec($@"CREATE TABLE Charts (
                id INTEGER PRIMARY KEY,
                setId INTEGER NOT NULL,
                lwt INTEGER NOT NULL,
                fileName TEXT NOT NULL,
                songTitle TEXT NOT NULL COLLATE NOCASE,
                songArtist TEXT NOT NULL COLLATE NOCASE,
                songFileName TEXT NOT NULL,
                songVolume INTEGER NOT NULL,
                chartOffset REAL NOT NULL,
                charter TEXT NOT NULL COLLATE NOCASE,
                jacketFileName TEXT,
                jacketArtist TEXT,
                backgroundFileName TEXT,
                backgroundArtist TEXT,
                diffLevel REAL NOT NULL,
                diffIndex INTEGER,
                diffName TEXT NOT NULL,
                diffNameShort TEXT,
                diffColor INTEGER,
                chartDuration REAL NOT NULL,
                tags TEXT NOT NULL COLLATE NOCASE,
                FOREIGN KEY(setId) REFERENCES Sets(id)
            )");

            Exec($@"CREATE TABLE Scores (
                id INTEGER PRIMARY KEY,
                chartId INTEGER NOT NULL,
                score INTEGER NOT NULL,
                FOREIGN KEY(chartId) REFERENCES Charts(id)
            )");

            Exec($@"CREATE TABLE LocalChartConfig (
                id INTEGER PRIMARY KEY,
                chartId INTEGER NOT NULL,
                config STRING NOT NULL,
                FOREIGN KEY(chartId) REFERENCES Charts(id)
            )");
        }

        public bool ContainsSetAtLocation(string entrySubDirectory)
        {
            return m_chartSetsByFilePath.ContainsKey(entrySubDirectory);
        }

        public void AddSet(ChartSetInfo setInfo)
        {
            string relPath = Path.Combine(setInfo.FilePath, setInfo.FileName);
            AddSetInfoToDatabase(relPath, setInfo);
        }

        public void RemoveSet(ChartSetInfo setInfo)
        {
            string relPath = Path.Combine(setInfo.FilePath, setInfo.FileName);

            m_chartSets.Remove(setInfo.ID, out var _);
            m_chartSetsByFilePath.Remove(relPath, out var _);

            m_setFiles.Remove(relPath);

            Exec("DELETE FROM Sets WHERE filePath=?", setInfo.FilePath);

            foreach (var chart in setInfo.Charts)
                RemoveChart(chart);
        }

        public void RemoveChart(ChartInfo chart)
        {
            m_charts.Remove(chart.ID, out var _);

            Exec("DELETE FROM Charts WHERE setId=? AND fileName=?", chart.SetID, chart.FileName);
            Exec("DELETE FROM LocalChartConfig WHERE chartId=?", chart.ID);
        }

        public string GetLocalConfigForChart(ChartInfo chartInfo)
        {
            using var reader = ExecReader($"SELECT config FROM LocalChartConfig WHERE chartId={ chartInfo.ID }");
            return reader.Read() ? reader.GetString(0) : string.Empty;
        }

        public void SaveLocalConfigForChart(ChartInfo chartInfo, string config)
        {
            Exec("UPDATE LocalChartConfig SET config=? WHERE chartId=?", config, chartInfo.ID);
        }

        private void AddSetInfoToDatabase(string relPath, ChartSetInfo setInfo)
        {
            Debug.Assert(Path.Combine(setInfo.FilePath, setInfo.FileName) == relPath);

            bool isUpdate = !m_setFiles.Add(relPath);
            m_chartSetsByFilePath[relPath] = setInfo;

            if (isUpdate)
            {
            }
            else
            {
                if (setInfo.ID != 0) Logger.Log($"Adding a set info with non-zero primary key already set. This will be overwritten with the new key.");
                int setResult = Exec("INSERT INTO Sets (lwt,uploadID,filePath,fileName) VALUES (?,?,?,?)",
                    setInfo.LastWriteTime,
                    setInfo.OnlineID,
                    setInfo.FilePath,
                    setInfo.FileName);
                if (setResult == 0)
                {
                    Logger.Log($"Failed to insert chart set { setInfo.FilePath }\\{ setInfo.FileName }");
                    return;
                }

                setInfo.ID = m_connection.LastInsertRowId;
                m_chartSets[setInfo.ID] = setInfo;

                foreach (var chart in setInfo.Charts)
                {
                    int chartResult = Exec("INSERT INTO Charts (setId,lwt,fileName,songTitle,songArtist,songFileName,songVolume,chartOffset,charter,jacketFileName,jacketArtist,backgroundFileName,backgroundArtist,diffLevel,diffIndex,diffName,diffNameShort,diffColor,chartDuration,tags) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                        setInfo.ID,
                        chart.LastWriteTime,
                        chart.FileName,
                        chart.SongTitle,
                        chart.SongArtist,
                        chart.SongFileName,
                        chart.SongVolume,
                        (double)chart.ChartOffset,
                        chart.Charter,
                        chart.JacketFileName,
                        chart.JacketArtist,
                        chart.BackgroundFileName,
                        chart.BackgroundArtist,
                        chart.DifficultyLevel,
                        chart.DifficultyIndex,
                        chart.DifficultyName,
                        chart.DifficultyNameShort,
                        chart.DifficultyColor == null ? (int?)null : Color.Vector3ToHex(chart.DifficultyColor.Value),
                        (double)chart.ChartDuration,
                        chart.Tags);

                    chart.ID = m_connection.LastInsertRowId;
                    Exec("INSERT INTO LocalChartConfig (chartId,config) VALUES (?,?)", string.Empty, chart.ID);
                }
            }
        }

        public void LoadData()
        {
            using (var reader = ExecReader("SELECT id,lwt,uploadID,filePath,fileName FROM Sets"))
            {
                while (reader.Read())
                {
                    var set = new ChartSetInfo();
                    set.ID = reader.GetInt64(0);
                    set.LastWriteTime = reader.GetInt64(1);
                    set.OnlineID = reader.GetInt64OrNull(2);
                    set.FilePath = reader.GetString(3);
                    set.FileName = reader.GetString(4);

                    string relPath = Path.Combine(set.FilePath, set.FileName);
                    m_setFiles.Add(relPath);

                    m_chartSets[set.ID] = set;
                    m_chartSetsByFilePath[relPath] = set;
                }
            }

            using (var reader = ExecReader("SELECT id,setId,lwt,fileName,songTitle,songArtist,songFileName,songVolume,chartOffset,charter,jacketFileName,jacketArtist,backgroundFileName,backgroundArtist,diffLevel,diffIndex,diffName,diffNameShort,diffColor,chartDuration,tags FROM Charts"))
            {
                while (reader.Read())
                {
                    var set = m_chartSets[reader.GetInt64(1)];
                    var chart = new ChartInfo();
                    chart.ID = reader.GetInt64(0);
                    chart.LastWriteTime = reader.GetInt64(2);
                    chart.FileName = reader.GetString(3);
                    chart.SongTitle = reader.GetString(4);
                    chart.SongArtist = reader.GetString(5);
                    chart.SongFileName = reader.GetString(6);
                    chart.SongVolume = reader.GetInt32(7);
                    chart.ChartOffset = reader.GetDouble(8);
                    chart.Charter = reader.GetString(9);
                    chart.JacketFileName = reader.GetStringOrNull(10);
                    chart.JacketArtist = reader.GetStringOrNull(11);
                    chart.BackgroundFileName = reader.GetStringOrNull(12);
                    chart.BackgroundArtist = reader.GetStringOrNull(13);
                    chart.DifficultyLevel = reader.GetDouble(14);
                    chart.DifficultyIndex = reader.GetInt32OrNull(15);
                    chart.DifficultyName = reader.GetString(16);
                    chart.DifficultyNameShort = reader.GetStringOrNull(17);
                    chart.DifficultyColor = reader.GetInt32OrNull(18) is int value ? Color.HexToVector3(value) : (Vector3?)null;
                    chart.ChartDuration = reader.GetDouble(19);
                    chart.Tags = reader.GetString(20);

                    set.Charts.Add(chart);
                    chart.Set = set;
                }
            }
        }

        public void SaveData()
        {
        }

        public void Update()
        {
        }

        public void Close()
        {
            m_connection.Close();
        }
    }

    static class DbExt
    {
        public static string? GetStringOrNull(this SQLiteDataReader reader, int colIndex) => reader.IsDBNull(colIndex) ? null : reader.GetString(colIndex);
        public static long? GetInt64OrNull(this SQLiteDataReader reader, int colIndex) => reader.IsDBNull(colIndex) ? (long?)null : reader.GetInt64(colIndex);
        public static int? GetInt32OrNull(this SQLiteDataReader reader, int colIndex) => reader.IsDBNull(colIndex) ? (int?)null : reader.GetInt32(colIndex);
    }
}
