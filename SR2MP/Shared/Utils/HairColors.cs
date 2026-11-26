namespace SR2MP.Shared.Utils;

internal static class HairColors
{
    internal static readonly string[] Names = { "Original", "Blond", "Brown", "Black", "Ginger", "Pink", "Purple" };
    internal static readonly string[] Hex = { "49D5C5", "EAC16B", "704333", "20232F", "C86532", "ED74AD", "9465D9" };
    internal static bool IsValid(int choice) => choice >= 0 && choice < Names.Length;
    internal static byte Normalize(int choice) => IsValid(choice) ? (byte)choice : (byte)0;
    internal static byte Next(int choice) => (byte)((Normalize(choice) + 1) % Names.Length);
    internal static string Caption(int choice) => "Hair color: " + Names[Normalize(choice)];
}
