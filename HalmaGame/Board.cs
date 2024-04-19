using System.Text;
using System.Text.RegularExpressions;

namespace HalmaBot;

public class Board
{
    private Piece[,] board;
    private readonly Piece[,] oldBoard;

    private readonly bool[,] player1Camp;
    private readonly bool[,] player2Camp;

    public int BoardSize { get; }

    public Board(int boardSize = 16)
    {
        BoardSize = boardSize;
        board = new Piece[boardSize, boardSize];
        oldBoard = new Piece[boardSize, boardSize];
        player1Camp = new bool[boardSize, boardSize];
        player2Camp = new bool[boardSize, boardSize];

        var campWidths = (int[]) [5, 5, 4, 3, 2];

        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < campWidths[y]; x++)
            {
                player1Camp[x, y] = true;
                player2Camp[boardSize - x - 1, boardSize - y - 1] = true;
            }
        }
    }

    public IEnumerable<Coord> Coords
    {
        get
        {
            for (var y = 0; y < BoardSize; y++)
            {
                for (var x = 0; x < BoardSize; x++)
                {
                    yield return new Coord(x, y);
                }
            }
        }
    }

    public bool IsInCamp(Coord coord, bool isPlayer1) =>
        isPlayer1 ? player1Camp[coord.X, coord.Y] : player2Camp[coord.X, coord.Y];

    public Piece this[Coord coord]
    {
        get => board[coord.X, coord.Y];
        set => board[coord.X, coord.Y] = value;
    }

    internal bool IsInBounds(Coord coord) => coord.X >= 0 && coord.X < BoardSize && coord.Y >= 0 && coord.Y < BoardSize;

    private bool MovePiece(Coord fromSquare, Coord toSquare, bool midJump = false)
    {
        // if (!IsMoveValid(fromSquare, toSquare, midJump)) 
        //     return false;

        (this[fromSquare], this[toSquare]) = (this[toSquare], this[fromSquare]);
        return true;
    }

    internal bool IsMoveLegal(Coord fromSquare, Coord toSquare, bool midJump = false)
    {
        if (!IsInBounds(fromSquare) || !IsInBounds(toSquare))
            return false;
        
        if ((this[fromSquare] & Piece.Occupied) == 0 && !midJump)
            return false;

        if ((this[toSquare] & Piece.Occupied) != 0)
            return false;

        return true;
    }

    internal bool IsJumpValid(Coord fromSquare, Coord step, bool midJump = false)
    {
        if (!IsMoveLegal(fromSquare, fromSquare + step, midJump))
            return false;

        if ((this[fromSquare + step / 2] & Piece.Occupied) == 0)
            return false;

        return true;
    }

    internal bool MoveLeavesOpposingCamp(Coord fromSquare, Coord toSquare, bool isPlayer1)
    {
        var camp = isPlayer1 ? player2Camp : player1Camp;
        return camp[fromSquare.X, fromSquare.Y] && !camp[toSquare.X, toSquare.Y];
    }
    
    internal GameState GameState 
    {
        get
        {
            var player1Won = true;
            var player2Won = true;
            for (var y = 0; y < BoardSize; y++)
            {
                for (var x = 0; x < BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    var piece = this[coord];
                    if (player2Camp[x, y] && (piece & Piece.Player1) == 0)
                        player1Won = false;
                    if (player1Camp[x, y] && (piece & Piece.Player2) == 0)
                        player2Won = false;
                }
            }

            if (player1Won)
                return GameState.Player1Won;
            if (player2Won)
                return GameState.Player2Won;
            return GameState.InProgress;
        }
    }

    public bool MakeMove(Move move)
    {
        var currentSquare = move.FromSquare;
        
        for (var y = 0; y < BoardSize; y++)
        for (var x = 0; x < BoardSize; x++)
            oldBoard[x, y] = board[x, y];

        var midJump = false;
        foreach (var step in move.Steps)
        {
            if (!MovePiece(currentSquare, currentSquare + step, midJump))
            {
                for (var y = 0; y < BoardSize; y++)
                for (var x = 0; x < BoardSize; x++)
                    board[x, y] = oldBoard[x, y];
                return false;
            }

            currentSquare += step;

            midJump = true;
        }

        return true;
    }

    public bool UnmakeMove(Move move)
    {
        var currentSquare = move.FinalSquare;
        
        for (var y = 0; y < BoardSize; y++)
        for (var x = 0; x < BoardSize; x++)
            oldBoard[x, y] = board[x, y];

        var i = move.Steps.Length - 1;
        foreach (var step in move.Steps.Reverse())
        {
            if (!MovePiece(currentSquare, currentSquare - step, i > 0))
            {
                for (var y = 0; y < BoardSize; y++)
                for (var x = 0; x < BoardSize; x++)
                    board[x, y] = oldBoard[x, y];
                return false;
            }

            i--;
        }

        return true;
    }
    
    public void LoadFromString(string gameStr)
    {
        board = new Piece[BoardSize, BoardSize];
        var y = 0;
        foreach (var line in gameStr.Split("/"))
        {
            var x = 0;
            var tokens = Regex.Matches(line, @"[0-9]+|[Pp]").Select(m => m.Value);
            foreach (var token in tokens)
            {
                if (int.TryParse(token, out var blankSpaces))
                {
                    for (var i = 0; i < blankSpaces; i++)
                        board[x++, y] = Piece.None;
                }
                else
                {
                    if (token == "P")
                        board[x++, y] = Piece.Player1;
                    else
                        board[x++, y] = Piece.Player2;
                }
            }

            y++;
        }
    }

    public string Print()
    {
        var builder = new StringBuilder();
        for (var y = 0; y < BoardSize; y++)
        {
            builder.Append($"{(y + 1):00} | ");
            for (var x = 0; x < BoardSize; x++)
            {
                var coord = new Coord(x, y);
                builder.Append(this[coord] switch
                {
                    Piece.Player1 => "P",
                    Piece.Player2 => "p",
                    _ => "."
                });
            }

            builder.AppendLine();
        }

        builder.Append("     ");
        for (var i = 0; i < BoardSize; i++)
            builder.Append('-');
        builder.AppendLine();
        
        builder.Append("     ");
        for (var i = 0; i < BoardSize; i++)
            builder.Append("abcdefghijklmnop"[i]);
        builder.AppendLine();

        return builder.ToString();
    }
}