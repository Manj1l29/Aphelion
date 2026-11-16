
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MissionDayManager : NetworkBehaviour
{
    public const int TotalDays = 5;

    public const float FadeDuration = 1.25f;
    public const float NetworkFadeBuffer = 0.3f;
    public const float BlackHoldDuration = 0.4f;

    // 0 = normal, 1 = fade out, 2 = fade in.
    public NetworkVariable<int> CurrentDay =
        new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public NetworkVariable<int> TransitionState =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Coroutine transitionRoutine;

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            "[DAY MANAGER] DAY " +
            CurrentDay.Value + " OF " + TotalDays
        );
    }

    public bool AdvanceDayServer()
    {
        if (!IsServer)
            return false;

        if (CurrentDay.Value >= TotalDays)
        {
            Debug.Log("[DAY MANAGER] All five days completed.");
            return false;
        }

        if (transitionRoutine != null || TransitionState.Value != 0)
            return false;

        transitionRoutine = StartCoroutine(AdvanceDayRoutine());

        return true;
    }

    private IEnumerator AdvanceDayRoutine()
    {
        Debug.Log("[DAY MANAGER] Starting day transition.");

        // All players fade to black.
        TransitionState.Value = 1;

        yield return new WaitForSecondsRealtime(
            FadeDuration + NetworkFadeBuffer
        );

        // Update the day while the screen is black.
        CurrentDay.Value++;

        Debug.Log(
            "[DAY MANAGER] NOW ENTERING DAY " +
            CurrentDay.Value + " OF " + TotalDays
        );

        yield return new WaitForSecondsRealtime(BlackHoldDuration);

        // All players fade back in.
        TransitionState.Value = 2;

        yield return new WaitForSecondsRealtime(
            FadeDuration + NetworkFadeBuffer
        );

        TransitionState.Value = 0;

        Debug.Log("[DAY MANAGER] Day transition finished.");

        transitionRoutine = null;
    }

    public override void OnNetworkDespawn()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }
}
