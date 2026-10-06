using System.Collections.Generic;
using UnityEngine;

// Remembers which cutscenes were watched during the current playthrough only.
// Starting a new game from the menu (SessionReset) clears it, so the first viewing in every run has no skip.
public static class CutsceneMemory
{
    const string Prefix = "cutscene_seen_";
    const string ListKey = "cutscene_seen_list";

    static readonly HashSet<string> seen = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { seen.Clear(); }

    public static bool Seen(string id)
    {
        return !string.IsNullOrEmpty(id) && seen.Contains(id);
    }

    public static void MarkSeen(string id)
    {
        if (!string.IsNullOrEmpty(id)) seen.Add(id);
    }

    public static void ForgetAll()
    {
        seen.Clear();
        // also clear the old saved-to-disk flags from earlier versions
        foreach (string id in PlayerPrefs.GetString(ListKey, "").Split('|'))
            if (!string.IsNullOrEmpty(id)) PlayerPrefs.DeleteKey(Prefix + id);
        PlayerPrefs.DeleteKey(ListKey);
        PlayerPrefs.Save();
    }
}
