namespace HalmaBot;

public static class MoveGenerator
{
    private static IEnumerable<Move> GenerateSimpleMoves(Board board, Coord square, bool isPlayer1)
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

                yield return new Move(square, [step]);
            }
        }
    }

    private static IEnumerable<Move> GenerateJumpMoves(Board board, Coord square, bool isPlayer1)
    {
        var initialMove = new Move(square, []);
        foreach (var move in GenerateJumps(initialMove, false))
            yield return move;
        yield break;

        IEnumerable<Move> GenerateJumps(Move move, bool midJump)
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
                    if (move.FinalSquare + step == square)
                        continue;
                    
                    // No backtracking
                    var positions = new HashSet<Coord>();
                    var curPos = move.FromSquare;
                    foreach (var s in move.Steps)
                    {
                        curPos += s;
                        positions.Add(curPos);
                    }

                    if (positions.Contains(move.FinalSquare + step))
                        continue;
                    
                    var jump = new Move(square, [..move.Steps, step]);
                    yield return jump;
                    foreach (var longerJumpMove in GenerateJumps(jump, true))
                        yield return longerJumpMove;
                }
            }
        }
    }
    
    public static IEnumerable<Move> GenerateMoves(Board board, bool isPlayer1)
    {
        for (var y = 0; y < board.BoardSize; y++)
        {
            for (var x = 0; x < board.BoardSize; x++)
            {
                var coord = new Coord(x, y);
                if ((board[coord] & Piece.Player1) != 0 && isPlayer1 || (board[coord] & Piece.Player2) != 0 && !isPlayer1)
                {
                    foreach (var move in GenerateSimpleMoves(board, coord, isPlayer1))
                        yield return move;
                    foreach (var move in GenerateJumpMoves(board, coord, isPlayer1))
                        yield return move;
                }
            }
        }
    }
}