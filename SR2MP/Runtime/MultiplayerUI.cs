namespace SR2MP.Components.UI;

[MelonLoader.RegisterTypeInIl2Cpp]
internal sealed class MultiplayerUI : MonoBehaviour
{
    public static MultiplayerUI Instance { get; private set; } = null!;
    public const byte SystemMessageNormal = 0, SystemMessageConnect = 1, SystemMessageDisconnect = 2, SystemMessageClose = 3;
    private void Awake() => Instance = this;
    public void RegisterSystemMessage(string message, string id, byte type) => SrLogger.LogMessage(message);
    public void RegisterChatMessage(string message, string name, string id) => SrLogger.LogMessage($"{name}: {message}");
    public void ClearChatMessages() { }
}
