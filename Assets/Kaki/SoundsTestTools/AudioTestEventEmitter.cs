using System.Collections.Generic;
using UnityEngine;

namespace Kaki
{
    public class AudioTestEventEmitter : MonoBehaviour
    {
        [SerializeField] private string status = "Waiting for input.";

        private AudioPlayer audioPlayer;
        private AudioManager audioManager;
        private List<AudioPlayer.PlayableEntry> playableEntries = new List<AudioPlayer.PlayableEntry>();

        public void SetStatus(string message)
        {
            status = string.IsNullOrWhiteSpace(message) ? "Waiting for input." : message;
        }

        public void ConfigureTargets(AudioPlayer player, AudioManager manager)
        {
            audioPlayer = player;
            audioManager = manager;
            RefreshPlayableEntries();
        }

        private void Awake()
        {
            RefreshTargetsIfNeeded();
            RefreshPlayableEntries();
        }

        private void OnGUI()
        {
            RefreshTargetsIfNeeded();

            float height = Mathf.Max(220f, 170f + playableEntries.Count * 30f);
            GUILayout.BeginArea(new Rect(16f, 16f, 520f, height), GUI.skin.box);
            GUILayout.Label("Prefab Audio Test");
            GUILayout.Label("Buttons are generated from the current AudioPlayer entries.");
            GUILayout.Space(6f);

            if (playableEntries.Count == 0)
            {
                GUILayout.Label("No playable entries found on AudioPlayer.");
            }
            else
            {
                for (int i = 0; i < playableEntries.Count; i++)
                {
                    var entry = playableEntries[i];
                    string groupLabel = string.IsNullOrWhiteSpace(entry.GroupName) ? "Audio Group" : entry.GroupName;
                    string buttonLabel = entry.TriggerMode == AudioPlayer.AudioGroupTriggerMode.Single
                        ? $"{groupLabel} | {entry.AudioType} {entry.PlayMode} - {entry.Label}"
                        : $"{groupLabel} | {entry.AudioType} {entry.TriggerMode}";
                    if (GUILayout.Button(buttonLabel))
                    {
                        PlayEntry(entry);
                    }
                }
            }

            if (GUILayout.Button("Stop BGM"))
            {
                StopBgm();
            }

            if (GUILayout.Button("Stop SFX"))
            {
                StopSfx();
            }

            GUILayout.Space(8f);
            GUILayout.Label("Status: " + status);
            GUILayout.EndArea();
        }

        private void RefreshTargetsIfNeeded()
        {
            if (audioPlayer == null)
            {
                audioPlayer = FindFirstObjectByType<AudioPlayer>();
            }

            if (audioManager == null)
            {
                audioManager = FindFirstObjectByType<AudioManager>();
            }

            RefreshPlayableEntries();
        }

        private void RefreshPlayableEntries()
        {
            playableEntries = audioPlayer != null
                ? audioPlayer.GetPlayableEntries()
                : new List<AudioPlayer.PlayableEntry>();
        }

        private void PlayEntry(AudioPlayer.PlayableEntry entry)
        {
            if (audioPlayer == null)
            {
                status = "AudioPlayer not found.";
                return;
            }

            bool played = audioPlayer.PlayEntryAt(entry.GroupIndex, entry.EntryIndex);
            status = played
                ? entry.TriggerMode == AudioPlayer.AudioGroupTriggerMode.Single
                    ? $"Played {entry.AudioType} {entry.PlayMode} - {entry.Label}."
                    : $"Triggered {entry.AudioType} {entry.TriggerMode} group - {entry.GroupName}."
                : $"Failed to play {entry.Label}.";
        }

        private void StopBgm()
        {
            if (audioManager != null)
            {
                audioManager.StopBGM();
                status = "Stopped BGM.";
                return;
            }

            status = "AudioManager not found.";
        }

        private void StopSfx()
        {
            if (audioManager != null)
            {
                audioManager.StopSFX();
                status = "Stopped SFX.";
                return;
            }

            status = "AudioManager not found.";
        }
    }
}
