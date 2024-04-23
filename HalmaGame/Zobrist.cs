namespace HalmaBot;

public static class Zobrist
{
    private static int boardSize;
    
    public static ulong Player1Turn { get; private set; }
    
    public static ulong[,] BoardBitstrings { get; private set; }

    public static void Init(Board board)
    {
        var rng = new Random(16347954);
        boardSize = board.BoardSize;

        BoardBitstrings = new ulong[boardSize * boardSize, 3];
        for (var y = 0; y < boardSize; y++)
        {
            for (var x = 0; x < boardSize; x++)
            {
                var index = y * boardSize + x;
                foreach (var pieceType in (ReadOnlySpan<Piece>)[Piece.Player1, Piece.Player2])
                {
                    BoardBitstrings[index, (int)pieceType] = GetRandomBitstring(rng);
                }
            }
        }
        
        Player1Turn = GetRandomBitstring(rng);
    }

    public static ulong Hash(Board board, bool isPlayer1)
    {
        var hash = 0ul;
        if (isPlayer1)
            hash ^= Player1Turn;

        for (var y = 0; y < boardSize; y++)
        {
            for (var x = 0; x < boardSize; x++)
            {
                var index = y * boardSize + x;
                var piece = board[new Coord(x, y)];
                if ((piece & Piece.Occupied) != 0)
                    hash ^= BoardBitstrings[index, (int)piece];
            }
        }

        return hash;
    }

    private static ulong GetRandomBitstring(Random rng)
    {
        var buffer = new byte[8];
        rng.NextBytes(buffer);
        return BitConverter.ToUInt64(buffer, 0);
    }

    public static ulong Get(Coord coord, Piece piece)
    {
        var index = coord.Y * boardSize + coord.Y;
        return BoardBitstrings[index, (int)piece];
    }
}