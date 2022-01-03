namespace SolmangoCLI.Statics;

public static class Extensions
{
    public static double ToSOL(this ulong lamports) => lamports / 1_000_000_000D;
}