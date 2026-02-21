using System;
using System.Collections.Generic;
using System.Linq;

namespace BlazorApp.Components
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        GameOver
    }

    public class TetrisGame
    {
        public const int Rows = 20;
        public const int Cols = 10;

        public int[,] Board { get; private set; }
        public int CurrentPieceType { get; private set; }
        public int NextPieceType { get; private set; }
        public int[,] CurrentShape { get; private set; }
        public int PieceX { get; private set; }
        public int PieceY { get; private set; }
        public int RotationState { get; private set; }

        public int Score { get; private set; }
        public int Level { get; set; }
        public int LinesCleared { get; private set; }
        public GameState State { get; set; }
        public List<int> ClearingRows { get; private set; }
        public bool IsClearing { get; set; }
        public bool JustLocked { get; set; }
        public int[] LockedCells { get; private set; }

        private static readonly Random _rng = new Random();

        private static readonly int[][,] Shapes = new int[][,]
        {
            new int[,] { {0,0,0,0}, {1,1,1,1}, {0,0,0,0}, {0,0,0,0} },
            new int[,] { {1,1}, {1,1} },
            new int[,] { {0,1,0}, {1,1,1}, {0,0,0} },
            new int[,] { {0,1,1}, {1,1,0}, {0,0,0} },
            new int[,] { {1,1,0}, {0,1,1}, {0,0,0} },
            new int[,] { {1,0,0}, {1,1,1}, {0,0,0} },
            new int[,] { {0,0,1}, {1,1,1}, {0,0,0} },
        };

        private static readonly string[] PieceColors = new string[]
        {
            "transparent",
            "#00f5ff",
            "#ffd700",
            "#b44dff",
            "#00ff88",
            "#ff3355",
            "#3388ff",
            "#ff8800",
        };

        public static string GetColor(int type)
        {
            if (type < 0 || type >= PieceColors.Length) return PieceColors[0];
            return PieceColors[type];
        }

        public TetrisGame()
        {
            Board = new int[Rows, Cols];
            ClearingRows = new List<int>();
            LockedCells = Array.Empty<int>();
            State = GameState.Menu;
            Level = 1;
        }

        public int GetDropInterval()
        {
            return Math.Max(80, 800 - (Level - 1) * 70);
        }

        public void StartGame()
        {
            Board = new int[Rows, Cols];
            Score = 0;
            LinesCleared = 0;
            ClearingRows.Clear();
            IsClearing = false;
            JustLocked = false;

            NextPieceType = _rng.Next(1, 8);
            SpawnPiece();
            State = GameState.Playing;
        }

        private void SpawnPiece()
        {
            CurrentPieceType = NextPieceType;
            NextPieceType = _rng.Next(1, 8);
            RotationState = 0;
            CurrentShape = CloneShape(Shapes[CurrentPieceType - 1]);

            int shapeW = CurrentShape.GetLength(1);
            PieceX = (Cols - shapeW) / 2;
            PieceY = 0;

            if (CheckCollision(CurrentShape, PieceX, PieceY))
            {
                State = GameState.GameOver;
            }
        }

        public bool Tick()
        {
            if (State != GameState.Playing) return false;
            if (IsClearing) return false;

            if (!TryMove(0, 1))
            {
                LockPiece();
                return true;
            }
            return false;
        }

        public void MoveLeft()
        {
            if (State != GameState.Playing || IsClearing) return;
            TryMove(-1, 0);
        }

        public void MoveRight()
        {
            if (State != GameState.Playing || IsClearing) return;
            TryMove(1, 0);
        }

        public void SoftDrop()
        {
            if (State != GameState.Playing || IsClearing) return;
            if (TryMove(0, 1))
            {
                Score += 1;
            }
        }

        public void HardDrop()
        {
            if (State != GameState.Playing || IsClearing) return;
            int dropped = 0;
            while (TryMove(0, 1))
            {
                dropped++;
            }
            Score += dropped * 2;
            LockPiece();
        }

        public void Rotate()
        {
            if (State != GameState.Playing || IsClearing) return;
            if (CurrentPieceType == 2) return;

            int[,] rotated = RotateClockwise(CurrentShape);

            if (!CheckCollision(rotated, PieceX, PieceY))
            {
                CurrentShape = rotated;
                RotationState = (RotationState + 1) % 4;
                return;
            }

            int[] kicks = { 1, -1, 2, -2 };
            foreach (int kick in kicks)
            {
                if (!CheckCollision(rotated, PieceX + kick, PieceY))
                {
                    CurrentShape = rotated;
                    PieceX += kick;
                    RotationState = (RotationState + 1) % 4;
                    return;
                }
            }

            if (!CheckCollision(rotated, PieceX, PieceY - 1))
            {
                CurrentShape = rotated;
                PieceY -= 1;
                RotationState = (RotationState + 1) % 4;
            }
        }

        private bool TryMove(int dx, int dy)
        {
            int newX = PieceX + dx;
            int newY = PieceY + dy;
            if (!CheckCollision(CurrentShape, newX, newY))
            {
                PieceX = newX;
                PieceY = newY;
                return true;
            }
            return false;
        }

        private void LockPiece()
        {
            var lockedList = new List<int>();
            int rows = CurrentShape.GetLength(0);
            int cols = CurrentShape.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (CurrentShape[r, c] != 0)
                    {
                        int boardR = PieceY + r;
                        int boardC = PieceX + c;
                        if (boardR >= 0 && boardR < Rows && boardC >= 0 && boardC < Cols)
                        {
                            Board[boardR, boardC] = CurrentPieceType;
                            lockedList.Add(boardR * Cols + boardC);
                        }
                    }
                }
            }
            LockedCells = lockedList.ToArray();
            JustLocked = true;

            var fullRows = FindFullRows();
            if (fullRows.Count > 0)
            {
                ClearingRows = fullRows;
                IsClearing = true;
            }
            else
            {
                SpawnPiece();
            }
        }

        public void FinishClearing()
        {
            if (!IsClearing) return;

            int count = ClearingRows.Count;
            foreach (int row in ClearingRows.OrderByDescending(r => r))
            {
                RemoveRow(row);
            }

            LinesCleared += count;
            int[] lineScores = { 0, 100, 300, 500, 800 };
            Score += lineScores[Math.Min(count, 4)] * Level;

            ClearingRows.Clear();
            IsClearing = false;
            SpawnPiece();
        }

        private List<int> FindFullRows()
        {
            var full = new List<int>();
            for (int r = 0; r < Rows; r++)
            {
                bool isFull = true;
                for (int c = 0; c < Cols; c++)
                {
                    if (Board[r, c] == 0)
                    {
                        isFull = false;
                        break;
                    }
                }
                if (isFull) full.Add(r);
            }
            return full;
        }

        private void RemoveRow(int row)
        {
            for (int r = row; r > 0; r--)
            {
                for (int c = 0; c < Cols; c++)
                {
                    Board[r, c] = Board[r - 1, c];
                }
            }
            for (int c = 0; c < Cols; c++)
            {
                Board[0, c] = 0;
            }
        }

        private bool CheckCollision(int[,] shape, int posX, int posY)
        {
            int rows = shape.GetLength(0);
            int cols = shape.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (shape[r, c] != 0)
                    {
                        int boardR = posY + r;
                        int boardC = posX + c;
                        if (boardC < 0 || boardC >= Cols || boardR >= Rows)
                            return true;
                        if (boardR >= 0 && Board[boardR, boardC] != 0)
                            return true;
                    }
                }
            }
            return false;
        }

        public int GetGhostY()
        {
            int ghostY = PieceY;
            while (!CheckCollision(CurrentShape, PieceX, ghostY + 1))
            {
                ghostY++;
            }
            return ghostY;
        }

        public int GetCellType(int row, int col)
        {
            if (State == GameState.Playing || State == GameState.Paused || State == GameState.GameOver)
            {
                if (CurrentShape != null && State != GameState.GameOver)
                {
                    int ghostY = GetGhostY();
                    int shapeRows = CurrentShape.GetLength(0);
                    int shapeCols = CurrentShape.GetLength(1);

                    int sr = row - PieceY;
                    int sc = col - PieceX;
                    if (sr >= 0 && sr < shapeRows && sc >= 0 && sc < shapeCols && CurrentShape[sr, sc] != 0)
                    {
                        return CurrentPieceType;
                    }

                    int gr = row - ghostY;
                    if (gr >= 0 && gr < shapeRows && sc >= 0 && sc < shapeCols && CurrentShape[gr, sc] != 0)
                    {
                        if (Board[row, col] == 0) return -CurrentPieceType;
                    }
                }
            }

            return Board[row, col];
        }

        private static int[,] RotateClockwise(int[,] shape)
        {
            int rows = shape.GetLength(0);
            int cols = shape.GetLength(1);
            int[,] result = new int[cols, rows];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    result[c, rows - 1 - r] = shape[r, c];
                }
            }
            return result;
        }

        private static int[,] CloneShape(int[,] shape)
        {
            return (int[,])shape.Clone();
        }
    }
}
