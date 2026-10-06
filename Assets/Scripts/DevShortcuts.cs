#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DevShortcuts
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        var go = new GameObject("Dev Shortcuts");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        float msgUntil;
        string msg = "";

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2)) { var le = FindAnyObjectByType<FPCharacter.LoopEnding>(); if (le != null) { Say("Playing the loop ending"); le.Play(); } else Say("No loop ending in this scene"); }
            if (Input.GetKeyDown(KeyCode.F3)) TeleportTo(new Vector3(216f, 0f, 0f), 90f, "Eclipse Keep");
            if (Input.GetKeyDown(KeyCode.F4)) TeleportTo(new Vector3(158f, 0f, 0f), 90f, "region 3 (white room)");
            if (Input.GetKeyDown(KeyCode.F6)) Teleport("Warm Statues Trigger", "Ember Keep");
            if (Input.GetKeyDown(KeyCode.F5)) { Say("Loading Shadow2D"); SceneManager.LoadScene("Shadow2D"); }
            if (Input.GetKeyDown(KeyCode.F7)) Jump("Shadow2D");
            if (Input.GetKeyDown(KeyCode.F8)) Julian();
            if (Input.GetKeyDown(KeyCode.F10)) { var je = FindAnyObjectByType<FPCharacter.JournalCompareEnding>(); if (je != null) { Say("Playing the journal ending"); je.Begin(); } else Say("No journal ending in this scene"); }
            if (Input.GetKeyDown(KeyCode.F11)) { CutsceneMemory.ForgetAll(); Say("Cutscenes marked unwatched (no skip on next view)"); }
            if (Input.GetKeyDown(KeyCode.F12)) { FPCharacter.ItemHintArrow.ForceShow(); Say("Item hint arrow forced on"); }
            if (Input.GetKeyDown(KeyCode.F9)) { Say("Loading WarmStatues2D"); SceneManager.LoadScene("WarmStatues2D"); }
        }

        void Jump(string next)
        {
            foreach (ComicPanelTransition t in FindObjectsByType<ComicPanelTransition>(FindObjectsInactive.Exclude))
            {
                if (t.nextScene != next) continue;
                t.SetPuzzleSolved();
                FPCharacter.FPCharacterMover mover = FindAnyObjectByType<FPCharacter.FPCharacterMover>();
                if (mover == null) { Say("No player in this scene"); return; }
                CharacterController cc = mover.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                mover.transform.position = t.transform.position + Vector3.down * 0.6f;
                if (cc != null) cc.enabled = true;
                Say("Jumped to the " + next + " trigger");
                return;
            }
            Say("No trigger for " + next + " in this scene");
        }

        void Julian()
        {
            FPCharacter.PuzzleTransitionLink link = FindAnyObjectByType<FPCharacter.PuzzleTransitionLink>();
            if (link == null || link.julian == null) { Jump("WarmStatues2D"); return; }
            Say("Playing the Julian sequence, then the minigame");
            link.Arm();
        }

        void Teleport(string triggerName, string label)
        {
            GameObject trig = GameObject.Find(triggerName);
            Transform ret = trig != null ? trig.transform.Find("Return Point") : null;
            FPCharacter.FPCharacterMover mover = FindAnyObjectByType<FPCharacter.FPCharacterMover>();
            if (ret == null || mover == null) { Say("Cannot teleport to " + label + " here"); return; }
            CharacterController cc = mover.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            mover.transform.SetPositionAndRotation(ret.position + Vector3.up * 0.1f, Quaternion.Euler(0f, ret.eulerAngles.y, 0f));
            if (cc != null) cc.enabled = true;
            Say("Teleported into " + label);
        }

        void TeleportTo(Vector3 p, float yaw, string label)
        {
            FPCharacter.FPCharacterMover mover = FindAnyObjectByType<FPCharacter.FPCharacterMover>();
            if (mover == null) { Say("No player in this scene"); return; }
            if (Physics.Raycast(new Vector3(p.x, 3f, p.z), Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            CharacterController cc = mover.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            mover.transform.SetPositionAndRotation(p + Vector3.up * 0.15f, Quaternion.Euler(0f, yaw, 0f));
            if (cc != null) cc.enabled = true;
            Say("Teleported into " + label);
        }

        void Say(string m) { msg = m; msgUntil = Time.unscaledTime + 2.5f; }

        void OnGUI()
        {
            if (Time.unscaledTime < msgUntil) GUI.Label(new Rect(12, 12, 600, 30), "[Dev] " + msg);
        }
    }
}
#endif
