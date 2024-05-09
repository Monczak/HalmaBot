using System.Drawing;
using Pastel;

namespace HalmaBot;

public static class MoveGenerator
{
    private static bool[]? positions = null;
    private static int index;

    private static void GenerateSimpleMoves(Board board, Coord square, bool isPlayer1, ref Span<Move> moves)
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

                if (index < moves.Length) moves[index++] = new Move(square, [step]);
                else Console.WriteLine("Move limit exceeded!".Pastel(Color.Red));
            }
        }
    }
    
    private static void GenerateJumps(Board board, bool isPlayer1, Coord square, Move move, bool midJump, ref Span<Move> moves)
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
                var backtracked = false;
                positions ??= new bool[board.BoardSize * board.BoardSize];
                var curPos = move.FromSquare;
                foreach (var s in move.Steps)
                {
                    curPos += s;
                    if (positions[curPos.Y * board.BoardSize + curPos.X])
                    {
                        backtracked = true;
                        break;
                    }
                    
                    positions[curPos.Y * board.BoardSize + curPos.X] = true;
                }

                if (backtracked)
                    continue;
                    
                var jump = new Move(square, [..move.Steps, step]);
                
                if (index < moves.Length) moves[index++] = jump;
                else Console.WriteLine("Move limit exceeded!".Pastel(Color.Red));
                
                GenerateJumps(board, isPlayer1, square, jump, true, ref moves);
            }
        }
    }

    private static void GenerateJumpMoves(Board board, Coord square, bool isPlayer1, ref Span<Move> moves)
    {
        var initialMove = new Move(square, []);
        GenerateJumps(board, isPlayer1, square, initialMove, false, ref moves);
    }
    
    public static void GenerateMoves(Board board, bool isPlayer1, ref Span<Move> moves)
    {
        index = 0;
        for (var y = 0; y < board.BoardSize; y++)
        {
            for (var x = 0; x < board.BoardSize; x++)
            {
                var coord = new Coord(x, y);
                if ((board[coord] & Piece.Player1) != 0 && isPlayer1 || (board[coord] & Piece.Player2) != 0 && !isPlayer1)
                {
                    GenerateSimpleMoves(board, coord, isPlayer1, ref moves);
                    GenerateJumpMoves(board, coord, isPlayer1, ref moves);
                }
            }
        }

        // Console.WriteLine(index);
        moves = moves[..index];
    }
}