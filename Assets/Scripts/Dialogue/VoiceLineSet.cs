using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewVoiceLines", menuName = "Casa del Silencio/Voice Lines")]
public class VoiceLineSet : ScriptableObject
{
    [Serializable]
    public class Line
    {
        public string key = "Hint_Torch";
        public AudioClip clip;
        public string speaker = "YOU";
        [TextArea(2, 4)] public string subtitle = "";
        [Range(0f, 1f)] public float volume = 1f;
    }

    public List<Line> lines = new List<Line>();
    public List<string> npcIgnoreKeys = new List<string> { "Npc_Ignore_01", "Npc_Ignore_02" };

    public Line Find(string key)
    {
        foreach (Line l in lines) if (l != null && l.key == key) return l;
        return null;
    }
}
