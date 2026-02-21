using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BlazorApp.Data
{
    public class ScoreEntry
    {
        public string Name { get; set; } = "";
        public int Score { get; set; }
        public int Level { get; set; }
        public int Lines { get; set; }
        public DateTime Date { get; set; }
    }

    public class LeaderboardService
    {
        private readonly string _filePath;
        private List<ScoreEntry> _entries;

        public LeaderboardService()
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "leaderboard.json");
            _entries = LoadFromFile();
        }

        public List<ScoreEntry> GetTopScores(int count = 10)
        {
            return _entries
                .OrderByDescending(e => e.Score)
                .Take(count)
                .ToList();
        }

        public void AddScore(string name, int score, int level, int lines)
        {
            _entries.Add(new ScoreEntry
            {
                Name = name.Trim(),
                Score = score,
                Level = level,
                Lines = lines,
                Date = DateTime.UtcNow
            });

            _entries = _entries
                .OrderByDescending(e => e.Score)
                .Take(50)
                .ToList();

            SaveToFile();
        }

        private List<ScoreEntry> LoadFromFile()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<List<ScoreEntry>>(json) ?? new List<ScoreEntry>();
                }
            }
            catch { }
            return new List<ScoreEntry>();
        }

        private void SaveToFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }
    }
}
