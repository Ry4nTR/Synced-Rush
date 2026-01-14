using UnityEngine;

public static class PlayerProfile
{
    private const string PlayerNameKey = "";

    public static string PlayerName
    {
        get => PlayerPrefs.GetString(PlayerNameKey, "");
        set
        {
            PlayerPrefs.SetString(PlayerNameKey, value);
            PlayerPrefs.Save();
        }
    }
}
