namespace HalmaBot;

[Flags]
public enum GameState
{
    InProgress = 1 << 0,
    Player1Won = 1 << 1,
    Player2Won = 1 << 2,
    
    Ended = Player1Won | Player2Won
}