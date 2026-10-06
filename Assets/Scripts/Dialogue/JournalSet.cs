using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewJournal", menuName = "Casa del Silencio/Journal")]
public class JournalSet : ScriptableObject
{
    [Serializable]
    public class Page
    {
        public string heading = "";
        [TextArea(4, 12)] public string body = "";
    }

    public string title = "Journal";
    public List<Page> pages = new List<Page>();
    [TextArea(1, 3)] public string thoughtAfterLastPage = "";
}
