using System.Collections.Generic;
using UnityEngine;

namespace Kaki
{
    public class FXTestEventEmitter : MonoBehaviour
    {
        [SerializeField] private string status = "Waiting for input.";

        private FXPlayer fxPlayer;
        private List<FXPlayer.TriggerableBinding> triggerableBindings = new List<FXPlayer.TriggerableBinding>();

        public void ConfigureTarget(FXPlayer player)
        {
            fxPlayer = player;
            RefreshTriggerableBindings();
        }

        private void Awake()
        {
            RefreshTargetIfNeeded();
            RefreshTriggerableBindings();
        }

        private void OnGUI()
        {
            RefreshTargetIfNeeded();

            float height = Mathf.Max(180f, 110f + triggerableBindings.Count * 30f);
            GUILayout.BeginArea(new Rect(560f, 16f, 520f, height), GUI.skin.box);
            GUILayout.Label("FX Test");
            GUILayout.Label("Buttons are generated from the current FXPlayer bindings.");
            GUILayout.Space(6f);

            if (triggerableBindings.Count == 0)
            {
                GUILayout.Label("No triggerable FX bindings found on FXPlayer.");
            }
            else
            {
                for (int i = 0; i < triggerableBindings.Count; i++)
                {
                    var binding = triggerableBindings[i];
                    string buttonLabel = $"{binding.TargetName} - {binding.Label}";
                    if (GUILayout.Button(buttonLabel))
                    {
                        TriggerBinding(binding);
                    }
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("Status: " + status);
            GUILayout.EndArea();
        }

        private void RefreshTargetIfNeeded()
        {
            if (fxPlayer == null)
            {
                fxPlayer = FindFirstObjectByType<FXPlayer>();
            }

            RefreshTriggerableBindings();
        }

        private void RefreshTriggerableBindings()
        {
            triggerableBindings = fxPlayer != null
                ? fxPlayer.GetTriggerableBindings()
                : new List<FXPlayer.TriggerableBinding>();
        }

        private void TriggerBinding(FXPlayer.TriggerableBinding binding)
        {
            if (fxPlayer == null)
            {
                status = "FXPlayer not found.";
                return;
            }

            bool triggered = fxPlayer.InvokeBindingAt(binding.GroupIndex, binding.BindingIndex);
            status = triggered
                ? $"Triggered {binding.TargetName} - {binding.Label}."
                : $"Failed to trigger {binding.Label}.";
        }
    }
}
