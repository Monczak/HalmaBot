using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using Pastel;

namespace HalmaBot;

public class Board
{
    private Piece[,] board;
    private readonly Piece[,] oldBoard;

    private readonly bool[,] player1Camp;
    private readonly bool[,] player2Camp;

    public int BoardSize { get; }
    
    public ulong ZobristKey { get; private set; }

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

    public void InitZobrist(bool isPlayer1)
    {
        Zobrist.Init(this);
        ZobristKey = Zobrist.Hash(this, isPlayer1);
    }

    public bool IsInCamp(Coord coord, bool isPlayer1) =>
        isPlayer1 ? player1Camp[coord.X, coord.Y] : player2Camp[coord.X, coord.Y];

    public Piece this[Coord coord]
    {
        get => board[coord.X, coord.Y];
        set => board[coord.X, coord.Y] = value;
    }

    internal bool IsInBounds(Coord coord) => coord.X >= 0 && coord.X < BoardSize && coord.Y >= 0 && coord.Y < BoardSize;

    private bool MovePiece(Coord fromSquare, Coord toSquare)
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
    
    public GameState GameState 
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
        var startSquare = move.FromSquare;
        var currentSquare = startSquare;
        var piece = this[startSquare];
        
        for (var y = 0; y < BoardSize; y++)
        for (var x = 0; x < BoardSize; x++)
            oldBoard[x, y] = board[x, y];

        foreach (var step in move.Steps)
        {
            if (!MovePiece(currentSquare, currentSquare + step))
            {
                for (var y = 0; y < BoardSize; y++)
                for (var x = 0; x < BoardSize; x++)
                    board[x, y] = oldBoard[x, y];
                return false;
            }

            currentSquare += step;
        }
        
        if ((piece & Piece.Player1) != 0) ZobristKey ^= Zobrist.Player1Turn;
        ZobristKey ^= Zobrist.Get(startSquare, piece);
        ZobristKey ^= Zobrist.Get(currentSquare, piece);

        return true;
    }

    public bool UnmakeMove(Move move)
    {
        var startSquare = move.FinalSquare;
        var currentSquare = startSquare;
        var piece = this[currentSquare];
        
        for (var y = 0; y < BoardSize; y++)
        for (var x = 0; x < BoardSize; x++)
            oldBoard[x, y] = board[x, y];
        
        for (var i = move.Steps.Length - 1; i >= 0; i--)
        {
            var step = move.Steps[i];
            if (!MovePiece(currentSquare, currentSquare - step))
            {
                for (var y = 0; y < BoardSize; y++)
                for (var x = 0; x < BoardSize; x++)
                    board[x, y] = oldBoard[x, y];
                return false;
            }

            currentSquare -= step;
        }
        
        if ((piece & Piece.Player1) != 0) ZobristKey ^= Zobrist.Player1Turn;
        ZobristKey ^= Zobrist.Get(startSquare, piece);
        ZobristKey ^= Zobrist.Get(currentSquare, piece);

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
        var checkerboardColor1 = Color.FromArgb(255, 240, 217, 181);
        var checkerboardColor2 = Color.FromArgb(255, 181, 136, 99);

        var player1CampColor1 = Color.FromArgb(255, 227, 93, 53);
        var player1CampColor2 = Color.FromArgb(255, 181, 75, 42);

        var player2CampColor1 = Color.FromArgb(255, 81, 125, 189);
        var player2CampColor2 = Color.FromArgb(255, 58, 90, 135);
        
        var builder = new StringBuilder();
        for (var y = 0; y < BoardSize; y++)
        {
            builder.Append($"{(y + 1):00} | ");
            for (var x = 0; x < BoardSize; x++)
            {
                var coord = new Coord(x, y);
                var isEven = (x + y) % 2 == 0;
                var isInPlayer1Camp = IsInCamp(coord, true);
                var isInPlayer2Camp = IsInCamp(coord, false);
                var bgColor = (isEven, isInPlayer1Camp, isInPlayer2Camp) switch
                {
                    (true, true, _) => player1CampColor1,
                    (true, false, true) => player2CampColor1,
                    (true, false, false) => checkerboardColor1,

                    (false, true, _) => player1CampColor2,
                    (false, false, true) => player2CampColor2,
                    (false, false, false) => checkerboardColor2,
                };
                builder.Append((this[coord] switch
                {
                    Piece.Player1 => " \u25a0 ".Pastel(Color.DarkRed),
                    Piece.Player2 => " \u25a0 ".Pastel(Color.DarkBlue),
                    _ => "   "
                }).PastelBg(bgColor));
            }

            builder.AppendLine();
        }

        builder.Append("     ");
        for (var i = 0; i < BoardSize; i++)
            builder.Append("---");
        builder.AppendLine();
        
        builder.Append("     ");
        for (var i = 0; i < BoardSize; i++)
            builder.Append(" " + "abcdefghijklmnop"[i] + " ");
        builder.AppendLine();

        return builder.ToString();
    }
}