namespace HalmaBot;

public static class MoveGenerator
{
    private static HashSet<Coord> positions;
    private static List<(Move, float)> moves;

    static MoveGenerator()
    {
        positions = new HashSet<Coord>();
        moves = new List<(Move, float)>();
    }

    private static void GenerateSimpleMoves(Board board, Coord square, bool isPlayer1)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) 
                    continue;
                
                var step = new Coord(dx, dy);
                var destPos = square + step;
                if (!board.IsInBounds(destPos)) continue;

                if (!board.IsMoveLegal(square, destPos)) continue;
                if (board.MoveLeavesOpposingCamp(square, destPos, isPlayer1)) continue;
                
                moves.Add((new Move(square, [step]), 0));
            }
        }
    }

    private static void GenerateJumpMoves(Board board, Coord square, bool isPlayer1)
    {
        var initialMove = new Move(square, []);
        GenerateJumps(initialMove, false);

        void GenerateJumps(Move move, bool midJump)
        {
            for (var dy = -2; dy <= 2; dy += 2)
            {
                for (var dx = -2; dx <= 2; dx += 2)
                {
                    if (dx == 0 && dy == 0) 
                        continue;
                    
                    var step = new Coord(dx, dy);
                    if (!board.IsJumpValid(move.FinalSquare, step, midJump))
                        continue;
                    if (board.MoveLeavesOpposingCamp(square, move.FinalSquare + step, isPlayer1))
                        continue;

                    // No round-trips
                    if (move.FinalSquare.X + step.X == square.X && move.FinalSquare.Y + step.Y == square.Y)
                        continue;
                    
                    // No backtracking
                    positions.Clear();
                    var curPos = move.FromSquare;
                    foreach (var s in move.Steps)
                    {
                        curPos += s;
                        positions.Add(curPos);
                    }

                    if (positions.Contains(move.FinalSquare + step))
                        continue;
                    
                    var jump = new Move(square, [..move.Steps, step]);
                    moves.Add((jump, 0));
                    GenerateJumps(jump, true);
                }
            }
        }
    }
    
    public static List<(Move, float)> GenerateMoves(Board board, bool isPlayer1)
    {
        moves.Clear();
        for (var y = 0; y < board.BoardSize; y++)
        {
            for (var x = 0; x < board.BoardSize; x++)
            {
                var coord = new Coord(x, y);
                if ((board[coord] & Piece.Player1) != 0 && isPlayer1 || (board[coord] & Piece.Player2) != 0 && !isPlayer1)
                {
                    GenerateSimpleMoves(board, coord, isPlayer1);
                    GenerateJumpMoves(board, coord, isPlayer1);
                }
            }
        }

        return moves;
    }
}