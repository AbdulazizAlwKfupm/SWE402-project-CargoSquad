using System;
using CargoSquad.Player;
using UnityEngine;

namespace CargoSquad.Missions
{
    /// <summary>Presentation subscriber only: a confirmed cargo recovery plays once per scene.</summary>
    [DisallowMultipleComponent]
    public sealed class MissionSuccessAudio : MonoBehaviour
    {
        private CargoExtraction mission;
        private PlayerAudioSettings settings;
        private AudioSource source;
        private bool played;
        public event Action Played;

        private void Awake()
        {
            settings = Resources.Load<PlayerAudioSettings>("CargoSquadPlayerAudio");
            mission = FindFirstObjectByType<CargoExtraction>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }

        private void OnEnable()
        {
            if (!mission) return;
            mission.ResultFinalized += OnResult;
            if (mission.Result.Outcome != MissionOutcome.None) OnResult(mission.Result);
        }

        private void OnResult(MissionResult result)
        {
            if (played || result.Outcome != MissionOutcome.CargoRecovered || !settings || !settings.missionSuccess) return;
            played = true;
            source.clip = settings.missionSuccess;
            source.volume = settings.missionSuccessVolume;
            source.Play(); // Audio DSP continues while the existing report pauses gameplay time.
            Played?.Invoke();
        }

        private void OnDisable()
        {
            if (mission) mission.ResultFinalized -= OnResult;
            if (source) source.Stop();
        }

        private void OnDestroy() { if (source) Destroy(source); }
    }
}
