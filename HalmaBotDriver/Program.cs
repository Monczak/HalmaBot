using HalmaBot;

internal class Program
{
    private class TestBot : IHalmaPlayer
    {
        public event IHalmaPlayer.PickMoveDelegate? MovePicked;

        private Move[] player1Moves =
        [
           Move.FromString("Je2>g4"),  Move.FromString("Jd1>f3"),
        ];
        
        private Move[] player2Moves =
        [
            Move.FromString("l16>k16"), Move.FromString("l15>k15"),
        ];

        public void OnPlayerTurn(int turn, bool isPlayer1, Board board)
        {
            if (isPlayer1)
            {
                var turn1 = turn / 2;
                if (turn1 >= player1Moves.Length) Console.ReadKey();
                else MovePicked?.Invoke(player1Moves[turn1]);
            }
            else
            {
                var turn2 = (turn - 1) / 2;
                if (turn2 >= player2Moves.Length) Console.ReadKey();
                else MovePicked?.Invoke(player2Moves[turn2]);
            }
        }
    }
    
    public static void Main(string[] args)
    {
        var player1 = new HalmaBot.HalmaBot()
            .UseAlphaBetaPruning()
            // .UseMoveOrdering()
            .UseTranspositionTable()
            .WithPiecesEnemyCanJumpOverWeight(0)
            .WithSumOfDistancesWeight(1)
            .WithPiecesUnableToJumpWeight(0)
            .WithNominalSearchDepth(2);
        
        var player2 = new HalmaBot.HalmaBot()
            .UseAlphaBetaPruning()
            // .UseMoveOrdering()
            .UseTranspositionTable()
            .WithPiecesEnemyCanJumpOverWeight(0)
            .WithSumOfDistancesWeight(1)
            .WithPiecesUnableToJumpWeight(0)
            .WithNominalSearchDepth(3);
        
        var game = new HalmaGame(player1, player2);
        Console.WriteLine(game.Board.Print());
        game.BoardUpdated += (sender, args) =>
        {
            Console.WriteLine($"Zobrist Key: {args.Board.ZobristKey}");
            Console.WriteLine(args.Board.Print());
            // Console.ReadKey();
        };
        game.GameStateUpdated += (sender, state) =>
        {
            Console.WriteLine(game.Board.Print());
            Console.WriteLine($"State updated: {state}");
        };
        
        game.StartGame().GetAwaiter().GetResult();
    }
}