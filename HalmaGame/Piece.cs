namespace HalmaBot;

[Flags]
public enum Piece
{
    Player1 = 1 << 0,
    Player2 = 1 << 1,
    
    Occupied = Player1 | Player2,
    
    None = 0
}