using System.Diagnostics;
using System.Numerics;

namespace HalmaBot;

public class HalmaBot : IHalmaPlayer
{
    public event IHalmaPlayer.PickMoveDelegate? MovePicked;

    private TranspositionTable transpositionTable = new();

    private object? lockObj = new();

    private static class Stats
    {
        public static int NodesVisited { get; set; }
        public static int TranspositionTableHits { get; set; }
    }
    
    private static class Heuristics
    {
        public static float SumOfDistances(Board board, bool isPlayer1)
        {
            var distSum = 0f;
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    if (isPlayer1 && (board[coord] & Piece.Player1) != 0 ||
                        !isPlayer1 && (board[coord] & Piece.Player2) != 0)
                    {
                        var target = isPlayer1 ? new Vector2(board.BoardSize - 1, board.BoardSize - 1) : new Vector2(0, 0);
                        distSum += (target - new Vector2(x, y)).LengthSquared();
                    }
                }    
            }

            return -distSum;
        }
        
        public static float PlayerProgress(Board board, bool isPlayer1)
        {
            var posSum = new Coord(0, 0);
            var count = 0;
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    if (isPlayer1 && (board[coord] & Piece.Player1) != 0 ||
                        !isPlayer1 && (board[coord] & Piece.Player2) != 0)
                    {
                        posSum += coord;
                        count++;
                    }
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
            var sum = 0;
            var color = isPlayer1 ? Piece.Player1 : Piece.Player2;
            
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    sum += board.IsInCamp(coord, !isPlayer1) && board[coord] == color ? 1 : 0;
                }
            }

            return sum;
        }

        public static float PiecesNotInPlayerCamp(Board board, bool isPlayer1)
        {
            var sum = 0;
            var color = isPlayer1 ? Piece.Player1 : Piece.Player2;
            
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    sum += !board.IsInCamp(coord, isPlayer1) && board[coord] == color ? 1 : 0;
                }
            }

            return sum;
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
    private float EvaluateFromPlayer(Board board, bool isPlayer1)
    {
        return
            Heuristics.SumOfDistances(board, isPlayer1) * 1f
            // Heuristics.PlayerProgress(board, isPlayer1) * 1000f
            + Heuristics.PiecesInOpposingCamp(board, isPlayer1) * 100f
            + Heuristics.PiecesNotInPlayerCamp(board, isPlayer1) * 100f;
    }

    private float Evaluate(Board board, bool isPlayer1)
    {
        var eval = EvaluateFromPlayer(board, isPlayer1) - EvaluateFromPlayer(board, !isPlayer1);
        return eval;
    }
    
    private (float eval, Move? bestMove) Search(Board board, bool isPlayer1, int depth, float alpha = float.NegativeInfinity, float beta = float.PositiveInfinity)
    {
        Move? bestMove = null;
        const float immediateWinScore = 1000000;
        
        if (depth == 0)
            return (Evaluate(board, isPlayer1), null);

        // if (transpositionTable.Query(board, depth, alpha, beta, out var ttEval) == TranspositionTable.QueryResult.Success)
        // {
        //     Stats.TranspositionTableHits++;
        //     return (ttEval.eval, ttEval.move);
        // }

        Span<Move> moves = new Move[8192];
        MoveGenerator.GenerateMoves(board, isPlayer1, ref moves);
        if (moves.Length == 0)
        {
            if ((board.GameState & GameState.Ended) != 0)
                return (immediateWinScore + depth, null);
            return (float.NegativeInfinity, null);
        }
        
        OrderMoves(board, isPlayer1, ref moves);

        var evaluationBound = TranspositionTable.NodeType.UpperBound;

        foreach (var move in moves)
        {
            board.MakeMove(move);
            var eval = -Search(board, !isPlayer1, depth - 1, -beta, -alpha).eval;
            board.UnmakeMove(move);
            Stats.NodesVisited++;
            
            if (eval >= beta)
            {
                transpositionTable.RecordState(board, eval, depth, TranspositionTable.NodeType.LowerBound, move);
                return (beta, move);
            }

            if (eval > alpha)
            {
                evaluationBound = TranspositionTable.NodeType.Exact;
                alpha = eval;
                bestMove = move;
            }
        }
        
        transpositionTable.RecordState(board, alpha, depth, evaluationBound, bestMove);

        return (alpha, bestMove);
    }
    
    private void OrderMoves(Board board, bool isPlayer1, ref Span<Move> moveList)
    {
        Span<float> scores = new float[moveList.Length];
        var target = isPlayer1 
            ? new Vector2(board.BoardSize - 1, board.BoardSize - 1) 
            : new Vector2(0, 0);

        for (var i = 0; i < moveList.Length; i++)
        {
            var move = moveList[i];
            var finalSquare = move.FinalSquare;
            scores[i] = -(target - new Vector2(finalSquare.X, finalSquare.Y)).LengthSquared();
        }

        Quicksort(ref moveList, scores, 0, moveList.Length - 1);
    }
    
    private static void Quicksort(ref Span<Move> values, Span<float> scores, int low, int high)
    {
        if (low < high)
        {
            var pivotIndex = Partition(ref values, scores, low, high);
            Quicksort(ref values, scores, low, pivotIndex - 1);
            Quicksort(ref values, scores, pivotIndex + 1, high);
        }
    }

    private static int Partition(ref Span<Move> values, Span<float> scores, int low, int high)
    {
        var pivotScore = scores[high];
        var i = low - 1;

        for (var j = low; j <= high - 1; j++)
        {
            if (scores[j] > pivotScore)
            {
                i++;
                (values[i], values[j]) = (values[j], values[i]);
                (scores[i], scores[j]) = (scores[j], scores[i]);
            }
        }
        (values[i + 1], values[high]) = (values[high], values[i + 1]);
        (scores[i + 1], scores[high]) = (scores[high], scores[i + 1]);

        return i + 1;
    }
    
    public void OnPlayerTurn(int turn, bool isPlayer1, Board board)
    {
        transpositionTable.Clear();
        
        Stats.NodesVisited = 0;
        Stats.TranspositionTableHits = 0;

        var piecesInOpposingCamp = Heuristics.PiecesInOpposingCamp(board, isPlayer1);
        var depth = piecesInOpposingCamp switch
        {
            15 or 16 => 4,
            17 => 5,
            18 => 6,
            _ => 2
        };
        
        Console.WriteLine($"Player {(isPlayer1 ? "1" : "2")} thinking ({depth} moves ahead)");

        var stopwatch = new Stopwatch();
        
        stopwatch.Start();
        var (eval, bestMove) = Search(board, isPlayer1, depth);
        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        
        Console.WriteLine($"Search took {elapsedSeconds:F3} s ({Stats.NodesVisited / elapsedSeconds:F1} nodes/s)");
        Console.WriteLine($"Transposition table entry count: {transpositionTable.Size} | Hits: {Stats.TranspositionTableHits} ({(double)Stats.TranspositionTableHits / Stats.NodesVisited:P} of all nodes)");
        Console.WriteLine($"Eval: {eval} | Best move: {bestMove} | Nodes visited: {Stats.NodesVisited}");
            
        MovePicked?.Invoke(bestMove);
    }
}