using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlazorApp.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorApp.Components
{
    public partial class GameField : IDisposable
    {
        private TetrisGame game = new TetrisGame();
        private Timer? gameTimer;
        private Timer? clearTimer;
        private string playerName = "";
        private List<ScoreEntry> leaderboardEntries = new List<ScoreEntry>();
        private ElementReference gameContainer;
        private bool disposed = false;

        private static readonly int[,][] ShapeCache = BuildShapeCache();

        protected override void OnInitialized()
        {
            leaderboardEntries = Leaderboard.GetTopScores();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await FocusGame();
            }
        }

        private async Task FocusGame()
        {
            try
            {
                await JS.InvokeVoidAsync("tetrisFocus", gameContainer);
            }
            catch { }
        }

        private void StartGame()
        {
            StopTimers();
            game = new TetrisGame();
            game.Level = 1;
            game.StartGame();
            playerName = "";

            int interval = game.GetDropInterval();
            gameTimer = new Timer(OnTick, null, interval, interval);
            _ = FocusGame();
        }

        private void PauseGame()
        {
            if (game.State != GameState.Playing) return;
            game.State = GameState.Paused;
            StopTimers();
        }

        private void ResumeGame()
        {
            if (game.State != GameState.Paused) return;
            game.State = GameState.Playing;
            int interval = game.GetDropInterval();
            gameTimer = new Timer(OnTick, null, interval, interval);
            _ = FocusGame();
        }

        private void EndGame()
        {
            StopTimers();
            game.State = GameState.GameOver;
        }

        private void OnTick(object? state)
        {
            if (game.State != GameState.Playing || game.IsClearing) return;

            InvokeAsync(() =>
            {
                game.Tick();

                if (game.IsClearing)
                {
                    StateHasChanged();
                    clearTimer = new Timer(OnClearComplete, null, 500, Timeout.Infinite);
                    return;
                }

                if (game.JustLocked)
                {
                    StateHasChanged();
                    Task.Delay(150).ContinueWith(_ =>
                    {
                        InvokeAsync(() =>
                        {
                            game.JustLocked = false;
                            StateHasChanged();
                        });
                    });
                }
                else
                {
                    StateHasChanged();
                }

                if (game.State == GameState.GameOver)
                {
                    StopTimers();
                    leaderboardEntries = Leaderboard.GetTopScores();
                    StateHasChanged();
                }

                UpdateTimerInterval();
            });
        }

        private void OnClearComplete(object? state)
        {
            InvokeAsync(() =>
            {
                game.FinishClearing();
                game.JustLocked = false;

                if (game.State == GameState.GameOver)
                {
                    StopTimers();
                    leaderboardEntries = Leaderboard.GetTopScores();
                }

                StateHasChanged();
                UpdateTimerInterval();
            });
        }

        private void UpdateTimerInterval()
        {
            if (gameTimer != null && game.State == GameState.Playing)
            {
                int interval = game.GetDropInterval();
                try
                {
                    gameTimer.Change(interval, interval);
                }
                catch { }
            }
        }

        private void HandleKeyDown(KeyboardEventArgs e)
        {
            if (game.State == GameState.Playing && !game.IsClearing)
            {
                switch (e.Key)
                {
                    case "ArrowLeft":
                        game.MoveLeft();
                        break;
                    case "ArrowRight":
                        game.MoveRight();
                        break;
                    case "ArrowDown":
                        game.SoftDrop();
                        break;
                    case "ArrowUp":
                        game.Rotate();
                        break;
                    case " ":
                        game.HardDrop();
                        if (game.IsClearing)
                        {
                            clearTimer = new Timer(OnClearComplete, null, 500, Timeout.Infinite);
                        }
                        else if (game.State == GameState.GameOver)
                        {
                            StopTimers();
                            leaderboardEntries = Leaderboard.GetTopScores();
                        }
                        break;
                }
                StateHasChanged();

                if (game.JustLocked && !game.IsClearing)
                {
                    Task.Delay(150).ContinueWith(_ =>
                    {
                        InvokeAsync(() =>
                        {
                            game.JustLocked = false;
                            StateHasChanged();
                        });
                    });
                }
            }
            else if (game.State == GameState.Paused && e.Key == "Escape")
            {
                ResumeGame();
                StateHasChanged();
            }
            else if (game.State == GameState.Playing && e.Key == "Escape")
            {
                PauseGame();
                StateHasChanged();
            }
        }

        private void SpeedUp()
        {
            if (game.Level < 15) game.Level++;
            UpdateTimerInterval();
            _ = FocusGame();
        }

        private void SpeedDown()
        {
            if (game.Level > 1) game.Level--;
            UpdateTimerInterval();
            _ = FocusGame();
        }

        private void SaveScore()
        {
            if (!string.IsNullOrWhiteSpace(playerName) && game.Score > 0)
            {
                Leaderboard.AddScore(playerName, game.Score, game.Level, game.LinesCleared);
                leaderboardEntries = Leaderboard.GetTopScores();
            }
        }

        private int[,]? GetNextShape()
        {
            if (game.NextPieceType < 1 || game.NextPieceType > 7) return null;
            return GetShapeForType(game.NextPieceType);
        }

        private static int[,] GetShapeForType(int type)
        {
            return type switch
            {
                1 => new int[,] { {0,0,0,0}, {1,1,1,1}, {0,0,0,0}, {0,0,0,0} },
                2 => new int[,] { {1,1}, {1,1} },
                3 => new int[,] { {0,1,0}, {1,1,1}, {0,0,0} },
                4 => new int[,] { {0,1,1}, {1,1,0}, {0,0,0} },
                5 => new int[,] { {1,1,0}, {0,1,1}, {0,0,0} },
                6 => new int[,] { {1,0,0}, {1,1,1}, {0,0,0} },
                7 => new int[,] { {0,0,1}, {1,1,1}, {0,0,0} },
                _ => new int[,] { {0} },
            };
        }

        private static int[,][] BuildShapeCache()
        {
            return new int[,][] { };
        }

        private void StopTimers()
        {
            gameTimer?.Dispose();
            gameTimer = null;
            clearTimer?.Dispose();
            clearTimer = null;
        }

        public void Dispose()
        {
            disposed = true;
            StopTimers();
        }
    }
}
