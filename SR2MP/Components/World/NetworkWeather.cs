using MelonLoader;
using SR2MP.Server.Managers;
using SR2MP.Shared.Utils;

namespace SR2MP.Components.World;

[MelonLoader.RegisterTypeInIl2Cpp]
internal sealed class NetworkWeather : MonoBehaviour
{
    private float updateTimer;

    public void Update()
    {
        updateTimer += UnityEngine.Time.deltaTime;

        if (updateTimer < Timers.WeatherTimer)
            return;

        updateTimer = 0;

        WeatherUpdateHelper.EnsureLookupInitialized();
        WeatherUpdateHelper.SendWeatherUpdate();
    }
}