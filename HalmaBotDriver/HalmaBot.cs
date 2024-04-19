using System.Numerics;

namespace HalmaBot;

public class HalmaBot : IHalmaPlayer
{
    public event IHalmaPlayer.PickMoveDelegate? MovePicked;

    private static class Heuristics
    {
        public static float PlayerProgress(Board board, bool isPlayer1)
        {
            var posSum = new Coord(0, 0);
            var count = 0;
            foreach (var coord in board.Coords)
            {
                if (isPlayer1 && (board[coord] & Piece.Player1) != 0 ||
                    !isPlayer1 && (board[coord] & Piece.Player2) != 0)
                {
                    posSum += coord;
                    count++;
                }
            }

            var posAvg = new Vector2((float)posSum.X / count, (float)posSum.Y / count);
            var startPoint = Vector2.Zero;
            var endPoint = Vector2.One * (board.BoardSize - 1);
            var diagProjection = Vector2.Dot(posAvg - startPoint, endPoint - startPoint) / Vector2.Dot(endPoint - startPoint, endPoint - startPoint) * (endPoint - startPoint);
            var progress = (diagProjection - startPoint).Length() / (endPoint - startPoint).Length();
            return isPlayer1 ? progress : 1 - progress;
        }

        public static float AvailableMoveCount(IReadOnlyCollection<Move> availableMoves)
        {
            return availableMoves.Count;
        }

        public static float PiecesInOpposingCamp(Board board, bool isPlayer1)
        {
            return board.Coords.Sum(coord => board.IsInCamp(coord, !isPlayer1) ? 1 : 0);
        }

        public static float PiecesNotInPlayerCamp(Board board, bool isPlayer1)
        {
            return board.Coords.Sum(coord => !board.IsInCamp(coord, isPlayer1) ? 1 : 0);
        }

        public static float PiecesEnemyCanJumpOver(Board board, bool isPlayer1)
        {
            throw new NotImplementedException();    // TODO
        }

        public static float AvgJumpLength(IEnumerable<Move> availableMoves)
        {
            var jumpLength = 0;
            var jumpCount = 0;
            foreach (var move in availableMoves)
            {
                if (move.IsJump)
                {
                    jumpLength += move.Steps.Length;
                    jumpCount++;
                } 
            }

            if (jumpCount == 0)
                return 0;
            return (float)jumpLength / jumpCount;
        }
    }
    
    // TODO: Maybe use a genetic algorithm to tune the bot - determine the best multipliers for heuristics?
    private float Evaluate(Board board, bool isPlayer1, IReadOnlyCollection<Move> availableMoves)
    {
        return 
            Heuristics.PlayerProgress(board, isPlayer1) * 10f
            + Heuristics.AvailableMoveCount(availableMoves) * 0.2f
            + Heuristics.PiecesInOpposingCamp(board, isPlayer1) * 1f
            + Heuristics.PiecesNotInPlayerCamp(board, isPlayer1) * 1f
            // + Heuristics.PiecesEnemyCanJumpOver(board, isPlayer1) * 1f
            + Heuristics.AvgJumpLength(availableMoves) * 1f;
    }
    
    public void OnPlayerTurn(int turn, bool isPlayer1, Board board)
    {
        Move[] moves = [..MoveGenerator.GenerateMoves(board, isPlayer1)];
        // foreach (var move in moves)
        //     Console.WriteLine(move);
        var eval = Evaluate(board, isPlayer1, moves);
        Console.WriteLine($"Eval: {eval}");
            
        if (moves.Length == 0)
            MovePicked?.Invoke(null);
        else
        {
            var picked = moves[new Random().Next(moves.Length)];
            Console.WriteLine($"Picked {picked}");
            MovePicked?.Invoke(picked);
        }
    }
}