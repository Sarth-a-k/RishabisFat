using UnityEngine;

public static class CutsceneMemory
{
    const string Prefix = "cutscene_seen_";
    const string ListKey = "cutscene_seen_list";

    public static bool Seen(string id)
    {
        return !string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(Prefix + id, 0) == 1;
    }

    public static void MarkSeen(string id)
    {
        if (string.IsNullOrEmpty(id) || Seen(id)) return;
        PlayerPrefs.SetInt(Prefix + id, 1);
        PlayerPrefs.SetString(ListKey, PlayerPrefs.GetString(ListKey, "") + id + "|");
        PlayerPrefs.Save();
    }

    public static void ForgetAll()
    {
        foreach (string id in PlayerPrefs.GetString(ListKey, "").Split('|'))
            if (!string.IsNullOrEmpty(id)) PlayerPrefs.DeleteKey(Prefix + id);
        PlayerPrefs.DeleteKey(ListKey);
        PlayerPrefs.Save();
    }
}
