using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Casa del Silencio/Dialogue Set")]
public class DialogueSet : ScriptableObject
{
    [Serializable]
    public class Speaker
    {
        public string id = "You";
        public string displayName = "YOU";
        public Color nameColor = new Color(0.95f, 0.78f, 0.42f);
    }

    [Serializable]
    public class Line
    {
        public string key = "Intro_01";
        public string speakerId = "You";
        [TextArea(2, 5)] public string text = "";
        public float holdSeconds = 3.2f;
    }

    public float defaultHoldSeconds = 3.2f;
    public List<Speaker> speakers = new List<Speaker>();
    public List<Line> lines = new List<Line>();

    public Line Find(string key)
    {
        foreach (Line l in lines) if (l != null && l.key == key) return l;
        return null;
    }

    public Speaker FindSpeaker(string id)
    {
        foreach (Speaker s in speakers) if (s != null && s.id == id) return s;
        return null;
    }

    public float HoldFor(Line l)
    {
        return l != null && l.holdSeconds > 0f ? l.holdSeconds : defaultHoldSeconds;
    }
}
