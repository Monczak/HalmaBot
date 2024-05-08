using System.Diagnostics;
using System.Numerics;

namespace HalmaBot;

public class HalmaBot : IHalmaPlayer
{
    public event IHalmaPlayer.PickMoveDelegate? MovePicked;

    private TranspositionTable transpositionTable = new();

    private static class Stats
    {
        public static int NodesVisited { get; set; }
        public static int TranspositionTableHits { get; set; }
    }

    private bool useAlphaBetaPruning = false;
    private bool useMoveOrdering = false;
    private bool useTranspositionTable = false;

    private float sumOfDistancesWeight = 1f;
    private float piecesEnemyCanJumpOverWeight = -10f;
    private float piecesInOpposingCampWeight = 10000f;
    private float piecesUnableToJumpWeight = -100f;
    private float piecesNotInPlayerCampWeight = 100f;

    private int nominalSearchDepth = 5;

    public HalmaBot UseAlphaBetaPruning()
    {
        useAlphaBetaPruning = true;
        return this;
    }
    
    public HalmaBot UseMoveOrdering()
    {
        useMoveOrdering = true;
        return this;
    }

    public HalmaBot UseTranspositionTable()
    {
        useTranspositionTable = true;
        return this;
    }

    public HalmaBot WithSumOfDistancesWeight(float weight)
    {
        sumOfDistancesWeight = weight;
        return this;
    }
    
    public HalmaBot WithPiecesEnemyCanJumpOverWeight(float weight)
    {
        piecesEnemyCanJumpOverWeight = weight;
        return this;
    }
    
    public HalmaBot WithPiecesInOpposingCampWeight(float weight)
    {
        piecesInOpposingCampWeight = weight;
        return this;
    }
    
    public HalmaBot WithPiecesUnableToJumpWeight(float weight)
    {
        piecesUnableToJumpWeight = weight;
        return this;
    }
    
    public HalmaBot WithPiecesNotInPlayerCampWeight(float weight)
    {
        piecesNotInPlayerCampWeight = weight;
        return this;
    }

    public HalmaBot WithNominalSearchDepth(int depth)
    {
        nominalSearchDepth = depth;
        return this;
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
                    if (PieceBelongsTo(board, isPlayer1, coord))
                    {
                        var target = isPlayer1 ? new Vector2(board.BoardSize - 1, board.BoardSize - 1) : new Vector2(0, 0);
                        distSum += (target - new Vector2(x, y)).LengthSquared();
                    }
                }    
            }

            return -distSum;
        }

        private static bool PieceBelongsTo(Board board, bool isPlayer1, Coord coord)
        {
            return isPlayer1 && (board[coord] & Piece.Player1) != 0 ||
                   !isPlayer1 && (board[coord] & Piece.Player2) != 0;
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
            var pieceCount = 0;
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    if (!PieceBelongsTo(board, isPlayer1, coord)) continue;
                    
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) 
                                continue;
                            if (x + dx < 0 || x + dx >= board.BoardSize || y + dy < 0 || y + dy >= board.BoardSize
                                || x - dx < 0 || x - dx >= board.BoardSize || y - dy < 0 || y - dy >= board.BoardSize)
                                continue;
                            
                            var delta = new Coord(dx, dy);
                            if (PieceBelongsTo(board, !isPlayer1, coord + delta) && board[coord - delta] == Piece.None)
                                pieceCount++;
                        }
                    }
                }
            }

            return pieceCount;
        }

        public static float PiecesUnableToJump(Board board, bool isPlayer1)
        {
            var pieceCount = 0;
            for (var y = 0; y < board.BoardSize; y++)
            {
                for (var x = 0; x < board.BoardSize; x++)
                {
                    var coord = new Coord(x, y);
                    if (!PieceBelongsTo(board, isPlayer1, coord)) continue;

                    var canJump = false;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) 
                                continue;
                            if (x + dx < 0 || x + dx >= board.BoardSize || y + dy < 0 || y + dy >= board.BoardSize
                                || x - dx < 0 || x - dx >= board.BoardSize || y - dy < 0 || y - dy >= board.BoardSize)
                                continue;
                            if (x + dx * 2 < 0 || x + dx * 2 >= board.BoardSize || y + dy * 2 < 0 || y + dy * 2 >= board.BoardSize
                                || x - dx * 2 < 0 || x - dx * 2 >= board.BoardSize || y - dy * 2 < 0 || y - dy * 2 >= board.BoardSize)
                                continue;
                            
                            var delta = new Coord(dx, dy);
                            if ((board[coord + delta] & Piece.Occupied) != 0 &&
                                board[coord + delta + delta] == Piece.None)
                            {
                                canJump = true;
                                break;
                            }
                        }

                        if (canJump) break;
                    }

                    if (!canJump) pieceCount++;
                }
            }

            return pieceCount;
        }
    }
    
    // TODO: Maybe use a genetic algorithm to tune the bot - determine the best multipliers for heuristics?
    private float EvaluateFromPlayer(Board board, bool isPlayer1)
    {
        return
            (sumOfDistancesWeight == 0 ? 0 : Heuristics.SumOfDistances(board, isPlayer1) * sumOfDistancesWeight)
            + (piecesEnemyCanJumpOverWeight == 0 ? 0 : Heuristics.PiecesEnemyCanJumpOver(board, isPlayer1) * piecesEnemyCanJumpOverWeight)
            + (piecesUnableToJumpWeight == 0 ? 0 : Heuristics.PiecesUnableToJump(board, isPlayer1) * piecesUnableToJumpWeight)
            + (piecesInOpposingCampWeight == 0 ? 0 : Heuristics.PiecesInOpposingCamp(board, isPlayer1) * piecesInOpposingCampWeight)
            + (piecesNotInPlayerCampWeight == 0 ? 0 : Heuristics.PiecesNotInPlayerCamp(board, isPlayer1) * piecesNotInPlayerCampWeight);
    }

    private float Evaluate(Board board, bool isPlayer1)
    {
        var eval = EvaluateFromPlayer(board, isPlayer1) - EvaluateFromPlayer(board, !isPlayer1);
        return eval;
    }
    
    private (float eval, Move? bestMove) Search(Board board, bool isPlayer1, int depth, float alpha = float.NegativeInfinity, float beta = float.PositiveInfinity)
    {
        Move? bestMove = null;
        const float immediateWinScore = 10000000;

        var alphaOrig = alpha;
        
        if (depth == 0)
            return (Evaluate(board, isPlayer1), null);

        if (useTranspositionTable && transpositionTable.Query(board, depth, alpha, beta, out var ttEval) == TranspositionTable.QueryResult.Success)
        {
            Stats.TranspositionTableHits++;
            return (ttEval.eval, ttEval.move);
        }
        
        Span<Move> moves = new Move[8192];
        var moveCount = MoveGenerator.GenerateMoves(board, isPlayer1, ref moves);
        if (moveCount == 0)
        {
            if ((board.GameState & GameState.Ended) != 0)
                return (immediateWinScore + depth, null);
            return (float.NegativeInfinity, null);
        }
        
        if (useMoveOrdering)
            OrderMoves(board, isPlayer1, moves, moveCount);
        
        var maxEval = float.NegativeInfinity;
        for (int i = 0; i < moveCount; i++)
        {
            var move = moves[i];
            board.MakeMove(move);
            var eval = -Search(board, !isPlayer1, depth - 1, -beta, -alpha).eval;
            board.UnmakeMove(move);
            Stats.NodesVisited++;
            
            if (eval > maxEval)
            {
                maxEval = eval;
                bestMove = move;
            }

            if (useAlphaBetaPruning)
            {
                alpha = Math.Max(alpha, eval);
                if (alpha >= beta)
                    break;
            }
        }

        if (useTranspositionTable)
        {
            TranspositionTable.NodeType nodeType;
            if (maxEval <= alphaOrig)
                nodeType = TranspositionTable.NodeType.UpperBound;
            else if (maxEval >= beta)
                nodeType = TranspositionTable.NodeType.LowerBound;
            else
                nodeType = TranspositionTable.NodeType.Exact;
            transpositionTable.RecordState(board, maxEval, depth, nodeType, bestMove);
        }

        return (maxEval, bestMove);
    }
    
    private static void OrderMoves(Board board, bool isPlayer1, Span<Move> moveList, int moveCount)
    {
        var scores = new int[moveCount];
        for (var i = 0; i < moveCount; i++)
        {
            var move = moveList[i];
            var score = 0;
                
            var target = isPlayer1 ? new Vector2(board.BoardSize - 1, board.BoardSize - 1) : new Vector2(0, 0);
            if ((new Vector2(move.FinalSquare.X, move.FinalSquare.Y) - target).LengthSquared() <
                (new Vector2(move.FromSquare.X, move.FromSquare.Y) - target).LengthSquared())
                score += 100;

            scores[i] = score;
        }

        Quicksort(moveList, scores, 0, moveCount - 1);
    }

    private static void Quicksort(Span<Move> values, int[] scores, int low, int high)
    {
        while (true)
        {
            if (low < high)
            {
                var pivotIndex = Partition(values, scores, low, high);
                Quicksort(values, scores, low, pivotIndex - 1);
                low = pivotIndex + 1;
                continue;
            }

            break;
        }
    }

    private static int Partition(Span<Move> values, int[] scores, int low, int high)
    {
        var pivotScore = scores[high];
        var i = low - 1;

        for (var j = low; j <= high - 1; j++)
        {
            if (scores[j] <= pivotScore) continue;
            
            i++;
            (values[i], values[j]) = (values[j], values[i]);
            (scores[i], scores[j]) = (scores[j], scores[i]);
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
            15 or 16 => nominalSearchDepth + 1,
            17 => nominalSearchDepth + 2,
            18 => nominalSearchDepth + 3,
            _ => nominalSearchDepth
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