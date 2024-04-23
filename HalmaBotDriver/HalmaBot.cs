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
        
        if (depth == 0)
            return (Evaluate(board, isPlayer1), null);

        if (transpositionTable.Query(board, depth, alpha, beta, out var ttEval) == TranspositionTable.QueryResult.Success)
        {
            Stats.TranspositionTableHits++;
            return (ttEval.eval, ttEval.move);
        }
        
        var moves = new List<(Move, float)>();
        foreach (var move in MoveGenerator.GenerateMoves(board, isPlayer1))
            moves.Add((move, 0));
        if (moves.Count == 0)
        {
            if ((board.GameState & GameState.Ended) != 0)
                return (float.PositiveInfinity, null);
            return (float.NegativeInfinity, null);
        }
        
        OrderMoves(moves);

        var evaluationBound = TranspositionTable.NodeType.UpperBound;

        foreach (var (move, _) in moves)
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

        void OrderMoves(List<(Move, float)> moveList)
        {
            for (var i = 0; i < moveList.Count; i++)
            {
                var (move, _) = moveList[i];
                board.MakeMove(move);
                var eval = Evaluate(board, isPlayer1);
                board.UnmakeMove(move);
                moveList[i] = (move, eval);
            }

            moveList.Sort((m1, m2) => m2.Item2.CompareTo(m1.Item2));
        }
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
            19 => 7,
            _ => 5
        };
        
        Console.WriteLine($"Player {(isPlayer1 ? "1" : "2")} thinking");
        var (eval, bestMove) = Search(board, isPlayer1, depth);
        Console.WriteLine($"Transposition table entry count: {transpositionTable.Size}");
        Console.WriteLine($"Eval: {eval} Best move: {bestMove} Nodes visited: {Stats.NodesVisited} TT hits: {Stats.TranspositionTableHits}");
            
        MovePicked?.Invoke(bestMove);
    }
}