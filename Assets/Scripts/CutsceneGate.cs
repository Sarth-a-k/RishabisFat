using System.Collections.Generic;
using UnityEngine;

public static class CutsceneGate
{
    static readonly HashSet<object> owners = new HashSet<object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { owners.Clear(); }

    public static bool Active
    {
        get
        {
            owners.RemoveWhere(o => o == null || (o is Object u && u == null));
            return owners.Count > 0;
        }
    }

    public static void Begin(object owner) { if (owner != null) owners.Add(owner); }
    public static void End(object owner) { owners.Remove(owner); }
}
