using HalmaBot;

internal class Program
{
    public static void Main(string[] args)
    {
        var player1 = new HalmaBot.HalmaBot()
            .UseAlphaBetaPruning()
            .UseMoveOrdering()
            .UseDeepSearchInEndgame()
            .WithNominalSearchDepth(2)
            .UseLogging("Player1.csv");
        var player2 = new HalmaBot.HalmaBot()
            .UseAlphaBetaPruning()
            .UseMoveOrdering()
            .UseDeepSearchInEndgame()
            .WithNominalSearchDepth(2)
            .UseLogging("Player2.csv");
        
        var game = new HalmaGame(player1, player2);
        Console.WriteLine(game.Board.Print());
        game.BoardUpdated += (sender, args) =>
        {
            Console.WriteLine($"Picked move: {args.PickedMove}");
            Console.WriteLine(args.Board.Print());
            // Console.ReadKey();
        };
        game.GameStateUpdated += (sender, state) =>
        {
            Console.WriteLine($"State updated: {state}");
        };
        
        game.StartGame().GetAwaiter().GetResult();
    }
}